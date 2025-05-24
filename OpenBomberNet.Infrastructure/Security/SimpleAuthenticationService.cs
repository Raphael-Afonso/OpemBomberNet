using OpenBomberNet.Application.Interfaces;
using System;
using System.Text;

namespace OpenBomberNet.Infrastructure.Security;

// ATENÇÃO: Esta implementação é extremamente simples e insegura, adequada apenas para estudo.
// Para um ambiente real, use bibliotecas robustas como JWT (JSON Web Tokens).
public class SimpleAuthenticationService : IAuthenticationService
{
    // Chave secreta muito simples (NÃO FAÇA ISSO EM PRODUÇÃO)
    private const string SecretKey = "YourSuperSecretKeyHere!";

    public string GenerateToken(Guid playerId, string nickname)
    {
        // Combina os dados e a chave secreta (forma muito básica)
        string dataToEncode = $"{playerId}|{nickname}|{SecretKey}";
        byte[] bytesToEncode = Encoding.UTF8.GetBytes(dataToEncode);

        // Codifica em Base64 (não é criptografia, apenas codificação)
        return Convert.ToBase64String(bytesToEncode);
    }

    public (bool IsValid, Guid? PlayerId, string? Nickname) ValidateToken(string token)
    {
        try
        {
            byte[] decodedBytes = Convert.FromBase64String(token);
            string decodedData = Encoding.UTF8.GetString(decodedBytes);

            // Separa os dados e a chave secreta
            string[] parts = decodedData.Split('|');

            // Verifica se o formato está correto e se a chave secreta confere
            if (parts.Length == 3 && parts[2] == SecretKey)
            {
                if (Guid.TryParse(parts[0], out Guid playerId))
                {
                    return (true, playerId, parts[1]);
                }
            }
        }
        catch (FormatException)
        {
            // Token inválido (não é Base64 válido)
            return (false, null, null);
        }
        catch (Exception ex)
        {
            // Logar o erro em um cenário real
            Console.WriteLine($"Erro ao validar token: {ex.Message}");
            return (false, null, null);
        }

        return (false, null, null);
    }
}
