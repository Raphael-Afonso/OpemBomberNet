namespace OpenBomberNet.Application.Interfaces;

public interface IAuthenticationService
{
    string GenerateToken(Guid playerId, string nickname);
    (bool IsValid, Guid? PlayerId, string? Nickname) ValidateToken(string token);
}
