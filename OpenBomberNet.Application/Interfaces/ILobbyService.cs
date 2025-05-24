using OpenBomberNet.Application.DTOs;
using OpenBomberNet.Domain.Entities;

namespace OpenBomberNet.Application.Interfaces;

public interface ILobbyService
{
    Task<bool> EnterLobbyAsync(string connectionId, string nickname);
    Task LeaveLobbyAsync(string connectionId);
    Task BroadcastMessageAsync(string senderConnectionId, string message);
    Task<IEnumerable<PlayerLobbyDto>> GetPlayersInLobbyAsync();
    // Outros métodos relacionados ao lobby (ex: iniciar partida)
}
