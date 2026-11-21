#region

using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;

#endregion

namespace Hearthstone_Deck_Tracker
{
	/// <summary>
	/// Tray icon of the Battlegrounds-only tracker. Deck related menu entries have been removed.
	/// </summary>
	public class TrayIcon
	{
		public NotifyIcon NotifyIcon { get; }

		public MenuItem MenuItemShow { get; }

		public MenuItem MenuItemStartHearthstone { get; }

		public MenuItem MenuItemBattlegroundsSession { get; }

		public MenuItem MenuItemSettings { get; }

		public MenuItem MenuItemQuit { get; }

		public TrayIcon()
		{
#if(DEV)
			const string name = "Battlegrounds Tracker [DEV]";
			const bool includeRevision = true;
#else
			const string name = "Battlegrounds Tracker";
			const bool includeRevision = false;
#endif
			NotifyIcon = new NotifyIcon
			{
				Visible = true,
				ContextMenu = new ContextMenu(),
				Text = name
			};

			var iconFile = new FileInfo("Images/HearthstoneDeckTracker16.ico");
			if(iconFile.Exists)
				NotifyIcon.Icon = new Icon(iconFile.FullName);
			else
				Log.Error($"Cant find tray icon at \"{iconFile.FullName}\"");

			NotifyIcon.ContextMenu.MenuItems.Add(new MenuItem($"{name} v{Helper.GetCurrentVersion().ToVersionString(includeRevision)}") { Enabled = false });
			NotifyIcon.ContextMenu.MenuItems.Add("-");

			MenuItemShow = new MenuItem(LocUtil.Get("TrayIcon_MenuItemShow"), (_, _) => Core.MainWindow.ActivateWindow());
			NotifyIcon.ContextMenu.MenuItems.Add(MenuItemShow);

			MenuItemBattlegroundsSession = new MenuItem("Battlegrounds Session", (_, _) =>
			{
				Core.Windows.BattlegroundsSessionWindow.Show();
				Core.Windows.BattlegroundsSessionWindow.Activate();
			});
			NotifyIcon.ContextMenu.MenuItems.Add(MenuItemBattlegroundsSession);

			MenuItemSettings = new MenuItem(LocUtil.Get("TrayIcon_MenuItemSettings"), (_, _) => Core.MainWindow.ShowSettings());
			NotifyIcon.ContextMenu.MenuItems.Add(MenuItemSettings);

			NotifyIcon.ContextMenu.MenuItems.Add("-");

			MenuItemStartHearthstone = new MenuItem(LocUtil.Get("TrayIcon_MenuItemStartHearthstone"), (_, _) => HearthstoneRunner.StartHearthstone().Forget());
			NotifyIcon.ContextMenu.MenuItems.Add(MenuItemStartHearthstone);
			HearthstoneRunner.StartingHearthstone += starting => MenuItemStartHearthstone.Enabled = !starting;

			NotifyIcon.ContextMenu.MenuItems.Add("-");

			MenuItemQuit = new MenuItem(LocUtil.Get("TrayIcon_MenuItemQuit"), (_, _) => _ = Core.Shutdown());
			NotifyIcon.ContextMenu.MenuItems.Add(MenuItemQuit);

			NotifyIcon.MouseClick += (_, args) =>
			{
				if(args.Button == MouseButtons.Left)
					Core.MainWindow.ActivateWindow();
			};

			NotifyIcon.BalloonTipClicked += (_, _) => Core.MainWindow.ActivateWindow();
		}

		public void ShowMessage(string text, string title = "Battlegrounds Tracker", int duration = 5, ToolTipIcon icon = ToolTipIcon.Info)
			=> NotifyIcon.ShowBalloonTip(duration, title, text, icon);
	}
}
