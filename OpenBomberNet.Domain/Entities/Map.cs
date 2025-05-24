using OpenBomberNet.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace OpenBomberNet.Domain.Entities;

public abstract class Map
{
    public Guid Id { get; private set; }
    public int Width { get; protected set; }
    public int Height { get; protected set; }
    public ConcurrentDictionary<Position, Block> Blocks { get; protected set; }
    public ConcurrentDictionary<Guid, Player> Players { get; protected set; }
    public ConcurrentDictionary<Position, Item> Items { get; protected set; }
    public ConcurrentDictionary<Position, Bomb> Bombs { get; protected set; } // Adicionando bombas ao mapa

    protected Map(int width, int height)
    {
        Id = Guid.NewGuid();
        Width = width;
        Height = height;
        Blocks = new ConcurrentDictionary<Position, Block>();
        Players = new ConcurrentDictionary<Guid, Player>();
        Items = new ConcurrentDictionary<Position, Item>();
        Bombs = new ConcurrentDictionary<Position, Bomb>();
        GenerateMapLayout();
    }

    protected abstract void GenerateMapLayout();

    public virtual bool IsPositionWalkable(Position position)
    {
        if (position.X < 0 || position.X >= Width || position.Y < 0 || position.Y >= Height)
            return false; // Fora dos limites

        if (Blocks.TryGetValue(position, out var block))
        {
            return block.IsWalkable;
        }

        return true; // Assume que é caminhável se não houver bloco (pode precisar ajustar)
    }

    public virtual void AddPlayer(Player player)
    {
        Players.TryAdd(player.Id, player);
    }

    public virtual void RemovePlayer(Guid playerId)
    {
        Players.TryRemove(playerId, out _);
    }

    public virtual void AddBomb(Bomb bomb)
    {
        Bombs.TryAdd(bomb.Position, bomb);
    }

    public virtual void RemoveBomb(Position position)
    {
        Bombs.TryRemove(position, out _);
    }

     public virtual Block? GetBlockAt(Position position)
    {
        Blocks.TryGetValue(position, out var block);
        return block;
    }

    public virtual Item? GetItemAt(Position position)
    {
        Items.TryGetValue(position, out var item);
        return item;
    }

    public virtual void RemoveItem(Position position)
    {
        Items.TryRemove(position, out _);
    }

    // Método para destruir um bloco e potencialmente gerar um item
    public virtual void DestroyBlockAt(Position position)
    {
        if (Blocks.TryGetValue(position, out var block) && block is DestructibleBlock destructibleBlock)
        {
            var item = destructibleBlock.Destroy();
            // Substitui o bloco destruível por um bloco livre
            Blocks.TryUpdate(position, new FreeBlock(position), block);

            if (item != null)
            {
                Items.TryAdd(item.Position, item);
                // Notificar sobre o item (pode ser feito em outro lugar)
            }
            // Notificar sobre a destruição do bloco (pode ser feito em outro lugar)
        }
    }
}

// Implementação concreta inicial (mapa quadrado)
public class SquareMap : Map
{
    private const int DefaultSize = 15; // Tamanho padrão do mapa quadrado

    public SquareMap() : base(DefaultSize, DefaultSize) { }
    public SquareMap(int size) : base(size, size) { }

    protected override void GenerateMapLayout()
    {
        // Lógica de geração do mapa (exemplo simples)
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var position = new Position(x, y);
                Block block;

                if (x == 0 || x == Width - 1 || y == 0 || y == Height - 1 || (x % 2 == 0 && y % 2 == 0))
                {
                    // Bordas e pilares indestrutíveis
                    block = new IndestructibleBlock(position);
                }
                else if (ShouldPlaceDestructibleBlock(position))
                {
                    // Blocos destrutíveis (com chance de item)
                    block = new DestructibleBlock(position);
                }
                else
                {
                    // Espaços livres
                    block = new FreeBlock(position);
                }
                Blocks.TryAdd(position, block);
            }
        }
    }

    private bool ShouldPlaceDestructibleBlock(Position position)
    {
        // Evita colocar blocos destrutíveis nos cantos iniciais dos jogadores (exemplo)
        if ((position.X <= 1 && position.Y <= 1) ||
            (position.X >= Width - 2 && position.Y <= 1) ||
            (position.X <= 1 && position.Y >= Height - 2) ||
            (position.X >= Width - 2 && position.Y >= Height - 2))
        {
            return false;
        }

        // Lógica para decidir se coloca um bloco destrutível (ex: aleatório)
        return new Random().Next(0, 5) < 3; // Exemplo: 60% de chance
    }
}

// Adicionando a entidade Bomb
public class Bomb
{
    public Guid Id { get; private set; }
    public Position Position { get; private set; }
    public Guid OwnerPlayerId { get; private set; }
    public int Radius { get; private set; }
    public DateTime PlacedTime { get; private set; }
    public float FuseTimeSeconds { get; private set; }
    public bool IsExploded { get; private set; } = false;

    public Bomb(Position position, Guid ownerPlayerId, int radius, float fuseTimeSeconds)
    {
        Id = Guid.NewGuid();
        Position = position;
        OwnerPlayerId = ownerPlayerId;
        Radius = radius;
        PlacedTime = DateTime.UtcNow;
        FuseTimeSeconds = fuseTimeSeconds;
    }

    public bool ShouldExplode(DateTime currentTime)
    {
        return !IsExploded && (currentTime - PlacedTime).TotalSeconds >= FuseTimeSeconds;
    }

    public void Explode()
    {
        IsExploded = true;
        // Lógica de explosão será tratada no serviço do jogo
    }
}
