namespace OpenBomberNet.Infrastructure.Configuration;

/// <summary>
/// Represents the settings required to connect to MongoDB.
/// Typically loaded from appsettings.json or environment variables.
/// </summary>
public class MongoDbSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;

    // Optional: Add collection names here if you don't want to derive them from class names
    // public string PlayersCollectionName { get; set; } = null!;
}

