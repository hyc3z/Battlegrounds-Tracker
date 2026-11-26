using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.BobsBuddy;
using Hearthstone_Deck_Tracker.Hearthstone;
using Entity = Hearthstone_Deck_Tracker.Hearthstone.Entities.Entity;

namespace Hearthstone_Deck_Tracker.Automation
{
	/// <summary>
	/// Serializes the live Hearthstone Battlegrounds game state (hero, gold, tier, board, hand,
	/// tavern shop, recent actions, and last Bob's Buddy summary) into a concise Chinese prompt
	/// for LLM analysis.
	/// </summary>
	internal static class BattlegroundsStateSerializer
	{
		internal static string BuildPromptContext(GameV2 game)
		{
			var sb = new StringBuilder();
			var turn = game.GetTurnNumber();
			var phase = game.IsBattlegroundsCombatPhase ? "战斗阶段" : "购物阶段";
			var hero = game.Player.Hero;
			var heroName = GetCardName(hero);
			var hp = hero?.Health ?? 0;
			var armor = hero?.GetTag(GameTag.ARMOR) ?? 0;
			var tier = hero?.GetTag(GameTag.PLAYER_TECH_LEVEL) ?? 0;
			var playerEntity = game.PlayerEntity;
			var totalGold = playerEntity?.GetTag(GameTag.RESOURCES) ?? 0;
			var usedGold = playerEntity?.GetTag(GameTag.RESOURCES_USED) ?? 0;
			var tempGold = playerEntity?.GetTag(GameTag.TEMP_RESOURCES) ?? 0;
			var availGold = Math.Max(0, totalGold - usedGold + tempGold);

			var upgradeEntity = game.Player.Board.FirstOrDefault(e =>
				e.IsInPlay && (e.CardId?.StartsWith("TB_BaconShopTechUp", StringComparison.Ordinal) ?? false));
			var upgradeCost = upgradeEntity?.GetTag(GameTag.COST);

			sb.AppendLine($"## 当前对局状态");
			sb.AppendLine($"- 回合：第 {turn} 回合（{phase}）");
			sb.AppendLine($"- 英雄：{heroName}（生命 {hp} + 护甲 {armor}）");
			sb.AppendLine($"- 酒馆等级：{tier} 本" + (upgradeCost.HasValue && upgradeCost.Value > 0 ? $"（升本费用 {upgradeCost.Value} 金）" : "（已满本）"));
			sb.AppendLine($"- 金币：当前可用 {availGold} / 上限 {totalGold}");

			// Available races
			if(game.CurrentGameStats != null)
			{
				var races = BattlegroundsUtils.GetAvailableRaces(game.CurrentGameStats.GameId)
					.Where(r => r != Race.INVALID && r != Race.ALL)
					.Select(TranslateRace)
					.Distinct()
					.ToList();
				if(races.Count > 0)
					sb.AppendLine($"- 本局可用种族：{string.Join("、", races)}");
			}

			// Anomaly
			var anomalyDbf = BattlegroundsUtils.GetBattlegroundsAnomalyDbfId(game.GameEntity);
			if(anomalyDbf.HasValue)
			{
				var anomalyCard = Database.GetCardFromDbfId(anomalyDbf.Value, false);
				if(anomalyCard != null)
					sb.AppendLine($"- 本局畸变：{anomalyCard.LocalizedName}（{CompactText(anomalyCard.Text)}）");
			}

			// Hero powers
			var heroPowers = game.Player.Board.Where(e => e.IsHeroPower).ToList();
			foreach(var hpEntity in heroPowers)
			{
				var hpCost = hpEntity.GetTag(GameTag.COST);
				var exhausted = hpEntity.HasTag(GameTag.EXHAUSTED) ? "已用" : "可用";
				sb.AppendLine($"- 英雄技能：{GetCardName(hpEntity)}（{hpCost}费，{exhausted}）：{CompactText(hpEntity.LatestCard.Text)}");
			}

			// Quests / Rewards / Trinkets
			foreach(var quest in game.Player.Quests)
			{
				var prog = quest.GetTag(GameTag.QUEST_PROGRESS);
				var total = quest.GetTag(GameTag.QUEST_PROGRESS_TOTAL);
				var rewardCard = Database.GetCardFromDbfId(quest.GetTag(GameTag.QUEST_REWARD_DATABASE_ID), false);
				sb.AppendLine($"- 进行中任务：{GetCardName(quest)}（{prog}/{total}）→ 奖励：{rewardCard?.LocalizedName ?? "未知"}（{CompactText(rewardCard?.Text)}）");
			}
			foreach(var reward in game.Player.QuestRewards)
				sb.AppendLine($"- 任务奖励：{GetCardName(reward)}（{CompactText(reward.LatestCard.Text)}）");
			foreach(var trinket in game.Player.Trinkets)
				sb.AppendLine($"- 饰品：{GetCardName(trinket)}（{CompactText(trinket.LatestCard.Text)}）");

			// Player Board
			var boardMinions = BobsBuddyUtils.GetOrderedMinions(game.Player.Board)
				.Where(e => e.IsControlledBy(game.Player.Id))
				.ToList();
			sb.AppendLine();
			sb.AppendLine($"## 我方场上阵容（从左到右共 {boardMinions.Count} 个）");
			if(boardMinions.Count == 0)
			{
				sb.AppendLine("- （空场）");
			}
			else
			{
				for(var i = 0; i < boardMinions.Count; i++)
					sb.AppendLine($"{i + 1}. {FormatMinion(boardMinions[i], includeCost: false)}");
			}

			// Player Hand
			var handCards = BobsBuddyUtils.GetOrderedHandEntities(game.Player.Hand).ToList();
			sb.AppendLine();
			sb.AppendLine($"## 我方手牌（共 {handCards.Count} 张）");
			if(handCards.Count == 0)
			{
				sb.AppendLine("- （无手牌）");
			}
			else
			{
				for(var i = 0; i < handCards.Count; i++)
				{
					var c = handCards[i];
					if(c.IsMinion)
						sb.AppendLine($"{i + 1}. [随从] {FormatMinion(c, includeCost: false)}");
					else
						sb.AppendLine($"{i + 1}. [法术/卡牌] {GetCardName(c)}：{CompactText(c.LatestCard.Text)}");
				}
			}

			// Current Tavern Shop (only meaningful during shopping)
			if(!game.IsBattlegroundsCombatPhase)
			{
				var shopCards = game.Opponent.Board
					.Where(e => e.IsInPlay && e.TakesBoardSlot && !string.IsNullOrEmpty(e.CardId))
					.OrderBy(e => e.GetTag(GameTag.ZONE_POSITION))
					.ToList();
				sb.AppendLine();
				sb.AppendLine($"## 当前酒馆商店（共 {shopCards.Count} 个可购项）");
				if(shopCards.Count == 0)
				{
					sb.AppendLine("- （商店为空）");
				}
				else
				{
					for(var i = 0; i < shopCards.Count; i++)
					{
						var s = shopCards[i];
						var frozen = s.HasTag(GameTag.FROZEN) ? " [已冻结]" : "";
						if(s.IsMinion)
							sb.AppendLine($"{i + 1}. [随从]{frozen} {FormatMinion(s, includeCost: true)}");
						else
							sb.AppendLine($"{i + 1}. [酒馆法术]{frozen} {GetCardName(s)}（{s.GetTag(GameTag.COST)}费）：{CompactText(s.LatestCard.Text, s)}");
					}
				}
			}

			// Last combat summary from BobsBuddy
			var lastCombat = BobsBuddyInvoker.GetLastCombatSummary();
			if(!string.IsNullOrWhiteSpace(lastCombat))
			{
				sb.AppendLine();
				sb.AppendLine("## 上一轮战斗参考");
				sb.AppendLine(lastCombat);
			}

			// Recent player actions
			var recentActions = BattlegroundsActionHistory.GetRecent(15);
			if(recentActions.Count > 0)
			{
				sb.AppendLine();
				sb.AppendLine("## 玩家近期操作记录");
				foreach(var act in recentActions)
					sb.AppendLine($"- {act}");
			}

			return sb.ToString();
		}

		private static string FormatMinion(Entity e, bool includeCost)
		{
			var name = GetCardName(e);
			var golden = e.HasTag(GameTag.PREMIUM) ? "★金 " : "";
			var atk = e.GetTag(GameTag.ATK);
			var hp = Math.Max(0, e.GetTag(GameTag.HEALTH) - e.GetTag(GameTag.DAMAGE));
			var tech = e.GetTag(GameTag.TECH_LEVEL);
			var race = TranslateRace((Race)e.GetTag(GameTag.CARDRACE));

			var tags = new List<string>();
			if(tech > 0)
				tags.Add($"{tech}本");
			if(!string.IsNullOrEmpty(race))
				tags.Add(race);
			if(includeCost)
			{
				var cost = e.GetTag(GameTag.COST);
				tags.Add($"{(cost > 0 ? cost : 3)}费");
			}
			if(e.HasTag(GameTag.TAUNT)) tags.Add("嘲讽");
			if(e.GetTag(GameTag.DIVINE_SHIELD) > 0) tags.Add("圣盾");
			if(e.HasTag(GameTag.POISONOUS)) tags.Add("剧毒");
			if(e.HasTag(GameTag.VENOMOUS)) tags.Add("烈毒");
			if(e.HasTag(GameTag.WINDFURY)) tags.Add("风怒");
			if(e.HasTag(GameTag.MEGA_WINDFURY)) tags.Add("超级风怒");
			if(e.HasTag(GameTag.REBORN)) tags.Add("复生");
			if(e.HasTag(GameTag.STEALTH)) tags.Add("潜行");

			var tagStr = tags.Count > 0 ? $"（{string.Join("/", tags)}）" : "";
			var text = CompactText(e.LatestCard.Text, e);
			var textStr = string.IsNullOrEmpty(text) ? "" : $" — {text}";
			return $"{golden}{name} {atk}/{hp}{tagStr}{textStr}";
		}

		private static string GetCardName(Entity? entity)
		{
			if(entity == null)
				return "未知";
			var loc = entity.LatestCard.LocalizedName;
			if(!string.IsNullOrWhiteSpace(loc))
				return loc!;
			if(!string.IsNullOrWhiteSpace(entity.Name))
				return entity.Name!;
			return entity.CardId ?? "未知";
		}

		private static string CompactText(string? text, Entity? entity = null)
		{
			if(string.IsNullOrWhiteSpace(text))
				return "";
			var cleaned = text!.Replace("\r", " ").Replace("\n", " ").Replace("  ", " ").Trim();
			if(entity != null && cleaned.Contains("{"))
			{
				var n1 = entity.GetTag(GameTag.TAG_SCRIPT_DATA_NUM_1);
				var n2 = entity.GetTag(GameTag.TAG_SCRIPT_DATA_NUM_2);
				var n3 = entity.GetTag(GameTag.TAG_SCRIPT_DATA_NUM_3);
				if(n1 != 0) cleaned = cleaned.Replace("{0}", n1.ToString());
				if(n2 != 0) cleaned = cleaned.Replace("{1}", n2.ToString());
				if(n3 != 0) cleaned = cleaned.Replace("{2}", n3.ToString());
			}
			return cleaned;
		}

		private static string TranslateRace(Race race) => race switch
		{
			Race.BEAST => "野兽",
			Race.DEMON => "恶魔",
			Race.DRAGON => "龙",
			Race.ELEMENTAL => "元素",
			Race.MECHANICAL => "机械",
			Race.MURLOC => "鱼人",
			Race.NAGA => "纳迦",
			Race.PIRATE => "海盗",
			Race.QUILBOAR => "野猪人",
			Race.UNDEAD => "亡灵",
			Race.ALL => "全部种族",
			_ => ""
		};
	}
}
