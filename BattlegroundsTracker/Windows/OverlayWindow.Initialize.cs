using System.Windows.Controls.Primitives;

namespace Hearthstone_Deck_Tracker.Windows
{
	public partial class OverlayWindow
	{
		private bool _initializedCollections;
		private void InitializeCollections()
		{
			if(_initializedCollections)
				return;
			_initializedCollections = true;
			_leaderboardDeadForText.AddRange(new[]
			{
				BattlegroundsTileText0,
				BattlegroundsTileText1,
				BattlegroundsTileText2,
				BattlegroundsTileText3,
				BattlegroundsTileText4,
				BattlegroundsTileText5,
				BattlegroundsTileText6,
				BattlegroundsTileText7
			});
			_leaderboardDeadForTurnText.AddRange(new[]
			{
				BattlegroundsTurnText0,
				BattlegroundsTurnText1,
				BattlegroundsTurnText2,
				BattlegroundsTurnText3,
				BattlegroundsTurnText4,
				BattlegroundsTurnText5,
				BattlegroundsTurnText6,
				BattlegroundsTurnText7
			});

			// the session recap is the only panel that can be moved around while the UI is unlocked
			_movableElements.Add(BattlegroundsSessionStackPanel, new ResizeGrip());

			_clickableElements.Add(HeroNotificationPanel);
			_clickableElements.Add(TimewarpNotificationPanel);
			_clickableElements.Add(BobsBuddyDisplay);
			_clickableElements.Add(BattlegroundsSession.BtnOptions);
			_clickableElements.Add(BattlegroundsSession.BattlegroundsSessionPanelTopGroup);
			_clickableElements.Add(BattlegroundsSession.BgLatestGamesSectionHeader);
		}
	}
}
