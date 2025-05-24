using OpenBomberNet.Domain.Interfaces; // For IEntity
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace OpenBomberNet.Domain.Interfaces;

/// <summary>
/// Defines a generic repository pattern interface for data access.
/// </summary>
/// <typeparam name="TEntity">The type of the entity, must implement IEntity.</typeparam>
public interface IRepository<TEntity> where TEntity : IEntity
{
    /// <summary>
    /// Gets an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity.</param>
    /// <returns>The entity if found; otherwise, null.</returns>
    Task<TEntity?> GetByIdAsync(Guid id);

    /// <summary>
    /// Gets all entities of the specified type.
    /// </summary>
    /// <returns>An enumerable collection of all entities.</returns>
    Task<IEnumerable<TEntity>> GetAllAsync();

    /// <summary>
    /// Finds entities based on a predicate.
    /// </summary>
    /// <param name="predicate">The expression to filter entities.</param>
    /// <returns>An enumerable collection of entities matching the predicate.</returns>
    Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(TEntity entity);

    /// <summary>
    /// Adds multiple entities to the repository.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddRangeAsync(IEnumerable<TEntity> entities);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <returns>A task representing the asynchronous operation, returning true if successful, false otherwise.</returns>
    Task<bool> UpdateAsync(TEntity entity);

    /// <summary>
    /// Removes an entity from the repository by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to remove.</param>
    /// <returns>A task representing the asynchronous operation, returning true if successful, false otherwise.</returns>
    Task<bool> RemoveAsync(Guid id);

    /// <summary>
    /// Removes multiple entities from the repository based on a predicate.
    /// </summary>
    /// <param name="predicate">The expression to filter entities for removal.</param>
    /// <returns>A task representing the asynchronous operation, returning the number of entities removed.</returns>
    Task<long> RemoveRangeAsync(Expression<Func<TEntity, bool>> predicate);
}

