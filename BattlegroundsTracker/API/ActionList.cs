using System;

namespace Hearthstone_Deck_Tracker.API
{
	/// <summary>
	/// Minimal replacement for the upstream plugin event list. The plugin system has been removed from
	/// this Battlegrounds-only fork; log line events are kept so that the log watcher can keep invoking
	/// them without any additional behavior.
	/// </summary>
	public class ActionList<T>
	{
		public event Action<T>? OnExecute;

		public void Add(Action<T> action) => OnExecute += action;

		public void Remove(Action<T> action) => OnExecute -= action;

		public void Execute() => Execute(default(T)!);

		public void Execute(T value)
		{
			var handlers = OnExecute;
			if(handlers == null)
				return;
			foreach(Action<T> handler in handlers.GetInvocationList())
			{
				try
				{
					handler(value);
				}
				catch
				{
					// plugin callbacks are gone; never let a handler break the log watcher
				}
			}
		}
	}
}
