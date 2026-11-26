using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.BobsBuddy;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace Hearthstone_Deck_Tracker.Automation
{
	/// <summary>
	/// Coordinates experimental v2 Battlegrounds automation rules and actions.
	/// All actions are opt-in, shopping-phase only, foreground-gated, and cancelable via F8.
	/// </summary>
	internal static class BattlegroundsAutomationController
	{
		private static readonly object Sync = new();
		private static CancellationTokenSource? _activeCts;
		private static int _lastTurn2UpgradeAttemptedGameHash;
		private static bool _isBusy;

		internal static bool IsBusy
		{
			get
			{
				lock(Sync)
					return _isBusy;
			}
		}

		internal static void CancelActive(string reason)
		{
			CancellationTokenSource? toCancel;
			lock(Sync)
			{
				toCancel = _activeCts;
				_activeCts = null;
			}
			if(toCancel != null)
			{
				Log.Info($"Automation: cancelling active task ({reason})");
				try
				{
					toCancel.Cancel();
				}
				catch
				{
				}
			}
		}

		internal static void OnShoppingTurnStart(GameV2 game, int turn)
		{
			CancelActive("new shopping turn");

			if(!game.IsBattlegroundsMatch || game.IsBattlegroundsDuosMatch)
				return;

			if(turn == 2 && Config.Instance.BgsAutoUpgradeTurn2)
				_ = TryAutoUpgradeTurn2Async(game);
		}

		internal static void OnCombatStart() => CancelActive("combat started");
		internal static void OnGameEnd() => CancelActive("game ended");

		private static async Task TryAutoUpgradeTurn2Async(GameV2 game)
		{
			var gameHash = game.CurrentGameStats?.GameId.GetHashCode() ?? 0;
			if(gameHash != 0 && gameHash == _lastTurn2UpgradeAttemptedGameHash)
				return;

			var ct = BeginTask();
			if(ct == null)
				return;

			try
			{
				// Wait for the turn-start shopping transition animation to finish before clicking.
				await Task.Delay(1200, ct.Value);

				if(game.IsBattlegroundsCombatPhase || game.GetTurnNumber() != 2)
					return;

				var currentTier = game.Player.Hero?.GetTag(GameTag.PLAYER_TECH_LEVEL) ?? 0;
				if(currentTier != 1)
				{
					Log.Info($"Automation(T2 Upgrade): skipped, current tavern tier is {currentTier}.");
					return;
				}

				var upgradeButton = game.Player.Board.FirstOrDefault(e =>
					e.IsInPlay && (e.CardId?.StartsWith("TB_BaconShopTechUp", StringComparison.Ordinal) ?? false));
				if(upgradeButton == null)
				{
					Log.Info("Automation(T2 Upgrade): skipped, tavern upgrade button entity not found in play.");
					return;
				}

				var upgradeCost = upgradeButton.GetTag(GameTag.COST);
				var availableGold = GetAvailableGold(game);
				if(upgradeCost <= 0 || availableGold < upgradeCost)
				{
					Log.Info($"Automation(T2 Upgrade): skipped, available gold ({availableGold}) < upgrade cost ({upgradeCost}).");
					return;
				}

				if(!BattlegroundsBoardCoordinates.TryGetClientRect(out var clientRect))
				{
					Log.Warn("Automation(T2 Upgrade): skipped, Hearthstone window rect unavailable.");
					return;
				}

				_lastTurn2UpgradeAttemptedGameHash = gameHash;
				var target = BattlegroundsBoardCoordinates.GetTavernUpgradeButton(clientRect);
				Log.Info($"Automation(T2 Upgrade): clicking tavern upgrade at ({target.X}, {target.Y}) (gold={availableGold}, cost={upgradeCost}).");
				await InputController.ClickAsync(target, ct.Value);

				await Task.Delay(500, ct.Value);
				var newTier = game.Player.Hero?.GetTag(GameTag.PLAYER_TECH_LEVEL) ?? 0;
				Log.Info($"Automation(T2 Upgrade): post-click tavern tier = {newTier}.");
			}
			catch(OperationCanceledException ex)
			{
				Log.Info($"Automation(T2 Upgrade): aborted ({ex.Message})");
			}
			catch(Exception ex)
			{
				Log.Error($"Automation(T2 Upgrade): {ex}");
			}
			finally
			{
				EndTask();
			}
		}

		internal static async Task OptimizeAndApplyPositioningAsync()
		{
			if(!BobsBuddyInvoker.CanRerunLastCombat)
				return;

			var ct = BeginTask();
			if(ct == null)
			{
				Log.Info("Automation(Positioning): another automation task is already running.");
				return;
			}

			var display = Core.Overlay.BobsBuddyDisplay;
			try
			{
				var context = BobsBuddyInvoker.TryCreateRerunContext();
				if(context == null)
					return;

				display.SetErrorState(BobsBuddyErrorState.None, null, true);
				display.HidePercentagesShowSpinners();

				var result = await PositioningOptimizer.OptimizeAsync(context, ct.Value);
				if(result == null)
				{
					display.ShowPercentagesHideSpinners();
					return;
				}

				display.ShowCompletedSimulation(
					result.BestOutput.winRate,
					result.BestOutput.tieRate,
					result.BestOutput.lossRate,
					result.BestOutput.theirDeathRate,
					result.BestOutput.myDeathRate,
					result.BestOutput.damageResults.ToList());

				if(!result.ShouldReorder)
				{
					Log.Info("Automation(Positioning): current board is already optimal (or gain < threshold).");
					return;
				}

				if(!Config.Instance.BgsAutoPositioningApply)
				{
					Log.Info("Automation(Positioning): dry-run mode enabled (BgsAutoPositioningApply=false), skipping mouse drags. "
						+ "Recommended order entity IDs: " + string.Join(", ", result.BestEntityIds));
					return;
				}

				await ApplyBoardOrderAsync(Core.Game, result.BestEntityIds, ct.Value);
			}
			catch(OperationCanceledException ex)
			{
				Log.Info($"Automation(Positioning): aborted ({ex.Message})");
				display.ShowPercentagesHideSpinners();
			}
			catch(Exception ex)
			{
				Log.Error($"Automation(Positioning): {ex}");
				display.ShowPercentagesHideSpinners();
			}
			finally
			{
				EndTask();
			}
		}

		private static async Task ApplyBoardOrderAsync(GameV2 game, int[] targetEntityIds, CancellationToken ct)
		{
			var initialIds = GetLivePlayerBoardEntityIds(game);
			if(initialIds.Length != targetEntityIds.Length || !new HashSet<int>(initialIds).SetEquals(targetEntityIds))
			{
				Log.Warn("Automation(Positioning): player board changed while optimizing, skipping drags.");
				return;
			}

			var plannedMoves = PositioningOptimizer.PlanMinimalMoves(initialIds, targetEntityIds);
			Log.Info($"Automation(Positioning): applying {plannedMoves.Count} drag move(s) to reach optimal board.");

			foreach(var move in plannedMoves)
			{
				if(game.IsBattlegroundsCombatPhase)
					throw new OperationCanceledException("已进入战斗阶段，停止调整站位。");

				// Re-read live board order before every drag in case a previous drag shifted unexpectedly.
				var liveIds = GetLivePlayerBoardEntityIds(game);
				if(liveIds.Length != targetEntityIds.Length)
					throw new OperationCanceledException("场面随从数量发生变化，停止调整站位。");

				var liveFrom = Array.IndexOf(liveIds, move.EntityId);
				if(liveFrom < 0)
					throw new OperationCanceledException($"找不到随从实体 {move.EntityId}，停止调整站位。");
				if(liveFrom == move.TargetIndex)
					continue;

				if(!BattlegroundsBoardCoordinates.TryGetClientRect(out var clientRect))
					throw new OperationCanceledException("无法获取炉石窗口区域。");

				var fromPt = BattlegroundsBoardCoordinates.GetPlayerMinionSlot(clientRect, liveIds.Length, liveFrom);
				var toPt = BattlegroundsBoardCoordinates.GetPlayerMinionDropTarget(clientRect, liveIds.Length, move.TargetIndex);

				Log.Info($"Automation(Positioning): dragging entity {move.EntityId} from slot {liveFrom} ({fromPt.X},{fromPt.Y}) to slot {move.TargetIndex} ({toPt.X},{toPt.Y}).");
				await InputController.DragAsync(fromPt, toPt, ct);

				// Wait briefly for Power.log ZONE_POSITION to reflect the drag before starting the next one.
				var settled = await WaitForEntityPositionAsync(game, move.EntityId, move.TargetIndex, timeoutMs: 800, ct);
				if(!settled)
					Log.Warn($"Automation(Positioning): entity {move.EntityId} did not confirm slot {move.TargetIndex} within timeout; continuing cautiously.");
			}
		}

		private static async Task<bool> WaitForEntityPositionAsync(GameV2 game, int entityId, int expectedZeroBasedIndex, int timeoutMs, CancellationToken ct)
		{
			var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
			while(DateTime.UtcNow < deadline)
			{
				var liveIds = GetLivePlayerBoardEntityIds(game);
				if(Array.IndexOf(liveIds, entityId) == expectedZeroBasedIndex)
					return true;
				await Task.Delay(50, ct);
			}
			return false;
		}

		private static int[] GetLivePlayerBoardEntityIds(GameV2 game)
			=> BobsBuddyUtils.GetOrderedMinions(game.Player.Board)
				.Where(e => e.IsControlledBy(game.Player.Id))
				.Select(e => e.Id)
				.ToArray();

		private static int GetAvailableGold(GameV2 game)
		{
			var playerEntity = game.PlayerEntity;
			if(playerEntity == null)
				return 0;
			var total = playerEntity.GetTag(GameTag.RESOURCES);
			var used = playerEntity.GetTag(GameTag.RESOURCES_USED);
			var temp = playerEntity.GetTag(GameTag.TEMP_RESOURCES);
			return Math.Max(0, total - used + temp);
		}

		private static CancellationToken? BeginTask()
		{
			lock(Sync)
			{
				if(_isBusy)
					return null;
				_isBusy = true;
				_activeCts?.Dispose();
				_activeCts = new CancellationTokenSource();
				return _activeCts.Token;
			}
		}

		private static void EndTask()
		{
			lock(Sync)
			{
				_isBusy = false;
				_activeCts?.Dispose();
				_activeCts = null;
			}
		}
	}
}
