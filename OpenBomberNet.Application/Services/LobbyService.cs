using Microsoft.Extensions.Logging;
using OpenBomberNet.Application.DTOs;
using OpenBomberNet.Application.Interfaces;
using OpenBomberNet.Common; // Added using for protocol constants
using OpenBomberNet.Domain.Entities;
using OpenBomberNet.Domain.Interfaces;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenBomberNet.Application.Services;

public class LobbyService : ILobbyService
{
    private readonly ILogger<LobbyService> _logger;
    private readonly ConcurrentDictionary<string, Player> _lobbyPlayers = new();
    private readonly IMessageSender _messageSender;
    private readonly IAuthenticationService _authService;
    // private readonly IConnectionManager _connectionManager;

    public LobbyService(ILogger<LobbyService> logger, IMessageSender messageSender, IAuthenticationService authService)
    {
        _logger = logger;
        _messageSender = messageSender;
        _authService = authService;
    }

    public async Task<bool> EnterLobbyAsync(string connectionId, string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            _logger.LogWarning("Attempt to enter lobby with empty nickname from connection {ConnectionId}.", connectionId);
            await _messageSender.SendMessageAsync(connectionId, $"{ProtocolCommands.Error}{ProtocolDelimiters.Primary}Nickname cannot be empty.");
            return false;
        }
        if (_lobbyPlayers.ContainsKey(connectionId))
        {
             _logger.LogWarning("Connection {ConnectionId} attempted to enter lobby multiple times.", connectionId);
             await SendLobbyStateToPlayer(connectionId);
             return true;
        }

        // TODO: Get PlayerId from IConnectionManager associated during Auth step
        var playerId = Guid.NewGuid(); // TEMPORARY
        var player = new Player(playerId, nickname, new Domain.ValueObjects.Position(0, 0));
        player.ConnectionId = connectionId;

        if (_lobbyPlayers.TryAdd(connectionId, player))
        {
            _logger.LogInformation("Player {Nickname} ({PlayerId}) using connection {ConnectionId} entered lobby.", player.Nickname, player.Id, connectionId);

            var joinMessage = $"{ProtocolCommands.LobbyJoin}{ProtocolDelimiters.Primary}{player.Id}{ProtocolDelimiters.Primary}{player.Nickname}";
            await BroadcastToOthersAsync(connectionId, joinMessage);
            await SendLobbyStateToPlayer(connectionId);
            return true;
        }
        else
        {
            _logger.LogError("Failed to add player {Nickname} ({ConnectionId}) to lobby dictionary.", nickname, connectionId);
            return false;
        }
    }

    public async Task LeaveLobbyAsync(string connectionId)
    {
        if (_lobbyPlayers.TryRemove(connectionId, out Player? player))
        {
             _logger.LogInformation("Player {Nickname} ({PlayerId}) using connection {ConnectionId} left lobby.", player.Nickname, player.Id, connectionId);
            var leaveMessage = $"{ProtocolCommands.LobbyLeave}{ProtocolDelimiters.Primary}{player.Id}{ProtocolDelimiters.Primary}{player.Nickname}";
            await BroadcastToAllAsync(leaveMessage);
        }
        else
        {
            _logger.LogWarning("Attempted to remove non-existent connection {ConnectionId} from lobby.", connectionId);
        }
    }

    public async Task BroadcastMessageAsync(string senderConnectionId, string message)
    {
        if (_lobbyPlayers.TryGetValue(senderConnectionId, out Player? sender))
        {
            var chatMessage = $"{ProtocolCommands.LobbyMessageBroadcast}{ProtocolDelimiters.Primary}{sender.Id}{ProtocolDelimiters.Primary}{sender.Nickname}{ProtocolDelimiters.Primary}{message}";
            await BroadcastToOthersAsync(senderConnectionId, chatMessage);
            _logger.LogInformation("Lobby chat from {Nickname} ({PlayerId}): {Message}", sender.Nickname, sender.Id, message);
        }
        else
        {
            _logger.LogWarning("Chat message received from unknown connection {ConnectionId}", senderConnectionId);
        }
    }

    public Task<IEnumerable<PlayerLobbyDto>> GetPlayersInLobbyAsync()
    {
        var players = _lobbyPlayers.Values.Select(p => new PlayerLobbyDto(p.Id, p.Nickname)).ToList();
        return Task.FromResult<IEnumerable<PlayerLobbyDto>>(players);
    }

    private async Task SendLobbyStateToPlayer(string connectionId)
    {
         var playersInLobby = await GetPlayersInLobbyAsync();
         // Format: LOBBY_STATE|PlayerId1:Nickname1;PlayerId2:Nickname2;...
         var lobbyPayload = string.Join(ProtocolDelimiters.Secondary, playersInLobby.Select(p => $"{p.Id}{ProtocolDelimiters.Tertiary}{p.Nickname}"));
         var lobbyStateMessage = $"{ProtocolCommands.LobbyState}{ProtocolDelimiters.Primary}{lobbyPayload}";
         await _messageSender.SendMessageAsync(connectionId, lobbyStateMessage);
    }

    private async Task BroadcastToAllAsync(string message)
    {
        var tasks = _lobbyPlayers.Keys.Select(connId => _messageSender.SendMessageAsync(connId, message));
        await Task.WhenAll(tasks);
    }

    private async Task BroadcastToOthersAsync(string senderConnectionId, string message)
    {
        var tasks = _lobbyPlayers.Keys
            .Where(connId => connId != senderConnectionId)
            .Select(connId => _messageSender.SendMessageAsync(connId, message));
        await Task.WhenAll(tasks);
    }
}

