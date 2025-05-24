namespace OpenBomberNet.Domain.Entities;

public class Item
{
    public ValueObjects.Position Position { get; private set; }
    public PowerUpType Type { get; private set; }
    public bool IsCollected { get; private set; } = false;

    public Item(ValueObjects.Position position, PowerUpType type)
    {
        Position = position;
        Type = type;
    }

    public void Collect()
    {
        IsCollected = true;
        // Lógica adicional ao coletar (ex: remover do mapa)
    }
}
