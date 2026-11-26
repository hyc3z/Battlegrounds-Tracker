#region

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Hearthstone_Deck_Tracker.HsReplay;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

#endregion

namespace Hearthstone_Deck_Tracker.Windows
{
	/// <summary>
	/// Main window of the Battlegrounds-only tracker. It used to host the deck manager, statistics and
	/// arena UI; all of which has been removed.
	/// </summary>
	public partial class MainWindow : MetroWindow
	{
		private readonly DispatcherTimer _statusTimer;
		private bool _initialized;

		public MainWindow()
		{
			InitializeComponent();

			if(Config.Instance.TrackerWindowTop.HasValue)
				Top = Config.Instance.TrackerWindowTop.Value;
			if(Config.Instance.TrackerWindowLeft.HasValue)
				Left = Config.Instance.TrackerWindowLeft.Value;

			if(Config.Instance.WindowHeight < 0)
				Config.Instance.Reset(nameof(Config.WindowHeight));
			Height = Config.Instance.WindowHeight;
			if(Config.Instance.WindowWidth < 0)
				Config.Instance.Reset(nameof(Config.WindowWidth));
			Width = Config.Instance.WindowWidth;

			if(BackgroundMode.StartMinimized || StartupFlags.IsBackgroundUpdate)
			{
				WindowState = WindowState.Minimized;
				if(BackgroundMode.MinimizeToTray || StartupFlags.IsBackgroundUpdate)
					MinimizeToTray();
			}

			TextBlockVersion.Text = $"v{Helper.GetCurrentVersion().ToVersionString()}";

			_statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			_statusTimer.Tick += (_, _) => UpdateStatus();

			Loaded += (_, _) => _statusTimer.Start();
		}

		private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
		{
			if(_initialized)
				return;
			_initialized = true;

			SliderOverlayOpacity.Value = Config.Instance.OverlayOpacity;
			LblOverlayOpacity.Text = $"{Config.Instance.OverlayOpacity:0}%";
			CheckboxHideOverlay.IsChecked = Config.Instance.HideOverlay;
			CheckboxHideInMenu.IsChecked = Config.Instance.HideInMenu;
			CheckboxHideInBackground.IsChecked = Config.Instance.HideInBackground;
			CheckboxUseAnimations.IsChecked = Config.Instance.UseAnimations;
			CheckboxShowBattlegroundsSessionOnStart.IsChecked = Config.Instance.BattlegroundsSessionRecapWindowOnStart;
			CheckboxShowBattlegroundsMechanicWindow.IsChecked = Config.Instance.ShowBattlegroundsMechanicWindow;

			OptionsBattlegrounds.Load();
			UpdateStatus();
		}

		private void UpdateStatus()
		{
			var game = Core.Game;
			string status;
			if(!game.IsRunning)
				status = Helper.HearthstoneDirExists ? "炉石传说未运行。" : "未找到炉石传说安装目录。";
			else if(game.IsBattlegroundsDuosMatch)
				status = "酒馆战棋双打对局进行中";
			else if(game.IsBattlegroundsMatch)
				status = "酒馆战棋对局进行中";
			else
				status = $"游戏中（模式：{game.CurrentMode}）";

			TextBlockStatus.Text = status;
			TextBlockHearthstoneStatus.Text = Helper.HearthstoneDirExists
				? "已找到炉石传说安装目录。"
				: "未找到炉石传说安装目录。";
			TextBlockHearthMirrorStatus.Text = game.IsRunning ? "已连接到炉石传说" : "";
			BtnStartHearthstone.IsEnabled = !game.IsRunning;
		}

		private void MetroWindow_StateChanged(object sender, EventArgs e)
		{
			if(BackgroundMode.MinimizeToTray && WindowState == WindowState.Minimized)
				MinimizeToTray();
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			if(Core.IsShuttingDown)
			{
				if(!double.IsNaN(Left))
					Config.Instance.TrackerWindowLeft = (int)Left;
				if(!double.IsNaN(Top))
					Config.Instance.TrackerWindowTop = (int)Top;
				if(!double.IsNaN(Width) && Width > 0)
					Config.Instance.WindowWidth = (int)Width;
				if(!double.IsNaN(Height) && Height > 0)
					Config.Instance.WindowHeight = (int)Height;
				return;
			}

			e.Cancel = true;
			if(BackgroundMode.CloseToTray)
				MinimizeToTray();
			else
				_ = Core.Shutdown();
		}

		internal void MinimizeToTray()
		{
			Core.TrayIcon.NotifyIcon.Visible = true;
			Hide();
			Visibility = Visibility.Collapsed;
			ShowInTaskbar = false;
		}

		public void ActivateWindow()
		{
			try
			{
				Show();
				ShowInTaskbar = true;
				Activate();
				WindowState = WindowState.Normal;
			}
			catch(Exception ex)
			{
				Log.Error(ex);
			}
		}

		internal void ShowSettings()
		{
			ActivateWindow();
			MainTabControl.SelectedIndex = 0;
		}

		private void SliderOverlayOpacity_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			if(!_initialized)
				return;
			Config.Instance.OverlayOpacity = Math.Round(e.NewValue);
			LblOverlayOpacity.Text = $"{Config.Instance.OverlayOpacity:0}%";
			Config.Save();
			Core.Overlay.Opacity = Config.Instance.OverlayOpacity / 100;
			Core.Overlay.UpdateVisibility();
		}

		private void CheckboxHideOverlay_OnClick(object sender, RoutedEventArgs e)
		{
			if(!_initialized)
				return;
			Config.Instance.HideOverlay = CheckboxHideOverlay.IsChecked ?? false;
			Config.Save();
			Core.Overlay.UpdateVisibility();
		}

		private void CheckboxHideInMenu_OnClick(object sender, RoutedEventArgs e)
		{
			if(!_initialized)
				return;
			Config.Instance.HideInMenu = CheckboxHideInMenu.IsChecked ?? false;
			Config.Save();
			Core.Overlay.UpdateVisibility();
		}

		private void CheckboxHideInBackground_OnClick(object sender, RoutedEventArgs e)
		{
			if(!_initialized)
				return;
			Config.Instance.HideInBackground = CheckboxHideInBackground.IsChecked ?? false;
			Config.Save();
			Core.Overlay.UpdateVisibility();
		}

		private void CheckboxUseAnimations_OnClick(object sender, RoutedEventArgs e)
		{
			if(!_initialized)
				return;
			Config.Instance.UseAnimations = CheckboxUseAnimations.IsChecked ?? false;
			Config.Save();
		}

		private void CheckboxShowSessionOnStart_OnClick(object sender, RoutedEventArgs e)
		{
			if(!_initialized)
				return;
			Config.Instance.BattlegroundsSessionRecapWindowOnStart = CheckboxShowBattlegroundsSessionOnStart.IsChecked ?? false;
			Config.Save();
		}

		private void CheckboxShowMechanicWindow_OnClick(object sender, RoutedEventArgs e)
		{
			if(!_initialized)
				return;
			Config.Instance.ShowBattlegroundsMechanicWindow = CheckboxShowBattlegroundsMechanicWindow.IsChecked ?? false;
			Config.Save();
			if(Config.Instance.ShowBattlegroundsMechanicWindow)
				Core.Windows.BattlegroundsMechanicWindow.Show();
			else
				Core.Windows.BattlegroundsMechanicWindowIfCreated?.Hide();
		}

		private void BtnStartHearthstone_OnClick(object sender, RoutedEventArgs e) => HearthstoneRunner.StartHearthstone().Forget();

		private void UpdateAccountStatus()
		{
			var account = HSReplayNetOAuth.AccountData;
			if(account == null)
			{
				TextBlockAccountStatus.Text = HSReplayNetOAuth.IsFullyAuthenticated
					? "已登录，但账号数据尚未加载。"
					: "未登录。";
				BtnLogin.IsEnabled = true;
				BtnLogout.IsEnabled = HSReplayNetOAuth.IsAuthenticatedForAnything();
				return;
			}

			TextBlockAccountStatus.Text = $"已登录：{account.Username}{(account.IsTier7 ? " (Tier7)" : "")}";
			BtnLogin.IsEnabled = false;
			BtnLogout.IsEnabled = true;
		}

		private async void BtnLogin_OnClick(object sender, RoutedEventArgs e)
		{
			BtnLogin.IsEnabled = false;
			try
			{
				var error = await HSReplayNetOAuth.Authenticate();
				if(error != AuthenticationErrorType.None)
					await this.ShowMessageAsync("HSReplay.net 登录失败", error.ToString());
			}
			finally
			{
				UpdateAccountStatus();
			}
		}

		private async void BtnLogout_OnClick(object sender, RoutedEventArgs e)
		{
			BtnLogout.IsEnabled = false;
			try
			{
				await HSReplayNetOAuth.Logout();
			}
			finally
			{
				UpdateAccountStatus();
			}
		}

		private void BtnSession_OnClick(object sender, RoutedEventArgs e)
		{
			Core.Windows.BattlegroundsSessionWindow.Show();
			Core.Windows.BattlegroundsSessionWindow.Activate();
		}

		private void BtnDiscord_OnClick(object sender, RoutedEventArgs e) => Helper.TryOpenUrl("https://hsreplay.net/discord/");
	}
}
