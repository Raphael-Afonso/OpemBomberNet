using System.Threading.Tasks;

namespace OpenBomberNet.Server;

// Interface para todos os manipuladores de mensagens
public interface IMessageHandler
{
    // Processa a mensagem recebida de uma conexão específica
    // `connectionId` identifica o cliente
    // `messageParts` contém os componentes da mensagem (ex: ["MOVE", "UP"])
    Task HandleAsync(string connectionId, string[] messageParts);
}

// Classe base ou DTO para representar uma mensagem recebida (opcional, mas pode ser útil)
public class IncomingMessage
{
    public string ConnectionId { get; set; }
    public string RawMessage { get; set; }
    public string Command { get; set; } // Ex: "AUTH", "MOVE"
    public string[] Arguments { get; set; } // Ex: ["token123"], ["UP"]

    public IncomingMessage(string connectionId, string rawMessage)
    {
        ConnectionId = connectionId;
        RawMessage = rawMessage;

        // Lógica simples de parsing (pode ser melhorada)
        var parts = rawMessage.Split("|", StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            Command = parts[0].ToUpperInvariant(); // Padroniza o comando
            Arguments = parts.Skip(1).ToArray();
        }
        else
        {
            Command = string.Empty;
            Arguments = Array.Empty<string>();
        }
    }
}
