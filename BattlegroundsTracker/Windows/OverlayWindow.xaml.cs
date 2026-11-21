#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using HearthDb.Enums;
using HearthMirror.Objects;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.Controls;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.ChinaModule;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Guides;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Guides.Anomalies;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Guides.Heroes;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Guides.Quests;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Guides.Trinkets;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.HeroPicking;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.HeroPowerPicking;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Inspiration;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.MinionPinning;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Minions;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.QuestPicking;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Session;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Tier7;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.TrinketPicking;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Enums.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.HsReplay;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Battlegrounds;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Utility.Overlay;
using Hearthstone_Deck_Tracker.Utility.RegionDrawer;
using Hearthstone_Deck_Tracker.Utility.RemoteData;
using HearthWatcher.EventArgs;
using HSReplay.Responses;
using static System.Windows.Visibility;
using Card = Hearthstone_Deck_Tracker.Hearthstone.Card;

#endregion

namespace Hearthstone_Deck_Tracker.Windows
{
	/// <summary>
	///     Interaction logic for OverlayWindow.xaml
	/// </summary>
	// ReSharper disable once RedundantExtendsListEntry
	public partial class OverlayWindow : Window, INotifyPropertyChanged
	{
		private readonly GameV2 _game;
		private readonly Dictionary<UIElement, ResizeGrip> _movableElements = new Dictionary<UIElement, ResizeGrip>();
		private readonly List<FrameworkElement> _clickableElements = new List<FrameworkElement>();
		private readonly HashSet<FrameworkElement> _hoverableElements = new HashSet<FrameworkElement>();
		private readonly List<HearthstoneTextBlock> _leaderboardDeadForText = new List<HearthstoneTextBlock>();
		private readonly List<HearthstoneTextBlock> _leaderboardDeadForTurnText = new List<HearthstoneTextBlock>();
		private bool _lmbDown;
		private User32.MouseInput? _mouseInput;
		private Point _mousePos;
		private bool _resizeElement;
		private UIElement? _selectedUiElement;
		private bool _uiMovable;

		private OverlayElementBehavior _heroNotificationBehavior;
		private OverlayElementBehavior _timewarpNotificationBehavior;
		private OverlayElementBehavior _bgsTopBarBehavior;
		private bool? _tavernMarkersPanelExpandedBeforeTimewarp;
		private OverlayElementBehavior _bgsBobsBuddyBehavior;
		private OverlayElementBehavior _bgsChinaModuleBehavior;
		private OverlayElementBehavior _bgsPastOpponentBoardBehavior;
		private OverlayElementBehavior _tier7PreLobbyBehavior;
		private OverlayElementBehavior _battlegroundsInspirationBehavior;

		public OverlayOpacityMask OpacityMaskOverlay { get; } = new();

		public BattlegroundsCompsGuidesViewModel BattlegroundsCompsGuidesVM { get; } = new();
		public BattlegroundsMinionsViewModel BattlegroundsMinionsVM { get; } = new();
		public BattlegroundsSessionViewModel BattlegroundsSessionViewModelVM => Core.Game.BattlegroundsSessionViewModel;
		public BattlegroundsHeroPickingViewModel BattlegroundsHeroPickingViewModel { get; } = new();
		public BattlegroundsQuestPickingViewModel BattlegroundsQuestPickingViewModel { get; } = new();
		public BattlegroundsTrinketPickingViewModel BattlegroundsTrinketPickingViewModel { get; } = new();
		public BattlegroundsHeroPowerPickingViewModel BattlegroundsHeroPowerPickingViewModel { get; } = new();
		public BattlegroundsInspirationViewModel BattlegroundsInspirationViewModel { get; } = new();

		public BattlegroundsGuidesTabsViewModel BattlegroundsGuidesTabsViewModel { get; } = new();
		public BattlegroundsHeroGuideListViewModel BattlegroundsHeroGuideListViewModel { get; } = new();
		public BattlegroundsAnomalyGuideListViewModel BattlegroundsAnomalyGuideListViewModel { get; } = new();
		public BattlegroundsTrinketGuideListViewModel BattlegroundsTrinketGuideListViewModel { get; } = new();
		public BattlegroundsQuestGuideListViewModel BattlegroundsQuestGuideListViewModel { get; } = new();

		public Tier7PreLobbyViewModel Tier7PreLobbyViewModel { get; } = new Tier7PreLobbyViewModel();

		public BattlegroundsMinionPinningViewModel BattlegroundsMinionPinningViewModel { get; } = new BattlegroundsMinionPinningViewModel();

		public ChinaModuleViewModel ChinaModuleVM { get; } = new();

		public OverlayWindow(GameV2 game)
		{
			// These need to be set before InitializeComponent is called
			OverlayExtensions.OnRegisterHitTestVisible += (e, clickable) =>
			{
				if(clickable)
					_clickableElements.Add(e);
				else
					_clickableElements.Remove(e);
			};

			OverlayExtensions.OnRegisterHoverVisible += (e, hoverable) =>
			{
				if(hoverable)
					_hoverableElements.Add(e);
				else
					_hoverableElements.Remove(e);
			};

			_winEventCallback = OnHearthstoneWindowLocationChange;

			StartInteractivityUpdates();

			OverlayExtensions.OnToolTipChanged += SetTooltip;

			_game = game;

			InitializeComponent();

			_heroNotificationBehavior = new OverlayElementBehavior(HeroNotificationPanel)
			{
				GetRight = () => 0,
				GetBottom = () => Height * 0.04,
				GetScaling = () => AutoScaling,
				AnchorSide = Side.Bottom,
				HideCallback = () => {
					ShowBgsTopBarAndBobsBuddyPanel();
				},
				EntranceAnimation = AnimationType.Slide,
				ExitAnimation = AnimationType.Slide,
			};

			_timewarpNotificationBehavior = new OverlayElementBehavior(TimewarpNotificationPanel)
			{
				GetRight = () => 0,
				GetBottom = () => Height * 0.05,
				GetScaling = () => AutoScaling,
				AnchorSide = Side.Bottom,
				EntranceAnimation = AnimationType.Slide,
				ExitAnimation = AnimationType.Slide,
			};

			_bgsTopBarBehavior = new OverlayElementBehavior(BgsTopBar)
			{
				GetRight = () => 0,
				GetTop = () => 0,
				GetScaling = () => AutoScaling,
				AnchorSide = Side.Top,
				EntranceAnimation = AnimationType.Slide,
				ExitAnimation = AnimationType.Slide,
				HideCallback = TearDownBgsTopBarState,
			};

			_bgsBobsBuddyBehavior = new OverlayElementBehavior(BobsBuddyDisplay)
			{
				GetLeft = () => Width / 2 - BobsBuddyDisplay.ActualWidth * AutoScaling / 2,
				GetTop = () => 0,
				GetScaling = () => AutoScaling,
				AnchorSide = Side.Top,
				EntranceAnimation = AnimationType.Slide,
				ExitAnimation = AnimationType.Slide,
			};

			_bgsPastOpponentBoardBehavior = new OverlayElementBehavior(BgsOpponentInfoContainer)
			{
				GetLeft = () => Width / 2 - BgsOpponentInfoContainer.ActualWidth * AutoScaling / 2,
				GetTop = () => 0,
				GetScaling = () => AutoScaling,
				AnchorSide = Side.Top,
				EntranceAnimation = AnimationType.Instant,
				ExitAnimation = AnimationType.Instant,
			};

			_tier7PreLobbyBehavior = new OverlayElementBehavior(Tier7PreLobby)
			{
				GetLeft = () => Helper.GetScaledXPos(0.079, (int)Width, ScreenRatio),
				GetTop = () => Height * 0.103,
				GetScaling = () => Height / 1080,
				AnchorSide = Side.Top,
				EntranceAnimation = AnimationType.Slide,
				ExitAnimation = AnimationType.Slide,
				Fade = true,
				Distance = 50,
				HideCallback = () =>
				{
					Tier7PreLobbyViewModel.SoftReset();
				},
			};

			_battlegroundsInspirationBehavior = new OverlayElementBehavior(BattlegroundsInspiration)
			{
				// Panel is set to 65% width in BattlegroundsInspiration.xaml
				// If the panel if close to or 80% wide, it should be slightly offset to the left to not cover the timer
				GetLeft = () => Helper.GetScaledXPos((1 - 0.65)/2, (int)Width, ScreenRatio),
				GetTop = () => Height * 0.13,
				GetScaling = () => Height / 1080,
				AnchorSide = Side.Top,
				EntranceAnimation = AnimationType.Slide,
				ExitAnimation = AnimationType.Slide,
				Fade = true,
				Distance = 40,
				HideCallback = () =>
				{
					BtnTier7Inspiration.IsEnabled = BattlegroundsInspirationViewModel.HasBeenActivated;
				}
			};

			_bgsChinaModuleBehavior = new OverlayElementBehavior(ChinaModulePanel)
			{
				GetLeft = () => 0,
				GetTop = () => 0,
				GetScaling = () => AutoScaling,
				AnchorSide = Side.Top,
				EntranceAnimation = AnimationType.Slide,
				ExitAnimation = AnimationType.Slide,
			};

			ShowInTaskbar = Config.Instance.ShowInTaskbar;
			if(Config.Instance.VisibleOverlay)
				Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#4C0000FF");
			InitializeCollections();
			GridMain.Visibility = Hidden;
			if(User32.GetHearthstoneWindow() != IntPtr.Zero)
			{
				HookGameWindow();
				UpdatePosition();
			}
			// wire before the initial Update, resets during it may recompute recommendations
			BattlegroundsMinionPinningViewModel.CompsGuidesVM = BattlegroundsCompsGuidesVM;

			Update(true);
			UpdateScaling();
			GridMain.Visibility = Visible;

			BattlegroundsMinionPinningViewModel.PropertyChanged += (_, e) =>
			{
				if(e.PropertyName == nameof(BattlegroundsMinionPinningViewModel.IsExpanded))
					UpdateElementSizes();
			};
			BattlegroundsInspirationViewModel.OnClose += HideBgsInspiration;
			Tier7Trial.OnTrialActivated += () =>
			{
				BattlegroundsMinionsVM.IsInspirationEnabled = _game.IsBattlegroundsMatch;
			};

			OpacityMaskOverlay.Changed += OpacityMaskOverlay_OnChanged;
		}

		internal double ScreenRatio => (4.0 / 3.0) / (Width / Height);
		public event PropertyChangedEventHandler? PropertyChanged;

		public double BattlegroundsTileHeight => Height * 0.69 / 8;
		public double BattlegroundsDuosTileToSpacingRatio = 0.137;
		public double BattlegroundsDuosTileHeight => Height * 0.69 * (1 - BattlegroundsDuosTileToSpacingRatio) / 8;
		public double BattlegroundsDuosSpacingHeight => Height * 0.69 * BattlegroundsDuosTileToSpacingRatio / 3;
		public double BattlegroundsTileWidth => BattlegroundsTileHeight;

		public Visibility BgsMinionPinningVisibility
		{
			get => BgsMinionPinning.Visibility;
			set
			{
				if(BgsMinionPinning.Visibility == value)
					return;
				BgsMinionPinning.Visibility = value;
				if(value == Visible)
				{
					Core.Game.Metrics.TavernMarkersDisplayed = true;
					Core.Game.Metrics.TavernMarkersOnTrial = !(HSReplayNetOAuth.AccountData?.IsTier7 ?? false);
				}

				// Clear all pins when the feature is hidden to prevent persistence
				if(value == Collapsed)
					BattlegroundsMinionPinningViewModel.ClearPins();

				OnPropertyChanged();
			}
		}

		private bool ShouldShowBgsMinionPinning()
		{
			if(!Config.Instance.ShowBattlegroundsBrowser)
				return false;

			if(!Config.Instance.ShowBattlegroundsTavernMarkers)
				return false;

			if(!_game.IsBattlegroundsHeroPickingDone)
				return false;

			return _game.SetupDone;
		}

		internal void UpdateBgsMinionPinningVisibility()
		{
			BattlegroundsMinionPinningViewModel.UpdateIsTier7Enabled();
			BgsMinionPinningVisibility = ShouldShowBgsMinionPinning() ? Visible : Collapsed;
		}

		internal void ShowQuickGuide()
		{
			BattlegroundsMinionPinningControl?.ShowGuide();
		}

		public bool IsViewingBGsTeammate { get; set; }

		private readonly User32.WinEventCallback _winEventCallback;
		private void OnHearthstoneWindowLocationChange(IntPtr hwineventhook, uint eventtype, IntPtr hwnd, uint idobject, long idchild, uint dweventthread, uint dwmseventtime)
		{
			const uint childIdSelf = 0;
			if(idobject != childIdSelf)
				return;
			UpdatePosition();
		}

		private IntPtr _windowHook = IntPtr.Zero;
		internal void HookGameWindow()
		{
			if(_windowHook != IntPtr.Zero)
				return;
			var thread = User32.GetHearthstoneWindowThread();
			if(thread.ProcId == 0)
				return;
			const uint dwFlagsOutOfContextIgnoreSelf = 0x0000 | 0x001 | 0x002;
			const uint eventObjectLocationchange = 0x800B;
			_windowHook = User32.SetWinEventHook(eventObjectLocationchange, eventObjectLocationchange, IntPtr.Zero,
				_winEventCallback, thread.ProcId, thread.ThreadId, dwFlagsOutOfContextIgnoreSelf);
		}

		internal void UnhookGameWindow()
		{
			if(_windowHook == IntPtr.Zero)
				return;
			User32.UnhookWinEvent(_windowHook);
			_windowHook = IntPtr.Zero;
		}

		/// <summary>
		/// Whether the overlay content is currently shown. While the game is running
		/// the window itself stays visible (with collapsed content) so that it remains
		/// enumerable and capturable by streaming software such as OBS.
		/// </summary>
		public bool IsContentVisible { get; private set; }

		public enum OverlayZState { Hidden, Behind, Visible }
		private OverlayZState _overlayZState = OverlayZState.Hidden;

		public void HideOverlayWindow()
		{
			IsContentVisible = false;
			_overlayZState = OverlayZState.Hidden;
			SetClickthrough(true);
			Hide();
		}

		public void ShowOverlay(bool enable)
		{
			IsContentVisible = enable;
			Opacity = enable ? Config.Instance.OverlayOpacity / 100 : 0;
			if(!enable)
				SetClickthrough(true);
			try
			{
				Show();
			}
			catch(InvalidOperationException e)
			{
				Log.Error(e);
			}
			if(enable && User32.GetForegroundWindow() == new WindowInteropHelper(this).Handle)
				User32.BringHsToForeground();
		}

		private void SetRect(int top, int left, int width, int height)
		{
			if(width < 0 || height < 0)
				return;
			Top = top;
			Left = left;
			Width = width;
			Height = height;
			CanvasInfo.Width = width;
			CanvasInfo.Height = height;
		}

		private void Window_SourceInitialized_1(object sender, EventArgs e)
		{
			var hwnd = new WindowInteropHelper(this).Handle;
			User32.SetWindowExStyle(hwnd, User32.WsExNoActivate | User32.WsExTransparent);
		}

		private bool _clickthrough = false;
		private bool SetClickthrough(bool clickthrough)
		{
			if(_clickthrough == clickthrough)
				return false;
			_clickthrough = clickthrough;
			var hwnd = new WindowInteropHelper(this).Handle;
			if(clickthrough)
				User32.SetWindowExStyle(hwnd, User32.WsExTransparent);
			else
				User32.RemoveWindowExStyle(hwnd, User32.WsExTransparent);
			return true;
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			StopInteractivityUpdates();
			UnhookGameWindow();
			if(_mouseInput != null)
				UnHookMouse();
		}

		private void OverlayWindow_OnLoaded(object sender, RoutedEventArgs e)
		{
			//in addition to setting this in mainwindow_load: (in case of minimized)
			var presentationsource = PresentationSource.FromVisual(this);
			Helper.DpiScalingX = presentationsource?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
			Helper.DpiScalingY = presentationsource?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

			OpacityMaskOverlay_OnChanged(); // Called once here to init
		}

		private void OpacityMaskOverlay_OnChanged()
		{
			OpacityMask = OpacityMaskOverlay.Mask;
		}

		public void SetFriendListOpacityMask(bool visible)
		{
			if(visible)
			{
				var regionDrawer = new RegionDrawer(Height, Width, ScreenRatio);
				var rect = regionDrawer.DrawFriendsListRegion();

				OpacityMaskOverlay.AddMaskedRegion("FriendsList", rect);
			}
			else
				OpacityMaskOverlay.RemoveMaskedRegion("FriendsList");
		}

		public void SetGameMenuOpacityMask(bool visible)
		{
			if(visible)
			{
				var regionDrawer = new RegionDrawer(Height, Width, ScreenRatio);
				var rect = regionDrawer.DrawGameMenuRegion();

				OpacityMaskOverlay.AddMaskedRegion("GameMenu", rect);
			}
			else
				OpacityMaskOverlay.RemoveMaskedRegion("GameMenu");
		}

		public void SetCardOpacityMask(BigCardState state)
		{
			if(IsViewingBGsTeammate) return;


			using var _ = OpacityMaskOverlay.StartBatchUpdate();

			var card = Database.GetCardFromId(state.CardId);
			var isFriendly = state.Side == (int)PlayerSide.FRIENDLY;
			var isHand = state.IsHand;

			OpacityMaskOverlay.RemoveMaskedRegion("BigCard");

			var regionDrawer = new RegionDrawer(Height, Width, ScreenRatio);

			// Enemy secret area
			if(card == null && state.ZonePosition > 0 && !isFriendly && !isHand)
			{
				var rects = regionDrawer.DrawSecretCardRegions(state.ZonePosition, isFriendly, state.TooltipHeights.Sum());

				foreach(var rect in rects)
				{
					OpacityMaskOverlay.AddMaskedRegion("BigCard", rect);
				}
			}

			if(card == null)
				return;

			if (card.Type is "Minion" or "Location" or "Battleground_Spell" && !isHand)
			{
				var rects = regionDrawer.DrawBoardCardRegions(state.ZoneSize, state.ZonePosition, isFriendly, state.TooltipHeights.Sum(), state.EnchantmentHeights.Sum());

				foreach(var rect in rects)
				{
					OpacityMaskOverlay.AddMaskedRegion("BigCard", rect);
				}
			}

			if(card.Type == "Spell" && !isHand)
			{
				var rects = regionDrawer.DrawSecretCardRegions(state.ZonePosition, isFriendly, state.TooltipHeights.Sum());

				foreach(var rect in rects)
				{
					OpacityMaskOverlay.AddMaskedRegion("BigCard", rect);
				}
			}

			if(card.Type == "Battleground_Trinket" && !isHand)
			{
				if(card.Id is "BG30_Trinket_1st" or "BG30_Trinket_2nd")
					return;

				var trinkets = isFriendly ? _game.Player.Trinkets : _game.Opponent.Trinkets;

				 var trinketEntity = trinkets.FirstOrDefault(x =>
					 x.HasTag(GameTag.TAG_SCRIPT_DATA_NUM_6) &&
					 x.Card.DbfId == card.DbfId
				 );

				 var hasAttachedCard = trinketEntity?.HasTag(GameTag.BACON_EVOLUTION_CARD_ID) ?? false;

				 var position = trinketEntity?.GetTag(GameTag.TAG_SCRIPT_DATA_NUM_6) ?? 0;

				if(position == 0)
					return;

				var rects = position == 3 ?
					regionDrawer.DrawBgHeroTrinketRegions(isFriendly, true, state.TooltipHeights.Sum())
					: regionDrawer.DrawBgTrinketRegions(position, hasAttachedCard, isFriendly, state.TooltipHeights.Sum());

				foreach(var rect in rects)
				{
					OpacityMaskOverlay.AddMaskedRegion("BigCard", rect);
				}
			}

			if(card.Type == "Hero Power" && !isHand)
			{
				var rect = regionDrawer.DrawHeroPowerRegion(isFriendly);

				OpacityMaskOverlay.AddMaskedRegion("BigCard", rect);
			}

			if(card.Type == "Weapon" && !isHand)
			{
				var rects = regionDrawer.DrawWeaponRegions(isFriendly, state.TooltipHeights.Sum(), state.EnchantmentHeights.Sum());

				foreach(var rect in rects)
				{
					OpacityMaskOverlay.AddMaskedRegion("BigCard", rect);
				}
			}

			if(isHand)
			{
				var rects = regionDrawer.DrawHandCardRegions(state.ZoneSize, state.ZonePosition, isFriendly, card.Type, state.TooltipHeights.Sum(), state.EnchantmentHeights.Sum());

				foreach(var rect in rects)
				{
					OpacityMaskOverlay.AddMaskedRegion("BigCard", rect);
				}
			}
		}

		public void SetHeroPickingTooltipMask(int zoneSize, int zonePosition, bool tooltipOnRight, int numCards, bool buddiesEnabled = false)
		{
			OpacityMaskOverlay.RemoveMaskedRegion("HeroPickingTooltip");

			if(zoneSize == 0)
				return;

			var regionDrawer = new RegionDrawer(Height, Width, ScreenRatio);

			var rects = regionDrawer.DrawBgHeroPickingTooltipRegion(zoneSize, zonePosition, tooltipOnRight, numCards, buddiesEnabled);

			using var _ = OpacityMaskOverlay.StartBatchUpdate();
			foreach(var rect in rects)
				OpacityMaskOverlay.AddMaskedRegion("HeroPickingTooltip", rect);
		}

		public void SetMulliganAnomalyMask(Card? card)
		{
			OpacityMaskOverlay.RemoveMaskedRegion("MulliganAnomaly");

			if(card == null)
				return;

			var regionDrawer = new RegionDrawer(Height, Width, ScreenRatio);

			var hasAttachedCard = card.GetTag(GameTag.BACON_EVOLUTION_CARD_ID) != 0;

			var rects = regionDrawer.DrawMulliganAnomalyRegions(hasAttachedCard, false);

			using var _ = OpacityMaskOverlay.StartBatchUpdate();
			foreach(var rect in rects)
				OpacityMaskOverlay.AddMaskedRegion("MulliganAnomaly", rect);
		}

		public void SetDiscoverCardOpacityMask(int zoneSize, bool hasDarkGifts)
		{
			OpacityMaskOverlay.RemoveMaskedRegion("DiscoverCard");

			if(zoneSize == 0)
				return;

			var regionDrawer = new RegionDrawer(Height, Width, ScreenRatio);
			var rects = regionDrawer.DrawDiscoverCardRegions(zoneSize, hasDarkGifts);

			using var _ = OpacityMaskOverlay.StartBatchUpdate();
			foreach(var rect in rects)
				OpacityMaskOverlay.AddMaskedRegion("DiscoverCard", rect);
		}

		public void SetTrinketPickingOpacityMask(int zoneSize)
		{
			OpacityMaskOverlay.RemoveMaskedRegion("DiscoverCard");

			if(zoneSize == 0)
				return;

			var regionDrawer = new RegionDrawer(Height, Width, ScreenRatio);
			var rects = regionDrawer.DrawTrinketPickingRegions(zoneSize);

			using var _ = OpacityMaskOverlay.StartBatchUpdate();
			foreach(var rect in rects)
				OpacityMaskOverlay.AddMaskedRegion("DiscoverCard", rect);
		}

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public void ShowRestartRequiredWarning() => TextBlockRestartWarning.Visibility = Visible;

		public void HideRestartRequiredWarning() => TextBlockRestartWarning.Visibility = Collapsed;

		internal void ShowBattlegroundsHeroPanel(int[] heroIds, bool duos, Dictionary<string, string>? parameters)
		{
			HeroNotificationPanel.HeroIds = heroIds;
			HeroNotificationPanel.Duos = duos;
			HeroNotificationPanel.AnomalyDbfId = BattlegroundsUtils.GetBattlegroundsAnomalyDbfId(_game.GameEntity);
			HeroNotificationPanel.Parameters = parameters;
			_heroNotificationBehavior.Show();
		}

		internal void UpdateBattlegroundsHeroPanel(int[] heroIds, Dictionary<string, string>? parameters)
		{
			HeroNotificationPanel.HeroIds = heroIds;
			HeroNotificationPanel.Parameters = parameters;
		}

		internal void HideBattlegroundsHeroPanel()
		{
			_heroNotificationBehavior.Hide();
		}

		internal void ShowBattlegroundsTimewarpPanel(List<BoardCard> boardCards)
		{
			// Only save the initial state if the panel is not already showing
			if(TimewarpNotificationPanel.Visibility != Visible)
			{
				// Save the current state and collapse the Tavern Markers Panel
				_tavernMarkersPanelExpandedBeforeTimewarp = BattlegroundsMinionPinningViewModel.IsExpanded;
				BattlegroundsMinionPinningViewModel.IsExpanded = false;
			}

			TimewarpNotificationPanel.BoardCards = boardCards;
			_timewarpNotificationBehavior.Show();
		}

		internal void HideBattlegroundsTimewarpPanel()
		{
			_timewarpNotificationBehavior.Hide();

			// Restore the Tavern Markers Panel to its initial state only if we saved one
			if(_tavernMarkersPanelExpandedBeforeTimewarp.HasValue)
			{
				BattlegroundsMinionPinningViewModel.IsExpanded = _tavernMarkersPanelExpandedBeforeTimewarp.Value;
				_tavernMarkersPanelExpandedBeforeTimewarp = null;
			}
		}

		private bool BgsGuidesPreLobbyVisible { get; set; }

		private void UpdateBgsTopBarContentVisibility()
		{
			// there are no turns to count before a match has started
			TurnCounter.Visibility = !BgsGuidesPreLobbyVisible && Config.Instance.ShowBattlegroundsTurnCounter ? Visible : Collapsed;

			// ShowBattlegroundsBrowser ("Show Minions and Guides Browser") hides the whole top bar apart from
			// the turn counter, ShowBattlegroundsGuides ("Show Hero and Comp Guides") only picks which panel shows
			var showBrowser = Config.Instance.ShowBattlegroundsBrowser;
			var showGuides = showBrowser && Config.Instance.ShowBattlegroundsGuides;
			GuidesTabs.Visibility = showGuides ? Visible : Collapsed;
			BattlegroundsMinions.Visibility = showBrowser && !showGuides ? Visible : Collapsed;
		}

		// showing or hiding any of the content changes the width of the bar
		internal void UpdateBgsTopBarContent()
		{
			UpdateBgsTopBarContentVisibility();
			_bgsTopBarBehavior.Refresh();
		}

		// these panels are centered on their own width, which changes with the length of the localized labels
		internal void RefreshCenteredBgsPanels()
		{
			// give the localized bindings a chance to update before the panels are measured again
			Dispatcher.BeginInvoke(new Action(() =>
			{
				_bgsBobsBuddyBehavior.Refresh();
				_bgsPastOpponentBoardBehavior.Refresh();
			}), DispatcherPriority.Background);
		}

		// the top bar stays up from the lobby into the match, so leaving the lobby state happens here
		// rather than by hiding and showing it again
		private void LeaveBgsGuidesPreLobby()
		{
			if(!BgsGuidesPreLobbyVisible)
				return;

			// the lobby allows filters the match may not have (tier 7, missing minion types)
			BgsGuidesPreLobbyVisible = false;
			BattlegroundsMinionsVM.Reset();
			BattlegroundsGuidesTabsViewModel.IsPreLobby = false;
			BtnTier7Inspiration.Visibility = Visible;
		}

		internal void ShowBgsTopBar()
		{
			// a hide that never finished still owes its teardown, and the setup below expects a clean slate
			if(_bgsTopBarTeardownPending)
				TearDownBgsTopBarState();

			LeaveBgsGuidesPreLobby();

			if(_game.GameEntity?.GetTag(GameTag.TURN) is int turn and > 0)
				Core.Overlay.TurnCounter.UpdateTurn((int)turn / 2);

			var anomalyDbfId = BattlegroundsUtils.GetBattlegroundsAnomalyDbfId(_game.GameEntity);
			var anomalyCardId = anomalyDbfId.HasValue ? Database.GetCardFromDbfId(anomalyDbfId.Value, false)?.Id : null;
			var availableRaces = BattlegroundsUtils.GetAvailableRaces();
			SetBgsAvailableRaces(availableRaces);
			if(availableRaces == null)
				SetBgsAvailableRacesOnceReadable().Forget();
			BattlegroundsMinionsVM.IsDuos = _game.IsBattlegroundsDuosMatch;
			BattlegroundsMinionsVM.Anomaly = anomalyCardId;
			BattlegroundsMinionsVM.PreloadCardTiles();


			var gameId = _game.MetaData.ServerInfo?.GameHandle;
			var userHasTier7 = (HSReplayNetOAuth.AccountData?.IsTier7 ?? false) || Tier7Trial.IsTrialForCurrentGameActive(gameId);
			BattlegroundsMinionsVM.IsInspirationEnabled = _game.IsBattlegroundsMatch && userHasTier7;

			IEnumerable<string> heroPowers = _game.Player.Board.Where(x => x.IsHeroPower).Select(x => x.Card.Id);
			if(!heroPowers.Any() && _game.GameEntity?.GetTag(GameTag.STEP) <= (int)Step.BEGIN_MULLIGAN)
			{
				var heroes = Core.Game.Player.PlayerEntities.Where(x => x.IsHero && (x.HasTag(GameTag.BACON_HERO_CAN_BE_DRAFTED) || x.HasTag(GameTag.BACON_SKIN)) && !x.HasTag(GameTag.BACON_LOCKED_MULLIGAN_HERO));
				heroPowers = heroes.Select(x => Database.GetCardFromDbfId(x.GetTag(GameTag.HERO_POWER), collectible: false)?.Id ?? "");
			}
			BattlegroundsMinionsVM.OnHeroPowers(heroPowers);

			UpdateBgsTopBarContentVisibility();

			BtnTier7Inspiration.IsEnabled = BattlegroundsInspirationViewModel.HasBeenActivated;

			BattlegroundsCompsGuidesVM.OnMatchStart();

			UpdateBgsMinionPinningVisibility();
			_bgsTopBarBehavior.Show();

			// coming from the lobby the bar is already up, so it has to be re-measured for the wider content
			_bgsTopBarBehavior.Refresh();
		}

		private void SetBgsAvailableRaces(HashSet<Race>? availableRaces)
		{
			BattlegroundsMinionsVM.AvailableRaces = availableRaces?.Concat(new[] { Race.INVALID, Race.ALL });
			BattlegroundsMinionPinningViewModel.AvailableRaces = availableRaces;
		}

		private async Task SetBgsAvailableRacesOnceReadable()
		{
			try
			{
				if(await BattlegroundsUtils.WaitForAvailableRaces() == null)
					return;
			}
			catch(OperationCanceledException)
			{
				return;
			}

			// the match may have ended meanwhile, and the lobby's minion browser must not pick up its races
			if(_game.IsInMenu || !_game.IsBattlegroundsMatch || BgsGuidesPreLobbyVisible)
				return;

			// read again rather than use the result, so a newer game only ever gets its own races
			if(BattlegroundsUtils.GetAvailableRaces() is { } availableRaces)
				SetBgsAvailableRaces(availableRaces);
		}

		internal void OnBattlegroundsMinionPoolLoaded()
		{
			BattlegroundsMinionsVM.OnMinionPoolChanged();
			BattlegroundsMinionPinningViewModel.OnMinionPoolChanged();
			BattlegroundsCompsGuidesVM.OnMinionPoolChanged();
		}

		internal void ShowBgsTopBarAndBobsBuddyPanel()
		{
			ShowBgsTopBar();
			UpdateBobsBuddyPanelVisibility();
			UpdateBgsChinaModulePanel();

			// starting HDT mid-match, IsBattlegroundsMatch was still unknown when the scene event fired
			UpdateBattlegroundsSessionVisibility();
		}

		// the top bar can stay up into the pre-lobby, so everything a match leaves behind is reset
		// here rather than in HideBgsTopBar
		private void ResetBgsMatchState()
		{
			BattlegroundsMinionsVM.Reset();
			BattlegroundsCompsGuidesVM.OnMatchEnd();
			BattlegroundsGuidesTabsViewModel.Reset();
			BattlegroundsHeroGuideListViewModel.Reset();
			BattlegroundsQuestGuideListViewModel.Reset();
			BattlegroundsTrinketGuideListViewModel.Reset();
			BattlegroundsAnomalyGuideListViewModel.Reset();
			ResetAnomalyGuidesMulliganTrigger();
			BattlegroundsHeroPickingViewModel.Reset();
			BattlegroundsQuestPickingViewModel.Reset();
			BattlegroundsTrinketPickingViewModel.Reset();
			BattlegroundsHeroPowerPickingViewModel.Reset();
			HideBattlegroundsHeroPanel();
			HideBattlegroundsTimewarpPanel();
			TurnCounter.UpdateTurn(1);

			// the panel hides itself once the match state says so, only its results are cleared here
			BobsBuddyDisplay.ResetDisplays();
			UpdateBobsBuddyPanelVisibility();

			BattlegroundsInspirationViewModel.Reset();
			HideBgsInspiration();
			BtnTier7Inspiration.IsEnabled = false;

			BattlegroundsMinionPinningViewModel.Reset();
			BgsMinionPinningVisibility = Collapsed;

			ChinaModuleVM.Reset();
			HideBgsChinaModulePanel();
		}

		private bool _bgsTopBarTeardownPending;

		// the teardown changes how the bar looks (the heroes tab regains its enabled state, panels
		// disappear), so while it is still on screen it has to wait for the exit animation
		private void TearDownBgsTopBarState()
		{
			_bgsTopBarTeardownPending = false;
			LeaveBgsGuidesPreLobby();
			ResetBgsMatchState();
		}

		internal void HideBgsTopBar()
		{
			if(BgsTopBar.Visibility != Visible)
			{
				// only tear down once after a hide, this is called on every overlay update tick
				// while in the menu or a non-Battlegrounds match
				if(_bgsTopBarTeardownPending)
					TearDownBgsTopBarState();
				return;
			}

			_bgsTopBarTeardownPending = true;
			_bgsTopBarBehavior.Hide();
		}

		internal void ShowBattlegroundsHeroPickingStats(
			IEnumerable<BattlegroundsHeroPickStats.BattlegroundsSingleHeroPickStats> heroStats,
			Dictionary<string, string>? parameters,
			int? minMmr,
			bool anomalyAdjusted
		)
		{
			BattlegroundsHeroPickingViewModel.SetHeroStats(heroStats, parameters, minMmr, anomalyAdjusted);
		}

		internal void InvalidateBattlegroundsHeroPickingStats(int dbfId)
		{
			BattlegroundsHeroPickingViewModel.InvalidateSingleHeroStats(dbfId);
		}

		internal void UpdateBgsChinaModulePanel()
		{
			if(Core.Game.CurrentRegion is Region.CHINA)
				Core.Game.Metrics.IsBattlegroundsChineseEnvironmentCorrect = BattlegroundsChinaModule.IsChineseEnvironment();

			if(!BattlegroundsChinaModule.IsChineseEnvironment())
			{
				HideBgsChinaModulePanel();

				if(Config.Instance.ShowChinaModuleOverlay == null)
					return;

				Core.Game.Metrics.ChinaModuleEnabled = false;
				Config.Instance.ShowChinaModuleOverlay = null;
				Config.Save();
				return;
			}

			Core.Game.Metrics.ChinaModuleEnabled = true;

			if(Config.Instance.ShowChinaModuleOverlay == null)
			{
				Config.Instance.ShowChinaModuleOverlay = true;
				Config.Save();
			}

			if(Config.Instance.ShowChinaModuleOverlay == true)
				_bgsChinaModuleBehavior.Show();
		}

		internal void HideBgsChinaModulePanel()
		{
			_bgsChinaModuleBehavior.Hide();
			ChinaModuleVM.Reset();
		}

		public void ShowBgsInspiration()
		{
			if(!_game.IsBattlegroundsMatch)
				return;
			_battlegroundsInspirationBehavior.Show();
			OpacityMaskOverlay.Disable();
			BtnTier7Inspiration.IsEnabled = false;
		}

		private void HideBgsInspiration()
		{
			_battlegroundsInspirationBehavior.Hide();
			OpacityMaskOverlay.Enable();
		}

		internal void SetBaconState(SelectedBattlegroundsGameMode mode)
		{
			Tier7PreLobbyViewModel.BattlegroundsGameMode = mode;
			BattlegroundsSessionViewModelVM.BattlegroundsGameMode = mode;
			UpdateTier7PreLobbyVisibility();
		}

		internal void SetBaconQueue(bool inQueue)
		{
			UpdateTier7PreLobbyVisibility();
		}

		private IReadOnlyList<string>? _pendingBgsCombatChoices;

		internal void SetChoicesVisible(bool choicesVisible, bool isShopChoice, IEnumerable<string>? cardIds)
		{
			BattlegroundsTrinketPickingViewModel.ChoicesVisible = choicesVisible;
			BattlegroundsHeroPowerPickingViewModel.ChoicesVisible = choicesVisible;

			if(_game.IsBattlegroundsMatch)
			{
				var cardIdList = (cardIds ?? Array.Empty<string>()).ToList();
				// shop choices (Timewarp tavern) look like the regular shop, not like floating discover cards
				if(!choicesVisible || isShopChoice || !cardIdList.Any())
				{
					_pendingBgsCombatChoices = null;
					OpacityMaskOverlay.RemoveMaskedRegion("DiscoverCard");
					return;
				}

				if(_game.IsBattlegroundsCombatPhase)
				{
					_pendingBgsCombatChoices = cardIdList;
					return;
				}

				_pendingBgsCombatChoices = null;
				ApplyDiscoverCardMask(cardIdList);
			}
		}

		internal void OnBattlegroundsShoppingStart()
		{
			if(_pendingBgsCombatChoices is { } pending)
			{
				_pendingBgsCombatChoices = null;
				ApplyDiscoverCardMask(pending);
			}
		}

		private void ApplyDiscoverCardMask(IReadOnlyList<string> cardIds)
		{
			var cards = cardIds.Select(Database.GetCardFromId).WhereNotNull().ToList();
			if(cards.All(x => x.TypeEnum == CardType.BATTLEGROUND_TRINKET))
				SetTrinketPickingOpacityMask(cards.Count);
			else if(cards.All(x => x.TypeEnum == CardType.MINION || x.TypeEnum == CardType.BATTLEGROUND_SPELL || x.TypeEnum == CardType.SPELL))
			{
				var hasDarkGifts = _game.Player.OfferedEntities.Any(x => x.GetTag(GameTag.DARK_GIFT_ENTITY) > 0);
				SetDiscoverCardOpacityMask(cards.Count, hasDarkGifts);
			}
		}

		internal void HandleSpecialShop(SpecialShopChoicesArgs args)
		{
			if(!_game.IsBattlegroundsMatch)
				return;

			var boardCards = args.BoardCards;

			var gameId = _game.MetaData.ServerInfo?.GameHandle;
			var userHasTier7 = (HSReplayNetOAuth.AccountData?.IsTier7 ?? false) || Tier7Trial.IsTrialForCurrentGameActive(gameId);
			var currentPeriod = (_game.IsBattlegroundsDuosMatch ? Remote.BattlegroundsDuosLiveMetaPeriod : Remote.BattlegroundsLiveMetaPeriod).Data;
			var hasTimewarpMechanic = currentPeriod?.Mechanics?.Contains("timewarp") ?? false;

			if(args.IsActive && boardCards.Count > 0 && userHasTier7 && hasTimewarpMechanic)
			{
				ShowBattlegroundsTimewarpPanel(boardCards);
			}
			else
			{
				HideBattlegroundsTimewarpPanel();
			}

			Core.Overlay.BattlegroundsMinionPinningViewModel.OnShopChange(boardCards, args.MousedOverSlot);
		}

		private void BgsInspirationCover_OnMouseDown(object sender, MouseButtonEventArgs e) => HideBgsInspiration();

		private void BtnBgsInspiration_OnMouseUp(object sender, MouseButtonEventArgs e)
		{
			ShowBgsInspiration();
			_game.Metrics.BattlegroundsInspirationToggleClicks++;
		}

		internal void UpdateOpponentDeadForTurns(List<int> turns)
		{
			var index = _game.BattlegroundsHeroCount() - 1;
			foreach(var text in _leaderboardDeadForText)
				text.Text = "";
			foreach(var text in _leaderboardDeadForTurnText)
				text.Text = "";
			foreach(var turn in turns)
			{
				if(index < _leaderboardDeadForText.Count && index < _leaderboardDeadForTurnText.Count && index >= 0)
				{
					_leaderboardDeadForText[index].Text = $"{turn}";
					_leaderboardDeadForTurnText[index].Text = turn == 1 ? LocUtil.Get("Overlay_Battlegrounds_Dead_For_Turn") : LocUtil.Get("Overlay_Battlegrounds_Dead_For_Turns");
				}
				index--;
			}
		}
	}
}
