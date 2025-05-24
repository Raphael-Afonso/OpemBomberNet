namespace OpenBomberNet.Common;

/// <summary>
/// Defines constant strings for network protocol commands.
/// </summary>
public static class ProtocolCommands
{
    // Client to Server
    public const string Authenticate = "AUTH";
    public const string LobbyMessage = "LOBBY_MESSAGE";
    public const string Move = "MOVE"; // Followed by direction (e.g., MOVE|UP)
    public const string PlaceBomb = "BOMB";
    public const string RequestGameStart = "GAME_START_REQ"; // Example for initiating game
    public const string LeaveLobby = "LOBBY_LEAVE_REQ"; // Explicit leave request

    // Server to Client
    public const string AuthenticationSuccess = "AUTH_SUCCESS";
    public const string AuthenticationFailure = "AUTH_FAIL";
    public const string LobbyJoin = "LOBBY_JOIN";
    public const string LobbyLeave = "LOBBY_LEAVE";
    public const string LobbyState = "LOBBY_STATE";
    public const string LobbyMessageBroadcast = "LOBBY_MSG";
    public const string GameStartSuccess = "GAME_START_OK";
    public const string GameStartFailure = "GAME_START_FAIL";
    public const string GameStateUpdate = "GAME_STATE"; // Full or partial state
    public const string MoveConfirm = "MOVE_CONFIRM";
    public const string BombPlaced = "BOMB_PLACE";
    public const string Explosion = "EXPLOSION";
    public const string PlayerDeath = "PLAYER_DEATH";
    public const string BlockDestroy = "BLOCK_DESTROY";
    public const string ItemSpawn = "ITEM_SPAWN";
    public const string ItemCollect = "ITEM_COLLECT";
    public const string GameOver = "GAME_OVER";
    public const string Error = "ERROR"; // General error message
}

/// <summary>
/// Defines constant characters used as delimiters in the protocol.
/// </summary>
public static class ProtocolDelimiters
{
    public const char Primary = '|';
    public const char Secondary = ';';
    public const char Tertiary = ':';
    public const char Coordinates = ',';
}

// Add more classes for specific message formats or error codes if needed.

