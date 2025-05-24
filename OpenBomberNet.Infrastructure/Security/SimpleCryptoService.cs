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
    // Chave de criptografia XOR muito simples (NÃO FAÇA ISSO EM PRODUÇÃO)
    private readonly byte[] _key = Encoding.UTF8.GetBytes("SimpleKey123!");

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        byte[] data = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedData = ProcessXOR(data);
        return Convert.ToBase64String(encryptedData);
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
        catch (FormatException)
        {
            // Handle invalid Base64 string - return original or throw?
            Console.WriteLine("Error: Invalid Base64 string during decryption.");
            return string.Empty; // Or throw specific exception
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during decryption: {ex.Message}");
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
