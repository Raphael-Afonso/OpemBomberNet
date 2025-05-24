using OpenBomberNet.Application.Interfaces;
using OpenBomberNet.Domain.Entities;
using OpenBomberNet.Domain.ValueObjects;
using OpenBomberNet.Infrastructure.Networking; // Assuming IMessageSender
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OpenBomberNet.Application.Services;

public class PlayerActionService : IPlayerActionService
{
    private readonly IGameService _gameService; // To get game/map state
    private readonly IMessageSender _messageSender; // To notify clients

    public PlayerActionService(IGameService gameService, IMessageSender messageSender)
    {
        _gameService = gameService;
        _messageSender = messageSender;
    }

    public async Task HandleMoveAsync(Guid gameId, Guid playerId, Direction direction)
    {
        var map = _gameService.GetActiveMap(gameId);
        if (map == null || !map.Players.TryGetValue(playerId, out var player) || !player.IsAlive)
        {
            // Game not found, player not found, or player is dead
            return;
        }

        var currentPos = player.Position;
        var targetPos = CalculateTargetPosition(currentPos, direction);

        // 1. Check Map Boundaries & Walkability
        if (!map.IsPositionWalkable(targetPos))
        {
            // Cannot move into wall or out of bounds
            // Optionally send a "move failed" message back to the specific player?
            return;
        }

        // 2. Check Collision with other players (optional - simple implementation allows overlap)
        // bool collision = map.Players.Values.Any(p => p.Id != playerId && p.IsAlive && p.Position == targetPos);
        // if (collision) return;

        // 3. Check Collision with Bombs (usually players can walk over bombs)
        // bool bombCollision = map.Bombs.ContainsKey(targetPos);
        // if (bombCollision) { /* Allow or disallow based on rules */ }

        // 4. Update Player Position
        player.Move(targetPos);
        Console.WriteLine($"Player {player.Nickname} moved to {targetPos}");

        // 5. Notify clients about the move
        // Format: MOVE_CONFIRM|PlayerId|NewX|NewY
        string moveMessage = $"MOVE_CONFIRM|{playerId}|{targetPos.X}|{targetPos.Y}";
        await SendToGamePlayersAsync(map, moveMessage);

        // 6. Check for Item Collection
        var item = map.GetItemAt(targetPos);
        if (item != null && !item.IsCollected)
        {
            item.Collect(); // Mark as collected
            player.ApplyPowerUp(item.Type);
            map.RemoveItem(targetPos); // Remove from map state
            Console.WriteLine($"Player {player.Nickname} collected item {item.Type} at {targetPos}");

            // Notify clients about item collection and player stat update
            // Format: ITEM_COLLECT|PlayerId|ItemType|NewMaxBombs|NewBombRadius|NewFuseMultiplier
            string itemCollectMessage = $"ITEM_COLLECT|{playerId}|{(int)item.Type}|{player.MaxBombs}|{player.BombRadius}|{player.BombFuseTimeMultiplier:F2}";
            await SendToGamePlayersAsync(map, itemCollectMessage);
        }
    }

    public async Task HandlePlaceBombAsync(Guid gameId, Guid playerId)
    {
        var map = _gameService.GetActiveMap(gameId);
        if (map == null || !map.Players.TryGetValue(playerId, out var player) || !player.IsAlive)
        {
            return; // Game/Player not found or player dead
        }

        var bombPosition = player.Position;

        // 1. Check Bomb Limit
        int currentBombsPlaced = map.Bombs.Count(kvp => kvp.Value.OwnerPlayerId == playerId && !kvp.Value.IsExploded);
        if (currentBombsPlaced >= player.MaxBombs)
        {
            Console.WriteLine($"Player {player.Nickname} cannot place more bombs (Limit: {player.MaxBombs})");
            return; // Reached bomb limit
        }

        // 2. Check if a bomb already exists at this position
        if (map.Bombs.ContainsKey(bombPosition))
        {
            Console.WriteLine($"Player {player.Nickname} cannot place bomb at {bombPosition}, already exists.");
            return; // Bomb already present
        }

        // 3. Create and Place Bomb
        // Use player's current stats for radius and fuse time
        // Base fuse time could be a constant, modified by player multiplier
        const float baseFuseTime = 3.0f;
        float actualFuseTime = baseFuseTime * player.BombFuseTimeMultiplier;
        var newBomb = new Bomb(bombPosition, playerId, player.BombRadius, actualFuseTime);

        if (map.Bombs.TryAdd(bombPosition, newBomb))
        {
            Console.WriteLine($"Player {player.Nickname} placed bomb at {bombPosition} (Radius: {newBomb.Radius}, Fuse: {newBomb.FuseTimeSeconds}s)");

            // 4. Notify clients about the bomb placement
            // Format: BOMB_PLACE|BombId|PosX|PosY|OwnerId|Radius|FuseTime
            string bombPlaceMessage = $"BOMB_PLACE|{newBomb.Id}|{bombPosition.X}|{bombPosition.Y}|{playerId}|{newBomb.Radius}|{newBomb.FuseTimeSeconds:F2}";
            await SendToGamePlayersAsync(map, bombPlaceMessage);
        }
        else
        {
            // Should not happen with ConcurrentDictionary if check passed, but log if it does
            Console.WriteLine($"Error: Failed to add bomb at {bombPosition} for player {player.Nickname}.");
        }
    }

    private Position CalculateTargetPosition(Position current, Direction direction)
    {
        return direction switch
        {
            Direction.Up => current with { Y = current.Y - 1 },
            Direction.Down => current with { Y = current.Y + 1 },
            Direction.Left => current with { X = current.X - 1 },
            Direction.Right => current with { X = current.X + 1 },
            _ => current // Should not happen
        };
    }

    // Helper to send messages to all players currently in a specific game
    // Duplicated from GameService - consider moving to a shared utility or using GameService for notifications
    private async Task SendToGamePlayersAsync(Map map, string message)
    {
        var tasks = new List<Task>();
        foreach (var player in map.Players.Values)
        {
            // We need the ConnectionId associated with the Player entity in the game map
            // This requires the GameService or LobbyService to populate ConnectionId when players join a game.
            if (!string.IsNullOrEmpty(player.ConnectionId))
            {
                tasks.Add(_messageSender.SendMessageAsync(player.ConnectionId, message));
            }
            else
            {
                // Log if connection ID is missing for an active player
                // Console.WriteLine($"Warning: Missing ConnectionId for Player {player.Id} in Game {map.Id}");
            }
        }
        await Task.WhenAll(tasks);
    }
}
