namespace Hearthstone_Deck_Tracker.API
{
	/// <summary>
	/// Minimal replacement for the plugin game events. No plugins are loaded in this fork; the lists
	/// are only kept so the game event handler call sites keep compiling.
	/// </summary>
	public class GameEvents
	{
		public static readonly ActionList<object?> OnEntityWillTakeDamage = new();
		public static readonly ActionList<object?> OnGameEnd = new();
		public static readonly ActionList<object?> OnGameLost = new();
		public static readonly ActionList<object?> OnGameStart = new();
		public static readonly ActionList<object?> OnGameTied = new();
		public static readonly ActionList<object?> OnGameWon = new();
		public static readonly ActionList<object?> OnInMenu = new();
		public static readonly ActionList<object?> OnModeChanged = new();
		public static readonly ActionList<object?> OnOpponentCreateInPlay = new();
		public static readonly ActionList<object?> OnOpponentDeckDiscard = new();
		public static readonly ActionList<object?> OnOpponentDeckToPlay = new();
		public static readonly ActionList<object?> OnOpponentDraw = new();
		public static readonly ActionList<object?> OnOpponentFatigue = new();
		public static readonly ActionList<object?> OnOpponentGet = new();
		public static readonly ActionList<object?> OnOpponentHandDiscard = new();
		public static readonly ActionList<object?> OnOpponentHandToDeck = new();
		public static readonly ActionList<object?> OnOpponentHeroPower = new();
		public static readonly ActionList<object?> OnOpponentJoustReveal = new();
		public static readonly ActionList<object?> OnOpponentMinionAttack = new();
		public static readonly ActionList<object?> OnOpponentMulligan = new();
		public static readonly ActionList<object?> OnOpponentPlay = new();
		public static readonly ActionList<object?> OnOpponentPlayToDeck = new();
		public static readonly ActionList<object?> OnOpponentPlayToGraveyard = new();
		public static readonly ActionList<object?> OnOpponentPlayToHand = new();
		public static readonly ActionList<object?> OnOpponentSecretTriggered = new();
		public static readonly ActionList<object?> OnPlayerCreateInDeck = new();
		public static readonly ActionList<object?> OnPlayerCreateInPlay = new();
		public static readonly ActionList<object?> OnPlayerDeckDiscard = new();
		public static readonly ActionList<object?> OnPlayerDeckToPlay = new();
		public static readonly ActionList<object?> OnPlayerDraw = new();
		public static readonly ActionList<object?> OnPlayerFatigue = new();
		public static readonly ActionList<object?> OnPlayerGet = new();
		public static readonly ActionList<object?> OnPlayerHandDiscard = new();
		public static readonly ActionList<object?> OnPlayerHeroPower = new();
		public static readonly ActionList<object?> OnPlayerJoustReveal = new();
		public static readonly ActionList<object?> OnPlayerMinionAttack = new();
		public static readonly ActionList<object?> OnPlayerMulligan = new();
		public static readonly ActionList<object?> OnPlayerPlay = new();
		public static readonly ActionList<object?> OnPlayerPlayToDeck = new();
		public static readonly ActionList<object?> OnPlayerPlayToGraveyard = new();
		public static readonly ActionList<object?> OnPlayerPlayToHand = new();
		public static readonly ActionList<object?> OnTurnStart = new();
		public static readonly ActionList<object?> OnPlayerSecretTriggered = new();
		public static readonly ActionList<object?> OnPlayerSecretPlayed = new();
		public static readonly ActionList<object?> OnOpponentSecretPlayed = new();
	}
}
