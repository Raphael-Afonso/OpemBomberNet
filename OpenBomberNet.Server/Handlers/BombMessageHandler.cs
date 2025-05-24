using OpenBomberNet.Application.Interfaces;
using OpenBomberNet.Infrastructure.Networking; // Assuming IMessageSender for potential errors
using System;
using System.Threading.Tasks;

namespace OpenBomberNet.Server.Handlers;

public class BombMessageHandler : IMessageHandler
{
    private readonly IPlayerActionService _playerActionService;
    private readonly IMessageSender _messageSender; // Optional: for sending errors back
    private readonly IConnectionManager _connectionManager; // To map connectionId to gameId/playerId

    public BombMessageHandler(IPlayerActionService playerActionService, IMessageSender messageSender, IConnectionManager connectionManager)
    {
        _playerActionService = playerActionService;
        _messageSender = messageSender;
        _connectionManager = connectionManager;
    }

    public async Task HandleAsync(string connectionId, string[] messageParts)
    {
        // Expected format: BOMB
        // No additional arguments needed for placing a bomb at the player's current location

        // Get player and game context from connection
        var playerContext = _connectionManager.GetPlayerContext(connectionId);
        if (playerContext == null || playerContext.GameId == Guid.Empty)
        {
            await _messageSender.SendMessageAsync(connectionId, "ERROR|You are not currently in a game.");
            return;
        }

        // Delegate the actual bomb placement logic to the application service
        await _playerActionService.HandlePlaceBombAsync(playerContext.GameId, playerContext.PlayerId);
        // Note: Success/failure/state updates are handled within HandlePlaceBombAsync via _messageSender
    }
}
