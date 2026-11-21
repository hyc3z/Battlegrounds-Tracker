using Hearthstone_Deck_Tracker.Enums;

namespace Hearthstone_Deck_Tracker.Utility;

/// <summary>
/// Keeps track of the settings that are changed through the background mode toggle. They should be read through this
/// class rather than through the Config.
/// </summary>
internal static class BackgroundMode
{
	public const bool IsAvailable = false;

	public static bool IsActive => IsAvailable && Config.Instance.BackgroundBehavior == BackgroundBehavior.KeepRunning;

	public static bool CloseToTray => IsActive || Config.Instance.CloseToTray;

	public static bool MinimizeToTray => IsActive || Config.Instance.MinimizeToTray;

	public static bool StartMinimized => IsActive ? StartupFlags.IsAutostart : Config.Instance.StartMinimized;

	public static bool StartsInTray => StartupFlags.IsBackgroundUpdate || IsActive && StartupFlags.IsAutostart;

	public static bool ShowSplashScreen => IsActive || Config.Instance.ShowSplashScreen;

	public static bool CheckForUpdates => IsActive || Config.Instance.CheckForUpdates;

	public static bool CloseWithHearthstone => !IsActive && Config.Instance.CloseWithHearthstone;

	public static bool StartHearthstoneWithHDT => !IsActive && Config.Instance.StartHearthstoneWithHDT;
}
