using Microsoft.Extensions.Logging;
using OpenBomberNet.Application.Interfaces; // Using the centralized interface
using OpenBomberNet.Domain.Entities;
using OpenBomberNet.Domain.Interfaces;
using OpenBomberNet.Domain.ValueObjects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenBomberNet.Application.Services;

public class GameService : IGameService
{
    private readonly ILogger<GameService> _logger;
    private readonly ConcurrentDictionary<Guid, Map> _activeGames = new();
    private readonly IPlayerRepository _playerRepository;
    private readonly IMessageSender _messageSender; // Using centralized IMessageSender

    public GameService(ILogger<GameService> logger, IPlayerRepository playerRepository, IMessageSender messageSender)
    {
        _logger = logger;
        _playerRepository = playerRepository;
        _messageSender = messageSender;
    }

    public async Task<Guid> CreateGameAsync(IEnumerable<Guid> playerIds, int mapSize = 15)
    {
        var map = new SquareMap(mapSize);
        var gameId = map.Id;

        var startingPositions = new List<Position>
        {
            new Position(1, 1),
            new Position(mapSize - 2, mapSize - 2),
            new Position(1, mapSize - 2),
            new Position(mapSize - 2, 1)
        };

        int posIndex = 0;
        var playersToAdd = new List<Player>();
        foreach (var playerId in playerIds)
        {
            // TODO: Fetch full player data from repository or context
            // Player? existingPlayer = await _playerRepository.GetByIdAsync(playerId);
            // if (existingPlayer == null) { ... continue; }

            // Placeholder
            var player = new Player(playerId, $"Player_{playerId.ToString().Substring(0, 4)}", startingPositions[posIndex % startingPositions.Count]);
            playersToAdd.Add(player);
            posIndex++;

            // TODO: Need player's ConnectionId. Fetch from IConnectionManager?
        }

        foreach(var player in playersToAdd)
        {
             map.AddPlayer(player);
        }

        if (_activeGames.TryAdd(gameId, map))
        {
            _logger.LogInformation("Game {GameId} created with {PlayerCount} players on a {MapSize}x{MapSize} map.", gameId, map.Players.Count, mapSize, mapSize);
            // TODO: Notify players game started, send initial state
            return gameId;
        }
        else
        {
            _logger.LogError("Error: Could not add game {GameId} to active games.", gameId);
            return Guid.Empty;
        }
    }

    public Task<Map?> GetMapStateAsync(Guid gameId)
    {
        _activeGames.TryGetValue(gameId, out var map);
        return Task.FromResult(map);
    }

    public Map? GetActiveMap(Guid gameId)
    {
        _activeGames.TryGetValue(gameId, out var map);
        return map;
    }

    public Task RemovePlayerFromGameAsync(Guid gameId, Guid playerId)
    {
        if (_activeGames.TryGetValue(gameId, out var map))
        {
            map.RemovePlayer(playerId);
            _logger.LogInformation("Player {PlayerId} removed from game {GameId}.", playerId, gameId);
            // TODO: Notify remaining players
            if (map.Players.Count <= 1)
            {
                EndGame(gameId);
            }
        }
        return Task.CompletedTask;
    }

    private void EndGame(Guid gameId)
    {
        if (_activeGames.TryRemove(gameId, out var map))
        {
            _logger.LogInformation("Game {GameId} ended.", gameId);
            // TODO: Notify players, clean up
        }
    }

    public async Task ProcessGameUpdatesAsync(Guid gameId, TimeSpan deltaTime)
    {
        if (!_activeGames.TryGetValue(gameId, out var map)) return;

        var now = DateTime.UtcNow;
        List<Bomb> bombsToRemove = new List<Bomb>();

        foreach (var kvp in map.Bombs.ToList())
        {
            var bomb = kvp.Value;
            if (bomb.ShouldExplode(now))
            {
                bomb.Explode();
                bombsToRemove.Add(bomb);
                await ProcessExplosionAsync(map, bomb);
            }
        }

        foreach (var bomb in bombsToRemove)
        {
            map.RemoveBomb(bomb.Position);
        }
        // Other game logic...
    }

    private async Task ProcessExplosionAsync(Map map, Bomb bomb)
    {
        _logger.LogDebug("Bomb at {Position} exploded with radius {Radius} in game {GameId}", bomb.Position, bomb.Radius, map.Id);
        var explosionTiles = CalculateExplosionTiles(map, bomb.Position, bomb.Radius);

        // TODO: Centralize message formats
        string explosionMessage = $"EXPLOSION|{bomb.Position.X},{bomb.Position.Y}|{bomb.Radius}";
        await SendToGamePlayersAsync(map, explosionMessage);

        foreach (var tilePos in explosionTiles)
        {
            foreach (var player in map.Players.Values.ToList())
            {
                if (player.IsAlive && player.Position == tilePos)
                {
                    player.Die();
                    _logger.LogInformation("Player {PlayerNickname} ({PlayerId}) hit by explosion at {Position} in game {GameId}!", player.Nickname, player.Id, tilePos, map.Id);
                    string deathMessage = $"PLAYER_DEATH|{player.Id}";
                    await SendToGamePlayersAsync(map, deathMessage);
                }
            }

            var block = map.GetBlockAt(tilePos);
            if (block is DestructibleBlock destructibleBlock)
            {
                _logger.LogDebug("Destructible block at {Position} hit by explosion in game {GameId}.", tilePos, map.Id);
                map.DestroyBlockAt(tilePos);
                string blockDestroyMessage = $"BLOCK_DESTROY|{tilePos.X},{tilePos.Y}";
                await SendToGamePlayersAsync(map, blockDestroyMessage);

                var item = map.GetItemAt(tilePos);
                if (item != null)
                {
                    string itemSpawnMessage = $"ITEM_SPAWN|{tilePos.X},{tilePos.Y}|{(int)item.Type}";
                    await SendToGamePlayersAsync(map, itemSpawnMessage);
                }
            }

            if (tilePos != bomb.Position && map.Bombs.TryGetValue(tilePos, out var otherBomb) && !otherBomb.IsExploded)
            {
                otherBomb.Explode();
                _logger.LogDebug("Chain reaction triggered for bomb at {Position} in game {GameId}", tilePos, map.Id);
            }
        }
    }

    private List<Position> CalculateExplosionTiles(Map map, Position center, int radius)
    {
        var tiles = new List<Position> { center };
        int mapWidth = map.Width;
        int mapHeight = map.Height;
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        for (int dir = 0; dir < 4; dir++)
        {
            for (int i = 1; i <= radius; i++)
            {
                int nx = center.X + dx[dir] * i;
                int ny = center.Y + dy[dir] * i;
                var pos = new Position(nx, ny);
                if (nx < 0 || nx >= mapWidth || ny < 0 || ny >= mapHeight) break;
                tiles.Add(pos);
                var block = map.GetBlockAt(pos);
                if (block != null && !block.IsWalkable)
                {
                    if (block is IndestructibleBlock) { tiles.Remove(pos); }
                    break;
                }
            }
        }
        return tiles;
    }

    private async Task SendToGamePlayersAsync(Map map, string message)
    {
        var tasks = new List<Task>();
        foreach (var player in map.Players.Values)
        {
            // TODO: Use IConnectionManager to get ConnectionId from PlayerId
             if (!string.IsNullOrEmpty(player.ConnectionId))
            {
                tasks.Add(_messageSender.SendMessageAsync(player.ConnectionId, message));
            }
            else
            {
                 _logger.LogWarning("Player {PlayerId} in Game {GameId} has no ConnectionId set.", player.Id, map.Id);
            }
        }
        await Task.WhenAll(tasks);
    }
}

