#region

using System;
using System.IO;
using Hearthstone_Deck_Tracker.Utility.Logging;

#endregion

namespace Hearthstone_Deck_Tracker.Utility
{
	public static class ConfigManager
	{
		public static Version? UpdatedVersion { get; private set; }
		public static Version? PreviousVersion { get; private set; }

		public static void Run()
		{
			PreviousVersion = string.IsNullOrEmpty(Config.Instance.CreatedByVersion) ? null : new Version(Config.Instance.CreatedByVersion);
			var currentVersion = Helper.GetCurrentVersion();

			// Assign current version to the config instance so that it will be saved when the config
			// is rewritten to disk, thereby telling us what version of the application created it
			Config.Instance.CreatedByVersion = currentVersion.ToString();

#if(!INSTALLED)
			if(!Directory.Exists(Config.Instance.DataDir))
				Config.Instance.Reset(nameof(Config.DataDirPath));
#endif

			if(PreviousVersion != null && currentVersion > PreviousVersion)
			{
				Log.Info($"Config created by {PreviousVersion}, updated to {currentVersion}");
				UpdatedVersion = currentVersion;
			}

			Config.Save();
		}
	}
}