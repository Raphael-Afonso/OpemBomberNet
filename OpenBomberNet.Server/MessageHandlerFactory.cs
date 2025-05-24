using Microsoft.Extensions.DependencyInjection; // Assuming DI container usage
using OpenBomberNet.Application.Interfaces;
using OpenBomberNet.Infrastructure.Networking;
using OpenBomberNet.Server.Handlers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenBomberNet.Server;

// Simple factory using a dictionary (can be replaced by DI container features)
public class MessageHandlerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _handlerMap = new();

    public MessageHandlerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        // Manually register handlers - In a real app, use reflection or DI attributes
        _handlerMap.Add("AUTH", typeof(AuthMessageHandler));
        _handlerMap.Add("LOBBY_MESSAGE", typeof(LobbyMessageMessageHandler));
        _handlerMap.Add("MOVE", typeof(MoveMessageHandler));
        _handlerMap.Add("BOMB", typeof(BombMessageHandler));
        // Add other handlers here (e.g., LOBBY_ENTER, LOBBY_LEAVE)
        // _handlerMap.Add("LOBBY_LEAVE", typeof(LobbyLeaveMessageHandler)); // Example
    }

    public IMessageHandler? GetHandler(string command)
    {
        if (_handlerMap.TryGetValue(command.ToUpperInvariant(), out Type? handlerType))
        {
            // Resolve the handler instance from the DI container
            return _serviceProvider.GetService(handlerType) as IMessageHandler;
        }
        return null; // No handler found for this command
    }
}

// Placeholder for LobbyLeave handler (as an example)
/*
namespace OpenBomberNet.Server.Handlers;
public class LobbyLeaveMessageHandler : IMessageHandler
{
    private readonly ILobbyService _lobbyService;
    public LobbyLeaveMessageHandler(ILobbyService lobbyService)
    {
        _lobbyService = lobbyService;
    }
    public async Task HandleAsync(string connectionId, string[] messageParts)
    {
        // Expected format: LOBBY_LEAVE
        await _lobbyService.LeaveLobbyAsync(connectionId);
    }
}
*/
