using System.Windows;
using System.Windows.Controls;

namespace Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Automation
{
	public partial class AutomationLogPanel : UserControl
	{
		private AutomationLogViewModel? _subscribedVm;

		public AutomationLogPanel()
		{
			InitializeComponent();
			DataContextChanged += OnDataContextChanged;
		}

		private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if(_subscribedVm != null)
				_subscribedVm.EntryAdded -= OnEntryAdded;

			_subscribedVm = e.NewValue as AutomationLogViewModel;
			if(_subscribedVm != null)
				_subscribedVm.EntryAdded += OnEntryAdded;
		}

		private void OnEntryAdded()
		{
			LogScrollViewer?.ScrollToEnd();
		}
	}
}
