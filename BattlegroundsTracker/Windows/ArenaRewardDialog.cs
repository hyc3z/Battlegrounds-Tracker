using System.Windows;
using Hearthstone_Deck_Tracker.Hearthstone;

namespace Hearthstone_Deck_Tracker.Windows
{
	/// <summary>
	/// Compatibility stub for the removed arena reward dialog. Arena is not part of this fork; the dialog
	/// is only instantiated by dormant upstream code paths and never displays anything.
	/// </summary>
	public class ArenaRewardDialog : Window
	{
		public ArenaRewardDialog(Deck deck)
		{
			Title = "Arena Rewards";
			Width = 0;
			Height = 0;
			Visibility = Visibility.Hidden;
			ShowInTaskbar = false;
		}
	}
}
