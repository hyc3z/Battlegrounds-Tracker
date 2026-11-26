using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Hearthstone_Deck_Tracker.Automation;
using Hearthstone_Deck_Tracker.Commands;
using Hearthstone_Deck_Tracker.Utility.MVVM;

namespace Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Automation
{
	public enum AutomationLogLevel
	{
		Info,
		Busy,
		Success,
		Warning,
		Error
	}

	public sealed class AutomationLogEntry
	{
		private static readonly Brush InfoBrush = CreateFrozenBrush(0xD8, 0xDE, 0xE4);
		private static readonly Brush BusyBrush = CreateFrozenBrush(0xF1, 0xC4, 0x0F);
		private static readonly Brush SuccessBrush = CreateFrozenBrush(0x8A, 0xC6, 0x6E);
		private static readonly Brush WarningBrush = CreateFrozenBrush(0xE5, 0xC0, 0x7B);
		private static readonly Brush ErrorBrush = CreateFrozenBrush(0xE0, 0x6C, 0x75);

		public string Timestamp { get; }
		public string Message { get; }
		public AutomationLogLevel Level { get; }

		public Brush MessageBrush => Level switch
		{
			AutomationLogLevel.Busy => BusyBrush,
			AutomationLogLevel.Success => SuccessBrush,
			AutomationLogLevel.Warning => WarningBrush,
			AutomationLogLevel.Error => ErrorBrush,
			_ => InfoBrush
		};

		public AutomationLogEntry(string timestamp, string message, AutomationLogLevel level)
		{
			Timestamp = timestamp;
			Message = message;
			Level = level;
		}

		private static Brush CreateFrozenBrush(byte r, byte g, byte b)
		{
			var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
			brush.Freeze();
			return brush;
		}
	}

	public class AutomationLogViewModel : ViewModel
	{
		private const int MaxEntries = 80;

		private static readonly Brush IdleDotBrush = CreateFrozenBrush(0x7F, 0x8C, 0x8D);
		private static readonly Brush BusyDotBrush = CreateFrozenBrush(0xF1, 0xC4, 0x0F);
		private static readonly Brush SuccessDotBrush = CreateFrozenBrush(0x8A, 0xC6, 0x6E);
		private static readonly Brush WarningDotBrush = CreateFrozenBrush(0xE5, 0xC0, 0x7B);
		private static readonly Brush ErrorDotBrush = CreateFrozenBrush(0xE0, 0x6C, 0x75);

		public ObservableCollection<AutomationLogEntry> Entries { get; } = new();

		public event Action? EntryAdded;

		public string StatusText
		{
			get => GetProp("待机") ?? "待机";
			private set => SetProp(value);
		}

		public AutomationLogLevel StatusLevel
		{
			get => GetProp(AutomationLogLevel.Info);
			private set
			{
				SetProp(value);
				OnPropertyChanged(nameof(StatusDotBrush));
			}
		}

		public Brush StatusDotBrush => StatusLevel switch
		{
			AutomationLogLevel.Busy => BusyDotBrush,
			AutomationLogLevel.Success => SuccessDotBrush,
			AutomationLogLevel.Warning => WarningDotBrush,
			AutomationLogLevel.Error => ErrorDotBrush,
			_ => IdleDotBrush
		};

		public bool IsBusy
		{
			get => GetProp(false);
			private set => SetProp(value);
		}

		public bool IsExpanded
		{
			get => GetProp(true);
			set
			{
				SetProp(value);
				OnPropertyChanged(nameof(ExpandToggleText));
			}
		}

		public string ExpandToggleText => IsExpanded ? "▴" : "▾";

		public ICommand ToggleExpandCommand => new Command(() => IsExpanded = !IsExpanded);

		public ICommand ClearCommand => new Command(() => RunOnUiThread(() => Entries.Clear()));

		public ICommand StopCommand => new Command(() => BattlegroundsAutomationController.CancelActive("用户点击停止按钮"));

		public void SetStatus(string status, AutomationLogLevel level = AutomationLogLevel.Info, bool isBusy = false)
		{
			RunOnUiThread(() =>
			{
				StatusText = status;
				StatusLevel = level;
				IsBusy = isBusy;
			});
		}

		public void AddLog(string message, AutomationLogLevel level = AutomationLogLevel.Info)
		{
			var timestamp = DateTime.Now.ToString("HH:mm:ss");
			RunOnUiThread(() =>
			{
				Entries.Add(new AutomationLogEntry(timestamp, message, level));
				while(Entries.Count > MaxEntries)
					Entries.RemoveAt(0);
				EntryAdded?.Invoke();
			});
		}

		public void Reset()
		{
			RunOnUiThread(() =>
			{
				Entries.Clear();
				StatusText = "待机";
				StatusLevel = AutomationLogLevel.Info;
				IsBusy = false;
			});
		}

		private static void RunOnUiThread(Action action)
		{
			var dispatcher = Application.Current?.Dispatcher;
			if(dispatcher == null || dispatcher.CheckAccess())
				action();
			else
				dispatcher.BeginInvoke(action);
		}

		private static Brush CreateFrozenBrush(byte r, byte g, byte b)
		{
			var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
			brush.Freeze();
			return brush;
		}
	}
}
