using Microsoft.Extensions.Logging;
using OpenBomberNet.Application.Interfaces;
using OpenBomberNet.Common; // Added using for protocol constants
using OpenBomberNet.Infrastructure.Networking; // TODO: This namespace might change if IConnectionManager moves
using OpenBomberNet.Server.Handlers; // For IConnectionManager
using System;
using System.Threading.Tasks;

namespace OpenBomberNet.Server.Handlers;

public class AuthMessageHandler : IMessageHandler
{
    private readonly ILogger<AuthMessageHandler> _logger;
    private readonly IAuthenticationService _authService;
    private readonly ILobbyService _lobbyService;
    private readonly IMessageSender _messageSender;
    private readonly IConnectionManager _connectionManager;

    public AuthMessageHandler(
        ILogger<AuthMessageHandler> logger,
        IAuthenticationService authService,
        ILobbyService lobbyService,
        IMessageSender messageSender,
        IConnectionManager connectionManager)
    {
        _logger = logger;
        _authService = authService;
        _lobbyService = lobbyService;
        _messageSender = messageSender;
        _connectionManager = connectionManager;
    }

    public async Task HandleAsync(string connectionId, string[] messageParts)
    {
        // Expected format: AUTH|nickname
        if (messageParts.Length < 2)
        {
            _logger.LogWarning("Invalid AUTH format received from {ConnectionId}. Expected {Command}|nickname.", connectionId, ProtocolCommands.Authenticate);
            // TODO: Centralize error messages content as well
            await _messageSender.SendMessageAsync(connectionId, $"{ProtocolCommands.Error}{ProtocolDelimiters.Primary}Invalid {ProtocolCommands.Authenticate} format. Use {ProtocolCommands.Authenticate}|nickname");
            return;
        }

        string nickname = messageParts[1];
        if (string.IsNullOrWhiteSpace(nickname))
        {
             _logger.LogWarning("Empty nickname received in AUTH message from {ConnectionId}.", connectionId);
             await _messageSender.SendMessageAsync(connectionId, $"{ProtocolCommands.Error}{ProtocolDelimiters.Primary}Nickname cannot be empty.");
             return;
        }

        // TODO: Implement proper user/credential handling
        var playerId = Guid.NewGuid(); // Placeholder
        var token = _authService.GenerateToken(playerId, nickname);

        _connectionManager.Associate(connectionId, playerId, null, token);
        _logger.LogInformation("Authentication successful for {Nickname} ({PlayerId}) on connection {ConnectionId}. Token generated.", nickname, playerId, connectionId);

        // Send token back to client
        // Format: AUTH_SUCCESS|playerId|token
        await _messageSender.SendMessageAsync(connectionId, $"{ProtocolCommands.AuthenticationSuccess}{ProtocolDelimiters.Primary}{playerId}{ProtocolDelimiters.Primary}{token}");

        // Automatically enter lobby after successful auth
        bool enteredLobby = await _lobbyService.EnterLobbyAsync(connectionId, nickname);
        if (!enteredLobby)
        {
             _logger.LogError("Failed to automatically enter lobby for {Nickname} ({PlayerId}) after authentication on connection {ConnectionId}.", nickname, playerId, connectionId);
             await _messageSender.SendMessageAsync(connectionId, $"{ProtocolCommands.Error}{ProtocolDelimiters.Primary}Failed to enter lobby after authentication.");
        }
    }
}

