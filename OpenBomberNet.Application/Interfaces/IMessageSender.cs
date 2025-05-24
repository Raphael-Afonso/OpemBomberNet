using System.Threading.Tasks;

namespace OpenBomberNet.Application.Interfaces;

/// <summary>
/// Defines the contract for sending messages to connected clients.
/// </summary>
public interface IMessageSender
{
    /// <summary>
    /// Sends a message to a specific client connection.
    /// </summary>
    /// <param name="connectionId">The unique identifier of the client connection.</param>
    /// <param name="message">The message payload to send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendMessageAsync(string connectionId, string message);

    /// <summary>
    /// Sends a message to all currently connected clients.
    /// </summary>
    /// <param name="message">The message payload to send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendMessageToAllAsync(string message);

    /// <summary>
    /// Sends a message to all currently connected clients except for one specific connection.
    /// </summary>
    /// <param name="excludedConnectionId">The connection ID to exclude from the broadcast.</param>
    /// <param name="message">The message payload to send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendMessageToAllExceptAsync(string excludedConnectionId, string message);
}

