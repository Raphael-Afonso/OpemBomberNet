using OpenBomberNet.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenBomberNet.Application.Interfaces;

public interface IGameService
{
    // Cria uma nova sessão de jogo, potencialmente com jogadores específicos do lobby
    Task<Guid> CreateGameAsync(IEnumerable<Guid> playerIds, int mapSize = 15); // Retorna GameId

    // Obtém o estado atual do mapa para um jogo
    Task<Map?> GetMapStateAsync(Guid gameId);

    // Remove um jogador de um jogo (ex: se desconectar)
    Task RemovePlayerFromGameAsync(Guid gameId, Guid playerId);

    // Obtém a instância do mapa de um jogo ativo
    Map? GetActiveMap(Guid gameId);

    // Outros métodos relacionados ao gerenciamento do jogo podem ser adicionados aqui
    // Ex: Processar ações do jogador (movimento, colocar bomba), atualizar estado do jogo
}
