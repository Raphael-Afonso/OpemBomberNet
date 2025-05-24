using OpenBomberNet.Application.Interfaces;
using OpenBomberNet.Domain.Entities;
using OpenBomberNet.Domain.Interfaces; // Assuming IPlayerRepository
using OpenBomberNet.Domain.ValueObjects;
using OpenBomberNet.Infrastructure.Networking; // For potential notifications
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenBomberNet.Application.Services;

public class GameService : IGameService
{
    // Stores active game sessions (maps)
    private readonly ConcurrentDictionary<Guid, Map> _activeGames = new();
    private readonly IPlayerRepository _playerRepository; // To get player details if needed
    private readonly IMessageSender _messageSender; // To send game state updates

    // Inject dependencies
    public GameService(IPlayerRepository playerRepository, IMessageSender messageSender)
    {
        _playerRepository = playerRepository;
        _messageSender = messageSender;
    }

    public async Task<Guid> CreateGameAsync(IEnumerable<Guid> playerIds, int mapSize = 15)
    {
        // Create the map instance (currently only SquareMap)
        var map = new SquareMap(mapSize);
        var gameId = map.Id;

        // Assign starting positions and add players to the map
        // Basic starting position logic (needs improvement for more players)
        var startingPositions = new List<Position>
        {
            new Position(1, 1), // Top-left corner (adjusting for indestructible blocks)
            new Position(mapSize - 2, mapSize - 2), // Bottom-right
            new Position(1, mapSize - 2), // Bottom-left
            new Position(mapSize - 2, 1)  // Top-right
        };

        int posIndex = 0;
        foreach (var playerId in playerIds)
        {
            // In a real app, fetch full player data. Here, we might create temporary ones
            // or assume they exist in a repository.
            // For simplicity, let's assume we can get basic info or create a placeholder.
            // Player player = await _playerRepository.GetByIdAsync(playerId);
            // if (player == null) continue; // Skip if player not found

            // Placeholder: Create player entity directly for the game session
            // Nickname might come from lobby service or repository
            var player = new Player(playerId, $"Player_{playerId.ToString().Substring(0, 4)}", startingPositions[posIndex % startingPositions.Count]);

            map.AddPlayer(player);
            posIndex++;

            // TODO: Need player's ConnectionId to send initial state
            // This requires linking Lobby players to Game players more robustly.
        }

        if (_activeGames.TryAdd(gameId, map))
        {
            Console.WriteLine($"Game {gameId} created with {map.Players.Count} players on a {mapSize}x{mapSize} map.");
            // TODO: Notify players that the game has started and send initial state.
            return gameId;
        }
        else
        {
            // Handle error - game ID collision? (Highly unlikely with GUIDs)
            Console.WriteLine($"Error: Could not add game {gameId} to active games.");
            // Consider throwing an exception or returning Guid.Empty
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
            Console.WriteLine($"Player {playerId} removed from game {gameId}.");

            // TODO: Notify remaining players about the departure.

            // Optional: Check if game should end (e.g., last player remaining)
            if (map.Players.Count <= 1) // Or specific game rules
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
            Console.WriteLine($"Game {gameId} ended.");
            // TODO: Notify players about game end, results, etc.
            // Clean up resources associated with the game map if necessary.
        }
    }

    // --- Game Loop / Update Logic (Placeholder) ---
    // In a real server, you'd have a loop or timer updating game state
    // (e.g., bomb timers, explosion processing, player movement validation)

    public async Task ProcessGameUpdatesAsync(Guid gameId, TimeSpan deltaTime)
    {
        if (!_activeGames.TryGetValue(gameId, out var map)) return;

        var now = DateTime.UtcNow;
        List<Position> explodedBombPositions = new List<Position>();
        List<Bomb> bombsToRemove = new List<Bomb>();

        // 1. Check for bomb explosions
        foreach (var kvp in map.Bombs)
        {
            var bomb = kvp.Value;
            if (bomb.ShouldExplode(now))
            {
                bomb.Explode(); // Mark as exploded
                bombsToRemove.Add(bomb);
                explodedBombPositions.Add(bomb.Position);
                // TODO: Calculate explosion area and effects
                await ProcessExplosionAsync(map, bomb);
            }
        }

        // Remove exploded bombs from the map's active list
        foreach (var bomb in bombsToRemove)
        {
            map.RemoveBomb(bomb.Position);
        }

        // 2. Process other game logic (e.g., power-up timers, etc.)

        // 3. Send updates to clients (e.g., new positions, explosions, player deaths)
        // This part needs careful design based on the message protocol
    }

    private async Task ProcessExplosionAsync(Map map, Bomb bomb)
    {
        Console.WriteLine($"Bomb at {bomb.Position} exploded with radius {bomb.Radius}");
        var explosionTiles = CalculateExplosionTiles(map, bomb.Position, bomb.Radius);

        // Notify clients about the explosion visuals
        string explosionMessage = $"EXPLOSION|{bomb.Position.X},{bomb.Position.Y}|{bomb.Radius}"; // Simplified
        await SendToGamePlayersAsync(map, explosionMessage);

        // Check for entities hit by the explosion
        foreach (var tilePos in explosionTiles)
        {
            // Check for players
            foreach (var player in map.Players.Values)
            {
                if (player.IsAlive && player.Position == tilePos)
                {
                    player.Die();
                    Console.WriteLine($"Player {player.Nickname} hit by explosion at {tilePos}!");
                    // Notify player death
                    string deathMessage = $"PLAYER_DEATH|{player.Id}";
                    await SendToGamePlayersAsync(map, deathMessage);
                    // Potentially remove player or mark as spectator
                }
            }

            // Check for destructible blocks
            var block = map.GetBlockAt(tilePos);
            if (block is DestructibleBlock destructibleBlock)
            {
                Console.WriteLine($"Destructible block at {tilePos} hit by explosion.");
                map.DestroyBlockAt(tilePos); // This handles item drop internally
                // Notify block destruction and potential item spawn
                string blockDestroyMessage = $"BLOCK_DESTROY|{tilePos.X},{tilePos.Y}";
                await SendToGamePlayersAsync(map, blockDestroyMessage);

                var item = map.GetItemAt(tilePos);
                if (item != null)
                {
                    string itemSpawnMessage = $"ITEM_SPAWN|{tilePos.X},{tilePos.Y}|{(int)item.Type}";
                    await SendToGamePlayersAsync(map, itemSpawnMessage);
                }
            }

            // Check for other bombs (chain reaction)
            if (tilePos != bomb.Position && map.Bombs.TryGetValue(tilePos, out var otherBomb) && !otherBomb.IsExploded)
            {
                // Trigger other bomb immediately (or after a very short delay)
                otherBomb.Explode();
                // Add to removal list if not already there
                // Need careful handling to avoid infinite loops in edge cases
                Console.WriteLine($"Chain reaction triggered for bomb at {tilePos}");
                // Recursive call or add to a list for next tick processing
                // await ProcessExplosionAsync(map, otherBomb); // Be careful with recursion depth
            }
        }
    }

    private List<Position> CalculateExplosionTiles(Map map, Position center, int radius)
    {
        var tiles = new List<Position> { center };
        int mapWidth = map.Width;
        int mapHeight = map.Height;

        // Directions: Right, Left, Down, Up
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        for (int dir = 0; dir < 4; dir++)
        {
            for (int i = 1; i <= radius; i++)
            {
                int nx = center.X + dx[dir] * i;
                int ny = center.Y + dy[dir] * i;
                var pos = new Position(nx, ny);

                // Check bounds
                if (nx < 0 || nx >= mapWidth || ny < 0 || ny >= mapHeight) break;

                tiles.Add(pos);

                // Check if the explosion ray is blocked
                var block = map.GetBlockAt(pos);
                if (block != null && !block.IsWalkable) // Hit a wall (destructible or indestructible)
                {
                    // If it's destructible, the explosion stops *after* hitting it.
                    // If it's indestructible, the explosion stops *before* it (so don't add, or handle differently).
                    if (block is IndestructibleBlock) {
                        tiles.Remove(pos); // Explosion doesn't reach indestructible block tile itself
                    }
                    break; // Stop this direction's ray
                }
            }
        }
        return tiles;
    }

    // Helper to send messages to all players currently in a specific game
    private async Task SendToGamePlayersAsync(Map map, string message)
    {
        var tasks = new List<Task>();
        foreach (var player in map.Players.Values)
        {
            if (!string.IsNullOrEmpty(player.ConnectionId))
            {
                tasks.Add(_messageSender.SendMessageAsync(player.ConnectionId, message));
            }
        }
        await Task.WhenAll(tasks);
    }
}
