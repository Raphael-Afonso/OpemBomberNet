using OpenBomberNet.Application.DTOs;
using OpenBomberNet.Application.Interfaces;
using OpenBomberNet.Domain.Entities;
using OpenBomberNet.Domain.Interfaces; // Assuming an IPlayerRepository or similar exists
using OpenBomberNet.Infrastructure.Networking; // Placeholder for message sending interface
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace OpenBomberNet.Application.Services;

public class LobbyService : ILobbyService
{
    // Using ConcurrentDictionary for thread-safe access to lobby players
    // Key: ConnectionId, Value: Player object (or a simpler DTO if preferred)
    private readonly ConcurrentDictionary<string, Player> _lobbyPlayers = new();
    private readonly IMessageSender _messageSender; // Interface to send messages back to clients
    private readonly IAuthenticationService _authService; // To potentially link auth

    // Inject dependencies (MessageSender, Auth Service, potentially Player Repository)
    public LobbyService(IMessageSender messageSender, IAuthenticationService authService)
    {
        _messageSender = messageSender;
        _authService = authService; // Store auth service if needed later
    }

    public async Task<bool> EnterLobbyAsync(string connectionId, string nickname)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(nickname) || _lobbyPlayers.ContainsKey(connectionId))
        {
            // Maybe send an error message back?
            return false;
        }

        // Create a new player entity for the lobby session
        // In a real scenario, this might link to an authenticated user account
        var player = new Player(Guid.NewGuid(), nickname, new Domain.ValueObjects.Position(0, 0)); // Initial position doesn't matter much in lobby
        player.ConnectionId = connectionId;

        if (_lobbyPlayers.TryAdd(connectionId, player))
        {
            // Notify all other players that a new player joined
            var joinMessage = $"LOBBY_JOIN|{player.Id}|{player.Nickname}"; // Example message format
            await BroadcastToOthersAsync(connectionId, joinMessage);

            // Send current lobby state to the new player
            var playersInLobby = await GetPlayersInLobbyAsync();
            var lobbyStateMessage = "LOBBY_STATE|" + string.Join(";", playersInLobby.Select(p => $"{p.Id}:{p.Nickname}"));
            await _messageSender.SendMessageAsync(connectionId, lobbyStateMessage);

            Console.WriteLine($"Player {nickname} ({connectionId}) entered lobby.");
            return true;
        }

        return false;
    }

    public async Task LeaveLobbyAsync(string connectionId)
    {
        if (_lobbyPlayers.TryRemove(connectionId, out Player? player))
        {
            // Notify all remaining players that this player left
            var leaveMessage = $"LOBBY_LEAVE|{player.Id}|{player.Nickname}";
            await BroadcastToAllAsync(leaveMessage);
            Console.WriteLine($"Player {player.Nickname} ({connectionId}) left lobby.");
        }
    }

    public async Task BroadcastMessageAsync(string senderConnectionId, string message)
    {
        if (_lobbyPlayers.TryGetValue(senderConnectionId, out Player? sender))
        {
            // Format the chat message (e.g., LOBBY_MSG|PlayerId|Nickname|MessageText)
            var chatMessage = $"LOBBY_MSG|{sender.Id}|{sender.Nickname}|{message}";
            await BroadcastToOthersAsync(senderConnectionId, chatMessage);
            Console.WriteLine($"Lobby chat from {sender.Nickname}: {message}");
        }
        else
        {
            // Handle case where sender is not found (should not happen ideally)
            Console.WriteLine($"Warning: Chat message received from unknown connection {senderConnectionId}");
        }
    }

    public Task<IEnumerable<PlayerLobbyDto>> GetPlayersInLobbyAsync()
    {
        var players = _lobbyPlayers.Values.Select(p => new PlayerLobbyDto(p.Id, p.Nickname)).ToList();
        return Task.FromResult<IEnumerable<PlayerLobbyDto>>(players);
    }

    // Helper to broadcast to all players in the lobby
    private async Task BroadcastToAllAsync(string message)
    {
        var tasks = _lobbyPlayers.Keys.Select(connId => _messageSender.SendMessageAsync(connId, message));
        await Task.WhenAll(tasks);
    }

    // Helper to broadcast to all players EXCEPT the sender
    private async Task BroadcastToOthersAsync(string senderConnectionId, string message)
    {
        var tasks = _lobbyPlayers.Keys
            .Where(connId => connId != senderConnectionId)
            .Select(connId => _messageSender.SendMessageAsync(connId, message));
        await Task.WhenAll(tasks);
    }
}

// Define a placeholder interface for sending messages (to be implemented in Infrastructure)
// This decouples the Application layer from the specific networking implementation.
namespace OpenBomberNet.Infrastructure.Networking
{
    public interface IMessageSender
    {
        Task SendMessageAsync(string connectionId, string message);
        Task SendMessageToAllAsync(string message);
        Task SendMessageToAllExceptAsync(string excludedConnectionId, string message);
    }
}
