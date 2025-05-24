namespace OpenBomberNet.Domain.Entities;

public class Player
{
    public Guid Id { get; private set; }
    public string Nickname { get; private set; }
    public ValueObjects.Position Position { get; private set; }
    public int MaxBombs { get; private set; } = 1;
    public int BombRadius { get; private set; } = 1;
    public float BombFuseTimeMultiplier { get; private set; } = 1.0f;
    public bool IsAlive { get; private set; } = true;
    public string? ConnectionId { get; set; } // Associar conexão TCP
    public string? AuthToken { get; set; } // Token de autenticação

    public Player(Guid id, string nickname, ValueObjects.Position initialPosition)
    {
        Id = id;
        Nickname = nickname;
        Position = initialPosition;
    }

    public void Move(ValueObjects.Position newPosition)
    {
        // Lógica de validação de movimento pode ser adicionada aqui ou na camada de aplicação
        Position = newPosition;
    }

    public void ApplyPowerUp(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.FasterFuse:
                BombFuseTimeMultiplier = Math.Max(0.5f, BombFuseTimeMultiplier * 0.8f); // Exemplo de redução
                break;
            case PowerUpType.BiggerRadius:
                BombRadius++;
                break;
            case PowerUpType.MoreBombs:
                MaxBombs++;
                break;
        }
    }

    public void Die()
    {
        IsAlive = false;
        // Lógica adicional ao morrer (ex: remover do mapa, notificar outros jogadores)
    }
}

public enum PowerUpType
{
    FasterFuse,
    BiggerRadius,
    MoreBombs
}
