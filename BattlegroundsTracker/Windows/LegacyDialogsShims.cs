using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Stats;
using Hearthstone_Deck_Tracker.Utility;

namespace Hearthstone_Deck_Tracker.Windows
{
	/// <summary>
	/// Compatibility stubs for the removed game/discard/note dialogs. The post-game workflow these belonged to
	/// (assigning games to decks) is not part of this fork, so the dialogs never display and immediately
	/// report the "keep" result.
	/// </summary>
	public class MoveGameDialog : Window
	{
		public MoveGameDialog(IEnumerable<Deck> decks)
		{
			Decks = decks.ToList();
		}

		public List<Deck> Decks { get; }
		public Deck? SelectedDeck { get; set; }
		public SerializableVersion? SelectedVersion { get; set; }
	}

	public class DiscardGameDialog : Window
	{
		public DiscardGameDialog(GameStats stats)
		{
			Stats = stats;
		}

		public GameStats Stats { get; }
		public DiscardGameDialogResult Result { get; set; } = DiscardGameDialogResult.Keep;
	}

	public class NoteDialog : Window
	{
		public NoteDialog(GameStats stats)
		{
			Stats = stats;
		}

		public GameStats Stats { get; }
	}
}
