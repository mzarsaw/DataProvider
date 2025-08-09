using Microsoft.Data.Sqlite;

namespace DataProvider.SQLite;

/// <summary>
/// Extension methods for <see cref="SqliteConnection"/>.
/// </summary>
public static class SqliteExtensions
{
    /// <summary>
    /// Executes a non-query SQL command asynchronously.
    /// </summary>
    /// <param name="connection">The SQLite connection.</param>
    /// <param name="sql">The SQL to execute.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ExecuteNonQueryAsync(this SqliteConnection connection, string sql)
    {
# pragma warning disable CA2100
        using var command = new SqliteCommand(sql, connection);
# pragma warning restore CA2100
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
