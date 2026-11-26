using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using HearthDb.Enums;
using HearthMirror;
using HearthMirror.Objects;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Enums.Hearthstone;
using Hearthstone_Deck_Tracker.HsReplay;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Animations;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Utility.RemoteData;
using HearthWatcher.EventArgs;
using static System.Windows.Visibility;

namespace Hearthstone_Deck_Tracker.Windows
{
	public partial class OverlayWindow
	{
		private async void SetTopmost()
		{
			var hsWindow = User32.GetHearthstoneWindow();
			if(hsWindow == IntPtr.Zero)
			{
				Log.Info("Hearthstone window not found");
				return;
			}

			var hwnd = new WindowInteropHelper(this).Handle;
			for(var i = 0; i < 20; i++)
			{
				if(_overlayZState == OverlayZState.Behind)
					return;

				// the topmost style can stay set while the window has dropped into the regular z-order band
				var isTopmost = User32.IsTopmost(hwnd);
				if(isTopmost && User32.IsAbove(hwnd, hsWindow))
				{
					Log.Info($"Overlay is topmost after {i + 1} tries.");
					return;
				}
				if(isTopmost)
					Log.Warn("Overlay has topmost style but is behind Hearthstone");

				Topmost = false;
				Topmost = true;
				await Task.Delay(250);
			}

			Log.Info("Could not set overlay as topmost");
		}

		public void OnHearthstoneFocused()
		{
			Update(true);

			if(_game.CurrentMode == Mode.BACON)
			{
				Tier7PreLobbyViewModel.OnFocus();
			}
		}

		public void Update(bool refresh)
		{
			if (refresh)
			{
				SetTopmost();
				UpdateVisibilities();
			}

			// Keep GridMain rendered but hidden, so that the window keeps its size and position
			Opacity = IsContentVisible ? Config.Instance.OverlayOpacity / 100 : 0;

			var inBattlegrounds = _game.IsBattlegroundsMatch || Core.Game.CurrentMode == Mode.BACON;

			if(_game.IsInMenu || !inBattlegrounds)
			{
				if(ShouldShowBattlegroundsGuidesPreLobby())
					UpdateBattlegroundsGuidesPreLobbyVisibility();
				else
					HideBgsTopBar();
			}

			UpdateElementSizes();
			UpdateElementPositions();
		}

		public void UpdateVisibility()
		{
			var isForeground = User32.IsHearthstoneInForeground();
			// minimized uses the opacity-0 path rather than moving the window: DWM stops
			// producing frames for offscreen windows, which would freeze OBS capture. In
			// place with opacity 0 the capture stays connected (transparent frames) and
			// the window keeps its size and position.
			var hardHidden = Config.Instance.HideOverlay
			                  || (Config.Instance.HideOverlayInSpectator && _game.CurrentGameMode == GameMode.Spectator)
			                  || Helper.GameWindowState == WindowState.Minimized;
			var behind = !isForeground &&
			             (_game.IsInMenu ? Config.Instance.HideMenuOverlayInBackground : Config.Instance.HideInBackground);

			var newState = hardHidden ? OverlayZState.Hidden
				: behind ? OverlayZState.Behind : OverlayZState.Visible;

			var contentVisible = newState != OverlayZState.Hidden;
			var updatePosition = contentVisible && !IsContentVisible;
			ShowOverlay(contentVisible);
			if(updatePosition)
				UpdatePosition();

			if(newState != _overlayZState)
			{
				_overlayZState = newState;
				if(newState == OverlayZState.Behind)
					SendToBack();
				else if(newState == OverlayZState.Visible)
					SetTopmost();
			}
			else if(newState == OverlayZState.Behind && User32.IsTopmost(new WindowInteropHelper(this).Handle))
				SendToBack();
		}

		private void SendToBack()
		{
			SetClickthrough(true);
			User32.SendWindowToBack(new WindowInteropHelper(this).Handle);
		}

		public void UpdatePosition()
		{
			if(Helper.GameWindowState == WindowState.Minimized)
				return;

			var hsRect = User32.GetHearthstoneRect(true);

			//hs window has height 0 if it just launched, screwing things up if the tracker is started before hs is.
			//this prevents that from happening.
			if(hsRect.Height == 0 || !IsContentVisible)
				return;

			var prevWidth = Width;
			var prevHeight = Height;
			SetRect(hsRect.Top, hsRect.Left, hsRect.Width, hsRect.Height);
			if(Width != prevWidth || Height != prevHeight)
				BattlegroundsGuidesTabsViewModel.AspectRatio = Width / Height;

			ApplyAutoScaling();
			UpdateElementSizes();
			UpdateElementPositions();

			if(_game is { IsBattlegroundsMatch: true, IsBattlegroundsHeroPickingDone: false })
				Core.Overlay.SetAnomalyGuidesMulliganTrigger();
		}

		internal void UpdateBattlegroundsOverlay()
		{
			try
			{
				var fadeBgsMinionsList = false;
				var turn = _game.GetTurnNumber();
				_leaderboardDeadForText.ForEach(x => x.Visibility = Collapsed);
				_leaderboardDeadForTurnText.ForEach(x => x.Visibility = Collapsed);
				if(turn == 0)
				{
					UpdateBobsBuddyPanelVisibility();
					return;
				}
				var shouldShowOpponentInfo = false;
				if(_leaderboardHoveredEntityId is int heroEntityId)
				{
					fadeBgsMinionsList = true;
					_leaderboardDeadForText.ForEach(x => x.Visibility = Visible);
					_leaderboardDeadForTurnText.ForEach(x => x.Visibility = Visible);

					// check if it's the team mate
					Core.Game.Entities.TryGetValue(heroEntityId, out var entity);
					var state = _game.GetBattlegroundsBoardStateFor(heroEntityId);
					BgsOpponentInfo.Update(heroEntityId, state, turn);
					shouldShowOpponentInfo = !(entity != null && (
						entity.IsControlledBy(_game.Player.Id) ||
						(
							Core.Game.IsBattlegroundsDuosMatch &&
							entity.GetTag(GameTag.BACON_DUO_TEAM_ID)
							== Core.Game.PlayerEntity?.GetTag(GameTag.BACON_DUO_TEAM_ID)
						)
					));
				}

				if(shouldShowOpponentInfo)
				{
					BgsOpponentInfo.Visibility = Visible;
					BgsOpponentInfo.UpdateLayout();
					_bgsPastOpponentBoardBehavior.Show();
				}
				else
				{
					BgsOpponentInfo.Visibility = Collapsed;
					_bgsPastOpponentBoardBehavior.Hide();
					BgsOpponentInfo.ClearLastKnownBoard();
				}

				// handing the slot over to the past opponent board and taking it back should not animate
				var swappingPastOpponentBoard = shouldShowOpponentInfo || _bgsPastOpponentBoardShown;
				_bgsPastOpponentBoardShown = shouldShowOpponentInfo;
				UpdateBobsBuddyPanelVisibility(swappingPastOpponentBoard ? AnimationType.Instant : null);

				// Only fade the minions, if we're out of mulligan
				if(_game.GameEntity?.GetTag(GameTag.STEP) <= (int)Step.BEGIN_MULLIGAN)
					fadeBgsMinionsList = false;
				BgsTopBar.Opacity = fadeBgsMinionsList ? 0.3 : 1;
				BgsMinionPinning.Opacity = fadeBgsMinionsList ? 0.3 : 1;
				BobsBuddyDisplay.Opacity = fadeBgsMinionsList ? 0.3 : 1;
			}
			catch(Exception ex)
			{
				Log.Error(ex);
			}
		}

		// The play zone drives the shop the minion pinning panel pins from, and the opponent panel
		// shows whatever the leaderboard hover last asked for, so refresh the overlay when it changes.
		internal void OnPlayZoneStateChanged(BoardStateArgs args) => UpdateBattlegroundsOverlay();

		public bool PointInsideControl(Point pos, double actualWidth, double actualHeight)
			=> PointInsideControl(pos, actualWidth, actualHeight, new Thickness(0));
		public bool PointInsideControl(Point pos, double actualWidth, double actualHeight, Thickness margin)
			=> pos.X > 0 - margin.Left && pos.X < actualWidth + margin.Right && (pos.Y > 0 - margin.Top && pos.Y < actualHeight + margin.Bottom);

		public void UpdateScaling()
		{
			BattlegroundsSession.RenderTransform = new ScaleTransform(Config.Instance.OverlaySessionRecapScaling / 100,
																	Config.Instance.OverlaySessionRecapScaling / 100);
		}

		public double AutoScaling { get; set; } = 1;

		private void UpdateElementPositions()
		{
			Canvas.SetTop(BattlegroundsSessionStackPanel, Height * Config.Instance.SessionRecapTop / 100);
			Canvas.SetLeft(BattlegroundsSessionStackPanel, Width * Config.Instance.SessionRecapLeft / 100);
		}

		internal void UpdateElementSizes()
		{
			OnPropertyChanged(nameof(BattlegroundsTileHeight));
			OnPropertyChanged(nameof(BattlegroundsTileWidth));

			var bgsTopBarScaling = _bgsTopBarBehavior.GetScaling?.Invoke() ?? 1.0;
			var bgsMinionPinningButtonSpace = BgsMinionPinningVisibility == Visible ? 20 : 0;
			var bgsMinionPinningHeight = BgsMinionPinningVisibility == Visible ? BattlegroundsMinionPinningControl.ActualHeight : 0;
			if(GuidesTabs.TabsContent is not null)
			{
				GuidesTabs.TabsContent.MaxHeight =  Math.Max(0, (ActualHeight - 54 + bgsMinionPinningButtonSpace) * 0.95 / bgsTopBarScaling - bgsMinionPinningHeight);
			}

			if(BattlegroundsMinions is not null)
			{
				BattlegroundsMinions.MaxHeight =  Math.Max(0, (ActualHeight + bgsMinionPinningButtonSpace) * 0.95 / bgsTopBarScaling - bgsMinionPinningHeight);
			}
		}

		public void ApplyAutoScaling()
		{
			var scaling = Height / 1080;
			AutoScaling = Math.Max(0.8, Math.Min(1.3, scaling));

			_bgsTopBarBehavior.UpdatePosition();
			_bgsTopBarBehavior.UpdateScaling();

			_heroNotificationBehavior.UpdatePosition();
			_heroNotificationBehavior.UpdateScaling();

			_timewarpNotificationBehavior.UpdatePosition();
			_timewarpNotificationBehavior.UpdateScaling();

			_bgsBobsBuddyBehavior.UpdatePosition();
			_bgsBobsBuddyBehavior.UpdateScaling();

			_bgsPastOpponentBoardBehavior.UpdatePosition();
			_bgsPastOpponentBoardBehavior.UpdateScaling();

			_tier7PreLobbyBehavior.UpdatePosition();
			_tier7PreLobbyBehavior.UpdateScaling();

			_battlegroundsInspirationBehavior.UpdatePosition();
			_battlegroundsInspirationBehavior.UpdateScaling();

			_bgsChinaModuleBehavior.UpdatePosition();
			_bgsChinaModuleBehavior.UpdateScaling();

			_bgsAutomationLogBehavior.UpdatePosition();
			_bgsAutomationLogBehavior.UpdateScaling();

			BattlegroundsHeroPickingViewModel.Scaling = scaling;
			BattlegroundsHeroPicking.Width = Width / scaling;
			BattlegroundsHeroPicking.Height = Height / scaling;

			BattlegroundsQuestPickingViewModel.Scaling = scaling;
			BattlegroundsQuestPicking.Width = Width / scaling;
			BattlegroundsQuestPicking.Height = Height / scaling;

			BattlegroundsTrinketPickingViewModel.Scaling = scaling;
			BattlegroundsTrinketPicking.Width = Width / scaling;
			BattlegroundsTrinketPicking.Height = Height / scaling;

			BattlegroundsHeroPowerPickingViewModel.Scaling = scaling;
			BattlegroundsHeroPowerPicking.Width = Width / scaling;
			BattlegroundsHeroPowerPicking.Height = Height / scaling;

			BattlegroundsMinionPinningViewModel.Scaling = scaling;
			BgsMinionPinningShop.Width = Width / scaling;
			BgsMinionPinningShop.Height = Height / scaling;
		}

		private void OverlayWindow_OnDeactivated(object sender, EventArgs e) => SetTopmost();

		public void UpdateVisibilities()
		{
			UpdateBattlegroundsSessionVisibility();
			UpdateBattlegroundsGuidesPreLobbyVisibility();
			UpdateTier7PreLobbyVisibility();
		}

		// In the battlegrounds lobby, in a battlegrounds match, or transitioning between the two
		private bool InBattlegroundsScene => SceneHandler.Scene switch
		{
			Mode.BACON => true,
			Mode.GAMEPLAY => Core.Game.IsBattlegroundsMatch,
			// Scene is transitioning - do not check for IsBattlegroundsMatch because that might not be set yet/still
			null => (
				// Start of Match
				(SceneHandler.LastScene == Mode.BACON && SceneHandler.NextScene == Mode.GAMEPLAY)
				// End of Match
				|| (SceneHandler.LastScene == Mode.GAMEPLAY && SceneHandler.NextScene == Mode.BACON)
			),
			_ => false
		};

		// the past opponent board takes the same slot as Bob's Buddy, so only one of them can be up
		private bool _bgsPastOpponentBoardShown;

		private bool ShouldShowBobsBuddyPanel()
		{
			if(!Config.Instance.RunBobsBuddy)
				return false;

			if(Remote.Config.Data?.BobsBuddy?.Disabled ?? false)
				return false;

			if(_bgsPastOpponentBoardShown)
				return false;

			// the game type outlives the match, and the two signals for leaving it (the scene and the
			// log) do not arrive in a fixed order, so the match is over as soon as either one says so.
			// A scene we cannot read is not one of them, or a stalled watcher would keep the panel down
			if(SceneHandler.Scene is { } scene && scene != Mode.GAMEPLAY)
				return false;

			return _game.IsBattlegroundsMatch && !_game.IsInMenu;
		}

		internal void UpdateBobsBuddyPanelVisibility(AnimationType? animation = null)
		{
			if(ShouldShowBobsBuddyPanel())
				_bgsBobsBuddyBehavior.Show(animation);
			else
				_bgsBobsBuddyBehavior.Hide(animation);
		}

		public void UpdateBattlegroundsSessionVisibility()
		{
			var show = _game.IsRunning
			    && (Config.Instance.ShowSessionRecap || _uiMovable)
				&& InBattlegroundsScene
				&& (SceneHandler.Scene == Mode.GAMEPLAY || Config.Instance.ShowSessionRecapBetweenGames);

			if(show || (_game.IsChinaModuleActive && Config.Instance.ShowSessionRecap))
			{
				FadeAnimation.SetVisibility(BattlegroundsSessionStackPanel, Visible);
				BattlegroundsSessionViewModelVM.Update();
				Core.Game.BattlegroundsSessionViewModel.UpdateSectionsVisibilities();
			}
			else
			{
				FadeAnimation.SetVisibility(BattlegroundsSessionStackPanel, Collapsed);
			}
		}

		private bool ShouldShowBattlegroundsGuidesPreLobby()
		{
			if(!_game.IsRunning || !InBattlegroundsScene)
				return false;

			if(!Config.Instance.ShowBattlegroundsBrowser)
				return false;

			if(!Config.Instance.ShowMinionBrowserBetweenGames)
				return false;

			// once up, the bar stays up while the match loads in, until ShowBgsTopBar hands it over
			return BgsGuidesPreLobbyVisible || SceneHandler.Scene == Mode.BACON;
		}

		public void UpdateBattlegroundsGuidesPreLobbyVisibility()
		{
			if(!ShouldShowBattlegroundsGuidesPreLobby())
			{
				if(BgsGuidesPreLobbyVisible)
					HideBgsTopBar();
				return;
			}

			// this runs on every overlay update, so only touch the view models when something actually changed
			var isDuos = Tier7PreLobbyViewModel.BattlegroundsGameMode == SelectedBattlegroundsGameMode.DUOS;
			BattlegroundsGuidesTabsViewModel.OnQueueChanged(_game.QueueEvents.IsInQueue, _game.QueueEvents.GameFound);
			if(BgsGuidesPreLobbyVisible)
			{
				if(BattlegroundsMinionsVM.IsDuos != isDuos)
					BattlegroundsMinionsVM.IsDuos = isDuos;
				return;
			}

			// coming back from a match the bar stays up, so nothing else has torn the match state down
			TearDownBgsTopBarState();

			BgsGuidesPreLobbyVisible = true;
			BattlegroundsGuidesTabsViewModel.IsPreLobby = true;
			BattlegroundsGuidesTabsViewModel.ActiveViewModel = null;
			BattlegroundsCompsGuidesVM.OnPreLobby();
			BattlegroundsMinionsVM.IsPreLobby = true;
			BattlegroundsMinionsVM.IsDuos = isDuos;
			BattlegroundsMinionsVM.PreloadCardTiles();

			BtnTier7Inspiration.Visibility = Collapsed;
			UpdateBgsTopBarContentVisibility();

			_bgsTopBarBehavior.Show();
		}

		public void UpdateTier7PreLobbyVisibility()
		{
			// hiding the panel resets the game mode and the watcher only emits on change
			if(Tier7PreLobbyViewModel.BattlegroundsGameMode == SelectedBattlegroundsGameMode.UNKNOWN && _game.IsInMenu && SceneHandler.Scene == Mode.BACON)
			{
				var mode = Reflection.Client.GetSelectedBattlegroundsGameMode();
				Tier7PreLobbyViewModel.BattlegroundsGameMode = mode;
				BattlegroundsSessionViewModelVM.BattlegroundsGameMode = mode;
			}

			var show = (
				_game.IsRunning &&
				_game.IsInMenu &&
				!_game.QueueEvents.IsInQueue &&
				SceneHandler.Scene == Mode.BACON &&
				Config.Instance.EnableBattlegroundsTier7Overlay &&
				// subs can toggle this independently, non-subs can just toggle the Tier7 setting above
				(Config.Instance.ShowBattlegroundsTier7PreLobby || !(HSReplayNetOAuth.AccountData?.IsTier7 ?? false)) &&
				(
					Tier7PreLobbyViewModel.BattlegroundsGameMode == SelectedBattlegroundsGameMode.SOLO ||
					Tier7PreLobbyViewModel.BattlegroundsGameMode == SelectedBattlegroundsGameMode.DUOS
				)
			);

			if(show)
			{
				_tier7PreLobbyBehavior.Show();
				Tier7PreLobbyViewModel.Update().Forget();
			}
			else
			{
				_tier7PreLobbyBehavior.Hide();
			}
		}

		private void BgsMinionsFilterCover_OnMouseDown(object sender, MouseButtonEventArgs e)
		{
			BattlegroundsMinionsVM.IsFiltersOpen = false;
		}

		private void BgsMinionsFilterCover_OnMouseEnter(object sender, MouseEventArgs mouseEventArgs)
		{
			BattlegroundsMinionsVM.IsFilterRegionHovered = true;
		}

		private void BgsMinionsFilterCover_OnMouseLeave(object sender, MouseEventArgs mouseEventArgs)
		{
			BattlegroundsMinionsVM.IsFilterRegionHovered = false;
		}
	}
}
