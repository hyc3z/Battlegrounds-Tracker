using System.Collections.Generic;
using System.Windows;

namespace Hearthstone_Deck_Tracker.Windows
{
	/// <summary>
	/// Compatibility members for the constructed/arena/mercenaries overlay panels that were removed from
	/// this fork. The remaining, mostly dormant upstream code paths still call them; they do nothing here.
	/// </summary>
	public partial class OverlayWindow
	{
		internal LinkOpponentDeckPanelCompat LinkOpponentDeckDisplay { get; } = new();
		internal ConstructedMulliganPreLobbyCompat ConstructedMulliganGuidePreLobbyViewModel { get; } = new();
		internal MercenariesTaskListCompat MercenariesTaskListVM { get; } = new();

		internal void HideTimers() { }
		internal void HideSecrets() { }
		internal void UnhideSecrects() { }
		internal void HideLinkOpponentDeckDisplay() { }
		internal void ShowLinkOpponentDeckDisplay() { }
		internal void HideMulliganGuideStats() { }
		internal void HideMulliganToast(bool _) { }
		internal void ShowMulliganToast(params object?[] _) { }
		internal void ShowMulliganGuideStats(params object?[] _) { }
		internal void ShowMulliganV2Stats(params object?[] _) { }
		internal void ShowMulliganV2StatsAfterMulligan(params object?[] _) { }
		internal void UpdateMercenariesOverlay() { }
		internal void HideMercenariesGameOverlay() { }
		internal void SetWinRates() { }
		internal void SetConstructedQueue(bool _) { }
		internal void UpdateTurnTimer(object? _) { }
		internal void ShowRelatedCardsPanel(params object?[] _) { }
		internal void ShowExperienceCounter() { }
		internal void HideExperienceCounter() { }
		internal void ShowMercenariesTasksButton() { }
		internal void HideMercenariesTasksButton() { }
	}

	internal class LinkOpponentDeckPanelCompat
	{
		public bool IsFriendlyMatch { get; set; }
	}

	internal class ConstructedMulliganPreLobbyCompat
	{
		public Dictionary<HearthDb.Enums.BnetGameType, Dictionary<string, SingleDeckState>> DeckStatusByDeckstring { get; } = new();
		public void InvalidateAllDecks() { }
	}

	internal class MercenariesTaskListCompat
	{
		public Visibility GameNoticeVisibility { get; set; } = Visibility.Collapsed;
		public void Update() { }
	}
}
