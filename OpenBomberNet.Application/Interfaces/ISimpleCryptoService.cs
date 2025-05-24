using System.Threading.Tasks;

namespace OpenBomberNet.Application.Interfaces;

// Interface para criptografia/descriptografia simples de mensagens
public interface ISimpleCryptoService
{
    // Criptografa uma string (mensagem)
    string Encrypt(string plainText);

    // Descriptografa uma string (mensagem)
    string Decrypt(string cipherText);
}
