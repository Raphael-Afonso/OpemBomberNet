using Microsoft.Extensions.Logging;
using OpenBomberNet.Application.Interfaces;
using System;
using System.Text;

namespace OpenBomberNet.Infrastructure.Security;

// ATENÇÃO: Esta implementação é extremamente simples e insegura, adequada apenas para estudo.
// Para um ambiente real, use bibliotecas robustas como JWT (JSON Web Tokens).
public class SimpleAuthenticationService : IAuthenticationService
{
    private readonly ILogger<SimpleAuthenticationService> _logger;
    // Chave secreta muito simples (NÃO FAÇA ISSO EM PRODUÇÃO)
    private const string SecretKey = "YourSuperSecretKeyHere!";

    public SimpleAuthenticationService(ILogger<SimpleAuthenticationService> logger)
    {
        _logger = logger;
    }

    public string GenerateToken(Guid playerId, string nickname)
    {
        // Combina os dados e a chave secreta (forma muito básica)
        string dataToEncode = $"{playerId}|{nickname}|{SecretKey}";
        byte[] bytesToEncode = Encoding.UTF8.GetBytes(dataToEncode);

        // Codifica em Base64 (não é criptografia, apenas codificação)
        _logger.LogDebug("Generated simple token for PlayerId {PlayerId}", playerId);
        return Convert.ToBase64String(bytesToEncode);
    }

    public (bool IsValid, Guid? PlayerId, string? Nickname) ValidateToken(string token)
    {
        try
        {
            byte[] decodedBytes = Convert.FromBase64String(token);
            string decodedData = Encoding.UTF8.GetString(decodedBytes);

            // Separa os dados e a chave secreta
            string[] parts = decodedData.Split("|");

            // Verifica se o formato está correto e se a chave secreta confere
            if (parts.Length == 3 && parts[2] == SecretKey)
            {
                if (Guid.TryParse(parts[0], out Guid playerId))
                {
                    _logger.LogDebug("Simple token validation successful for PlayerId {PlayerId}", playerId);
                    return (true, playerId, parts[1]);
                }
                else
                {
                    _logger.LogWarning("Failed to parse PlayerId from token: {Token}", token);
                }
            }
            else
            {
                 _logger.LogWarning("Token validation failed: Invalid format or secret key mismatch. Token: {Token}", token);
            }
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Token validation failed: Invalid Base64 format. Token: {Token}", token);
            return (false, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token validation. Token: {Token}", token);
            return (false, null, null);
        }

        return (false, null, null);
    }
}

