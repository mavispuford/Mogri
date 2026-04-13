using System.Collections.Generic;
using System.Threading.Tasks;
using LiteDB;
using Mogri.Models;

namespace Mogri.Interfaces.Services;

/// <summary>
/// Provides CRUD operations for local prompt styles.
/// </summary>
public interface IPromptStyleService
{
    Task<List<PromptStyleEntity>> GetAllAsync();
    Task SaveAsync(PromptStyleEntity entity);
    Task DeleteAsync(ObjectId id);
    Task SeedDefaultsIfEmptyAsync();
}