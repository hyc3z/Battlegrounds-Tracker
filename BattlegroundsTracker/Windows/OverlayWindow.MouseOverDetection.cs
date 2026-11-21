// #define HOVER_DEBUG // Uncomment to enable hover debug overlay

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Hearthstone_Deck_Tracker.Utility;

#endregion


namespace Hearthstone_Deck_Tracker.Windows
{
	public partial class OverlayWindow
	{
		//Adjusts OpponentDeadFor textblocks left by this amount depending on what position they represent on the leaderboard.
		const double LeftAdjust = .0017;
		//Adjusts the OpponentDeadFor textblock of the next oponent by this to the right so it aligns correctly with the hero portrait.
		const double NextOpponentRightAdjust = .023;
		const double DuosNextOpponentRightAdjust = .015;
		private double LeaderboardTop => Height * 0.15;
		private int? _leaderboardHoveredEntityId = null;

		internal void PositionDeadForText(int nextOpponentLeaderboardPosition)
		{
			if(Core.Game.IsBattlegroundsDuosMatch)
			{
				for(int i = 0; i < _leaderboardDeadForText.Count; i++)
				{
					int j = i / 2;
					var top = LeaderboardTop + BattlegroundsDuosTileHeight * i + BattlegroundsDuosSpacingHeight * j;
					var left = ((_leaderboardDeadForText.Count / 2) - i) * LeftAdjust;
					if(j == nextOpponentLeaderboardPosition - 1)
						left += DuosNextOpponentRightAdjust;
					Canvas.SetTop(_leaderboardDeadForText[i], top);
					Canvas.SetLeft(_leaderboardDeadForText[i], Helper.GetScaledXPos(left, (int)Width, ScreenRatio));
					Canvas.SetTop(_leaderboardDeadForTurnText[i], top);
					Canvas.SetLeft(_leaderboardDeadForTurnText[i], Helper.GetScaledXPos(left, (int)Width, ScreenRatio));
				}
			}
			else
			{
				for(int i = 0; i < _leaderboardDeadForText.Count; i++)
				{
					var top = LeaderboardTop + BattlegroundsTileHeight * i;
					var left = ((_leaderboardDeadForText.Count / 2) - i) * LeftAdjust;
					if(i == nextOpponentLeaderboardPosition - 1)
						left += NextOpponentRightAdjust;
					Canvas.SetTop(_leaderboardDeadForText[i], top);
					Canvas.SetLeft(_leaderboardDeadForText[i], Helper.GetScaledXPos(left, (int)Width, ScreenRatio));
					Canvas.SetTop(_leaderboardDeadForTurnText[i], top);
					Canvas.SetLeft(_leaderboardDeadForTurnText[i], Helper.GetScaledXPos(left, (int)Width, ScreenRatio));
				}
			}
		}

		private Point? GetCursorPos()
		{
			if(!IsContentVisible)
				return null;
			try
			{
				var pos = User32.GetMousePos();
				return CanvasInfo.PointFromScreen(new Point(pos.X, pos.Y));
			}
			catch(InvalidOperationException)
			{
				return null;
			}
		}

		public void SetHoveredBattlegroundsLeaderboardEntityId(int? entityId)
		{
			_leaderboardHoveredEntityId = entityId;
		}

		private bool _runInteractivityUpdates;
		private async void StartInteractivityUpdates()
		{
			if(_runInteractivityUpdates)
				return;
			const int sixtyHz = 1000 / 60;
			_runInteractivityUpdates = true;
			while(_runInteractivityUpdates)
			{
				UpdateHoverable();
				await Task.Delay(sixtyHz);
			}
		}

		private void StopInteractivityUpdates()
		{
			_runInteractivityUpdates = false;
		}

		/// <summary>
		/// Wrapper for MouseEventArgs, used to check whether an event was triggered by custom hover logic.
		/// </summary>
		public class CustomMouseEventArgs : MouseEventArgs
		{
			public CustomMouseEventArgs(MouseDevice mouse, int timestamp) : base(mouse, timestamp) { }
		}

		private HashSet<FrameworkElement> _mouseOverElements = new();

		private static readonly List<FrameworkElement> NoMouseOver = new();

		private class ElementCache<T>
		{
			private readonly TimeSpan _maxAge;
			private DateTime _cacheDate = DateTime.MinValue;
			public readonly Dictionary<FrameworkElement, T> Dict = new();

			public ElementCache(TimeSpan maxAge) => _maxAge = maxAge;

			public bool IsInvalid => DateTime.Now.Subtract(_cacheDate) > _maxAge;

			public void Clear()
			{
				Dict.Clear();
				_cacheDate = DateTime.Now;
			}
		}

		// Scale should rarely ever change. This can most likely be indefinite, but updating it once a second is cheap enough.
		private readonly ElementCache<Vector> _scaleCache = new(TimeSpan.FromSeconds(1));

		// Transforms (element positions relative to overlay), while they will rarely change, they will change sometimes,
		// e.g. when scrolling a card list, and we need to react somewhat fast.
		private readonly ElementCache<Point> _transformCache = new(TimeSpan.FromMilliseconds(200));

		private void UpdateHoverable()
		{
			var cursorPos = GetCursorPos();
			if(cursorPos == null)
				return;

			if(_scaleCache.IsInvalid)
				_scaleCache.Clear();
			if(_transformCache.IsInvalid)
				_transformCache.Clear();

			// behind Hearthstone the overlay is invisible to the player, so treat everything as un-hovered.
			// going through the regular path (rather than returning early) lets the leave events fire for
			// whatever was still hovered when the overlay went behind.
			var behind = _overlayZState == OverlayZState.Behind;

			var clickableMouseOver = behind ? NoMouseOver
				: _clickableElements.Where(e => ElementContains(e, (Point)cursorPos, _scaleCache.Dict, _transformCache.Dict)).ToList();
			SetClickthrough(clickableMouseOver.Count == 0);

			var hoverableMouseOver = behind ? NoMouseOver
				: _hoverableElements.Where(x => ElementContains(x, (Point)cursorPos, _scaleCache.Dict, _transformCache.Dict)).ToList();

#if HOVER_DEBUG
			ClearHoverDebug();
#endif

			// We want to support emitting hover events for multiple elements if they are nested/related,
			// meaning, if the share the same "root" element in CanvasInfo, but not otherwise.
			// This prevents events from firing on elements that are "below" others. This is really only a problem
			// because we don't have proper event propagation here. Might be worth investing in in the future.
			if(hoverableMouseOver.Count > 0)
			{
				var rootDict = new Dictionary<DependencyObject, OverlayElement>();
				foreach(var element in hoverableMouseOver)
				{
					var root = GetCanvasInfoParentRoot(element);
					if(root == null)
						continue;
					if(!rootDict.TryGetValue(root, out var value))
						value = rootDict[root] = new OverlayElement();
					value.Hoverables.Add(element);
				}

				// Check all clickable elements as well. If the "top most" hovered element is clickable we don't
				// want to emit any custom hover event.
				foreach(var element in clickableMouseOver)
				{
					var root = GetCanvasInfoParentRoot(element);
					if(root == null)
						continue;
					if(!rootDict.TryGetValue(root, out var value))
						value = rootDict[root] = new OverlayElement();
					value.Clickables.Add(element);
				}

				if(rootDict.Count > 0)
				{
					// Since we only want to emit hovers for the "top most" element, we need to index and sort them.
					var rootChildrenCount = VisualTreeHelper.GetChildrenCount(CanvasInfo);
					for(var i = 0; i < rootChildrenCount; i++)
					{
						var child = VisualTreeHelper.GetChild(CanvasInfo, i);
						if(rootDict.TryGetValue(child, out var value))
							value.Index = i;
					}

					var newMouseOverElements = rootDict.Values.OrderByDescending(x => x.Index).First().Hoverables;

					EmitMouseLeave(newMouseOverElements); // Emit mouse out before new mouse enter events

					foreach(var mouseOverElement in newMouseOverElements)
					{
						if(!_mouseOverElements.Contains(mouseOverElement))
						{
							mouseOverElement?.RaiseEvent(new CustomMouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
						}

#if HOVER_DEBUG
						if(clickableMouseOver.Count > 0)
							ShowHoverDebug(mouseOverElement, Brushes.Orange, "(Hover)");
						else
							ShowHoverDebug(mouseOverElement, Brushes.Lime, "Hover");
#endif
					}

#if HOVER_DEBUG
					var roots = rootDict.Values.OrderByDescending(x => x.Index).ToList();
					foreach(var element in roots.First().Clickables)
					{
						ShowHoverDebug(element, Brushes.Lime, "HitTest");
					}

					foreach(var root in roots.Skip(1))
					{
						foreach(var element in root.Hoverables)
							ShowHoverDebug(element, Brushes.Red, "COVERED Hover");
						foreach(var element in root.Clickables)
							ShowHoverDebug(element, Brushes.Lime, "HitTest");
					}
#endif

					// remember all elements that are currently hovered
					_mouseOverElements = new HashSet<FrameworkElement>(newMouseOverElements);
				}
				else
				{
					EmitMouseLeave(hoverableMouseOver);
					_mouseOverElements.Clear();
				}
			}
			else
			{

#if HOVER_DEBUG
				if(clickableMouseOver.Count > 0)
				{
					foreach(var element in clickableMouseOver)
						ShowHoverDebug(element, Brushes.Lime, "HitTest");
				}
#endif
				EmitMouseLeave(hoverableMouseOver);
				_mouseOverElements.Clear();
			}

			return;

			void EmitMouseLeave(IList<FrameworkElement> hoveredElements)
			{
				foreach(var previousMouseOverElement in _mouseOverElements)
				{
					if(!hoveredElements.Contains(previousMouseOverElement))
					{
						previousMouseOverElement?.RaiseEvent(new CustomMouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
					}
				}
			}

			DependencyObject? GetCanvasInfoParentRoot(FrameworkElement e)
			{
				DependencyObject element = e;
				var parent = VisualTreeHelper.GetParent(e);
				while(parent != CanvasInfo && parent != null)
				{
					element = parent;
					parent = VisualTreeHelper.GetParent(parent);
				}

				if(parent != null)
					return element;
				return null;
			}
		}

#if HOVER_DEBUG
		private Canvas? _debugCanvas;
		private Canvas DebugCanvas
		{
			get
			{
				if(_debugCanvas == null)
				{
					_debugCanvas = new Canvas()
					{
						HorizontalAlignment = HorizontalAlignment.Stretch,
						VerticalAlignment = VerticalAlignment.Stretch,
					};
					CanvasInfo.Children.Add(_debugCanvas);
				}

				return _debugCanvas;
			}
		}

		private void ClearHoverDebug()
		{
			DebugCanvas.Children.Clear();
			DebugCanvas.Width = CanvasInfo.Width;
			DebugCanvas.Height = CanvasInfo.Height;
		}

		private void ShowHoverDebug(FrameworkElement? element, SolidColorBrush color, string text)
		{
			if(element == null)
				return;
			var scale = Helper.GetTotalScaleTransform(element);
			var name = Helper.GetVisualParent<UserControl>(element)?.GetType().Name;
			var border = new Border
			{
				BorderThickness = new Thickness(1),
				BorderBrush = color,
				Width = element.ActualWidth * scale.X - 2,
				Height = element.ActualHeight * scale.Y - 2,
				IsHitTestVisible = false,
				Child = new TextBlock
				{
					Text = $"{text} ({element.GetType().Name}, {name})",
					Background = Brushes.White,
					VerticalAlignment = VerticalAlignment.Top,
					HorizontalAlignment = HorizontalAlignment.Left
				},
			};
			DebugCanvas.Children.Add(border);
			var point = element.TransformToAncestor(CanvasInfo).Transform(new Point(0, 0));
			Canvas.SetTop(border, point.Y);
			Canvas.SetLeft(border, point.X);
		}
#endif

		private record OverlayElement()
		{
			public List<FrameworkElement> Hoverables { get; } = new();
			public List<FrameworkElement> Clickables { get; } = new();
			public int Index { get; set; } = -1;
		}

		public bool ElementContains(FrameworkElement element, Point location, Dictionary<FrameworkElement, Vector>? scaleCache = null, Dictionary<FrameworkElement, Point>? transformCache = null)
		{
			if(!element.IsVisible || !element.IsLoaded || element.ActualWidth <= 0 || element.ActualHeight <= 0)
				return false;
			if(VisualTreeHelper.GetParent(element) is not FrameworkElement)
				return false;

			var scale = scaleCache == null ? Helper.GetTotalScaleTransform(element)
				: Helper.GetTotalScaleTransform(element, scaleCache);

			try
			{
				Point point;
				if(transformCache == null)
				{
					point = element.TransformToAncestor(CanvasInfo).Transform(new Point(0, 0));
				}
				else
				{
					if(!transformCache.TryGetValue(element, out point))
					{
						point = element.TransformToAncestor(CanvasInfo).Transform(new Point(0, 0));
						transformCache[element] = point;
					}
				}

				if(element is Polygon p)
				{
					var geometryLocalPoint = new Point((location.X - point.X) / scale.X, (location.Y - point.Y) / scale.Y);
					return p.RenderedGeometry.FillContains(geometryLocalPoint);
				}

				var contains= location.X > point.X && location.X < point.X + element.ActualWidth * scale.X && location.Y > point.Y
					   && location.Y < point.Y + element.ActualHeight * scale.Y;
				return contains;
			}
			catch(InvalidOperationException)
			{
				return false;
			}
		}
	}
}
