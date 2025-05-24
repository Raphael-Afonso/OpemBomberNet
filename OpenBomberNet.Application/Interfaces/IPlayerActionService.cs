using OpenBomberNet.Domain.Entities;
using OpenBomberNet.Domain.ValueObjects;
using System.Threading.Tasks;

namespace OpenBomberNet.Application.Interfaces;

// Interface para lidar com ações específicas do jogador dentro do jogo
public interface IPlayerActionService
{
    Task HandleMoveAsync(Guid gameId, Guid playerId, Direction direction);
    Task HandlePlaceBombAsync(Guid gameId, Guid playerId);
    // Potencialmente outras ações como usar um item especial, etc.
}

// Enum para direções de movimento
public enum Direction
{
    Up,
    Down,
    Left,
    Right
}
