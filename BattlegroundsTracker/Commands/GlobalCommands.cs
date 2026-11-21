using System.Windows.Input;
using Hearthstone_Deck_Tracker.HsReplay;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Extensions;

namespace Hearthstone_Deck_Tracker.Commands
{
	/// <summary>
	/// Global commands used by the Battlegrounds overlays. The deck/arena/mercenaries commands of the upstream
	/// project have been removed.
	/// </summary>
	internal static class GlobalCommands
	{
		public static ICommand ShowSettings { get; } = new Command(() => Core.MainWindow.ShowSettings());

		public static ICommand OpenUrl { get; } = new Command<string>(url => Helper.TryOpenUrl(url));

		public static ICommand SignInCommand { get; } = new Command(() => SignIn());

		private static async void SignIn()
		{
			try
			{
				await HSReplayNetOAuth.Authenticate();
			}
			catch
			{
				// the user cancelled or the browser flow failed; the account status in the main window reflects it
			}
		}
	}
}
