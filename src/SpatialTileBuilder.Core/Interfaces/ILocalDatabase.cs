namespace SpatialTileBuilder.Core.Interfaces;

using System.Data;
using System.Threading.Tasks;

/// <summary>
/// Interface for Local SQLite Database access
/// </summary>
public interface ILocalDatabase
{
    /// <summary>
    /// Creates a new connection to the local database.
    /// </summary>
    IDbConnection CreateConnection();

    /// <summary>
    /// Initializes the database (migrations, seeding).
    /// </summary>
    Task InitializeAsync();
}
