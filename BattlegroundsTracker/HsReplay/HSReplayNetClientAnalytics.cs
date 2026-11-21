namespace Hearthstone_Deck_Tracker.HsReplay
{
	/// <summary>
	/// No-op replacement for the HSReplay.net client analytics of the upstream project. Analytics have been
	/// removed from this Battlegrounds-only fork; the methods are kept so the call sites keep compiling.
	/// </summary>
	internal static class HSReplayNetClientAnalytics
	{
		public static void Initialize(params object?[] _) { }
		public static void OnSetInitialConfigLanguage(params object?[] _) { }
		public static void OnShowPersonalStats(params object?[] _) { }
		public static void OnConstructedMatchEnds(params object?[] _) { }
		public static void OnBattlegroundsMatchEnds(params object?[] _) { }
		public static void OnMercenariesMatchEnds(params object?[] _) { }
		public static void OnClickSubscribeNowLink(params object?[] _) { }
		public static void TryTrackToastClick(params object?[] _) { }
		public static void TryTrackToastClose(params object?[] _) { }
		public static void OnArenaDraftStart(params object?[] _) { }
		public static void OnArenasmithMissingScore(params object?[] _) { }
	}
}
