using Microsoft.Extensions.Logging;
using OpenBomberNet.Application.Interfaces;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenBomberNet.Infrastructure.Security;

// ATENÇÃO: Implementação de criptografia XOR simples, **altamente insegura**.
// Adequada apenas para fins didáticos e de estudo, **NÃO USE EM PRODUÇÃO**.
// Para segurança real, use AES ou outras cifras robustas com gerenciamento de chaves adequado.
public class SimpleCryptoService : ISimpleCryptoService
{
    private readonly ILogger<SimpleCryptoService> _logger;
    // Chave de criptografia XOR muito simples (NÃO FAÇA ISSO EM PRODUÇÃO)
    private readonly byte[] _key = Encoding.UTF8.GetBytes("SimpleKey123!");

    public SimpleCryptoService(ILogger<SimpleCryptoService> logger)
    {
        _logger = logger;
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedData = ProcessXOR(data);
            return Convert.ToBase64String(encryptedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during simple encryption.");
            return string.Empty; // Return empty or throw?
        }
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        try
        {
            byte[] data = Convert.FromBase64String(cipherText);
            byte[] decryptedData = ProcessXOR(data);
            return Encoding.UTF8.GetString(decryptedData);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Error during simple decryption: Invalid Base64 string. Ciphertext (start): {CipherTextStart}", cipherText.Length > 10 ? cipherText.Substring(0, 10) : cipherText);
            return string.Empty; // Or throw specific exception
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during simple decryption. Ciphertext (start): {CipherTextStart}", cipherText.Length > 10 ? cipherText.Substring(0, 10) : cipherText);
            return string.Empty; // Or throw
        }
    }

    private byte[] ProcessXOR(byte[] data)
    {
        byte[] result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ _key[i % _key.Length]);
        }
        return result;
    }
}

