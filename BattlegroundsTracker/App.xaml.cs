#region

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Windows;

#endregion

namespace Hearthstone_Deck_Tracker
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		internal const string AppUserModelId = "com.battlegroundstracker.BattlegroundsTracker";

		private static bool _createdReport;

		static App()
		{
			EmbeddedResourceManager.Install();
		}

		private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			if(!_createdReport)
			{
				_createdReport = true;
				var date = DateTime.Now;
				var fileName = "Crash Reports\\" + $"Crash report {date.Day}{date.Month}{date.Year}-{date.Hour}{date.Minute}";

				if(!Directory.Exists("Crash Reports"))
					Directory.CreateDirectory("Crash Reports");

				using(var sr = new StreamWriter(fileName + ".txt", true))
				{
					sr.WriteLine("########## " + DateTime.Now + " ##########");
					sr.WriteLine(e.Exception);
					sr.WriteLine("Logfile: " + Log.CurrentLogFile);
				}
				new CrashDialog(e.Exception).ShowDialog();
				e.Handled = true;
				Shutdown();
			}
			e.Handled = true;
		}

		private void App_OnStartup(object sender, StartupEventArgs e)
		{
			Shell32.SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
			ShutdownMode = ShutdownMode.OnExplicitShutdown;
			Core.Initialize();
		}

		private void HandleManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e) => e.Handled = true;
	}
}
