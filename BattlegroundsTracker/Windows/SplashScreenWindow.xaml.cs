using System;
using System.Windows.Interop;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Utility.MVVM;

namespace Hearthstone_Deck_Tracker.Windows
{
	public partial class SplashScreenWindow
	{
		public SplashScreenWindow()
		{
			InitializeComponent();
		}

		private void Window_SourceInitialized(object sender, EventArgs e)
		{
			var hwnd = new WindowInteropHelper(this).Handle;
			User32.SetWindowExStyle(hwnd, User32.WsExToolWindow);
		}

		public void ShowConditional()
		{
			var startsInTray = StartupFlags.IsAutostart && BackgroundMode.StartMinimized && BackgroundMode.MinimizeToTray;
			if(BackgroundMode.ShowSplashScreen && !StartupFlags.IsBackgroundUpdate && !startsInTray)
			{
				Log.Info("Showing splashscreen...");
				Show();
			}
		}
	}

	public class SplashScreenWindowViewModel : ViewModel
	{
		private const string LocLoading = "SplashScreen_Text_Loading";

		public string? VersionString
		{
			get => GetProp(Helper.GetCurrentVersion().ToVersionString());
			set => SetProp(value);
		}

		public string? LoadingString
		{
			get => GetProp(LocUtil.Get(LocLoading));
			set => SetProp(value);
		}
	}
}
