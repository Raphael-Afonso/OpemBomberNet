using System;

namespace OpenBomberNet.Domain.Interfaces;

/// <summary>
/// Base interface for domain entities to ensure they have an Id.
/// </summary>
public interface IEntity
{
    Guid Id { get; set; } // Or the appropriate type for your Id (e.g., string for MongoDB ObjectId)
                         // Using Guid for consistency with existing code.
}

