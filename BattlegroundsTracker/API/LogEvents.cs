namespace Hearthstone_Deck_Tracker.API
{
	/// <summary>
	/// Minimal replacement for the upstream plugin log events. No plugins are loaded in this fork, so the
	/// lists are only kept to preserve the log watcher call sites.
	/// </summary>
	public class LogEvents
	{
		public static readonly ActionList<string> OnAchievementsLogLine = new();
		public static readonly ActionList<string> OnArenaLogLine = new();
		public static readonly ActionList<string> OnAssetLogLine = new();
		public static readonly ActionList<string> OnBobLogLine = new();
		public static readonly ActionList<string> OnPowerLogLine = new();
		public static readonly ActionList<string> OnPowerGamestateLogLine = new();
		public static readonly ActionList<string> OnGameplayLogLine = new();
	}
}
