namespace Hearthstone_Deck_Tracker.API
{
	/// <summary>
	/// Minimal plugin event arguments. The plugin system has been removed from this fork; the types are
	/// kept because the game event handler still raises the (unsubscribed) events.
	/// </summary>
	public class AttackInfo
	{
		public AttackInfo(Hearthstone.Card attacker, Hearthstone.Card defender)
		{
			Attacker = attacker;
			Defender = defender;
		}

		public Hearthstone.Card Attacker { get; }
		public Hearthstone.Card Defender { get; }
	}

	public class PredamageInfo
	{
		public PredamageInfo(Hearthstone.Entities.Entity entity, int value)
		{
			Entity = entity;
			Value = value;
		}

		public Hearthstone.Entities.Entity Entity { get; }
		public int Value { get; }
	}
}
