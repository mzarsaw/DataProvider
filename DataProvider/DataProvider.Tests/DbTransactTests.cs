using Microsoft.Data.Sqlite;
using Xunit;

#pragma warning disable CS1998 // This async method lacks 'await' operators and will run synchronously

namespace DataProvider.Tests;

/// <summary>
/// Tests for DbTransact extension methods
/// </summary>
public sealed class DbTransactTests : IDisposable
{
    private readonly string _connectionString = "Data Source=:memory:";
    private readonly SqliteConnection _connection;

    public DbTransactTests()
    {
        _connection = new SqliteConnection(_connectionString);
    }

    [Fact]
    public async Task Transact_SqliteConnection_CommitsSuccessfulTransaction()
    {
        // Arrange
        await _connection.OpenAsync();
        await CreateTestTable();

        // Act
        await _connection.Transact(async tx =>
        {
            using var command = new SqliteCommand(
                "INSERT INTO TestTable (Name) VALUES ('Test1')",
                _connection,
                tx as SqliteTransaction
            );
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        });

        // Assert
        using var selectCommand = new SqliteCommand("SELECT COUNT(*) FROM TestTable", _connection);
        var count = Convert.ToInt32(
            await selectCommand.ExecuteScalarAsync(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Transact_SqliteConnection_RollsBackFailedTransaction()
    {
        // Arrange
        await _connection.OpenAsync();
        await CreateTestTable();

        // Act & Assert
        await Assert.ThrowsAsync<SqliteException>(async () =>
        {
            await _connection
                .Transact(async tx =>
                {
                    using var command1 = new SqliteCommand(
                        "INSERT INTO TestTable (Name) VALUES ('Test1')",
                        _connection,
                        tx as SqliteTransaction
                    );
                    await command1.ExecuteNonQueryAsync().ConfigureAwait(false);

                    // This will fail due to constraint violation (assuming we make Name unique)
                    using var command2 = new SqliteCommand(
                        "INSERT INTO InvalidTable (Name) VALUES ('Test2')",
                        _connection,
                        tx as SqliteTransaction
                    );
                    await command2.ExecuteNonQueryAsync().ConfigureAwait(false);
                })
                .ConfigureAwait(false);
        });

        // Assert - should be 0 because transaction was rolled back
        using var selectCommand = new SqliteCommand("SELECT COUNT(*) FROM TestTable", _connection);
        var count = Convert.ToInt32(
            await selectCommand.ExecuteScalarAsync(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Transact_SqliteConnection_WithReturnValue_ReturnsCorrectValue()
    {
        // Arrange
        await _connection.OpenAsync();
        await CreateTestTable();

        // Act
        var result = await _connection.Transact(async tx =>
        {
            using var command = new SqliteCommand(
                "INSERT INTO TestTable (Name) VALUES ('Test1'); SELECT last_insert_rowid();",
                _connection,
                tx as SqliteTransaction
            );
            var id = await command.ExecuteScalarAsync().ConfigureAwait(false);
            return Convert.ToInt64(id, System.Globalization.CultureInfo.InvariantCulture);
        });

        // Assert
        Assert.Equal(1L, result);
    }

    [Fact]
    public async Task Transact_SqliteConnection_WithReturnValue_RollsBackOnException()
    {
        // Arrange
        await _connection.OpenAsync();
        await CreateTestTable();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _connection
                .Transact<long>(async tx =>
                {
                    using var command = new SqliteCommand(
                        "INSERT INTO TestTable (Name) VALUES ('Test1')",
                        _connection,
                        tx as SqliteTransaction
                    );
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                    throw new InvalidOperationException("Test exception");
                })
                .ConfigureAwait(false);
        });

        // Assert - should be 0 because transaction was rolled back
        using var selectCommand = new SqliteCommand("SELECT COUNT(*) FROM TestTable", _connection);
        var count = Convert.ToInt32(
            await selectCommand.ExecuteScalarAsync(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Transact_SqliteConnection_OpensConnectionIfClosed()
    {
        // Arrange
        // Connection starts closed

        // Act
        await _connection.Transact(async tx =>
        {
            await CreateTestTable(tx as SqliteTransaction).ConfigureAwait(false);
            using var command = new SqliteCommand(
                "INSERT INTO TestTable (Name) VALUES ('Test1')",
                _connection,
                tx as SqliteTransaction
            );
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        });

        // Assert
        using var selectCommand = new SqliteCommand("SELECT COUNT(*) FROM TestTable", _connection);
        var count = Convert.ToInt32(
            await selectCommand.ExecuteScalarAsync(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Transact_SqliteConnection_ThrowsOnNullConnection()
    {
        // Arrange
        SqliteConnection nullConnection = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await nullConnection.Transact(async tx => { }).ConfigureAwait(false);
        });
    }

    [Fact]
    public async Task Transact_SqliteConnection_ThrowsOnNullBody()
    {
        // Arrange
        await _connection.OpenAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _connection.Transact(null!).ConfigureAwait(false);
        });
    }

    [Fact]
    public async Task Transact_SqliteConnection_WithReturnValue_ThrowsOnNullConnection()
    {
        // Arrange
        SqliteConnection nullConnection = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await nullConnection.Transact(async tx => 42).ConfigureAwait(false);
        });
    }

    [Fact]
    public async Task Transact_SqliteConnection_WithReturnValue_ThrowsOnNullBody()
    {
        // Arrange
        await _connection.OpenAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _connection.Transact(null!).ConfigureAwait(false);
        });
    }

    private async Task CreateTestTable(SqliteTransaction? transaction = null)
    {
        using var command = new SqliteCommand(
            @"
            CREATE TABLE IF NOT EXISTS TestTable (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            )",
            _connection,
            transaction
        );
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public void Dispose() => _connection?.Dispose();
}
