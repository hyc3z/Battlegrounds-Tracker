using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.BobsBuddy;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Newtonsoft.Json;

namespace Hearthstone_Deck_Tracker.Automation
{
	internal sealed class MatchScreenshotEntry
	{
		public string Timestamp { get; set; } = "";
		public string Label { get; set; } = "";
		public string RelativePath { get; set; } = "";
	}

	internal sealed class MatchTurnRecord
	{
		public int Turn { get; set; }
		public string StartedAt { get; set; } = "";
		public int Tier { get; set; }
		public int AvailableGold { get; set; }
		public int Health { get; set; }
		public int Armor { get; set; }
		public string ShoppingSnapshot { get; set; } = "";
		public string CombatSnapshot { get; set; } = "";
		public string BobsBuddySummary { get; set; } = "";
		public List<string> Actions { get; set; } = new();
		public List<string> AiAdvices { get; set; } = new();
		public List<MatchScreenshotEntry> Screenshots { get; set; } = new();
	}

	internal sealed class MatchReviewRecord
	{
		public string MatchId { get; set; } = "";
		public string StartedAt { get; set; } = "";
		public string? EndedAt { get; set; }
		public string HeroName { get; set; } = "未知";
		public List<string> AvailableRaces { get; set; } = new();
		public string? Anomaly { get; set; }
		public int? FinalPlacement { get; set; }
		public int CrashRestartCount { get; set; }
		public List<string> Events { get; set; } = new();
		public List<MatchTurnRecord> Turns { get; set; } = new();
	}

	/// <summary>
	/// Records each Battlegrounds match to %APPDATA%\BattlegroundsTracker\MatchReviews\<timestamp>\
	/// with match.json, review.md, automation.log, and multiple per-turn screenshots for post-game review.
	/// Survives mid-match Hearthstone crashes and automatically resumes into the same match folder on reconnect.
	/// </summary>
	internal static class BattlegroundsMatchRecorder
	{
		private static readonly object Sync = new();
		private static readonly TimeSpan CrashResumeWindow = TimeSpan.FromMinutes(3);

		private static MatchReviewRecord? _currentMatch;
		private static string? _currentMatchDir;
		private static DateTime _lastCrashUtc = DateTime.MinValue;
		private static CancellationTokenSource? _turnCaptureCts;
		private static int _shotCounterInTurn;

		internal static string ReviewsRootDir => Path.Combine(Config.AppDataPath, "MatchReviews");

		internal static string? CurrentMatchDir
		{
			get
			{
				lock(Sync)
					return _currentMatchDir;
			}
		}

		internal static bool HasUnfinishedCrashedMatch
		{
			get
			{
				lock(Sync)
				{
					return _currentMatch != null
						&& _currentMatch.EndedAt == null
						&& _lastCrashUtc != DateTime.MinValue
						&& DateTime.UtcNow - _lastCrashUtc <= CrashResumeWindow;
				}
			}
		}

		internal static void EnsureMatchSession(GameV2 game, bool isReconnectHint = false)
		{
			if(!Config.Instance.BgsSaveMatchReviews || !game.IsBattlegroundsMatch)
				return;

			lock(Sync)
			{
				var resumingAfterCrash = _currentMatch != null
					&& _currentMatch.EndedAt == null
					&& (isReconnectHint || (_lastCrashUtc != DateTime.MinValue && DateTime.UtcNow - _lastCrashUtc <= CrashResumeWindow));

				if(resumingAfterCrash)
				{
					_lastCrashUtc = DateTime.MinValue;
					AppendEventLocked("已重连回对局，继续记录同一复盘目录");
					UpdateMetadataFromGameLocked(game);
					FlushLocked();
					return;
				}

				if(_currentMatch != null && _currentMatch.EndedAt == null)
				{
					// Previous match already active in the same game
					UpdateMetadataFromGameLocked(game);
					return;
				}

				var now = DateTime.Now;
				var matchId = now.ToString("yyyy-MM-dd_HH-mm-ss");
				var dir = Path.Combine(ReviewsRootDir, matchId);
				Directory.CreateDirectory(Path.Combine(dir, "screenshots"));

				_currentMatchDir = dir;
				_currentMatch = new MatchReviewRecord
				{
					MatchId = matchId,
					StartedAt = now.ToString("yyyy-MM-dd HH:mm:ss")
				};
				_lastCrashUtc = DateTime.MinValue;
				UpdateMetadataFromGameLocked(game);
				AppendEventLocked($"开始记录对局复盘：{dir}");
				FlushLocked();
			}
		}

		internal static void OnShoppingTurnStart(GameV2 game, int turn)
		{
			if(!Config.Instance.BgsSaveMatchReviews || !game.IsBattlegroundsMatch)
				return;

			EnsureMatchSession(game, game.CurrentGameStats?.IsReconnect ?? false);
			CancelTurnCaptures();

			lock(Sync)
			{
				if(_currentMatch == null)
					return;
				UpdateMetadataFromGameLocked(game);
				var tr = GetOrCreateTurnLocked(turn);
				var hero = game.Player.Hero;
				tr.Tier = hero?.GetTag(GameTag.PLAYER_TECH_LEVEL) ?? tr.Tier;
				tr.Health = hero?.Health ?? tr.Health;
				tr.Armor = hero?.GetTag(GameTag.ARMOR) ?? tr.Armor;
				var pe = game.PlayerEntity;
				if(pe != null)
					tr.AvailableGold = Math.Max(0, pe.GetTag(GameTag.RESOURCES) - pe.GetTag(GameTag.RESOURCES_USED) + pe.GetTag(GameTag.TEMP_RESOURCES));
				_shotCounterInTurn = 0;
				FlushLocked();
			}

			var cts = new CancellationTokenSource();
			lock(Sync)
				_turnCaptureCts = cts;

			_ = CaptureShoppingTurnSequenceAsync(game, turn, cts.Token);
		}

		internal static void OnCombatStart(GameV2 game, int turn)
		{
			if(!Config.Instance.BgsSaveMatchReviews || !game.IsBattlegroundsMatch)
				return;

			CancelTurnCaptures();
			EnsureMatchSession(game);

			var snapshot = BattlegroundsStateSerializer.BuildPromptContext(game);
			lock(Sync)
			{
				if(_currentMatch == null)
					return;
				UpdateMetadataFromGameLocked(game);
				var tr = GetOrCreateTurnLocked(turn);
				tr.CombatSnapshot = snapshot;
				FlushLocked();
			}

			_ = CaptureDelayedAsync(game, turn, delayMs: 1200, suffix: "combat_start", label: $"T{turn} 战斗开局", CancellationToken.None, updateCombatSummary: true);
		}

		internal static void RecordAction(int turn, string action)
		{
			if(!Config.Instance.BgsSaveMatchReviews)
				return;
			lock(Sync)
			{
				if(_currentMatch == null || _currentMatch.EndedAt != null)
					return;
				var tr = GetOrCreateTurnLocked(turn);
				tr.Actions.Add($"[{DateTime.Now:HH:mm:ss}] {action}");
			}
		}

		internal static void RecordAiAdvice(int turn, string advice)
		{
			if(!Config.Instance.BgsSaveMatchReviews)
				return;
			lock(Sync)
			{
				if(_currentMatch == null || _currentMatch.EndedAt != null)
					return;
				var tr = GetOrCreateTurnLocked(turn);
				tr.AiAdvices.Add($"[{DateTime.Now:HH:mm:ss}]\n{advice}");
				FlushLocked();
			}
		}

		internal static void AppendLogLine(string message)
		{
			if(!Config.Instance.BgsSaveMatchReviews)
				return;
			lock(Sync)
			{
				if(_currentMatch == null || string.IsNullOrEmpty(_currentMatchDir))
					return;
				var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
				_currentMatch.Events.Add(line);
				try
				{
					File.AppendAllText(Path.Combine(_currentMatchDir!, "automation.log"), line + Environment.NewLine, Encoding.UTF8);
				}
				catch
				{
				}
			}
		}

		internal static void OnHearthstoneExitedMidMatch(bool triggeredAutoRestart)
		{
			CancelTurnCaptures();
			lock(Sync)
			{
				if(_currentMatch == null || _currentMatch.EndedAt != null)
					return;
				_lastCrashUtc = DateTime.UtcNow;
				if(triggeredAutoRestart)
					_currentMatch.CrashRestartCount++;
				AppendEventLocked(triggeredAutoRestart
					? "炉石传说在对局中意外退出，已触发自动重启（重连后将继续写入本复盘目录）"
					: "炉石传说在对局中退出，已即时落盘保存当前复盘数据");
				FlushLocked();
			}
		}

		internal static void OnGameEnd(GameV2 game)
		{
			CancelTurnCaptures();
			lock(Sync)
			{
				if(_currentMatch == null || _currentMatch.EndedAt != null)
					return;
				UpdateMetadataFromGameLocked(game);
				var placement = game.Player.Hero?.GetTag(GameTag.PLAYER_LEADERBOARD_PLACE) ?? 0;
				if(placement > 0)
					_currentMatch.FinalPlacement = placement;
				_currentMatch.EndedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
				AppendEventLocked($"对局结束（排名：{(placement > 0 ? $"第 {placement} 名" : "未知")}），复盘已保存");
				FlushLocked();
				_currentMatch = null;
				_currentMatchDir = null;
				_lastCrashUtc = DateTime.MinValue;
			}
		}

		internal static async Task<bool> CaptureManualScreenshotAsync(GameV2 game)
		{
			if(!game.IsBattlegroundsMatch)
				return false;

			EnsureMatchSession(game);
			var turn = Math.Max(1, game.GetTurnNumber());
			return await CaptureNowAsync(turn, $"manual_{DateTime.Now:HHmmss}", $"T{turn} 手动截图");
		}

		internal static void OpenReviewsFolder()
		{
			try
			{
				Directory.CreateDirectory(ReviewsRootDir);
				var target = CurrentMatchDir != null && Directory.Exists(CurrentMatchDir) ? CurrentMatchDir : ReviewsRootDir;
				Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
			}
			catch(Exception ex)
			{
				Log.Warn($"BattlegroundsMatchRecorder: failed to open folder: {ex.Message}");
			}
		}

		private static async Task CaptureShoppingTurnSequenceAsync(GameV2 game, int turn, CancellationToken ct)
		{
			try
			{
				// 1) Initial shopping screenshot + state snapshot after turn transition finishes
				await Task.Delay(1500, ct);
				if(game.IsBattlegroundsCombatPhase || game.GetTurnNumber() != turn)
					return;

				var shoppingSnapshot = BattlegroundsStateSerializer.BuildPromptContext(game);
				lock(Sync)
				{
					if(_currentMatch != null)
					{
						var tr = GetOrCreateTurnLocked(turn);
						tr.ShoppingSnapshot = shoppingSnapshot;
						FlushLocked();
					}
				}
				await CaptureNowAsync(turn, "shop_start", $"T{turn} 购物开始");

				// 2) Additional periodic screenshots during the same shopping turn for richer replay
				var intervalSec = Math.Max(10, Config.Instance.BgsMatchReviewScreenshotIntervalSec);
				for(var shot = 1; shot <= 2; shot++)
				{
					await Task.Delay(TimeSpan.FromSeconds(intervalSec), ct);
					if(game.IsBattlegroundsCombatPhase || game.GetTurnNumber() != turn)
						return;
					await CaptureNowAsync(turn, $"shop_mid{shot}", $"T{turn} 购物过程 #{shot}");
				}
			}
			catch(OperationCanceledException)
			{
			}
			catch(Exception ex)
			{
				Log.Warn($"BattlegroundsMatchRecorder: shopping capture sequence error: {ex.Message}");
			}
		}

		private static async Task CaptureDelayedAsync(GameV2 game, int turn, int delayMs, string suffix, string label, CancellationToken ct, bool updateCombatSummary)
		{
			try
			{
				await Task.Delay(delayMs, ct);
				if(updateCombatSummary)
				{
					var bbSummary = BobsBuddyInvoker.GetLastCombatSummary();
					if(!string.IsNullOrWhiteSpace(bbSummary))
					{
						lock(Sync)
						{
							if(_currentMatch != null)
							{
								var tr = GetOrCreateTurnLocked(turn);
								tr.BobsBuddySummary = bbSummary!;
								FlushLocked();
							}
						}
					}
				}
				await CaptureNowAsync(turn, suffix, label);
			}
			catch(OperationCanceledException)
			{
			}
			catch(Exception ex)
			{
				Log.Warn($"BattlegroundsMatchRecorder: delayed capture error: {ex.Message}");
			}
		}

		private static async Task<bool> CaptureNowAsync(int turn, string suffix, string label)
		{
			string? matchDir;
			int seq;
			lock(Sync)
			{
				if(_currentMatch == null || string.IsNullOrEmpty(_currentMatchDir))
					return false;
				matchDir = _currentMatchDir;
				seq = ++_shotCounterInTurn;
			}

			var fileName = $"T{turn:00}_{seq:00}_{suffix}.jpg";
			var relPath = $"screenshots/{fileName}";
			var fullPath = Path.Combine(matchDir!, "screenshots", fileName);

			var ok = await ScreenCaptureHelper.CaptureHearthstoneAsync(fullPath);
			if(!ok)
				return false;

			lock(Sync)
			{
				if(_currentMatch == null || _currentMatchDir != matchDir)
					return false;
				var tr = GetOrCreateTurnLocked(turn);
				tr.Screenshots.Add(new MatchScreenshotEntry
				{
					Timestamp = DateTime.Now.ToString("HH:mm:ss"),
					Label = label,
					RelativePath = relPath
				});
				FlushLocked();
			}
			return true;
		}

		private static void CancelTurnCaptures()
		{
			CancellationTokenSource? cts;
			lock(Sync)
			{
				cts = _turnCaptureCts;
				_turnCaptureCts = null;
			}
			if(cts != null)
			{
				try
				{
					cts.Cancel();
					cts.Dispose();
				}
				catch
				{
				}
			}
		}

		private static MatchTurnRecord GetOrCreateTurnLocked(int turn)
		{
			var existing = _currentMatch!.Turns.FirstOrDefault(t => t.Turn == turn);
			if(existing != null)
				return existing;
			var created = new MatchTurnRecord
			{
				Turn = turn,
				StartedAt = DateTime.Now.ToString("HH:mm:ss")
			};
			_currentMatch.Turns.Add(created);
			return created;
		}

		private static void UpdateMetadataFromGameLocked(GameV2 game)
		{
			if(_currentMatch == null)
				return;
			var heroName = game.Player.Hero?.LatestCard.LocalizedName;
			if(!string.IsNullOrWhiteSpace(heroName) && heroName != "BaconPHhero")
				_currentMatch.HeroName = heroName!;

			if(_currentMatch.AvailableRaces.Count == 0 && game.CurrentGameStats != null)
			{
				_currentMatch.AvailableRaces = BattlegroundsUtils.GetAvailableRaces(game.CurrentGameStats.GameId)
					.Where(r => r != Race.INVALID && r != Race.ALL)
					.Select(r => r.ToString())
					.Distinct()
					.ToList();
			}

			if(string.IsNullOrEmpty(_currentMatch.Anomaly))
			{
				var anomalyDbf = BattlegroundsUtils.GetBattlegroundsAnomalyDbfId(game.GameEntity);
				if(anomalyDbf.HasValue)
					_currentMatch.Anomaly = Database.GetCardFromDbfId(anomalyDbf.Value, false)?.LocalizedName;
			}
		}

		private static void AppendEventLocked(string message)
		{
			if(_currentMatch == null || string.IsNullOrEmpty(_currentMatchDir))
				return;
			var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
			_currentMatch.Events.Add(line);
			try
			{
				File.AppendAllText(Path.Combine(_currentMatchDir!, "automation.log"), line + Environment.NewLine, Encoding.UTF8);
			}
			catch
			{
			}
		}

		private static void FlushLocked()
		{
			if(_currentMatch == null || string.IsNullOrEmpty(_currentMatchDir))
				return;
			try
			{
				var jsonPath = Path.Combine(_currentMatchDir!, "match.json");
				File.WriteAllText(jsonPath, JsonConvert.SerializeObject(_currentMatch, Formatting.Indented), Encoding.UTF8);

				var mdPath = Path.Combine(_currentMatchDir!, "review.md");
				File.WriteAllText(mdPath, BuildMarkdownReport(_currentMatch), Encoding.UTF8);
			}
			catch(Exception ex)
			{
				Log.Warn($"BattlegroundsMatchRecorder: failed to flush match review: {ex.Message}");
			}
		}

		private static string BuildMarkdownReport(MatchReviewRecord match)
		{
			var sb = new StringBuilder();
			sb.AppendLine($"# 酒馆战棋对局复盘 - {match.HeroName} ({match.MatchId})");
			sb.AppendLine();
			sb.AppendLine($"- **开始时间**：{match.StartedAt}");
			if(!string.IsNullOrEmpty(match.EndedAt))
				sb.AppendLine($"- **结束时间**：{match.EndedAt}");
			if(match.FinalPlacement.HasValue)
				sb.AppendLine($"- **最终排名**：第 {match.FinalPlacement.Value} 名");
			if(match.AvailableRaces.Count > 0)
				sb.AppendLine($"- **可用种族**：{string.Join(", ", match.AvailableRaces)}");
			if(!string.IsNullOrEmpty(match.Anomaly))
				sb.AppendLine($"- **畸变**：{match.Anomaly}");
			if(match.CrashRestartCount > 0)
				sb.AppendLine($"- **闪退自动重启次数**：{match.CrashRestartCount}");
			sb.AppendLine();

			foreach(var tr in match.Turns.OrderBy(t => t.Turn))
			{
				sb.AppendLine($"---");
				sb.AppendLine($"## 第 {tr.Turn} 回合（{tr.StartedAt} | {tr.Tier} 本 | 金币 {tr.AvailableGold} | 血量 {tr.Health}+{tr.Armor}）");
				sb.AppendLine();

				if(tr.Screenshots.Count > 0)
				{
					sb.AppendLine("### 截图");
					foreach(var shot in tr.Screenshots)
						sb.AppendLine($"- `{shot.Timestamp}` **{shot.Label}**\n\n  ![{shot.Label}]({shot.RelativePath})");
					sb.AppendLine();
				}

				if(!string.IsNullOrWhiteSpace(tr.ShoppingSnapshot))
				{
					sb.AppendLine("### 购物阶段局面");
					sb.AppendLine(tr.ShoppingSnapshot.Trim());
					sb.AppendLine();
				}

				if(tr.Actions.Count > 0)
				{
					sb.AppendLine("### 本回合操作");
					foreach(var act in tr.Actions)
						sb.AppendLine($"- {act}");
					sb.AppendLine();
				}

				if(tr.AiAdvices.Count > 0)
				{
					sb.AppendLine("### AI 教练建议");
					foreach(var adv in tr.AiAdvices)
						sb.AppendLine(adv + "\n");
				}

				if(!string.IsNullOrWhiteSpace(tr.BobsBuddySummary))
				{
					sb.AppendLine("### 战斗模拟结果");
					sb.AppendLine(tr.BobsBuddySummary.Trim());
					sb.AppendLine();
				}
			}

			if(match.Events.Count > 0)
			{
				sb.AppendLine("---");
				sb.AppendLine("## 系统与自动化日志");
				foreach(var ev in match.Events)
					sb.AppendLine($"- {ev}");
			}

			return sb.ToString();
		}
	}
}
