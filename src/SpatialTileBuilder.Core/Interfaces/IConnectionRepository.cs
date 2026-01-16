namespace SpatialTileBuilder.Core.Interfaces;

using System.Collections.Generic;
using System.Threading.Tasks;
using SpatialTileBuilder.Core.Entities;

public interface IConnectionRepository
{
    Task<IEnumerable<DbConnection>> GetAllAsync();
    Task<DbConnection?> GetAsync(int id);
    Task<int> AddAsync(DbConnection connection);
    Task UpdateAsync(DbConnection connection);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(string name);
}
