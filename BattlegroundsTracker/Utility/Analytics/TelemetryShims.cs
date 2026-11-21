using System;

namespace Hearthstone_Deck_Tracker.Utility.Analytics
{
	/// <summary>
	/// No-op replacement for the telemetry layer of the upstream project. Telemetry (Influx/HSReplay
	/// analytics) has been removed from this Battlegrounds-only fork; the methods remain so that the
	/// Battlegrounds code, which reports anonymous usage statistics in the upstream project, keeps
	/// compiling. Nothing is recorded or sent.
	/// </summary>
	internal static class Influx
	{
		public static void OnAppStart(params object?[] _) { }
		public static void OnAppExit(params object?[] _) { }
		public static void OnMainWindowActivated() { }
		public static void OnMainWindowDeactivated() { }
		public static void OnMemoryReading(params object?[] _) { }
		public static void OnHearthMirrorExit(params object?[] _) { }
		public static void OnUnevenPermissions() { }
		public static void OnHighMemoryUsage(params object?[] _) { }
		public static void OnBobsBuddyEnabledChanged(bool _) { }
		public static void OnBobsBuddyCombatInShoppingTurn(params object?[] _) { }
		public static void OnBobsBuddyUnsupportedInteraction(params object?[] _) { }
		public static void OnBobsBuddySimulationCompleted(
			object? result,
			object? output,
			int turn,
			string region,
			object? anomaly,
			object? terminalCase,
			bool isDuos,
			bool isOpposingAkazamzarak,
			bool isOpposingKelThuzad,
			bool reconnected) { }
		public static void OnBattlegroundsGameCompleted(params object?[] _) { }
		public static void OnBattlegroundsHDTToolsExit(params object?[] _) { }
		public static void OnBattlegroundsHDTToolsExecutionProblem(params object?[] _) { }
		public static void OnBattlegroundsHDTToolsNotFound(params object?[] _) { }
		public static void OnGetBattlegroundsCompositionGuidesError(params object?[] _) { }
		public static void OnGetBattlegroundsCompositionStatsError(params object?[] _) { }
		public static void OnMulliganGuideDeckSerializationError(params object?[] _) { }
		public static void OnSessionRecapEnabledChanged(bool _) { }
		public static void OnCollectionSyncingBannerClicked(params object?[] _) { }
		public static void OnCollectionSyncingBannerClosed() { }
		public static void OnCollectionSyncingEnabled(params object?[] _) { }
		public static void OnHsReplayAutoUploadChanged(params object?[] _) { }
		public static void OnHsReplayDataLoaded() { }
		public static void OnEndOfGameUploadError(params object?[] _) { }
		public static void OnMulliganToastClose() { }
		public static void SendQueuedMetrics() { }
	}

	/// <summary>
	/// No-op replacement for the Sentry crash reporting of the upstream project.
	/// </summary>
	internal static class SentryReporter
	{
		public static void Initialize(params object?[] _) { }
		public static void CaptureException(params object?[] _) { }
		public static void CaptureUserFeedback(params object?[] _) { }
		public static void CaptureHDTToolsExecutionProblem(params object?[] _) { }
		public static void CaptureHDTToolsExitProblem(params object?[] _) { }
		public static void FlushBattlegroundsEvents(params object?[] _) { }
		public static void FlushBeforeShutdown() { }
		public static void CaptureBobsBuddyException(Exception exception, object? input, int turn, bool isDuos, BobsBuddySentryReportContext context) { }
		public static void QueueBobsBuddyTerminalCase(
			object? input,
			object? output,
			object? result,
			int turn,
			object? region,
			bool isDuos,
			bool isOpposingAkazamzarak,
			BobsBuddySentryReportContext reportContext) { }
	}

	internal sealed class BobsBuddySentryReportContext
	{
		public BobsBuddySentryReportContext(bool IsDuosPartialCombat, int ReRunCount)
		{
			this.IsDuosPartialCombat = IsDuosPartialCombat;
			this.ReRunCount = ReRunCount;
		}

		public bool IsDuosPartialCombat { get; }
		public int ReRunCount { get; }
	}
}
