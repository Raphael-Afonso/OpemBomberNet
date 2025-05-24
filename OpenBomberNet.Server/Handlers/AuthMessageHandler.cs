using OpenBomberNet.Application.Interfaces;
using OpenBomberNet.Infrastructure.Networking;
using System.Threading.Tasks;

namespace OpenBomberNet.Server.Handlers;

public class AuthMessageHandler : IMessageHandler
{
    private readonly IAuthenticationService _authService;
    private readonly ILobbyService _lobbyService; // To potentially link auth with lobby entry
    private readonly IMessageSender _messageSender;

    public AuthMessageHandler(IAuthenticationService authService, ILobbyService lobbyService, IMessageSender messageSender)
    {
        _authService = authService;
        _lobbyService = lobbyService;
        _messageSender = messageSender;
    }

    public async Task HandleAsync(string connectionId, string[] messageParts)
    {
        // Expected format: AUTH|nickname (or AUTH|token if re-authenticating)
        // For simplicity, let's assume initial auth uses nickname to get a token.
        if (messageParts.Length < 2)
        {
            await _messageSender.SendMessageAsync(connectionId, "ERROR|Invalid AUTH format. Use AUTH|nickname");
            return;
        }

        string nickname = messageParts[1];

        // In a real system, you might check credentials here.
        // For this example, we just generate a token based on a new player Guid.
        var playerId = Guid.NewGuid(); // Or retrieve existing based on nickname/credentials
        var token = _authService.GenerateToken(playerId, nickname);

        // Associate token/player info with the connection (needs a connection manager)
        // ConnectionManager.Associate(connectionId, playerId, token);
        Console.WriteLine($"Auth successful for {nickname} ({connectionId}). Token generated.");

        // Send token back to client
        // Format: AUTH_SUCCESS|playerId|token
        await _messageSender.SendMessageAsync(connectionId, $"AUTH_SUCCESS|{playerId}|{token}");

        // Automatically enter lobby after successful auth
        // Note: This tightly couples Auth and Lobby. Could be separate steps.
        bool enteredLobby = await _lobbyService.EnterLobbyAsync(connectionId, nickname);
        if (!enteredLobby)
        {
             await _messageSender.SendMessageAsync(connectionId, "ERROR|Failed to enter lobby after authentication.");
        }
    }
}
