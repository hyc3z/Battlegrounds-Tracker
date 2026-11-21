#region

using System.Threading.Tasks;
using System.Windows;
using Hearthstone_Deck_Tracker.Utility;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using static MahApps.Metro.Controls.Dialogs.MessageDialogStyle;

#endregion

namespace Hearthstone_Deck_Tracker.Windows
{
	/// <summary>
	/// Message dialogs of the Battlegrounds-only tracker. Deck, statistics and upload related dialogs were removed.
	/// </summary>
	public static class MessageDialogs
	{
		private const string LocRestartTitle = "MessageDialogs_Restart_Title";
		private const string LocRestartText = "MessageDialogs_Restart_Text";
		private const string LocRestartButtonRestart = "MessageDialogs_Restart_Button_Restart";
		private const string LocRestartButtonLater = "MessageDialogs_Restart_Button_Later";

		private const string LocLogConfigTitle = "MessageDialogs_LogConfig_Title";
		private const string LocLogConfigDescription1 = "MessageDialogs_LogConfig_Description1";
		private const string LocLogConfigDescription2 = "MessageDialogs_LogConfig_Description2";
		private const string LocLogConfigDescription3 = "MessageDialogs_LogConfig_Description3";
		private const string LocLogConfigButtonInstructions = "MessageDialogs_LogConfig_Button_Instructions";
		private const string LocLogConfigButtonClose = "MessageDialogs_LogConfig_Button_Close";

		private const string LocPermissionsTitle = "MessageDialogs_Permissions_Title";
		private const string LocPermissionsDescription = "MessageDialogs_Permissions_Description";
		private const string LocPermissionsButtonInstructions = "MessageDialogs_UnevenPermissions_Button_Instructions";
		private const string LocPermissionsButtonClose = "MessageDialogs_UnevenPermissions_Button_Close";

		public static async void ShowRestartDialog(this MainWindow window)
		{
			var result = await window.ShowMessageAsync(LocUtil.Get(LocRestartTitle), LocUtil.Get(LocRestartText),
				AffirmativeAndNegative,
				new Settings
				{
					AffirmativeButtonText = LocUtil.Get(LocRestartButtonRestart),
					NegativeButtonText = LocUtil.Get(LocRestartButtonLater)
				});
			if(result == MessageDialogResult.Affirmative)
				Core.RestartApplication();
		}

		public static async Task ShowMessage(this MetroWindow window, string title, string message) => await window.ShowMessageAsync(title, message);

		public static async Task ShowLogConfigUpdateFailedMessage(this MetroWindow window)
		{
			var settings = new Settings
			{
				AffirmativeButtonText = LocUtil.Get(LocLogConfigButtonInstructions),
				NegativeButtonText = LocUtil.Get(LocLogConfigButtonClose)
			};
			var result = await window.ShowMessageAsync(LocUtil.Get(LocLogConfigTitle),
				LocUtil.Get(LocLogConfigDescription1) + System.Environment.NewLine + System.Environment.NewLine
				+ LocUtil.Get(LocLogConfigDescription2) + System.Environment.NewLine + System.Environment.NewLine
				+ LocUtil.Get(LocLogConfigDescription3),
				AffirmativeAndNegative, settings);
			if(result == MessageDialogResult.Affirmative)
				Helper.TryOpenUrl("https://github.com/HearthSim/Hearthstone-Deck-Tracker/wiki/Setting-up-the-log.config");
		}

		public static async Task ShowUnevenPermissionsMessage(this MetroWindow window)
		{
			var settings = new Settings
			{
				AffirmativeButtonText = LocUtil.Get(LocPermissionsButtonInstructions),
				NegativeButtonText = LocUtil.Get(LocPermissionsButtonClose)
			};
			var result = await window.ShowMessageAsync(LocUtil.Get(LocPermissionsTitle), LocUtil.Get(LocPermissionsDescription),
				AffirmativeAndNegative, settings);
			if(result == MessageDialogResult.Affirmative)
				Helper.TryOpenUrl("https://help.hearthsim.net/en/articles/16945833-how-can-i-fix-the-error-unable-to-connect-to-hearthstone");
		}

		public class Settings : MetroDialogSettings
		{
			public Settings()
			{
				AnimateHide = AnimateShow = Config.Instance.UseAnimations;
			}
		}
	}
}
