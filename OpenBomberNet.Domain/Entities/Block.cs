namespace OpenBomberNet.Domain.Entities;

public abstract class Block
{
    public ValueObjects.Position Position { get; protected set; }
    public bool IsWalkable { get; protected set; }

    protected Block(ValueObjects.Position position)
    {
        Position = position;
    }
}

public class FreeBlock : Block
{
    public FreeBlock(ValueObjects.Position position) : base(position)
    {
        IsWalkable = true;
    }
}

public class IndestructibleBlock : Block
{
    public IndestructibleBlock(ValueObjects.Position position) : base(position)
    {
        IsWalkable = false;
    }
}

public class DestructibleBlock : Block
{
    public bool HasItem { get; private set; }
    public PowerUpType? ItemType { get; private set; }

    public DestructibleBlock(ValueObjects.Position position, bool hasItem = false, PowerUpType? itemType = null) : base(position)
    {
        IsWalkable = false;
        HasItem = hasItem;
        ItemType = itemType;

        // Lógica para determinar aleatoriamente se tem item e qual tipo
        if (!HasItem && new Random().Next(0, 3) == 0) // Exemplo: 1/3 chance de ter item
        {
            HasItem = true;
            ItemType = (PowerUpType)new Random().Next(Enum.GetNames(typeof(PowerUpType)).Length);
        }
    }

    public Item? Destroy()
    {
        // Transforma em FreeBlock e retorna o item se houver
        if (HasItem && ItemType.HasValue)
        {
            return new Item(Position, ItemType.Value);
        }
        return null;
    }
}
