using System.Collections.Generic;
using HearthMirror;
using HearthMirror.Enums;
using HearthMirror.Objects;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.MinionPinning;
using Hearthstone_Deck_Tracker.Enums.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using HearthWatcher;
using HearthWatcher.Providers;

namespace Hearthstone_Deck_Tracker.Hearthstone
{
	/// <summary>
	/// Bridges HearthWatcher providers to the Battlegrounds overlay. Everything that only existed for
	/// constructed/arena/mercenaries has been removed.
	/// </summary>
	public static class Watchers
	{
		static Watchers()
		{
			QueueWatcher.InQueueChanged += (sender, args) => Core.Game.QueueEvents.Handle(args);
			BaconWatcher.Change += OnBaconChange;
			UiWatcher.Change += OnUiChange;
			SceneWatcher.Change += (sender, args) => SceneHandler.OnSceneUpdate((Mode)args.PrevMode, (Mode)args.Mode, args.SceneLoaded, args.Transitioning, args.LoadingScreenPhase);
			ChoicesWatcher.Change += (sender, args) => Core.Overlay.SetChoicesVisible(args.CurrentChoice?.IsVisible ?? false, args.CurrentChoice?.IsShopChoice ?? false, args.CurrentChoice?.Cards);
			SpecialShopChoicesStateWatcher.Change += (sender, args) => Core.Overlay.HandleSpecialShop(args);
			DiscoverStateWatcher.Change += OnDiscoverStateChange;
			BigCardWatcher.Change += OnBigCardChange;
			PlayZoneWatcher.Change += OnPlayZoneChange;
			BattlegroundsTeammateBoardStateWatcher.Change += OnBattlegroundsTeammateBoardStateChange;
			BattlegroundsLeaderboardWatcher.Change += (sender, args) => Core.Overlay.SetHoveredBattlegroundsLeaderboardEntityId(args.HoveredEntityId);
			BattlegroundsLobbyInfoWatcher.Change += OnBattlegroundsLobbyInfoChange;
			MulliganTooltipWatcher.Change += OnMulliganTooltipChange;
		}

		internal static void Stop()
		{
			QueueWatcher.Stop();
			BaconWatcher.Stop();
			UiWatcher.Stop();
			SceneWatcher.Stop();
			ChoicesWatcher.Stop();
			SpecialShopChoicesStateWatcher.Stop();
			BigCardWatcher.Stop();
			DiscoverStateWatcher.Stop();
			PlayZoneWatcher.Stop();
			BattlegroundsTeammateBoardStateWatcher.Stop();
			BattlegroundsLeaderboardWatcher.Stop();
			BattlegroundsLobbyInfoWatcher.Stop();
			MulliganTooltipWatcher.Stop();
		}

		internal static void OnBaconChange(object sender, HearthWatcher.EventArgs.BaconEventArgs args)
		{
			Core.Overlay.SetBaconState(args.SelectedBattlegroundsGameMode);
		}

		internal static void OnUiChange(object sender, HearthWatcher.EventArgs.UIEventArgs args)
		{
			var isCriticalUiOpen = args.IsShopOpen || args.IsJournalOpen || args.IsPopupShowing || args.IsBlurActive;
			Core.Overlay.Tier7PreLobbyViewModel.IsGameCriticalUiOpen = isCriticalUiOpen;
			Core.Overlay.SetFriendListOpacityMask(args.IsFriendsListVisible);
			Core.Overlay.SetGameMenuOpacityMask(args.IsGameMenuShown);
		}

		internal static void OnBigCardChange(object sender, HearthWatcher.EventArgs.BigCardArgs args)
		{
			Core.Overlay.SetAnomalyGuidesTrigger(args.CardId);
		}

		internal static void OnDiscoverStateChange(object sender, HearthWatcher.EventArgs.DiscoverStateArgs args)
		{
			var state = new DiscoverState
			{
				CardId = args.CardId,
				ZoneSize = args.ZoneSize,
				ZonePosition = args.ZonePosition,
				EntityId = args.EntityId
			};

			Core.Overlay.SetTrinketGuidesTrigger(state.ZoneSize, state.ZonePosition, state.CardId);
			Core.Overlay.SetQuestGuidesTrigger(state);
		}

		internal static void OnMulliganTooltipChange(object sender, HearthWatcher.EventArgs.MulliganTooltipArgs args)
		{
			var buddiesEnabled = Core.Game.GameEntity?.GetTag(HearthDb.Enums.GameTag.BACON_BUDDY_ENABLED) > 0;
			Core.Overlay.SetHeroPickingTooltipMask(
				args.ZoneSize, args.ZonePosition, args.IsTooltipOnRight, args.TooltipCards.Length, buddiesEnabled
			);

			Core.Overlay.SetHeroGuidesTrigger(args.ZoneSize, args.ZonePosition, args.IsTooltipOnRight, args.TooltipCards, buddiesEnabled);
		}

		internal static void OnPlayZoneChange(object sender, HearthWatcher.EventArgs.BoardStateArgs args)
		{
			// In Battlegrounds the opposing play zone is Bob's shop.
			if(Core.Game.IsBattlegroundsMatch)
			{
				var boardCards = args.Opposing?.BoardCards ?? new List<BoardCard>();
				var mousedOverSlot = args.Opposing?.MousedOverSlot ?? -1;
				Core.Overlay.BattlegroundsMinionPinningViewModel.OnShopChange(boardCards, mousedOverSlot);
			}

			Core.Overlay.OnPlayZoneStateChanged(args);
		}

		internal static void OnBattlegroundsTeammateBoardStateChange(object sender, HearthWatcher.EventArgs.BattlegroundsTeammateBoardStateArgs args)
		{
			Core.Overlay.BattlegroundsHeroPickingViewModel.IsViewingTeammate = args.IsViewingTeammate;
			Core.Overlay.BattlegroundsHeroPowerPickingViewModel.IsViewingTeammate = args.IsViewingTeammate;
			Core.Overlay.IsViewingBGsTeammate = args.IsViewingTeammate;

			if(args is { IsViewingTeammate: false, Entities.Count: 0, MulliganHeroes.Count: 0 })
			{
				Core.Game.BattlegroundsDuosBoardState = null;
				return;
			}

			var state = new BattlegroundsDuosBoardState(args.IsViewingTeammate, args.MulliganHeroes, args.Entities);
			Core.Game.BattlegroundsDuosBoardState = state;
		}

		internal static void OnBattlegroundsLobbyInfoChange(object sender, HearthWatcher.EventArgs.BattlegroundsLobbyInfoArgs args)
		{
			Core.Game.MetaData.BattlegroundsLobbyInfo = args.LobbyInfo;
		}

		public static QueueWatcher QueueWatcher { get; } = new(new HearthMirrorQueueProvider());
		public static BaconWatcher BaconWatcher { get; } = new(new HearthMirrorBaconProvider());
		public static UiWatcher UiWatcher { get; } = new(new HearthMirrorUiProvider());
		public static SceneWatcher SceneWatcher { get; } = new(new HearthMirrorSceneProvider());
		public static ChoicesWatcher ChoicesWatcher { get; } = new(new HearthMirrorChoicesProvider());
		public static SpecialShopChoicesStateWatcher SpecialShopChoicesStateWatcher { get; } = new(new HearthMirrorSpecialShopChoicesProvider());
		public static BigCardStateWatcher BigCardWatcher { get; } = new(new HearthMirrorBigCardProvider());
		public static PlayZoneWatcher PlayZoneWatcher { get; } = new(new HearthMirrorBoardStateProvider());
		public static DiscoverStateWatcher DiscoverStateWatcher { get; } = new(new HearthMirrorDiscoverStateProvider());
		public static BattlegroundsTeammateBoardStateWatcher BattlegroundsTeammateBoardStateWatcher { get; } = new(new HearthMirrorBattlegroundsTeammateBoardStateProvider());
		public static BattlegroundsLeaderboardWatcher BattlegroundsLeaderboardWatcher { get; } = new(new HearthMirrorBattlegroundsLeaderboardProvider());
		public static BattlegroundsLobbyInfoWatcher BattlegroundsLobbyInfoWatcher { get; } = new(new HearthMirrorBattlegroundsLobbyInfoProvider());
		public static MulliganTooltipWatcher MulliganTooltipWatcher { get; } = new(new HearthMirrorMulliganTooltipProvider());
	}

	public class HearthMirrorQueueProvider : IQueueProvider
	{
		public FindGameState? FindGameState => Reflection.Client.GetFindGameState();
	}

	public class HearthMirrorBaconProvider : IBaconProvider
	{
		public SelectedBattlegroundsGameMode? SelectedBattlegroundsGameMode => Reflection.Client.GetSelectedBattlegroundsGameMode();
	}

	public class HearthMirrorUiProvider : IUiProvider
	{
		public bool? IsShopOpen => Reflection.Client.IsShopOpen();
		public bool? IsJournalOpen => Reflection.Client.IsJournalOpen();
		public bool? IsPopupShowing => Reflection.Client.IsPopupShowing();
		public bool? IsFriendsListVisible => Reflection.Client.IsFriendsListVisible();
		public bool? IsBlurActive => Reflection.Client.GetIsBlurActive();
		public bool? IsGameMenuShown => Reflection.Client.IsGameMenuShown();
	}

	public class HearthMirrorSceneProvider : ISceneProvider
	{
		public SceneMgrState? State => Reflection.Client.GetSceneMgrState();
	}

	public class HearthMirrorBigCardProvider : IBigCardProvider
	{
		public BigCardState? State => Reflection.Client.GetBigCardState();
	}

	public class HearthMirrorDiscoverStateProvider : IDiscoverStateProvider
	{
		public DiscoverState? State => Reflection.Client.GetDiscoverState();
	}

	public class HearthMirrorBattlegroundsTeammateBoardStateProvider : IBattlegroundsTeammateBoardStateProvider
	{
		public BattlegroundsTeammateBoardState? BattlegroundsTeammateBoardState =>
			Reflection.Client.GetBattlegroundsTeammateBoardState();
	}

	public class HearthMirrorBattlegroundsLeaderboardProvider : IBattlegroundsLeaderboardProvider
	{
		public int? BattlegroundsLeaderboardHoveredEntityId =>
			Reflection.Client.GetBattlegroundsLeaderboardHoveredEntityId();
	}

	public class HearthMirrorChoicesProvider : IChoicesProvider
	{
		public CardChoices? CurrentChoice => Reflection.Client.GetCardChoices();
	}

	public class HearthMirrorSpecialShopChoicesProvider : ISpecialShopChoicesProvider
	{
		public SpecialShopChoicesState? SpecialShopChoicesState => Reflection.Client.GetSpecialShopChoices();
	}

	public class HearthMirrorMulliganTooltipProvider : IMulliganTooltipProvider
	{
		public MulliganTooltipState? State => Reflection.Client.GetMulliganTooltipState();
	}

	public class HearthMirrorBoardStateProvider : IBoardStateProvider
	{
		public BoardState? BoardState => Reflection.Client.GetBoardState(false);
	}

	public class HearthMirrorBattlegroundsLobbyInfoProvider : IBattlegroundsLobbyInfoProvider
	{
		public BattlegroundsLobbyInfo? BattlegroundsLobbyInfo => Reflection.Client.GetBattlegroundsLobbyInfo();
	}
}
