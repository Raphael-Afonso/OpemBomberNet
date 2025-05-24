using Microsoft.Extensions.Logging;
using OpenBomberNet.Application.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace OpenBomberNet.Server.Handlers;

public class LobbyMessageMessageHandler : IMessageHandler
{
    private readonly ILogger<LobbyMessageMessageHandler> _logger;
    private readonly ILobbyService _lobbyService;
    // Assuming IMessageSender might be needed to send error back directly
    private readonly OpenBomberNet.Infrastructure.Networking.IMessageSender _messageSender;

    public LobbyMessageMessageHandler(ILogger<LobbyMessageMessageHandler> logger, ILobbyService lobbyService, OpenBomberNet.Infrastructure.Networking.IMessageSender messageSender)
    {
        _logger = logger;
        _lobbyService = lobbyService;
        _messageSender = messageSender;
    }

    public async Task HandleAsync(string connectionId, string[] messageParts)
    {
        // Expected format: LOBBY_MESSAGE|message_text
        if (messageParts.Length < 2 || string.IsNullOrWhiteSpace(messageParts[1]))
        {
            _logger.LogWarning("Invalid LOBBY_MESSAGE format or empty message received from {ConnectionId}.", connectionId);
            // Optionally send an error back to the sender
            // TODO: Centralize error messages
            await _messageSender.SendMessageAsync(connectionId, "ERROR|Invalid LOBBY_MESSAGE format or empty message.");
            return;
        }

        // Combine remaining parts in case message contains the delimiter '|'
        string messageText = string.Join("|", messageParts.Skip(1));

        // The LobbyService handles finding the sender and broadcasting
        // LobbyService already logs the broadcast action internally.
        await _lobbyService.BroadcastMessageAsync(connectionId, messageText);
    }
}

