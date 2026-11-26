using System;
using System.Drawing;

namespace Hearthstone_Deck_Tracker.Automation
{
	/// <summary>
	/// Maps Hearthstone Battlegrounds UI elements from normalized 4:3 board coordinates
	/// to physical screen pixels (matching Win32 SendInput / GetCursorPos coordinates).
	/// </summary>
	internal static class BattlegroundsBoardCoordinates
	{
		// Normalized (0..1, 0..1) coordinates inside Hearthstone's centered 4:3 playable area.
		private const double TavernUpgradeX = 0.398;
		private const double TavernUpgradeY = 0.215;

		private const double TavernRerollX = 0.602;
		private const double TavernRerollY = 0.215;

		private const double TavernFreezeX = 0.655;
		private const double TavernFreezeY = 0.215;

		private const double PlayerHeroPowerX = 0.592;
		private const double PlayerHeroPowerY = 0.780;

		private const double BoardCenterX = 0.500;
		private const double PlayerBoardY = 0.565;
		private const double ShopBoardY = 0.390;

		// Horizontal center-to-center spacing between adjacent board minions in 4:3 space.
		private const double MinionSlotSpacingX = 0.0975;

		internal static bool TryGetClientRect(out Rectangle clientRect)
		{
			clientRect = User32.GetHearthstoneRect(dpiScaling: false);
			return clientRect.Width >= 200 && clientRect.Height >= 150;
		}

		internal static Point ToScreenPoint(Rectangle clientRect, double normalizedX, double normalizedY)
		{
			GetBoardBounds(clientRect, out var boardLeft, out var boardTop, out var boardWidth, out var boardHeight);
			var x = (int)Math.Round(boardLeft + normalizedX * boardWidth);
			var y = (int)Math.Round(boardTop + normalizedY * boardHeight);
			return new Point(x, y);
		}

		internal static Point GetTavernUpgradeButton(Rectangle clientRect)
			=> ToScreenPoint(clientRect, TavernUpgradeX, TavernUpgradeY);

		internal static Point GetTavernRerollButton(Rectangle clientRect)
			=> ToScreenPoint(clientRect, TavernRerollX, TavernRerollY);

		internal static Point GetTavernFreezeButton(Rectangle clientRect)
			=> ToScreenPoint(clientRect, TavernFreezeX, TavernFreezeY);

		internal static Point GetPlayerHeroPower(Rectangle clientRect)
			=> ToScreenPoint(clientRect, PlayerHeroPowerX, PlayerHeroPowerY);

		internal static Point GetPlayerMinionSlot(Rectangle clientRect, int minionCount, int zeroBasedIndex)
		{
			var nx = GetSlotNormalizedX(minionCount, zeroBasedIndex);
			return ToScreenPoint(clientRect, nx, PlayerBoardY);
		}

		internal static Point GetShopMinionSlot(Rectangle clientRect, int shopCount, int zeroBasedIndex)
		{
			var nx = GetSlotNormalizedX(shopCount, zeroBasedIndex);
			return ToScreenPoint(clientRect, nx, ShopBoardY);
		}

		/// <summary>
		/// Computes the drop point when dragging one player minion to <paramref name="targetIndex"/>
		/// (0-based in the resulting board of size <paramref name="minionCount"/>).
		/// While dragging, the remaining (n - 1) minions collapse around center; the midpoint between
		/// remaining slots (targetIndex - 1) and targetIndex coincides with the n-minion slot center,
		/// while outer edges receive a small outward margin so Hearthstone reliably snaps to the ends.
		/// </summary>
		internal static Point GetPlayerMinionDropTarget(Rectangle clientRect, int minionCount, int targetIndex)
		{
			var nx = GetSlotNormalizedX(minionCount, targetIndex);
			if(minionCount > 1)
			{
				if(targetIndex == 0)
					nx -= MinionSlotSpacingX * 0.25;
				else if(targetIndex == minionCount - 1)
					nx += MinionSlotSpacingX * 0.25;
			}
			return ToScreenPoint(clientRect, nx, PlayerBoardY);
		}

		private static double GetSlotNormalizedX(int count, int zeroBasedIndex)
		{
			if(count <= 1)
				return BoardCenterX;
			var clamped = Math.Max(0, Math.Min(count - 1, zeroBasedIndex));
			return BoardCenterX + (clamped - (count - 1) / 2.0) * MinionSlotSpacingX;
		}

		private static void GetBoardBounds(Rectangle clientRect, out double left, out double top, out double width, out double height)
		{
			height = clientRect.Height;
			width = height * (4.0 / 3.0);
			if(width > clientRect.Width)
			{
				width = clientRect.Width;
				height = width * (3.0 / 4.0);
			}
			left = clientRect.Left + (clientRect.Width - width) / 2.0;
			top = clientRect.Top + (clientRect.Height - height) / 2.0;
		}
	}
}
