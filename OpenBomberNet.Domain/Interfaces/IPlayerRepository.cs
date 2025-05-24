using OpenBomberNet.Domain.Entities;

namespace OpenBomberNet.Domain.Interfaces;

public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(Guid id);
    Task<Player?> GetByConnectionIdAsync(string connectionId);
    Task AddAsync(Player player);
    Task UpdateAsync(Player player);
    Task RemoveAsync(Guid id);
    Task<IEnumerable<Player>> GetAllAsync(); // Pode ser útil para o lobby
}
