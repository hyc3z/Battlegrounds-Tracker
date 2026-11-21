using System;
using System.IO;
using System.Windows;
using System.Windows.Forms.VisualStyles;
using System.Windows.Media.Animation;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Analytics;
using MahApps.Metro;

namespace Hearthstone_Deck_Tracker.Windows
{
	public partial class CrashDialog
	{
		// HRESULT_FROM_WIN32(ERROR_SYSTEM_INTEGRITY_POLICY_VIOLATION), e.g. when blocked by Smart App Control
		private const int SystemIntegrityPolicyViolation = unchecked((int)0x800711C7);

		private readonly Exception _exception;
		

		public CrashDialog(Exception exception)
		{
			_exception = exception;
			SpecialMessage = GetSpecialMessage(exception);
			InitializeComponent();

			if(SpecialMessage != null)
			{
				LabelCrashed.Visibility = Visibility.Collapsed;
				TextBlockSpecialMessage.Visibility = Visibility.Visible;
			}


		}

		public string ExceptionMessage => _exception.Message;

		public string FullExceptionText => _exception.ToString();

		public string? SpecialMessage { get; }

		private static string? GetSpecialMessage(Exception exception)
		{
			for(var e = exception; e != null; e = e.InnerException)
			{
				if(e is FileLoadException { HResult: SystemIntegrityPolicyViolation })
					return LocUtil.Get("CrashDialog_Label_Special_UnsignedBlocked");
			}
			return null;
		}

		private void ButtonSend_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void ButtonClose_Click(object sender, RoutedEventArgs e) => Close();

		private void ButtonShowStacktrace_OnClick(object sender, RoutedEventArgs e)
		{
			if(TextBoxStackTrace.Visibility == Visibility.Collapsed)
			{
				TextBoxStackTrace.Visibility = Visibility.Visible;
				(TextBoxStackTrace.TryFindResource("ShowStackTraceStoryboard") as Storyboard)?.Begin();
				ButtonShowStacktrace.Content = "Hide Stacktrace";
			}
			else
			{
				TextBoxStackTrace.Visibility = Visibility.Collapsed;
				TextBoxStackTrace.Height = 0;
				(TextBoxStackTrace.TryFindResource("HideStackTraceStoryboard") as Storyboard)?.Begin();
				
				ButtonShowStacktrace.Content = "Show Stacktrace";
			}
		}

		private void CrashDialog_Closed(object sender, EventArgs e)
		{
			// Crash reporting (Sentry) has been removed from this fork; the crash report is written by App.xaml.cs.
		}
	}
}
