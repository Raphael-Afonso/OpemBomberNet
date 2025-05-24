using OpenBomberNet.Application.Interfaces;
using OpenBomberNet.Infrastructure.Networking; // Assuming IMessageSender for potential errors
using System;
using System.Threading.Tasks;

namespace OpenBomberNet.Server.Handlers;

public class MoveMessageHandler : IMessageHandler
{
    private readonly IPlayerActionService _playerActionService;
    private readonly IMessageSender _messageSender; // Optional: for sending errors back
    private readonly IConnectionManager _connectionManager; // To map connectionId to gameId/playerId

    public MoveMessageHandler(IPlayerActionService playerActionService, IMessageSender messageSender, IConnectionManager connectionManager)
    {
        _playerActionService = playerActionService;
        _messageSender = messageSender;
        _connectionManager = connectionManager;
    }

    public async Task HandleAsync(string connectionId, string[] messageParts)
    {
        // Expected format: MOVE|direction (e.g., MOVE|UP)
        if (messageParts.Length < 2)
        {
            await _messageSender.SendMessageAsync(connectionId, "ERROR|Invalid MOVE format. Use MOVE|UP/DOWN/LEFT/RIGHT");
            return;
        }

        // Get player and game context from connection
        var playerContext = _connectionManager.GetPlayerContext(connectionId);
        if (playerContext == null || playerContext.GameId == Guid.Empty)
        {
            await _messageSender.SendMessageAsync(connectionId, "ERROR|You are not currently in a game.");
            return;
        }

        if (!Enum.TryParse<Direction>(messageParts[1], true, out var direction))
        {
            await _messageSender.SendMessageAsync(connectionId, "ERROR|Invalid direction specified.");
            return;
        }

        // Delegate the actual move logic to the application service
        await _playerActionService.HandleMoveAsync(playerContext.GameId, playerContext.PlayerId, direction);
        // Note: Success/failure/state updates are handled within HandleMoveAsync via _messageSender
    }
}

// Placeholder for Connection Manager Interface (needs implementation)
public interface IConnectionManager
{
    void Associate(string connectionId, Guid playerId, Guid? gameId = null, string? token = null);
    void Disassociate(string connectionId);
    PlayerConnectionContext? GetPlayerContext(string connectionId);
    string? GetConnectionId(Guid playerId);
    // Add methods to update gameId when player joins a game
    void SetPlayerGame(string connectionId, Guid gameId);
}

// Placeholder for Player Context
public class PlayerConnectionContext
{
    public string ConnectionId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid GameId { get; set; } // Guid.Empty if not in a game
    public string? AuthToken { get; set; }

    public PlayerConnectionContext(string connectionId, Guid playerId, Guid? gameId = null, string? authToken = null)
    {
        ConnectionId = connectionId;
        PlayerId = playerId;
        GameId = gameId ?? Guid.Empty;
        AuthToken = authToken;
    }
}
