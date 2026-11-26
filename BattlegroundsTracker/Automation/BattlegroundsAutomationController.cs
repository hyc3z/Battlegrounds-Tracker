using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.BobsBuddy;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Automation;
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
		private static CancellationTokenSource? _adviceCts;
		private static int _lastTurn2UpgradeAttemptedGameHash;
		private static bool _isBusy;
		private static bool _isAdvising;

		private static AutomationLogViewModel? UiLog => Core.Overlay?.AutomationLogVM;

		internal static bool IsBusy
		{
			get
			{
				lock(Sync)
					return _isBusy;
			}
		}

		private static void EmitLog(string message, AutomationLogLevel level = AutomationLogLevel.Info)
		{
			if(level == AutomationLogLevel.Error)
				Log.Error($"[Automation] {message}");
			else if(level == AutomationLogLevel.Warning)
				Log.Warn($"[Automation] {message}");
			else
				Log.Info($"[Automation] {message}");

			UiLog?.AddLog(message, level);
		}

		private static void SetStatus(string status, AutomationLogLevel level = AutomationLogLevel.Info, bool isBusy = false)
		{
			UiLog?.SetStatus(status, level, isBusy);
		}

		internal static void CancelActive(string reason)
		{
			CancellationTokenSource? toCancel;
			CancellationTokenSource? adviceToCancel;
			lock(Sync)
			{
				toCancel = _activeCts;
				_activeCts = null;
				adviceToCancel = _adviceCts;
				_adviceCts = null;
			}
			if(toCancel != null)
			{
				EmitLog($"中止当前任务：{reason}", AutomationLogLevel.Warning);
				SetStatus("已中止", AutomationLogLevel.Warning, isBusy: false);
				try
				{
					toCancel.Cancel();
				}
				catch
				{
				}
			}
			if(adviceToCancel != null)
			{
				try
				{
					adviceToCancel.Cancel();
				}
				catch
				{
				}
			}
		}

		internal static void OnShoppingTurnStart(GameV2 game, int turn)
		{
			CancelActive("进入新回合");

			if(!game.IsBattlegroundsMatch || game.IsBattlegroundsDuosMatch)
				return;

			var tier = game.Player.Hero?.GetTag(GameTag.PLAYER_TECH_LEVEL) ?? 0;
			var gold = GetAvailableGold(game);
			SetStatus($"购物中 (T{turn})", AutomationLogLevel.Info, isBusy: false);
			EmitLog($"第 {turn} 回合购物开始（{tier} 本，可用金币 {gold}）");

			if(turn == 2)
			{
				if(Config.Instance.BgsAutoUpgradeTurn2)
					_ = TryAutoUpgradeTurn2Async(game);
				else
					EmitLog("T2 自动升本未开启（可在设置中启用）");
			}

			if(Config.Instance.BgsLlmAutoAdviseOnTurnStart)
				_ = RequestLlmAdviceDelayedAsync(game, delayMs: 1600);
		}

		internal static void OnCombatStart()
		{
			CancelActive("战斗开始");
			SetStatus("战斗中", AutomationLogLevel.Info, isBusy: false);
		}

		internal static void OnGameEnd()
		{
			CancelActive("对局结束");
			BattlegroundsActionHistory.Reset();
			SetStatus("待机", AutomationLogLevel.Info, isBusy: false);
		}

		private static async Task RequestLlmAdviceDelayedAsync(GameV2 game, int delayMs)
		{
			try
			{
				await Task.Delay(delayMs);
				if(!game.IsBattlegroundsMatch || game.IsBattlegroundsCombatPhase)
					return;
				await RequestLlmAdviceAsync();
			}
			catch
			{
			}
		}

		internal static async Task RequestLlmAdviceAsync()
		{
			var game = Core.Game;
			if(!game.IsBattlegroundsMatch)
			{
				EmitLog("仅在酒馆战棋对局中支持 AI 分析", AutomationLogLevel.Warning);
				return;
			}

			CancellationToken ct;
			lock(Sync)
			{
				if(_isAdvising)
				{
					EmitLog("AI 正在分析中，请稍候...", AutomationLogLevel.Warning);
					return;
				}
				_isAdvising = true;
				_adviceCts?.Dispose();
				_adviceCts = new CancellationTokenSource();
				ct = _adviceCts.Token;
			}

			try
			{
				if(UiLog != null)
					UiLog.IsExpanded = true;
				var modelName = string.IsNullOrWhiteSpace(Config.Instance.BgsLlmModel) ? "gpt-4o-mini" : Config.Instance.BgsLlmModel.Trim();
				SetStatus("AI 思考中...", AutomationLogLevel.Busy, isBusy: true);
				EmitLog($"正在调用 {modelName} 分析当前局势与操作...", AutomationLogLevel.Busy);

				var statePrompt = BattlegroundsStateSerializer.BuildPromptContext(game);
				Log.Info($"[Automation/LLM] Prompt:\n{statePrompt}");

				var response = await LlmAdvisorClient.RequestAdviceAsync(statePrompt, ct);
				if(response.Success)
				{
					EmitLog($"AI 建议（耗时 {response.ElapsedMs / 1000.0:0.1}s）：\n{response.Content}", AutomationLogLevel.Advice);
					SetStatus($"AI 建议就绪 ({response.ElapsedMs / 1000.0:0.1}s)", AutomationLogLevel.Success, isBusy: false);
				}
				else
				{
					EmitLog(response.Content, AutomationLogLevel.Error);
					SetStatus("AI 调用失败", AutomationLogLevel.Error, isBusy: false);
				}
			}
			catch(OperationCanceledException)
			{
				EmitLog("AI 分析已取消", AutomationLogLevel.Warning);
				SetStatus("待机", AutomationLogLevel.Info, isBusy: false);
			}
			catch(Exception ex)
			{
				EmitLog($"AI 分析异常：{ex.Message}", AutomationLogLevel.Error);
				Log.Error(ex);
				SetStatus("AI 调用异常", AutomationLogLevel.Error, isBusy: false);
			}
			finally
			{
				lock(Sync)
				{
					_isAdvising = false;
					_adviceCts?.Dispose();
					_adviceCts = null;
				}
			}
		}

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
				SetStatus("T2 准备升本...", AutomationLogLevel.Busy, isBusy: true);
				EmitLog("T2 自动升本：等待过场动画 (1.2s)...", AutomationLogLevel.Busy);

				// Wait for the turn-start shopping transition animation to finish before clicking.
				await Task.Delay(1200, ct.Value);

				if(game.IsBattlegroundsCombatPhase || game.GetTurnNumber() != 2)
					return;

				var currentTier = game.Player.Hero?.GetTag(GameTag.PLAYER_TECH_LEVEL) ?? 0;
				if(currentTier != 1)
				{
					EmitLog($"T2 跳过升本：当前已是 {currentTier} 本", AutomationLogLevel.Warning);
					SetStatus("待机", AutomationLogLevel.Info, isBusy: false);
					return;
				}

				var upgradeButton = game.Player.Board.FirstOrDefault(e =>
					e.IsInPlay && (e.CardId?.StartsWith("TB_BaconShopTechUp", StringComparison.Ordinal) ?? false));
				if(upgradeButton == null)
				{
					EmitLog("T2 跳过升本：未检测到升本按钮实体", AutomationLogLevel.Warning);
					SetStatus("待机", AutomationLogLevel.Info, isBusy: false);
					return;
				}

				var upgradeCost = upgradeButton.GetTag(GameTag.COST);
				var availableGold = GetAvailableGold(game);
				if(upgradeCost <= 0 || availableGold < upgradeCost)
				{
					EmitLog($"T2 跳过升本：金币不足 ({availableGold}/{upgradeCost})", AutomationLogLevel.Warning);
					SetStatus("待机", AutomationLogLevel.Info, isBusy: false);
					return;
				}

				if(!BattlegroundsBoardCoordinates.TryGetClientRect(out var clientRect))
				{
					EmitLog("T2 跳过升本：无法获取炉石窗口区域", AutomationLogLevel.Warning);
					SetStatus("待机", AutomationLogLevel.Info, isBusy: false);
					return;
				}

				_lastTurn2UpgradeAttemptedGameHash = gameHash;
				var target = BattlegroundsBoardCoordinates.GetTavernUpgradeButton(clientRect);
				SetStatus("T2 点击升本...", AutomationLogLevel.Busy, isBusy: true);
				EmitLog($"T2 点击升本按钮 ({target.X}, {target.Y})，费用 {upgradeCost} 金", AutomationLogLevel.Busy);
				await InputController.ClickAsync(target, ct.Value);

				await Task.Delay(500, ct.Value);
				var newTier = game.Player.Hero?.GetTag(GameTag.PLAYER_TECH_LEVEL) ?? 0;
				if(newTier >= 2)
				{
					EmitLog($"T2 自动升本成功：当前 {newTier} 本", AutomationLogLevel.Success);
					SetStatus($"已升至 {newTier} 本", AutomationLogLevel.Success, isBusy: false);
				}
				else
				{
					EmitLog($"T2 已点击升本，但当前仍为 {newTier} 本（可能坐标需微调）", AutomationLogLevel.Warning);
					SetStatus("升本未生效", AutomationLogLevel.Warning, isBusy: false);
				}
			}
			catch(OperationCanceledException ex)
			{
				EmitLog($"T2 自动升本已中止：{ex.Message}", AutomationLogLevel.Warning);
				SetStatus("已中止", AutomationLogLevel.Warning, isBusy: false);
			}
			catch(Exception ex)
			{
				EmitLog($"T2 自动升本异常：{ex.Message}", AutomationLogLevel.Error);
				Log.Error(ex);
				SetStatus("执行异常", AutomationLogLevel.Error, isBusy: false);
			}
			finally
			{
				EndTask();
			}
		}

		internal static async Task OptimizeAndApplyPositioningAsync()
		{
			if(!BobsBuddyInvoker.CanRerunLastCombat)
			{
				EmitLog("当前无法优化站位（需在购物阶段且有上轮对手记录）", AutomationLogLevel.Warning);
				return;
			}

			var ct = BeginTask();
			if(ct == null)
			{
				EmitLog("已有自动操作正在执行，请等待或按 F8 中止", AutomationLogLevel.Warning);
				return;
			}

			var display = Core.Overlay.BobsBuddyDisplay;
			try
			{
				var context = BobsBuddyInvoker.TryCreateRerunContext();
				if(context == null)
				{
					EmitLog("未能读取当前阵容或上轮对手数据", AutomationLogLevel.Warning);
					SetStatus("待机", AutomationLogLevel.Info, isBusy: false);
					return;
				}

				SetStatus("搜索最优站位...", AutomationLogLevel.Busy, isBusy: true);
				EmitLog($"开始搜索最优站位（我方 {context.PlayerEntityIds.Length} 怪 vs 第 {context.CombatTurn} 回合对手）...", AutomationLogLevel.Busy);

				display.SetErrorState(BobsBuddyErrorState.None, null, true);
				display.HidePercentagesShowSpinners();

				var result = await PositioningOptimizer.OptimizeAsync(context, ct.Value);
				if(result == null)
				{
					display.ShowPercentagesHideSpinners();
					EmitLog("站位模拟未返回结果", AutomationLogLevel.Warning);
					SetStatus("待机", AutomationLogLevel.Info, isBusy: false);
					return;
				}

				display.ShowCompletedSimulation(
					result.BestOutput.winRate,
					result.BestOutput.tieRate,
					result.BestOutput.lossRate,
					result.BestOutput.theirDeathRate,
					result.BestOutput.myDeathRate,
					result.BestOutput.damageResults.ToList());

				var baseWin = result.BaselineOutput.winRate * 100;
				var bestWin = result.BestOutput.winRate * 100;
				var baseTie = result.BaselineOutput.tieRate * 100;
				var bestTie = result.BestOutput.tieRate * 100;

				if(!result.ShouldReorder)
				{
					EmitLog($"当前站位已接近最优（胜 {baseWin:0.#}% / 平 {baseTie:0.#}%），无需调整", AutomationLogLevel.Success);
					SetStatus("站位已最优", AutomationLogLevel.Success, isBusy: false);
					return;
				}

				var recommendedNames = string.Join(" → ", result.BestEntityIds.Select(id => GetEntityDisplayName(Core.Game, id)));
				EmitLog($"找到更优站位：胜 {baseWin:0.#}% → {bestWin:0.#}%（平 {bestTie:0.#}%）", AutomationLogLevel.Success);
				EmitLog($"推荐顺序：{recommendedNames}");

				if(!Config.Instance.BgsAutoPositioningApply)
				{
					EmitLog("仅模拟模式（未开启自动拖拽），跳过操作", AutomationLogLevel.Info);
					SetStatus("测算完成", AutomationLogLevel.Success, isBusy: false);
					return;
				}

				await ApplyBoardOrderAsync(Core.Game, result.BestEntityIds, ct.Value);
				EmitLog("最优站位调整完成", AutomationLogLevel.Success);
				SetStatus($"已调优 ({bestWin:0.#}%胜)", AutomationLogLevel.Success, isBusy: false);
			}
			catch(OperationCanceledException ex)
			{
				EmitLog($"站位优化已中止：{ex.Message}", AutomationLogLevel.Warning);
				SetStatus("已中止", AutomationLogLevel.Warning, isBusy: false);
				display.ShowPercentagesHideSpinners();
			}
			catch(Exception ex)
			{
				EmitLog($"站位优化异常：{ex.Message}", AutomationLogLevel.Error);
				Log.Error(ex);
				SetStatus("执行异常", AutomationLogLevel.Error, isBusy: false);
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
				EmitLog("搜索期间场面已变化，放弃自动拖拽", AutomationLogLevel.Warning);
				return;
			}

			var plannedMoves = PositioningOptimizer.PlanMinimalMoves(initialIds, targetEntityIds);
			EmitLog($"规划最少拖拽步骤：共 {plannedMoves.Count} 步", AutomationLogLevel.Busy);

			for(var step = 0; step < plannedMoves.Count; step++)
			{
				var move = plannedMoves[step];
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
				var name = GetEntityDisplayName(game, move.EntityId);

				SetStatus($"拖拽中 ({step + 1}/{plannedMoves.Count})...", AutomationLogLevel.Busy, isBusy: true);
				EmitLog($"拖拽 [{name}] 位置 {liveFrom + 1} → {move.TargetIndex + 1} ({fromPt.X},{fromPt.Y} → {toPt.X},{toPt.Y})", AutomationLogLevel.Busy);
				await InputController.DragAsync(fromPt, toPt, ct);

				// Wait briefly for Power.log ZONE_POSITION to reflect the drag before starting the next one.
				var settled = await WaitForEntityPositionAsync(game, move.EntityId, move.TargetIndex, timeoutMs: 800, ct);
				if(!settled)
					EmitLog($"[{name}] 未在 0.8s 内确认落位 {move.TargetIndex + 1}，继续下一步", AutomationLogLevel.Warning);
			}
		}

		private static string GetEntityDisplayName(GameV2 game, int entityId)
		{
			if(game.Entities.TryGetValue(entityId, out var entity))
			{
				var localized = entity.LatestCard.LocalizedName;
				if(!string.IsNullOrWhiteSpace(localized))
					return localized!;
				if(!string.IsNullOrWhiteSpace(entity.Name))
					return entity.Name!;
			}
			return $"#{entityId}";
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
