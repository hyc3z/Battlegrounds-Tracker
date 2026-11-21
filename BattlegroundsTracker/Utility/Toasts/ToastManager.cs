#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.Utility.Toasts.ToastControls;
using System.Drawing;

#endregion

namespace Hearthstone_Deck_Tracker.Utility.Toasts
{
	public static class ToastManager
	{
		private static readonly List<ToastHelper> Toasts = new List<ToastHelper>();

		internal static void ShowBattlegroundsToast(int[] heroDbfIds, bool duos, int? anomalyDbfId)
		{
			var th = new ToastHelper(new BattlegroundsToast(heroDbfIds, duos, anomalyDbfId, null));
			ShowToast(th, 6);
		}

		public static void ShowGameResultToast(params object?[] _) { }

		public static void ShowCustomToast(UserControl content) => ShowToast(new ToastHelper(content));

		public static void ForceCloseToast(UserControl control)
		{
			var toast = Toasts.FirstOrDefault(x => x.IsToastWindow(control));
			toast?.ForceClose();
		}

		private static async void ShowToast(ToastHelper toastHelper, int fadeOutDelay = 0)
		{
			Toasts.Add(toastHelper);
			toastHelper.Show();
			UpdateToasts();
			await toastHelper.HandleToast(fadeOutDelay);
			Toasts.Remove(toastHelper);
		}

		internal static void UpdateToasts()
		{
			var hsWindow = User32.GetHearthstoneWindow();
			var hsRect = hsWindow == IntPtr.Zero ? (Rectangle?)null : User32.GetHearthstoneRect(true);
			var bottom = hsRect?.Bottom ?? SystemParameters.WorkArea.Bottom;
			var right = hsRect?.Right ?? SystemParameters.WorkArea.Right;
			var offset = 0;
			foreach(var toast in Toasts)
				offset = toast.SetPosition(bottom, right, offset);
		}
	}
}
