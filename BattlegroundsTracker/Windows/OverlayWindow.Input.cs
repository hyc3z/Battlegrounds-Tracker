using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace Hearthstone_Deck_Tracker.Windows
{
	public partial class OverlayWindow
	{
		private void MouseInputOnLmbUp(object sender, EventArgs eventArgs)
		{
			if (Visibility != Visibility.Visible)
				return;
			if (_selectedUiElement != null)
				Config.Save();
			_selectedUiElement = null;
			_lmbDown = false;
			_resizeElement = false;
		}

		private void MouseInputOnMouseMoved(object sender, EventArgs eventArgs)
		{
			if (!_lmbDown || Visibility != Visibility.Visible)
				return;

			var pos = User32.GetMousePos();
			var newPos = new Point(pos.X, pos.Y);
			var delta = new Point((newPos.X - _mousePos.X) * 100, (newPos.Y - _mousePos.Y) * 100);
			_mousePos = newPos;

			// the session recap is the only movable panel the Battlegrounds overlay still has
			if(_selectedUiElement is Panel panel && panel.Equals(BattlegroundsSessionStackPanel) && !_resizeElement)
			{
				Config.Instance.SessionRecapTop += delta.Y / Height;
				Config.Instance.SessionRecapLeft += delta.X / Width;
				Canvas.SetTop(_movableElements[panel], Height * Config.Instance.SessionRecapTop / 100);
				Canvas.SetLeft(_movableElements[panel], Width * Config.Instance.SessionRecapLeft / 100);
			}
		}

		private void MouseInputOnLmbDown(object sender, EventArgs eventArgs)
		{
			if (!User32.IsHearthstoneInForeground() || Visibility != Visibility.Visible)
				return;

			var pos = User32.GetMousePos();
			_mousePos = new Point(pos.X, pos.Y);

			if (_uiMovable)
			{
				_lmbDown = true;
				foreach (var element in _movableElements)
				{
					var relativePos = element.Value.PointFromScreen(_mousePos);
					if(element.Key is Panel && PointInsideControl(relativePos, element.Value.ActualWidth, element.Value.ActualHeight))
					{
						_selectedUiElement = element.Key;
						return;
					}
				}
			}
		}

		public async Task<bool> UnlockUi(bool battlegroundsMode = false)
		{
			_uiMovable = !_uiMovable;
			Update(false);
			if (_uiMovable)
			{
				HookMouse();

				// small delay to allow for elements to be added to overlay
				await Task.Delay(50);

				foreach (var movableElement in _movableElements)
				{
					try
					{
						// in non-Battlegrounds mode the session recap is not one of the movable panels
						if(!battlegroundsMode && movableElement.Key == BattlegroundsSessionStackPanel)
							continue;

						if (!CanvasInfo.Children.Contains(movableElement.Value))
							CanvasInfo.Children.Add(movableElement.Value);

						movableElement.Value.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#4C0000FF");

						Canvas.SetTop(movableElement.Value, Canvas.GetTop(movableElement.Key));
						Canvas.SetLeft(movableElement.Value, Canvas.GetLeft(movableElement.Key));

						var elementSize = GetUiElementSize(movableElement.Key);
						if(movableElement.Key == BattlegroundsSessionStackPanel)
						{
							movableElement.Value.Height = elementSize.Height > 0 ? elementSize.Height * Config.Instance.OverlaySessionRecapScaling / 100 : 0;
							movableElement.Value.Width = elementSize.Width > 0 ? elementSize.Width * Config.Instance.OverlaySessionRecapScaling / 100 : 0;
						}
						else
						{
							movableElement.Value.Height = elementSize.Height > 0 ? elementSize.Height : 0;
							movableElement.Value.Width = elementSize.Width > 0 ? elementSize.Width : 0;
						}

						movableElement.Value.Visibility = Visibility.Visible;
					}
					catch (Exception ex)
					{
						Log.Info(ex.ToString());
					}
				}
			}
			else
			{
				UnHookMouse();

				foreach(var movableElement in _movableElements)
					movableElement.Value.Visibility = Visibility.Collapsed;
			}

			return _uiMovable;
		}

		private Size GetUiElementSize(UIElement element)
		{
			if (element is FrameworkElement frameworkElement)
				return new Size(frameworkElement.ActualWidth, frameworkElement.ActualHeight);
			return new Size();
		}

		public void HookMouse()
		{
			if (_mouseInput != null)
				return;
			_mouseInput = new User32.MouseInput();
			_mouseInput.LmbDown += MouseInputOnLmbDown;
			_mouseInput.LmbUp += MouseInputOnLmbUp;
			_mouseInput.MouseMoved += MouseInputOnMouseMoved;
			Log.Info("Enabled mouse hook");
		}

		public void UnHookMouse()
		{
			if (_uiMovable || _mouseInput == null)
				return;
			_mouseInput.Dispose();
			_mouseInput = null;
			Log.Info("Disabled mouse hook");
		}
	}
}
