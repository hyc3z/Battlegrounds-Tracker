using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthstone_Deck_Tracker.Automation
{
	/// <summary>
	/// Records a compact chronological log of the player's Battlegrounds shopping actions
	/// so the LLM advisor can review recent plays and critique ordering/economy.
	/// </summary>
	internal static class BattlegroundsActionHistory
	{
		private const int MaxEntries = 40;
		private static readonly object Sync = new();
		private static readonly List<(int Turn, string Time, string Action)> Entries = new();

		internal static void Reset()
		{
			lock(Sync)
				Entries.Clear();
		}

		internal static void Record(int turn, string action)
		{
			if(string.IsNullOrWhiteSpace(action))
				return;
			lock(Sync)
			{
				Entries.Add((turn, DateTime.Now.ToString("HH:mm:ss"), action));
				while(Entries.Count > MaxEntries)
					Entries.RemoveAt(0);
			}
		}

		internal static List<string> GetRecent(int maxCount = 15)
		{
			lock(Sync)
			{
				var skip = Math.Max(0, Entries.Count - maxCount);
				return Entries.Skip(skip)
					.Select(e => $"[T{e.Turn} {e.Time}] {e.Action}")
					.ToList();
			}
		}
	}
}
