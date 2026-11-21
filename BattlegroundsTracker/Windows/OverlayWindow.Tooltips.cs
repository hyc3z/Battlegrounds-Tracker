#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using HearthDb.Enums;
using HearthMirror.Objects;
using Hearthstone_Deck_Tracker.Controls.Tooltips;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Utility.MVVM;
using static System.Windows.Visibility;
using Card = Hearthstone_Deck_Tracker.Hearthstone.Card;

#endregion

namespace Hearthstone_Deck_Tracker.Windows;

public partial class OverlayWindow
{
	private readonly Dictionary<DependencyObject, FrameworkElement> _activeTooltips = new();
	private readonly HashSet<DependencyObject> _delayedTooltips = new();
	private async void SetTooltip(Func<FrameworkElement>? getTooltip, DependencyObject target)
	{
		if(_activeTooltips.TryGetValue(target, out var current))
		{
			_activeTooltips.Remove(target);
			OverlayTooltip.Children.Remove(current);
		}

		if(getTooltip == null)
		{
			_delayedTooltips.Remove(target);
			return;
		}

		if(!ToolTipService.GetIsEnabled(target))
			return;

		var delay = ToolTipService.GetInitialShowDelay(target);
		if(delay > 0 && delay != 1000) // 1000 is the default
		{
			_delayedTooltips.Add(target);
			await Task.Delay(delay);
			if(!_delayedTooltips.Remove(target))
			{
				// Already removed. This means SetTooltip was called again with null for the same element.
				// We no longer want to show it.
				return;
			}
		}

		if(OverlayTooltip.Children.Count > 0)
		{
			// We already have a tooltip. For now, we don't support more than one.
			// We could potentially decide which one to use based on z-level?
			return;
		}

		var feTarget = target as FrameworkElement ?? Helper.GetLogicalParent<FrameworkElement>(target);
		if(feTarget == null)
			return;

		Point targetPos;
		try
		{
			targetPos = feTarget.TransformToAncestor(this).Transform(new Point(0, 0));
		}
		catch(InvalidOperationException)
		{
			// Element was probably unloaded
			return;
		}

		var tooltip = getTooltip();

		// The content of OverlayExtensions.ToolTip is not part of the visual tree (OverlayExtensions.ToolTip is
		// a property, not a framework element), and does therefore not have a DataContext.For bindings to work
		// correctly we need to set it here to match the target.
		tooltip.DataContext = target is FrameworkContentElement fce ? fce.DataContext : feTarget.DataContext;

		try
		{
			OverlayTooltip.Children.Add(tooltip);
		}
		catch(InvalidOperationException e)
		{
			// Likely happens if the element is already a child of another element.
			// Maybe SetTooltip got called twice?
			Log.Error(e);
			return;
		}
		_activeTooltips[target] = tooltip;


		// Normalize placement to a direction. We don't support the other, more advanced, placement methods.
		var placement = ToolTipService.GetPlacement(target) switch
		{
			PlacementMode.Top => PlacementMode.Top,
			PlacementMode.Bottom => PlacementMode.Bottom,
			PlacementMode.Left => PlacementMode.Left,
			_ => PlacementMode.Right,
		};

		var placementAwareTooltip = tooltip as IPlacementAware;
		// Set placement before calculating layout
		placementAwareTooltip?.SetPlacement(placement);

		// Force a layout update so that ActualWidth and ActualHeight are seg
		tooltip.UpdateLayout();

		var targetScale = Helper.GetTotalScaleTransform(feTarget);
		var targetWidth = targetScale.X * feTarget.ActualWidth;
		var targetHeight = targetScale.Y * feTarget.ActualHeight;

		if(OverlayExtensions.GetAutoScaleToolTip(feTarget))
			tooltip.LayoutTransform = new ScaleTransform(targetScale.X, targetScale.Y);

		var tooltipScale = Helper.GetTotalScaleTransform(tooltip);
		var tooltipWidth = tooltipScale.X * tooltip.ActualWidth;
		var tooltipHeight = tooltipScale.Y * tooltip.ActualHeight;

		// Correct placement if tooltip would go outside of window, and it fit on the other side
		switch (placement)
		{
			case PlacementMode.Top:
				if(targetPos.Y - tooltipHeight < 0 && targetPos.Y + targetHeight + tooltipHeight <= ActualHeight)
					placement = PlacementMode.Bottom;
				break;
			case PlacementMode.Bottom:
				if(targetPos.Y + targetHeight + tooltipHeight > ActualHeight && targetPos.Y - tooltipHeight >= 0)
					placement = PlacementMode.Top;
				break;
			case PlacementMode.Left:
				if(targetPos.X - tooltipWidth < 0 && targetPos.X + targetWidth + tooltipWidth <= ActualWidth)
					placement = PlacementMode.Right;
				break;
			case PlacementMode.Right:
				if(targetPos.X + targetWidth + tooltipWidth > ActualWidth && targetPos.X - tooltipWidth >= 0)
					placement = PlacementMode.Left;
				break;
		}

		// Update placement, since it may have swapped sides. We don't recalculate the size/placement again.
		// Hopefully the layout of the tooltip should not affect the size when swapping left/right.
		placementAwareTooltip?.SetPlacement(placement);

		var offsetX = ToolTipService.GetHorizontalOffset(target);
		var offsetY = ToolTipService.GetVerticalOffset(target);

		var (left, top) = placement switch
		{
			PlacementMode.Top => (targetPos.X + targetWidth / 2 - tooltipWidth / 2 + offsetX, targetPos.Y - tooltipHeight - offsetY),
			PlacementMode.Bottom => (targetPos.X + targetWidth / 2 - tooltipWidth / 2 + offsetX, targetPos.Y + targetHeight + offsetY),
			PlacementMode.Left => (targetPos.X - tooltipWidth - offsetX, targetPos.Y + targetHeight / 2 - tooltipHeight / 2 + offsetY),
			PlacementMode.Right => (targetPos.X + targetWidth + offsetX, targetPos.Y + targetHeight / 2 - tooltipHeight / 2 + offsetY),
		};

		var alignment = OverlayExtensions.GetToolTipAlignment(feTarget);

		switch(alignment)
		{
			case AlignmentMode.Start:
				if(placement is PlacementMode.Top or PlacementMode.Bottom)
					left = targetPos.X + offsetX;
				else
					top = targetPos.Y + offsetY;
				break;
			case AlignmentMode.End:
				if(placement is PlacementMode.Top or PlacementMode.Bottom)
					left = targetPos.X + targetWidth - tooltipWidth + offsetX;
				else
					top = targetPos.Y + targetHeight - tooltipHeight + offsetY;
				break;
		}

		var actualLeft = Math.Max(0, Math.Min(left, ActualWidth - tooltipWidth));
		var actualTop = Math.Max(0, Math.Min(top, ActualHeight - tooltipHeight));

		if(tooltip is IScreenBoundaryAware boundaryAware)
			boundaryAware.SetScreenBoundaryOffset(left - actualLeft, top - actualTop);

		Canvas.SetLeft(OverlayTooltip, actualLeft);
		Canvas.SetTop(OverlayTooltip, actualTop);

		if(target is UIElement uiElement)
			uiElement.RaiseEvent(new RoutedEventArgs(OverlayExtensions.TooltipLoadedEvent));
		else if (target is ContentElement contentElement)
			contentElement.RaiseEvent(new RoutedEventArgs(OverlayExtensions.TooltipLoadedEvent));
	}

	public void SetHeroGuidesTrigger(int zoneSize, int zonePosition, bool tooltipOnRight, string[] cards, bool buddiesEnabled = false)
	{
		var vm = (CardGridTooltipViewModel)GuidesTooltipTrigger.DataContext;
		vm.Reset();

		if(zoneSize != 4 || cards.Length == 0)
			return;

		vm.Cards = cards.Select(Database.GetCardFromId).WhereNotNull().ToList();

		if(vm.Cards == null || vm.Cards.Count == 0)
			return;

		GuidesTooltipTrigger.UpdateLayout();
		UpdateHoverable();

		var bgHeroPickHeroWidth = 0.165;
		var bgHeroPickHeroXSpacing = 0.075;
		var totalWidth = zoneSize * bgHeroPickHeroWidth + (zoneSize - 1) * bgHeroPickHeroXSpacing;
		var leftEdge = 0.5 - totalWidth / 2;

		var zoneIndex = Math.Max(zonePosition - 1, 0);
		var heroX =  leftEdge + zoneIndex * (bgHeroPickHeroWidth + bgHeroPickHeroXSpacing - 0.005) - (tooltipOnRight ?  0 : 0.165);

		vm.Scale = Height / 1080;
		vm.Left = Helper.GetScaledXPos(heroX, (int)Width, ScreenRatio);
		vm.Top = Height * 0.21;
		vm.Height = Height * 0.40;
		vm.Width = Height * 0.47;
		vm.TooltipPlacement = buddiesEnabled
			? (tooltipOnRight ? PlacementMode.Left : PlacementMode.Right)
			: (tooltipOnRight ? PlacementMode.Right : PlacementMode.Left);
	}

	public void SetAnomalyGuidesTrigger(string cardId)
	{
		var vm = (CardGridTooltipViewModel)GuidesTooltipTrigger.DataContext;

		vm.Reset();

		if(cardId == "")
			return;

		var card = Database.GetCardFromId(cardId);

		if(card == null)
			return;

		if(card.TypeEnum != CardType.BATTLEGROUND_ANOMALY)
			return;

		vm.Cards = new List<Card> { card };

		GuidesTooltipTrigger.UpdateLayout();
		UpdateHoverable();

		vm.Scale = Height / 1080;
		vm.Left = Helper.GetScaledXPos(0.90, (int)Width, ScreenRatio);
		vm.Top = Height * 0.33;
		vm.Height = Height * 0.1;
		vm.Width = Height * 0.13;
		vm.TooltipHorizontalOffset = -340 * vm.Scale;
		vm.TooltipVerticalOffset = 100 * vm.Scale;
		vm.TooltipPlacement = PlacementMode.Bottom;
	}

	public void SetTrinketGuidesTrigger(int zoneSize, int zonePosition, string cardId)
	{
		var vm = (CardGridTooltipViewModel)DiscoveryGuidesTooltipTrigger.DataContext;

		vm.Reset();

		if(cardId == "")
			return;

		var card = Database.GetCardFromId(cardId);

		if(card == null)
			return;

		if(card.TypeEnum != CardType.BATTLEGROUND_TRINKET)
			return;

		vm.Cards = new List<Card> { card };

		DiscoveryGuidesTooltipTrigger.UpdateLayout();
		UpdateHoverable();

		var bgTrinketPickWidth = 0.192;
		var leftEdge = 0.122;

		var trinketX = leftEdge + zonePosition * (bgTrinketPickWidth);

		vm.Scale = Height / 1080;
		vm.Left = Helper.GetScaledXPos(trinketX, (int)Width, ScreenRatio);
		vm.Top = Height * 0.28;
		vm.Height = Height * 0.35;
		vm.Width = Height * 0.25;
		vm.TooltipPlacement = PlacementMode.Bottom;
		vm.TooltipVerticalOffset = 10 * vm.Scale;
	}

	public void SetQuestGuidesTrigger(DiscoverState state)
	{
		var vm = (CardGridTooltipViewModel)DiscoveryGuidesTooltipTrigger.DataContext;

		if(state.EntityId is null or 0)
			return;

		if(!Core.Game.Entities.TryGetValue((int)state.EntityId, out var entity))
			return;

		var questRewardCardId = entity.GetTag(GameTag.QUEST_REWARD_DATABASE_ID);
		var questRewardCard = Database.GetCardFromDbfId(questRewardCardId, false);

		if(questRewardCard == null)
			return;

		if(questRewardCard.TypeEnum != CardType.BATTLEGROUND_QUEST_REWARD)
			return;

		vm.Reset();
		vm.Cards = new List<Card> { questRewardCard };

		DiscoveryGuidesTooltipTrigger.UpdateLayout();
		UpdateHoverable();

		var bgQuestPickWidth = 0.270;
		var leftEdge = 0.117;

		var questX = leftEdge + state.ZonePosition * bgQuestPickWidth;

		var rewardCardId = entity.GetTag(GameTag.BACON_CARD_DBID_REWARD);
		var rewardCard = Database.GetCardFromDbfId(rewardCardId, false);
		var isRewardCardPresent = rewardCard != null;
		var isGameTooltipRight = state.ZonePosition == 0;

		if(isRewardCardPresent)
			isGameTooltipRight = state.ZonePosition + 1 == state.ZoneSize;

		vm.Scale = Height / 1080;
		vm.Left = Helper.GetScaledXPos(questX, (int)Width, ScreenRatio);
		vm.Top = Height * 0.22;
		vm.Height = Height * 0.55;
		vm.Width = Height * 0.30;
		vm.TooltipPlacement = isGameTooltipRight ? PlacementMode.Right : PlacementMode.Left;
		vm.TooltipVerticalOffset = 32 * vm.Scale;
		vm.TooltipAlignment = AlignmentMode.Start;
	}


	public void ResetAnomalyGuidesMulliganTrigger()
	{
		var vm = (CardGridTooltipViewModel)AnomalyGuidesMulliganTrigger.DataContext;
		vm.Reset();
	}

	public void SetAnomalyGuidesMulliganTrigger()
	{
		var vm = (CardGridTooltipViewModel)AnomalyGuidesMulliganTrigger.DataContext;

		vm.Reset();

		var anomalyDbfId = BattlegroundsUtils.GetBattlegroundsAnomalyDbfId(_game.GameEntity);
		var anomalyCard = anomalyDbfId.HasValue ? Database.GetCardFromDbfId(anomalyDbfId.Value, false) : null;

		if(anomalyCard is not { TypeEnum: CardType.BATTLEGROUND_ANOMALY })
			return;

		vm.Cards = new List<Card> { anomalyCard };

		GuidesTooltipTrigger.UpdateLayout();
		UpdateHoverable();

		vm.Scale = Height / 1080;
		vm.Left = Helper.GetScaledXPos(0.635, (int)Width, ScreenRatio);
		vm.Top = Height * 0.0825;
		vm.Height = Height * 0.123;
		vm.Width = Height * 0.1188;
		vm.TooltipHorizontalOffset = -600 * vm.Scale;
		vm.TooltipVerticalOffset = 20 * vm.Scale;
		vm.TooltipPlacement = PlacementMode.Bottom;
	}

	// This is only needed for anomaly during mulligan because there is no way to retrieve the "hover action" by memory reading it.
	private void AnomalyGuidesMulliganTrigger_OnMouseEnter(object sender, MouseEventArgs e)
	{
		var anomalyDbfId = BattlegroundsUtils.GetBattlegroundsAnomalyDbfId(_game.GameEntity);
		var anomalyCardId = anomalyDbfId.HasValue ? Database.GetCardFromDbfId(anomalyDbfId.Value, false) : null;

		SetMulliganAnomalyMask(anomalyCardId);
	}
	private void AnomalyGuidesMulliganTrigger_OnMouseLeave(object sender, MouseEventArgs e)
	{
		SetMulliganAnomalyMask(null);
	}
}

public class CardGridTooltipViewModel : ViewModel
{
	public double Width
	{
		get => GetProp(0.0);
		set => SetProp(value);
	}

	public double Height
	{
		get => GetProp(0.0);
		set => SetProp(value);
	}

	public double Top
	{
		get => GetProp(0.0);
		set => SetProp(value);
	}

	public double Left
	{
		get => GetProp(0.0);
		set => SetProp(value);
	}

	public double Scale
	{
		get => GetProp(1.0);
		set => SetProp(value);
	}

	public PlacementMode TooltipPlacement
	{
		get => GetProp(PlacementMode.Top);
		set => SetProp(value);
	}

	public double TooltipHorizontalOffset
	{
		get => GetProp(0.0);
		set => SetProp(value);
	}

	public double TooltipVerticalOffset
	{
		get => GetProp(0.0);
		set => SetProp(value);
	}

	public AlignmentMode TooltipAlignment
	{
		get => GetProp(AlignmentMode.Center);
		set => SetProp(value);
	}

	public List<Card>? Cards
	{
		get => GetProp<List<Card>?>(null);
		set => SetProp(value);
	}

	public void Reset()
	{
		Width = 0;
		Height = 0;
		Top = -1;
		Left = -1;
		TooltipHorizontalOffset = 0;
		TooltipVerticalOffset = 0;
		Cards = null;
		TooltipAlignment = AlignmentMode.Center;
		TooltipPlacement = PlacementMode.Top;
	}
}
