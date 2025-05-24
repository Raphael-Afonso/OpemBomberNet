using Microsoft.Extensions.Logging;
using OpenBomberNet.Application.Interfaces; // Using the centralized interface
using OpenBomberNet.Domain.Entities;
using OpenBomberNet.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenBomberNet.Application.Services;

public class PlayerActionService : IPlayerActionService
{
    private readonly ILogger<PlayerActionService> _logger;
    private readonly IGameService _gameService;
    private readonly IMessageSender _messageSender; // Using centralized IMessageSender
    // private readonly IConnectionManager _connectionManager; // Dependency needed for PlayerId mapping

    public PlayerActionService(ILogger<PlayerActionService> logger, IGameService gameService, IMessageSender messageSender)
    {
        _logger = logger;
        _gameService = gameService;
        _messageSender = messageSender;
    }

    public async Task HandleMoveAsync(Guid gameId, Guid playerId, Direction direction)
    {
        var map = _gameService.GetActiveMap(gameId);
        if (map == null)
        {
            _logger.LogWarning("HandleMoveAsync failed: Game {GameId} not found.", gameId);
            return;
        }
        if (!map.Players.TryGetValue(playerId, out var player))
        {
             _logger.LogWarning("HandleMoveAsync failed: Player {PlayerId} not found in game {GameId}.", playerId, gameId);
            return;
        }
        if (!player.IsAlive)
        {
            _logger.LogDebug("HandleMoveAsync ignored: Player {PlayerId} in game {GameId} is dead.", playerId, gameId);
            return;
        }

        var currentPos = player.Position;
        var targetPos = CalculateTargetPosition(currentPos, direction);

        if (!map.IsPositionWalkable(targetPos))
        {
             _logger.LogDebug("Player {PlayerId} move to {TargetPosition} blocked by terrain/bounds in game {GameId}.", playerId, targetPos, gameId);
            return;
        }

        player.Move(targetPos);
        _logger.LogDebug("Player {PlayerNickname} ({PlayerId}) moved to {TargetPosition} in game {GameId}", player.Nickname, playerId, targetPos, gameId);

        // TODO: Centralize message formats
        string moveMessage = $"MOVE_CONFIRM|{playerId}|{targetPos.X}|{targetPos.Y}";
        await SendToGamePlayersAsync(map, moveMessage);

        var item = map.GetItemAt(targetPos);
        if (item != null && !item.IsCollected)
        {
            item.Collect();
            player.ApplyPowerUp(item.Type);
            map.RemoveItem(targetPos);
            _logger.LogInformation("Player {PlayerNickname} ({PlayerId}) collected item {ItemType} at {Position} in game {GameId}", player.Nickname, playerId, item.Type, targetPos, gameId);

            string itemCollectMessage = $"ITEM_COLLECT|{playerId}|{(int)item.Type}|{player.MaxBombs}|{player.BombRadius}|{player.BombFuseTimeMultiplier:F2}";
            await SendToGamePlayersAsync(map, itemCollectMessage);
        }
    }

    public async Task HandlePlaceBombAsync(Guid gameId, Guid playerId)
    {
        var map = _gameService.GetActiveMap(gameId);
         if (map == null)
        {
            _logger.LogWarning("HandlePlaceBombAsync failed: Game {GameId} not found.", gameId);
            return;
        }
        if (!map.Players.TryGetValue(playerId, out var player))
        {
             _logger.LogWarning("HandlePlaceBombAsync failed: Player {PlayerId} not found in game {GameId}.", playerId, gameId);
            return;
        }
        if (!player.IsAlive)
        {
             _logger.LogDebug("HandlePlaceBombAsync ignored: Player {PlayerId} in game {GameId} is dead.", playerId, gameId);
            return;
        }

        var bombPosition = player.Position;

        int currentBombsPlaced = map.Bombs.Count(kvp => kvp.Value.OwnerPlayerId == playerId && !kvp.Value.IsExploded);
        if (currentBombsPlaced >= player.MaxBombs)
        {
            _logger.LogDebug("Player {PlayerNickname} ({PlayerId}) cannot place more bombs (Limit: {BombLimit}) in game {GameId}", player.Nickname, playerId, player.MaxBombs, gameId);
            return;
        }

        if (map.Bombs.ContainsKey(bombPosition))
        {
            _logger.LogDebug("Player {PlayerNickname} ({PlayerId}) cannot place bomb at {Position}, already exists in game {GameId}.", player.Nickname, playerId, bombPosition, gameId);
            return;
        }

        const float baseFuseTime = 3.0f;
        float actualFuseTime = baseFuseTime * player.BombFuseTimeMultiplier;
        var newBomb = new Bomb(bombPosition, playerId, player.BombRadius, actualFuseTime);

        if (map.Bombs.TryAdd(bombPosition, newBomb))
        {
            _logger.LogInformation("Player {PlayerNickname} ({PlayerId}) placed bomb at {Position} (Radius: {Radius}, Fuse: {FuseTime}s) in game {GameId}", player.Nickname, playerId, bombPosition, newBomb.Radius, newBomb.FuseTimeSeconds, gameId);

            // TODO: Centralize message formats
            string bombPlaceMessage = $"BOMB_PLACE|{newBomb.Id}|{bombPosition.X}|{bombPosition.Y}|{playerId}|{newBomb.Radius}|{newBomb.FuseTimeSeconds:F2}";
            await SendToGamePlayersAsync(map, bombPlaceMessage);
        }
        else
        {
            _logger.LogError("Error: Failed to add bomb at {Position} for player {PlayerNickname} ({PlayerId}) in game {GameId}.", bombPosition, player.Nickname, playerId, gameId);
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
            _ => current
        };
    }

    // Helper to send messages to all players currently in a specific game
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

