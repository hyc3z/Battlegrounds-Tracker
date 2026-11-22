#region

using System;
using Hearthstone_Deck_Tracker.BobsBuddy;
using Hearthstone_Deck_Tracker.Enums.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.LogReader.Interfaces;
using HearthWatcher.LogReader;

#endregion

namespace Hearthstone_Deck_Tracker.LogReader.Handlers
{
	/// <summary>
	/// The LoadingScreen log carries the scene transitions and the two lines that tell us a match began.
	/// Hearthstone does not log a game start in the Power log, so the current mode and the game start
	/// notification can only come from here.
	/// </summary>
	public class LoadingScreenHandler
	{
		public void Handle(LogLine logLine, IHsGameState gameState, IGame game)
		{
			// OnScenePreUnload/OnSceneUnloaded announce the next scene; OnSceneLoaded is the one that happened.
			if(LogConstants.NextGameModeRegex.IsMatch(logLine.Line))
				return;

			var match = LogConstants.GameModeRegex.Match(logLine.Line);
			if(match.Success)
			{
				game.PreviousMode = GetMode(match.Groups["prev"].Value);
				game.CurrentMode = GetMode(match.Groups["curr"].Value);

				// Leaving the game scene ends the match. This is what makes the session panel, the
				// stats and the reset on the next game start work; nothing in the Power log says so.
				if(game.PreviousMode == Mode.GAMEPLAY && game.CurrentMode != Mode.GAMEPLAY)
					gameState.GameHandler?.HandleInMenu();

				// The queue watcher only has to poll while we are not in a match.
				if(game.CurrentMode > Mode.LOGIN && game.CurrentMode != Mode.GAMEPLAY)
					Watchers.QueueWatcher.Run();
				else
					Watchers.QueueWatcher.Stop();

				return;
			}

			if(logLine.LineContent.Contains("Gameplay.Start"))
			{
				// The Chinese client module reads the game state through its own bridge.
				if(Core.Game.IsChinaModuleActive)
				{
					Core.Game.PowerLog.Clear();
					Core.Game.Entities.Clear();
					return;
				}
				gameState.Reset();
				gameState.GameHandler?.HandleGameStart(logLine.Time);
			}
			else if(logLine.LineContent.Contains("MulliganManager.HandleGameStart")
			        && logLine.LineContent.Contains("IsPastBeginPhase()=True"))
			{
				// Joined after the mulligan: the beginning of the game was never logged, so this is
				// a reconnect rather than a new game.
				BobsBuddyInvoker.OnGameReconnect();
				if(Core.Game.IsChinaModuleActive)
					return;
				gameState.GameHandler?.HandleGameReconnect(logLine.Time);
			}
		}

		private static Mode GetMode(string modeString) => Enum.TryParse(modeString, out Mode mode) ? mode : Mode.INVALID;
	}
}
