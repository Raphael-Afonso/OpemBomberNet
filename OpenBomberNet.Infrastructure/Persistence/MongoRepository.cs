using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OpenBomberNet.Domain.Interfaces;
using OpenBomberNet.Infrastructure.Configuration;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace OpenBomberNet.Infrastructure.Persistence;

/// <summary>
/// Generic repository implementation using MongoDB.
/// </summary>
/// <typeparam name="TEntity">The type of the entity, must implement IEntity.</typeparam>
public class MongoRepository<TEntity> : IRepository<TEntity> where TEntity : class, IEntity
{
    private readonly IMongoCollection<TEntity> _collection;

    public MongoRepository(IMongoClient mongoClient, IOptions<MongoDbSettings> settings)
    {
        var database = mongoClient.GetDatabase(settings.Value.DatabaseName);
        // Convention: Collection name is the pluralized entity name (e.g., Player -> Players)
        // You might want a more robust way to determine collection names (e.g., attributes, config)
        _collection = database.GetCollection<TEntity>(GetCollectionName(typeof(TEntity)));
    }

    // Simple helper to pluralize entity name for collection name (can be improved)
    private static string GetCollectionName(Type entityType)
    {
        // Basic pluralization, add more rules or use a library if needed
        return entityType.Name.EndsWith("s") ? entityType.Name : entityType.Name + "s";
    }

    public async Task<TEntity?> GetByIdAsync(Guid id)
    {
        // MongoDB typically uses _id field. Ensure your entities map Guid Id to _id or handle appropriately.
        // If using MongoDB ObjectId strings, the IEntity interface and this method need adjustment.
        var filter = Builders<TEntity>.Filter.Eq(doc => doc.Id, id);
        return await _collection.Find(filter).SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return await _collection.Find(predicate).ToListAsync();
    }

    public async Task AddAsync(TEntity entity)
    {
        // Consider handling potential duplicate key errors if Id is not always unique before insert
        await _collection.InsertOneAsync(entity);
    }

    public async Task AddRangeAsync(IEnumerable<TEntity> entities)
    {
        if (entities.Any())
        {
            await _collection.InsertManyAsync(entities);
        }
    }

    public async Task<bool> UpdateAsync(TEntity entity)
    {
        var filter = Builders<TEntity>.Filter.Eq(doc => doc.Id, entity.Id);
        var result = await _collection.ReplaceOneAsync(filter, entity);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task<bool> RemoveAsync(Guid id)
    {
        var filter = Builders<TEntity>.Filter.Eq(doc => doc.Id, id);
        var result = await _collection.DeleteOneAsync(filter);
        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    public async Task<long> RemoveRangeAsync(Expression<Func<TEntity, bool>> predicate)
    {
        var result = await _collection.DeleteManyAsync(predicate);
        return result.IsAcknowledged ? result.DeletedCount : 0;
    }
}

