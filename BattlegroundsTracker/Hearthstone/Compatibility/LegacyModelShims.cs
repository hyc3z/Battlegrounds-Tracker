using Hearthstone_Deck_Tracker.Enums;
using System;

namespace Hearthstone_Deck_Tracker.Controls.Stats
{
	/// <summary>
	/// Compatibility model for the arena reward tracking of the upstream project. The arena UI has been
	/// removed from this fork; the type is retained because the deck model still carries (empty) rewards.
	/// </summary>
	public class ArenaReward
	{
		public int Gold;
		public int Dust;
		public bool CrowdsFavor;
		public int TavernTickets;
		public ArenaRewardPacks[] Packs = Array.Empty<ArenaRewardPacks>();
		public ArenaRewardCard?[] Cards = Array.Empty<ArenaRewardCard?>();
	}

	public class ArenaRewardCard
	{
		public string? CardId;
		public bool Golden;
	}
}

namespace Hearthstone_Deck_Tracker.Controls.Overlay.Constructed.Mulligan
{
	/// <summary>
	/// Compatibility model for the constructed mulligan guide statistics. The mulligan guide UI has been
	/// removed from this fork; the BG game model still exposes the (never populated) card statistics.
	/// </summary>
	public class SingleCardStats
	{
		public SingleCardStats(int dbfId)
		{
			DbfId = dbfId;
		}

		public int DbfId { get; set; }
		public float? OpeningHandWinrate { get; set; }
		public float? BaseWinrate { get; set; }

		public static System.Collections.Generic.Dictionary<int, SingleCardStats> GroupCardStats<T>(
			System.Collections.Generic.Dictionary<int, T> grouped, double? baseWinrate)
		{
			var result = new System.Collections.Generic.Dictionary<int, SingleCardStats>();
			foreach(var key in grouped.Keys)
				result[key] = new SingleCardStats(key) { BaseWinrate = (float?)baseWinrate };
			return result;
		}
	}
}
