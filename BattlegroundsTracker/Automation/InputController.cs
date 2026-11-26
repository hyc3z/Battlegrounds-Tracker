using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace Hearthstone_Deck_Tracker.Automation
{
	/// <summary>
	/// Safe Win32 SendInput mouse executor for Battlegrounds automation.
	/// Enforces Hearthstone foreground checks, F8 emergency stop, user mouse-override
	/// detection, and guaranteed mouse-button release on cancellation or error.
	/// </summary>
	internal static class InputController
	{
		private const uint InputMouse = 0;
		private const uint MouseEventfMove = 0x0001;
		private const uint MouseEventfLeftDown = 0x0002;
		private const uint MouseEventfLeftUp = 0x0004;
		private const uint MouseEventfVirtualDesk = 0x4000;
		private const uint MouseEventfAbsolute = 0x8000;

		private const int SmXVirtualScreen = 76;
		private const int SmYVirtualScreen = 77;
		private const int SmCxVirtualScreen = 78;
		private const int SmCyVirtualScreen = 79;

		private const int VkF8 = 0x77;
		private const int KeyDownMask = 0x8000;

		private const int UserOverrideThresholdPx = 35;

		[StructLayout(LayoutKind.Sequential)]
		private struct INPUT
		{
			public uint Type;
			public MOUSEINPUT Mouse;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MOUSEINPUT
		{
			public int Dx;
			public int Dy;
			public uint MouseData;
			public uint DwFlags;
			public uint Time;
			public UIntPtr DwExtraInfo;
		}

		[DllImport("user32.dll", SetLastError = true)]
		private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

		[DllImport("user32.dll")]
		private static extern short GetAsyncKeyState(int vKey);

		[DllImport("user32.dll")]
		private static extern int GetSystemMetrics(int nIndex);

		internal static bool IsEmergencyStopPressed() => (GetAsyncKeyState(VkF8) & KeyDownMask) != 0;

		internal static void ThrowIfAborted(CancellationToken ct, Point? expectedCursor = null)
		{
			ct.ThrowIfCancellationRequested();

			if(IsEmergencyStopPressed())
				throw new OperationCanceledException("检测到紧急停止热键 (F8)，已中止自动操作。");

			if(!User32.IsHearthstoneInForeground())
				throw new OperationCanceledException("炉石传说不在前台，已中止自动操作。");

			if(expectedCursor.HasValue)
			{
				var actual = User32.GetMousePos();
				var dx = Math.Abs(actual.X - expectedCursor.Value.X);
				var dy = Math.Abs(actual.Y - expectedCursor.Value.Y);
				if(dx > UserOverrideThresholdPx || dy > UserOverrideThresholdPx)
					throw new OperationCanceledException($"检测到手动移动鼠标 (偏移 {dx},{dy}px)，已中止自动操作。");
			}
		}

		internal static async Task ClickAsync(Point target, CancellationToken ct)
		{
			ThrowIfAborted(ct);
			await MoveSmoothlyAsync(target, steps: 10, stepDelayMs: 8, ct);
			await Task.Delay(35, ct);
			ThrowIfAborted(ct, target);

			var pressed = false;
			try
			{
				SendMouseFlags(MouseEventfLeftDown);
				pressed = true;
				await Task.Delay(45, ct);
				SendMouseFlags(MouseEventfLeftUp);
				pressed = false;
			}
			finally
			{
				if(pressed)
					SendMouseFlags(MouseEventfLeftUp);
			}
		}

		internal static async Task DragAsync(Point from, Point to, CancellationToken ct)
		{
			ThrowIfAborted(ct);
			await MoveSmoothlyAsync(from, steps: 10, stepDelayMs: 8, ct);
			await Task.Delay(45, ct);
			ThrowIfAborted(ct, from);

			var pressed = false;
			try
			{
				SendMouseFlags(MouseEventfLeftDown);
				pressed = true;
				await Task.Delay(95, ct);
				ThrowIfAborted(ct, from);

				await MoveSmoothlyAsync(to, steps: 16, stepDelayMs: 11, ct);
				await Task.Delay(80, ct);
				ThrowIfAborted(ct, to);

				SendMouseFlags(MouseEventfLeftUp);
				pressed = false;
				await Task.Delay(60, ct);
			}
			finally
			{
				if(pressed)
				{
					SendMouseFlags(MouseEventfLeftUp);
					Log.Debug("InputController: released left mouse button during cleanup.");
				}
			}
		}

		private static async Task MoveSmoothlyAsync(Point target, int steps, int stepDelayMs, CancellationToken ct)
		{
			var start = User32.GetMousePos();
			var lastExpected = start;

			for(var i = 1; i <= steps; i++)
			{
				ThrowIfAborted(ct, i > 1 ? lastExpected : null);
				var t = (double)i / steps;
				// Smoothstep ease-in-out
				var eased = t * t * (3.0 - 2.0 * t);
				var next = new Point(
					(int)Math.Round(start.X + (target.X - start.X) * eased),
					(int)Math.Round(start.Y + (target.Y - start.Y) * eased)
				);
				SendAbsoluteMove(next);
				lastExpected = next;
				await Task.Delay(stepDelayMs, ct);
			}

			SendAbsoluteMove(target);
		}

		private static void SendAbsoluteMove(Point screenPoint)
		{
			var vx = GetSystemMetrics(SmXVirtualScreen);
			var vy = GetSystemMetrics(SmYVirtualScreen);
			var vw = Math.Max(1, GetSystemMetrics(SmCxVirtualScreen));
			var vh = Math.Max(1, GetSystemMetrics(SmCyVirtualScreen));

			// Windows SendInput maps [0..65535] across the virtual desktop.
			var normX = (int)Math.Round((screenPoint.X - vx) * 65535.0 / Math.Max(1, vw - 1));
			var normY = (int)Math.Round((screenPoint.Y - vy) * 65535.0 / Math.Max(1, vh - 1));
			normX = Math.Max(0, Math.Min(65535, normX));
			normY = Math.Max(0, Math.Min(65535, normY));

			var input = new[]
			{
				new INPUT
				{
					Type = InputMouse,
					Mouse = new MOUSEINPUT
					{
						Dx = normX,
						Dy = normY,
						DwFlags = MouseEventfMove | MouseEventfAbsolute | MouseEventfVirtualDesk
					}
				}
			};
			SendInput(1, input, Marshal.SizeOf<INPUT>());
		}

		private static void SendMouseFlags(uint flags)
		{
			var input = new[]
			{
				new INPUT
				{
					Type = InputMouse,
					Mouse = new MOUSEINPUT { DwFlags = flags }
				}
			};
			SendInput(1, input, Marshal.SizeOf<INPUT>());
		}
	}
}
