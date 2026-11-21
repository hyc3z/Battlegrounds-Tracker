#region

using System.Collections.Generic;
using System.Linq;
using Hearthstone_Deck_Tracker.Utility.Extensions;

#endregion

namespace Hearthstone_Deck_Tracker.Utility.HotKeys
{
	public class PredefinedHotKeyActionInfo
	{
		public PredefinedHotKeyActionInfo(string title, string description, string methodName)
		{
			Title = title;
			Description = description;
			MethodName = methodName;
		}

		public string Title { get; }
		public string Description { get; }
		public string MethodName { get; }
	}

	/// <summary>
	/// Hotkey actions of the Battlegrounds-only tracker. Everything related to deck management, importing,
	/// screenshots and the constructed overlay has been removed.
	/// </summary>
	public class PredefinedHotKeyActions
	{
		public static IEnumerable<PredefinedHotKeyActionInfo> PredefinedActionNames
		{
			get
			{
				return
					typeof(PredefinedHotKeyActions).GetMethods()
						.Where(x => x.GetCustomAttributes(typeof(PredefinedHotKeyActionAttribute), false).Any())
						.Select(x =>
						{
							var attr = ((PredefinedHotKeyActionAttribute)x.GetCustomAttributes(typeof(PredefinedHotKeyActionAttribute), false)[0]);
							return new PredefinedHotKeyActionInfo(attr.Title, attr.Description, x.Name);
						});
			}
		}

		[PredefinedHotKeyAction("Toggle overlay", "Turns the overlay on or off (if the game is running).")]
		public static void ToggleOverlay()
		{
			if(!Core.Game.IsRunning)
				return;
			Config.Instance.HideOverlay = !Config.Instance.HideOverlay;
			Config.Save();
			Core.Overlay.UpdatePosition();
		}

		[PredefinedHotKeyAction("Toggle tavern markers", "Turns the tavern markers (minion pinning) on or off.")]
		public static void ToggleTavernMarkers()
		{
			Config.Instance.ShowBattlegroundsTavernMarkers = !Config.Instance.ShowBattlegroundsTavernMarkers;
			Config.Save();
			Core.Overlay.UpdateBattlegroundsOverlay();
		}

		[PredefinedHotKeyAction("Toggle minion browser", "Turns the Battlegrounds minion browser on or off.")]
		public static void ToggleBattlegroundsBrowser()
		{
			Config.Instance.ShowBattlegroundsBrowser = !Config.Instance.ShowBattlegroundsBrowser;
			Config.Save();
			Core.Overlay.UpdateBattlegroundsOverlay();
		}

		[PredefinedHotKeyAction("Show session results", "Brings up the Battlegrounds session recap window.")]
		public static void ShowBattlegroundsSession()
		{
			Core.Windows.BattlegroundsSessionWindow.Show();
			Core.Windows.BattlegroundsSessionWindow.Activate();
		}

		[PredefinedHotKeyAction("Start Hearthstone", "Starts the Battle.net launcher and/or Hearthstone.")]
		public static void StartHearthstone() => HearthstoneRunner.StartHearthstone().Forget();

		[PredefinedHotKeyAction("Show main window", "Brings up the main window.")]
		public static void ShowMainWindow() => Core.MainWindow.ActivateWindow();

		[PredefinedHotKeyAction("Reset tracker", "Resets the tracker to the last game start.")]
		public static void ReloadDeck() => _ = Core.Reset();

		[PredefinedHotKeyAction("Close tracker", "Closes the tracker.")]
		public static void CloseHdt() => _ = Core.Shutdown();
	}
}
