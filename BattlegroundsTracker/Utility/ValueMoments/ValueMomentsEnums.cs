namespace Hearthstone_Deck_Tracker.Utility.ValueMoments.Enums
{
	/// <summary>
	/// Compatibility enums for the analytics "value moments" of the upstream project. The analytics layer
	/// has been removed from this fork; the enums are kept so the Battlegrounds telemetry call sites compile.
	/// </summary>
	public enum Franchise
	{
		Battlegrounds
	}

	public enum ValueMomentType
	{
		None
	}
}

namespace Hearthstone_Deck_Tracker.Utility.ValueMoments.Actions
{
	public static class ToastAction
	{
		public enum Toast
		{
			None,
			BattlegroundsHeroPicker,
			BattlegroundsTimewarp
		}
	}

	public static class ClickSubscribeNowAction
	{
		public enum Button
		{
			None,
			BattlegroundsPreLobby
		}
	}
}
