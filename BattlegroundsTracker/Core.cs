#region

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using HearthMirror;
using HearthWatcher;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.LogReader;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Assets;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.HotKeys;
using Hearthstone_Deck_Tracker.Utility.LogConfig;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Utility.RemoteData;
using Hearthstone_Deck_Tracker.Windows;
using MahApps.Metro.Controls.Dialogs;
using WPFLocalizeExtension.Engine;

#endregion

namespace Hearthstone_Deck_Tracker
{
	/// <summary>
	/// Application-wide entry points. This is the Battlegrounds-only fork of HDT's core: deck management,
	/// statistics, plugin, updater and telemetry infrastructure have been removed.
	/// </summary>
	public static class Core
	{
		private const int UpdateDelay = 16;
		private static TrayIcon? _trayIcon;
		private static OverlayWindow? _overlay;
		private static readonly LogWatcherManager LogWatcherManger = new();

		// Should be global to application. Always use this one instead of
		// instantiating a new HttpClient.
		public static readonly HttpClient HttpClient = new(new HttpClientHandler
			{ AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });

		internal static GameV2? _game;
		public static GameV2 Game => _game ??= new GameV2();
		private static MainWindow? _mainWindow;
		public static MainWindow MainWindow => _mainWindow ??= new MainWindow();

		public static bool Initialized { get; private set; }

		public static TrayIcon TrayIcon => _trayIcon ??= new TrayIcon();

		public static OverlayWindow Overlay => _overlay ??= new OverlayWindow(Game);

		public static bool IsShuttingDown { get; private set; }

		internal static event Action<bool>? GameIsRunningChanged;

		internal static DateTime LastGameRunning { get; private set; } = DateTime.MinValue;

		public static async void Initialize()
		{
						LocalizeDictionary.Instance.Culture = CultureInfo.GetCultureInfo("en-US");
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
			ServicePointManager.DefaultConnectionLimit = 30;
			Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

			HearthDb.Config.AutoLoadCardDefs = false; // Needs to be set before accessing HearthDb.Cards

			if(!Config.IsLoaded)
				Config.Load();

			LocUtil.UpdateCultureInfo();

			if(!SingleInstance.TryClaim(OnSecondInstance))
			{
								Application.Current.Shutdown();
				return;
			}

			var forceSoftwareRendering = Config.Instance.UseHardwareAcceleration == false;
			if(!forceSoftwareRendering)
			{
				// avoid using hardware acceleration if the render capability is low. This solves the most common OOM crashes
				var renderingTier = RenderCapability.Tier >> 16;
				var isIntelGpu = Helper.IsIntelGpu();
				forceSoftwareRendering = renderingTier == 0 || isIntelGpu;
			}

			if(forceSoftwareRendering)
				RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

						var splashScreenWindow = new SplashScreenWindow();
						splashScreenWindow.ShowConditional();
			
						ConfigManager.Run();

			Log.Info($"Battlegrounds Tracker: {Helper.GetCurrentVersion()}, Operating System: {Helper.GetWindowsVersion()}, .NET Framework: {Helper.GetInstalledDotNetVersion()}");

						Log.Initialize();
						if(SynchronizationContext.Current is { } syncContext)
				PollingWatcher.SetEventContext(syncContext);
			else
				Log.Warn("No SynchronizationContext, watcher events will fire on pool threads");

			Reflection.LogDebugMessage += msg => Log.Debug("HearthMirror RPC[client]: " + msg);
			Reflection.LogMessage += msg => Log.Info("HearthMirror RPC [client]: " + msg);
			Reflection.StdErr += (sender, args) =>
			{
				if(!string.IsNullOrWhiteSpace(args.Data))
					Log.Info("HearthMirror RPC [stderr]: " + args.Data);
			};
			Reflection.StdOut += (sender, args) =>
			{
				if(!string.IsNullOrWhiteSpace(args.Data))
					Log.Info("HearthMirror RPC [stdout]: " + args.Data);
			};
			Reflection.AccessDenied += OnAccessDenied;
			Reflection.Exception += e => Log.Warn("HearthMirror Exception: " + e);

			LocUtil.UpdateCultureInfo();
			Helper.UpdateCardLanguage();

			CardDefsManager.InitialDefsLoaded += () =>
			{
				// Defer starting log watchers until we have at least the initial card defs.
				_ = LogWatcherManger.Start(Game);
			};
			// Load initial card defs
			CardDefsManager.EnsureLatestCardDefs();

			LogConfigUpdater.Run().Forget();
			LogConfigWatcher.Start();

			UITheme.InitializeTheme().Forget();

						MainWindow.Show();
						// StateChanged only sends the window to the tray when the user minimizes to tray
			if(BackgroundMode.StartsInTray)
				MainWindow.MinimizeToTray();
			else if(MainWindow.WindowState != WindowState.Minimized)
				MainWindow.Activate();
			splashScreenWindow.Close();

			UpdateOverlayAsync();

			if(Config.Instance.ShowBattlegroundsMechanicWindow)
				Windows.BattlegroundsMechanicWindow.Show();

			Remote.Config.Load();
			HotKeyManager.Load();

			if(Helper.HearthstoneDirExists && BackgroundMode.StartHearthstoneWithHDT && !Game.IsRunning && !StartupFlags.IsBackgroundUpdate && !StartupFlags.IsAutostart)
				HearthstoneRunner.StartHearthstone().Forget();

			if(Config.Instance.BattlegroundsSessionRecapWindowOnStart && !BackgroundMode.StartsInTray)
				Windows.BattlegroundsSessionWindow.Show();

			Helper.EnsureClientLogConfig();

			Initialized = true;
			
			Config.Instance.CleanShutdown = false;
			Config.Save();
		}

		private static void OnSecondInstance()
		{
			Log.Info("Another instance was started, activating the main window");
			ActivateMainWindow();
		}

		private static void ActivateMainWindow()
		{
			// the main window may not exist yet, in which case it will show up on its own
			if(!Initialized)
				return;
			MainWindow.ActivateWindow();
		}

		private static bool _notifyAccessDenied = true;
		private static async void OnAccessDenied()
		{
			if(!_notifyAccessDenied)
				return;
			_notifyAccessDenied = false;
			Log.Warn("Hearthstone is probably running with elevated permissions. Stopping all watchers.");
			LogWatcherManger.IsEnabled = false;
			await LogWatcherManger.Stop(true);
			Application.Current.Dispatcher.Invoke(() =>
			{
				MainWindow.ShowUnevenPermissionsMessage().Forget();
				MainWindow.ActivateWindow();
			});
		}

		internal static async Task Shutdown()
		{
			await PrepareShutdown();
			Application.Current.Shutdown();
		}

		private static async Task PrepareShutdown()
		{
			try
			{
				Log.Info("Shutting down...");
				var logWatcher = LogWatcherManger.Stop(true);

				//wait for update to finish, might otherwise crash when overlay gets disposed
				for(var i = 0; i < 100; i++)
				{
					if(IsShuttingDown)
						break;
					await Task.Delay(50);
				}

				await logWatcher;

				Windows.CloseAll();
				Overlay.Close();
				MainWindow.Close();

				TrayIcon.NotifyIcon.Visible = false;

				Config.Instance.CleanShutdown = true;
				Config.Save();
			}
			catch(Exception e)
			{
				Log.Error(e);
			}
		}

		internal static async void RestartApplication()
		{
			// This event is fired by Application.Shutdown
			Application.Current.Exit += (_, _) => Process.Start(Application.ResourceAssembly.Location);
			await Shutdown();
		}

		private static async Task ShowRestartRequiredMessageAsync()
		{
			MainWindow.ActivateWindow();
			while(MainWindow.Visibility != Visibility.Visible || MainWindow.WindowState == WindowState.Minimized)
				await Task.Delay(100);
			await MainWindow.ShowMessageAsync("Hearthstone restart required", "The log.config file has been updated. The tracker may not work properly until Hearthstone has been restarted.");
		}

		private static async void UpdateOverlayAsync()
		{
			while(true)
			{
				if(User32.GetHearthstoneWindow() != IntPtr.Zero)
				{
					if(Game.CurrentRegion == Region.UNKNOWN)
					{
						//game started
						Helper.VerifyHearthstonePath();
						Helper.UpdateCardLanguage();

						var ok = Helper.EnsureClientLogConfig();
						var logConfigUpdated = await UpdateLogConfigForRunningClient();
						if(!ok || logConfigUpdated)
						{
							ShowRestartRequiredMessageAsync().Forget();
							Overlay.ShowRestartRequiredWarning();
						}
						Game.CurrentRegion = await Helper.GetCurrentRegion();
						if(Game.CurrentRegion != Region.UNKNOWN)
							Game.MetaData.HearthstoneBuild = null;

						Watchers.SceneWatcher.Run();
						Watchers.UiWatcher.Run();

						Remote.Config.Load();

						Reflection.StartIpcClient();

						CardDefsManager.EnsureLatestCardDefs();
					}
					Overlay.UpdateVisibility(); // Always run, this handles the game being in background, etc

					var gameStarted = !Game.IsRunning;
					if(gameStarted)
					{
						Overlay.HookGameWindow();
						Overlay.UpdatePosition(); // Only needs to be called once, HookGameWindow will trigger subsequent position updates.
						Overlay.Update(true);
					}

					if(Overlay.IsContentVisible)
						Overlay.UpdateBattlegroundsOverlay();

					Game.IsRunning = true;
					LastGameRunning = DateTime.Now;

					// ContentVisibility depends on Game.IsRunning, so notify only after it is set.
					if(gameStarted)
					{
						TrayIcon.MenuItemStartHearthstone.Visible = false;
						GameIsRunningChanged?.Invoke(true);
					}

					Helper.GameWindowState = User32.GetHearthstoneWindowState();
					if(User32.IsHearthstoneInForeground() && Helper.GameWindowState != WindowState.Minimized)
					{
						// nothing to do here for the time being
					}
				}
				else if(Game.IsRunning)
				{
					Game.IsRunning = false;
					GameIsRunningChanged?.Invoke(false);
					Overlay.UnhookGameWindow();
					Overlay.HideOverlayWindow();
					SceneHandler.Reset();
					Watchers.Stop();
					Log.Info("Exited game");
					Game.CurrentRegion = Region.UNKNOWN;
					LogWatcherManger.IsEnabled = true;
					_notifyAccessDenied = true;
					await Reset();
					Game.IsInMenu = true;
					Game.InvalidateMatchInfoCache();
					Overlay.HideRestartRequiredWarning();
					Helper.ClearCachedHearthstoneBuild();
					Overlay.BattlegroundsHeroPickingViewModel.Reset();
					Overlay.BattlegroundsQuestPickingViewModel.Reset();
					Overlay.BattlegroundsTrinketPickingViewModel.Reset();
					Overlay.BattlegroundsHeroPowerPickingViewModel.Reset();
					Overlay.HideBattlegroundsHeroPanel();
					Overlay.HideBattlegroundsTimewarpPanel();
					Overlay.BattlegroundsCompsGuidesVM.Reset();
					Overlay.ChinaModuleVM.Reset();

					TrayIcon.MenuItemStartHearthstone.Visible = true;

					Reflection.StopIpcClient();

					if(BackgroundMode.CloseWithHearthstone)
						MainWindow.Close();
				}

				await Task.Delay(UpdateDelay);
			}
		}

		private static async Task<bool> UpdateLogConfigForRunningClient()
		{
			var path = LogConfigConstants.GetLogConfigPath(Helper.GetHearthstoneCommandLine());
			if(path == LogConfigConstants.LogConfigPath)
				return false;
			try
			{
				return await LogConfigUpdater.Run(path);
			}
			catch(Exception e)
			{
				Log.Error(e);
				return false;
			}
		}

		private static bool _resetting;
		public static async Task Reset(bool updateUI = true)
		{
			if(_resetting)
			{
				Log.Warn("Reset already in progress.");
				return;
			}
			_resetting = true;
			var stoppedReader = await LogWatcherManger.Stop();
			Game.Reset(updateUI: updateUI);
			await Task.Delay(1000);
			if(stoppedReader)
				LogWatcherManger.Start(Game).Forget();
			if(updateUI)
				Overlay.Update(false);
			_resetting = false;
		}

		public static async void HandleRewind(DateTime playTime, DateTime rewindTime)
		{
			await LogWatcherManger.HandleRewind(playTime, rewindTime);
		}

		public static void HandleGameEnd()
		{
			LogWatcherManger.IgnoredTimeRanges.Clear();
		}

		// Constructed overcast/deck-list update hooks. The Battlegrounds overlay has no deck lists; these are
		// kept as no-ops for the remaining game-model call sites.
		internal static void UpdatePlayerCards(bool reset = false) { }
		internal static void UpdateOpponentCards(bool reset = false) { }
		internal static void ResetPlayerResourcesWidgets() { }

		internal static void UpdatePlayerResourcesWidget() { }
		internal static void UpdateOpponentResourcesWidget() { }
		public static class Windows
		{
			private static BattlegroundsSessionWindow? _bgsSessionWindow;
			private static BattlegroundsMechanicWindow? _bgsMechanicWindow;

			public static BattlegroundsSessionWindow BattlegroundsSessionWindow => _bgsSessionWindow ??= new BattlegroundsSessionWindow();
			public static BattlegroundsMechanicWindow BattlegroundsMechanicWindow => _bgsMechanicWindow ??= new BattlegroundsMechanicWindow();
			internal static BattlegroundsMechanicWindow? BattlegroundsMechanicWindowIfCreated => _bgsMechanicWindow;

			// The capturable streaming overlay was removed from this fork. The accessor stays (always null) because
			// the remaining game-model code checks it when deciding whether the overlay content is visible.
			internal static CapturableOverlayWindow? CapturableOverlay => null;

			internal static void CloseAll()
			{
				_bgsSessionWindow?.Close();
				_bgsMechanicWindow?.Close();
			}
		}
	}
}
