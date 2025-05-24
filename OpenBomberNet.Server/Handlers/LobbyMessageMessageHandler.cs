using OpenBomberNet.Application.Interfaces;
using OpenBomberNet.Infrastructure.Networking;
using System.Threading.Tasks;

namespace OpenBomberNet.Server.Handlers;

public class LobbyMessageMessageHandler : IMessageHandler
{
    private readonly ILobbyService _lobbyService;
    // No need for IMessageSender here as LobbyService handles broadcasting

    public LobbyMessageMessageHandler(ILobbyService lobbyService)
    {
        _lobbyService = lobbyService;
    }

    public async Task HandleAsync(string connectionId, string[] messageParts)
    {
        // Expected format: LOBBY_MESSAGE|message_text
        if (messageParts.Length < 2)
        {
            // Optionally send an error back to the sender?
            // Depends on whether IMessageSender is available/injected here
            Console.WriteLine($"Invalid LOBBY_MESSAGE format from {connectionId}");
            return;
        }

        // Combine remaining parts in case message contains the delimiter '|'
        string messageText = string.Join("|", messageParts.Skip(1));

        // The LobbyService handles finding the sender and broadcasting
        await _lobbyService.BroadcastMessageAsync(connectionId, messageText);
    }
}
