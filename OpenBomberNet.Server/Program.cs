using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using OpenBomberNet.Application.Interfaces;
using OpenBomberNet.Application.Services;
using OpenBomberNet.Common; // For ProtocolCommands etc.
using OpenBomberNet.Domain.Entities; // For Player etc.
using OpenBomberNet.Domain.Interfaces;
using OpenBomberNet.Infrastructure.Configuration;
using OpenBomberNet.Infrastructure.Networking; // For IConnectionManager, IMessageSender implementation
using OpenBomberNet.Infrastructure.Persistence;
using OpenBomberNet.Infrastructure.Security;
using OpenBomberNet.Server.Handlers;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenBomberNet.Server;

public class TcpServer : IHostedService // Implement IHostedService for better integration with generic host
{
    private readonly ILogger<TcpServer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MessageHandlerFactory _messageHandlerFactory;
    private readonly IConnectionManager _connectionManager;
    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _serverCts;

    public TcpServer(ILogger<TcpServer> logger, IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _port = configuration.GetValue<int>("ServerSettings:Port", 8888); // Get port from config or default

        // Resolve necessary services from DI container
        // Use GetRequiredService to ensure they are registered
        _messageHandlerFactory = _serviceProvider.GetRequiredService<MessageHandlerFactory>();
        _connectionManager = _serviceProvider.GetRequiredService<IConnectionManager>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _serverCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _logger.LogInformation("TCP Server started on port {Port}. Waiting for connections.", _port);

        // Start accepting clients in a background task
        _ = AcceptClientsAsync(_serverCts.Token);

        return Task.CompletedTask;
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        if (_listener == null) return;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _logger.LogInformation("Client connected: {RemoteEndPoint}", client.Client.RemoteEndPoint);
                // Handle each client connection in its own task scope
                _ = HandleClientAsync(client, cancellationToken); // Fire and forget
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Server stopping accepting new connections...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting client connections.");
        }
        finally
        {
             _listener?.Stop();
             _logger.LogInformation("TCP Listener stopped.");
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        // Create a scope for the connection to resolve scoped services if needed
        // using var scope = _serviceProvider.CreateScope();
        // var scopedServiceProvider = scope.ServiceProvider;
        // Resolve services needed per connection (e.g., if handlers were scoped)

        string connectionId = Guid.NewGuid().ToString();
        _logger.LogDebug("Assigning ConnectionId {ConnectionId} to {RemoteEndPoint}", connectionId, client.Client.RemoteEndPoint);

        NetworkStream? stream = null;
        StreamReader? reader = null;
        StreamWriter? writer = null;

        try
        {
            stream = client.GetStream();
            // Use buffers for potentially better performance
            reader = new StreamReader(stream, Encoding.UTF8, true, 1024);
            writer = new StreamWriter(stream, Encoding.UTF8, 1024) { AutoFlush = true };

            // Register the connection
            _connectionManager.AddConnection(connectionId, writer);

            while (client.Connected && !cancellationToken.IsCancellationRequested)
            {
                string? message = await reader.ReadLineAsync(cancellationToken);
                if (message == null)
                {
                    _logger.LogDebug("Client {ConnectionId} disconnected gracefully (stream closed).", connectionId);
                    break; // Client disconnected gracefully
                }

                _logger.LogTrace("[{ConnectionId}] C->S: {Message}", connectionId, message);

                // Parse and handle the message
                var parts = message.Split(ProtocolDelimiters.Primary, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    string command = parts[0].ToUpperInvariant();
                    IMessageHandler? handler = _messageHandlerFactory.GetHandler(command);

                    if (handler != null)
                    {
                        try
                        {
                            // Execute the handler
                            await handler.HandleAsync(connectionId, parts);
                        }
                        catch (Exception handlerEx)
                        {
                            _logger.LogError(handlerEx, "Error executing handler {HandlerType} for command {Command} from {ConnectionId}.", handler.GetType().Name, command, connectionId);
                            // Optionally send a generic error message back
                            await _messageSender.SendMessageAsync(connectionId, $"{ProtocolCommands.Error}{ProtocolDelimiters.Primary}Internal server error processing command.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("[{ConnectionId}] No handler found for command: {Command}", connectionId, command);
                        await _messageSender.SendMessageAsync(connectionId, $"{ProtocolCommands.Error}{ProtocolDelimiters.Primary}Unknown command: {command}");
                    }
                }
                else
                {
                    _logger.LogWarning("[{ConnectionId}] Received empty message.", connectionId);
                }
            }
        }
        catch (IOException ex) when (ex.InnerException is SocketException sex)
        {
            _logger.LogWarning("[{ConnectionId}] Socket error ({SocketErrorCode}): {ErrorMessage}", connectionId, sex.SocketErrorCode, sex.Message);
        }
        catch (OperationCanceledException)
        {
             _logger.LogDebug("[{ConnectionId}] Read operation cancelled.", connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ConnectionId}] Error handling client.", connectionId);
        }
        finally
        {
            _logger.LogInformation("Client disconnected: {RemoteEndPoint} (ID: {ConnectionId})", client.Client.RemoteEndPoint, connectionId);

            // Clean up player state associated with this connection
            var playerContext = _connectionManager.GetPlayerContext(connectionId);
            if (playerContext != null && playerContext.PlayerId != Guid.Empty)
            {
                // Use resolved services to handle cleanup
                try
                {
                    var lobbyService = _serviceProvider.GetRequiredService<ILobbyService>();
                    await lobbyService.LeaveLobbyAsync(connectionId); // Lobby service handles player removal from lobby state

                    if(playerContext.GameId != Guid.Empty)
                    {
                        var gameService = _serviceProvider.GetRequiredService<IGameService>();
                        await gameService.RemovePlayerFromGameAsync(playerContext.GameId, playerContext.PlayerId);
                        _logger.LogInformation("Removed player {PlayerId} from game {GameId} due to disconnect.", playerContext.PlayerId, playerContext.GameId);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, "Error during disconnect cleanup for player {PlayerId} connection {ConnectionId}", playerContext.PlayerId, connectionId);
                }
            }

            _connectionManager.RemoveConnection(connectionId);

            writer?.Dispose();
            reader?.Dispose();
            stream?.Dispose();
            client.Close();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StopAsync called. Initiating server shutdown...");
        _serverCts?.Cancel();
        _listener?.Stop(); // Ensure listener is stopped
        _serverCts?.Dispose();
        return Task.CompletedTask;
    }

    // Need access to IMessageSender for error messages within HandleClientAsync
    private IMessageSender _messageSender => _serviceProvider.GetRequiredService<IMessageSender>();
}

public class Program
{
    public static async Task Main(string[] args)
    {
        await CreateHostBuilder(args).Build().RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                // Configure additional sources if needed (e.g., appsettings.json)
                // Environment variables are loaded by default
                config.AddEnvironmentVariables();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                // Add other providers like Debug, EventLog, etc. if needed
            })
            .ConfigureServices((hostContext, services) =>
            {
                ConfigureAppServices(services, hostContext.Configuration);
            });

    private static void ConfigureAppServices(IServiceCollection services, IConfiguration configuration)
    {
        // --- Configure Settings --- 
        // Bind MongoDbSettings from configuration (expects section "MongoDbSettings")
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDbSettings"));
        // Ensure settings are valid (optional but recommended)
        services.AddOptions<MongoDbSettings>()
            .ValidateDataAnnotations() // Add Microsoft.Extensions.Options.DataAnnotations for this
            .ValidateOnStart();

        // --- Register Infrastructure Services --- 
        // MongoDB Client (Singleton recommended)
        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            // Potentially configure MongoClientSettings further (timeouts, read/write concerns)
            return new MongoClient(settings.ConnectionString);
        });

        // Generic Repository
        services.AddScoped(typeof(IRepository<>), typeof(MongoRepository<>));

        // Specific Repositories (if they don't just use IRepository<T> directly)
        // Example: If IPlayerRepository has specific methods not in IRepository<Player>
        // services.AddScoped<IPlayerRepository, MongoPlayerRepository>(); 
        // If IPlayerRepository *is* just IRepository<Player>, services can resolve IRepository<Player> directly.
        // Let's assume for now services will request IRepository<Player>.

        // Other Infrastructure
        services.AddSingleton<IAuthenticationService, SimpleAuthenticationService>();
        services.AddSingleton<ISimpleCryptoService, SimpleCryptoService>();
        services.AddSingleton<IConnectionManager, InMemoryConnectionManager>();
        // Register IMessageSender implementation (which is InMemoryConnectionManager in this case)
        services.AddSingleton<IMessageSender>(sp => sp.GetRequiredService<IConnectionManager>() as IMessageSender ?? throw new InvalidOperationException("IConnectionManager must implement IMessageSender"));

        // --- Register Application Services --- 
        services.AddSingleton<ILobbyService, LobbyService>();
        services.AddSingleton<IGameService, GameService>();
        services.AddSingleton<IPlayerActionService, PlayerActionService>();

        // --- Register Message Handlers --- 
        services.AddTransient<AuthMessageHandler>();
        services.AddTransient<LobbyMessageMessageHandler>();
        services.AddTransient<MoveMessageHandler>();
        services.AddTransient<BombMessageHandler>();
        // Add other handlers here...

        // --- Register Factory --- 
        services.AddSingleton<MessageHandlerFactory>();

        // --- Register the TCP Server as a Hosted Service --- 
        services.AddHostedService<TcpServer>();
    }
}

// --- Placeholder Implementations needed for DI (already provided in previous steps, ensure they exist) ---
// Ensure InMemoryConnectionManager, IConnectionManager, PlayerConnectionContext exist
// Ensure IMessageHandler implementations exist
// Ensure MessageHandlerFactory exists

// Example: Make sure InMemoryConnectionManager is defined and implements necessary interfaces
namespace OpenBomberNet.Infrastructure.Networking
{
    public class PlayerConnectionContext
    {
        public string ConnectionId { get; }
        public Guid PlayerId { get; set; } // Associated Player ID after auth
        public Guid GameId { get; set; } // Current Game ID if in game
        public string? AuthToken { get; set; } // Store token if needed for crypto

        public PlayerConnectionContext(string connectionId, Guid playerId)
        {
            ConnectionId = connectionId;
            PlayerId = playerId;
            GameId = Guid.Empty;
        }
    }

    public interface IConnectionManager : IMessageSender // Combine interfaces for simplicity here
    {
        void AddConnection(string connectionId, StreamWriter writer);
        void RemoveConnection(string connectionId);
        void Associate(string connectionId, Guid playerId, Guid? gameId, string? token);
        void Disassociate(string connectionId);
        PlayerConnectionContext? GetPlayerContext(string connectionId);
        string? GetConnectionId(Guid playerId);
        void SetPlayerGame(string connectionId, Guid gameId);
    }

    public class InMemoryConnectionManager : IConnectionManager
    {
        private readonly ILogger<InMemoryConnectionManager> _logger;
        private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();
        // Add reverse lookup for efficiency
        private readonly ConcurrentDictionary<Guid, string> _playerIdToConnectionId = new();

        private class ConnectionInfo
        {
            public StreamWriter Writer { get; }
            public PlayerConnectionContext Context { get; set; }
            public ConnectionInfo(StreamWriter writer, PlayerConnectionContext context)
            {
                Writer = writer;
                Context = context;
            }
        }

        public InMemoryConnectionManager(ILogger<InMemoryConnectionManager> logger)
        {
            _logger = logger;
        }

        public void AddConnection(string connectionId, StreamWriter writer)
        {
            var context = new PlayerConnectionContext(connectionId, Guid.Empty);
            if (_connections.TryAdd(connectionId, new ConnectionInfo(writer, context)))
            {
                _logger.LogDebug("Connection added: {ConnectionId}", connectionId);
            }
            else
            {
                 _logger.LogWarning("Failed to add duplicate connection: {ConnectionId}", connectionId);
            }
        }

        public void RemoveConnection(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var info))
            {
                if (info.Context.PlayerId != Guid.Empty)
                {
                    _playerIdToConnectionId.TryRemove(info.Context.PlayerId, out _);
                }
                _logger.LogDebug("Connection removed: {ConnectionId}", connectionId);
            }
        }

        public void Associate(string connectionId, Guid playerId, Guid? gameId, string? token)
        {
            if (_connections.TryGetValue(connectionId, out var info))
            {
                // Remove old association if player was already associated with another connection
                if (playerId != Guid.Empty && _playerIdToConnectionId.TryGetValue(playerId, out var oldConnectionId) && oldConnectionId != connectionId)
                {
                    _logger.LogWarning("Player {PlayerId} was already associated with connection {OldConnectionId}. Re-associating with {NewConnectionId}.", playerId, oldConnectionId, connectionId);
                    // Optionally disconnect old connection?
                    if (_connections.TryGetValue(oldConnectionId, out var oldInfo))
                    {
                        oldInfo.Context.PlayerId = Guid.Empty; // Disassociate old connection
                    }
                }

                info.Context.PlayerId = playerId;
                info.Context.GameId = gameId ?? Guid.Empty;
                info.Context.AuthToken = token;
                if (playerId != Guid.Empty)
                {
                    _playerIdToConnectionId[playerId] = connectionId;
                }
                 _logger.LogDebug("Associated Connection {ConnectionId} with Player {PlayerId}", connectionId, playerId);
            }
            else
            {
                 _logger.LogWarning("Cannot associate: Connection {ConnectionId} not found.", connectionId);
            }
        }

        public void Disassociate(string connectionId)
        {
             if (_connections.TryGetValue(connectionId, out var info))
            {
                if (info.Context.PlayerId != Guid.Empty)
                {
                    _playerIdToConnectionId.TryRemove(info.Context.PlayerId, out _);
                }
                info.Context.PlayerId = Guid.Empty;
                info.Context.GameId = Guid.Empty;
                info.Context.AuthToken = null;
                _logger.LogDebug("Disassociated Connection {ConnectionId}", connectionId);
            }
        }

        public PlayerConnectionContext? GetPlayerContext(string connectionId)
        {
            _connections.TryGetValue(connectionId, out var info);
            return info?.Context;
        }

        public string? GetConnectionId(Guid playerId)
        {
            _playerIdToConnectionId.TryGetValue(playerId, out var connectionId);
            return connectionId;
        }

        public void SetPlayerGame(string connectionId, Guid gameId)
        {
             if (_connections.TryGetValue(connectionId, out var info))
            {
                info.Context.GameId = gameId;
                 _logger.LogDebug("Set GameId {GameId} for Connection {ConnectionId}", gameId, connectionId);
            }
             else
            {
                 _logger.LogWarning("Cannot set game: Connection {ConnectionId} not found.", connectionId);
            }
        }

        // --- IMessageSender Implementation ---
        public async Task SendMessageAsync(string connectionId, string message)
        {
            if (_connections.TryGetValue(connectionId, out var info))
            {
                try
                {
                    // TODO: Add encryption based on context/token?
                    await info.Writer.WriteLineAsync(message);
                    _logger.LogTrace("[{ConnectionId}] S->C: {Message}", connectionId, message);
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogWarning("Attempted to write to disposed writer for connection {ConnectionId}. Removing connection.", connectionId);
                    RemoveConnection(connectionId); // Clean up broken connection
                }
                catch (IOException ioEx)
                {
                     _logger.LogWarning(ioEx, "IOException sending message to {ConnectionId}. Removing connection.", connectionId);
                     RemoveConnection(connectionId); // Clean up broken connection
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending message to {ConnectionId}.", connectionId);
                    // Consider removing the connection if sending fails repeatedly
                    // RemoveConnection(connectionId);
                }
            }
            else
            {
                _logger.LogWarning("Attempted to send message to unknown connection {ConnectionId}", connectionId);
            }
        }

        public async Task SendMessageToAllAsync(string message)
        {
            // Create a snapshot of keys to avoid issues with modification during iteration
            var connectionIds = _connections.Keys.ToList();
            var tasks = connectionIds.Select(id => SendMessageAsync(id, message));
            await Task.WhenAll(tasks);
        }

        public async Task SendMessageToAllExceptAsync(string excludedConnectionId, string message)
        {
            var connectionIds = _connections.Keys.Where(id => id != excludedConnectionId).ToList();
            var tasks = connectionIds.Select(id => SendMessageAsync(id, message));
            await Task.WhenAll(tasks);
        }
    }
}

