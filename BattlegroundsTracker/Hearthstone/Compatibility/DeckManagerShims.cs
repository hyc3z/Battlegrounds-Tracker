using System.Collections.Generic;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using Hearthstone_Deck_Tracker.Stats;

namespace Hearthstone_Deck_Tracker
{
	/// <summary>
	/// Compatibility stub for the removed deck manager. Deck management is not part of this fork; the
	/// remaining upstream game-model code paths that used to import/auto-select decks do nothing here.
	/// </summary>
	internal static class DeckManager
	{
		public static void ResetIgnoredDeckId() { }
		public static void ResetAutoSelectCount() { }
		public static Task DetectCurrentDeck() => Task.CompletedTask;
		public static void AutoSelectDeckById(IGame game, long deckId) { }
		public static void AutoSelectTemplateDeckByDeckTemplateId(IGame game, int templateId) { }
		public static void AutoSelectTemplateDeckByDeckId(IGame game, int deckId) { }
		public static void ImportDecks(params object?[] _) { }
	}

	internal static class LogUploader
	{
		public static Task<bool> Upload(string[] log, GameMetaData metaData, GameStats stats) => Task.FromResult(false);
	}

	/// <summary>
	/// Compatibility stubs for the HSReplay.net trial/feature helpers of the upstream project.
	/// </summary>
	internal static class MulliganGuideTrial
	{
		public static int? RemainingTrials => 0;
		public static void Clear() { }
		public static Task<string?> ActivateOrContinue(params object?[] _) => Task.FromResult<string?>(null);
		public static Task Update(params object?[] _) => Task.CompletedTask;
	}

	internal static class OutfinderTrial
	{
		public static bool HasAccess => false;
		public static string? Token => null;
		public static void Clear() { }
		public static Task<bool> ActivateOrContinue(IGame game) => Task.FromResult(false);
	}

	internal static class ArenaTrial
	{
		public static Task EnsureLoaded(params object?[] _) => Task.CompletedTask;
		public static bool IsDeckResumable(long deckId) => false;
	}

	internal static class HsReplayDataManager
	{
		public static HsReplayDecksData Decks { get; } = new();
	}

	internal sealed class HsReplayDecksData
	{
		public HashSet<string> AvailableDecks { get; } = new();
	}

	public enum SingleDeckState
	{
		None,
		V1_READY,
		V2_READY,
		V2_PARTIAL
	}
}
