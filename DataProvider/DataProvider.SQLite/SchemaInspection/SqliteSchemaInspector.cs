using System.Globalization;
using Microsoft.Data.Sqlite;
using Results;

namespace DataProvider.SQLite.SchemaInspection;

/// <summary>
/// SQLite implementation of schema inspection
/// </summary>
public sealed class SqliteSchemaInspector : ISchemaInspector
{
    private readonly string _connectionString;

    // .NET type constants
    private const string DotNetInt32 = "Int32";
    private const string DotNetInt64 = "Int64";
    private const string DotNetDouble = "Double";
    private const string DotNetString = "String";
    private const string DotNetBoolean = "Boolean";
    private const string DotNetDateTime = "DateTime";
    private const string DotNetDecimal = "Decimal";
    private const string DotNetByteArray = "Byte[]";

    // SQLite type constants
    private const string SqliteInteger = "INTEGER";
    private const string SqliteReal = "REAL";
    private const string SqliteText = "TEXT";
    private const string SqliteBlob = "BLOB";

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteSchemaInspector"/> class
    /// </summary>
    /// <param name="connectionString">SQLite connection string</param>
    public SqliteSchemaInspector(string connectionString)
    {
        _connectionString = connectionString ?? string.Empty;
    }

    /// <summary>
    /// Gets table information including columns and their metadata
    /// </summary>
    /// <param name="schema">Table schema (typically "main" for SQLite)</param>
    /// <param name="tableName">Table name</param>
    /// <returns>Table information or null if not found</returns>
    public async Task<DatabaseTable?> GetTableAsync(string schema, string tableName)
    {
        if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(tableName))
            return null;

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var columns = await GetColumnsAsync(connection, tableName).ConfigureAwait(false);

            if (columns.Count == 0)
            {
                return null;
            }

            return new DatabaseTable
            {
                Schema = schema,
                Name = tableName,
                Columns = columns,
            };
        }
        catch (SqliteException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets all tables in the database
    /// </summary>
    /// <returns>List of all tables</returns>
    public async Task<IReadOnlyList<DatabaseTable>> GetAllTablesAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var tables = new List<DatabaseTable>();
            var tableNames = await GetTableNamesAsync(connection).ConfigureAwait(false);

            foreach (var tableName in tableNames)
            {
                var columns = await GetColumnsAsync(connection, tableName).ConfigureAwait(false);

                tables.Add(
                    new DatabaseTable
                    {
                        Schema = "main",
                        Name = tableName,
                        Columns = columns,
                    }
                );
            }

            return tables.AsReadOnly();
        }
        catch (SqliteException)
        {
            return new List<DatabaseTable>().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets metadata about the columns returned by a SQL query
    /// </summary>
    /// <param name="sqlQuery">The SQL query to analyze</param>
    /// <returns>Result containing metadata about the query result columns</returns>
    public async Task<Result<SqlQueryMetadata, SqlError>> GetSqlQueryMetadataAsync(string sqlQuery)
    {
        ArgumentNullException.ThrowIfNull(sqlQuery);

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            // Execute the query with LIMIT 0 to get schema without data
            // Note: sqlQuery comes from compile-time source generation, not user input
            var schemaQuery = $"SELECT * FROM ({sqlQuery}) LIMIT 0";
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
            using var command = new SqliteCommand(schemaQuery, connection);
#pragma warning restore CA2100
            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

            var columns = new List<DatabaseColumn>();
            var schemaTable = await reader.GetSchemaTableAsync().ConfigureAwait(false);

            if (schemaTable != null)
            {
                foreach (System.Data.DataRow row in schemaTable.Rows)
                {
                    var columnName = row["ColumnName"]?.ToString() ?? string.Empty;
                    var dataType = row["DataType"] as Type;
                    var isNullable = row["AllowDBNull"] as bool? ?? true;
                    var maxLength = row["ColumnSize"] as int?;

                    // Map .NET type to SQLite type string
                    var sqlType = dataType?.Name switch
                    {
                        DotNetInt32 => SqliteInteger,
                        DotNetInt64 => SqliteInteger,
                        DotNetDouble => SqliteReal,
                        DotNetString => SqliteText,
                        DotNetBoolean => SqliteInteger,
                        DotNetDateTime => SqliteText,
                        DotNetDecimal => SqliteReal,
                        DotNetByteArray => SqliteBlob,
                        _ => SqliteText,
                    };

                    var csharpType = SqliteTypeToCSharpType(sqlType, isNullable);

                    columns.Add(
                        new DatabaseColumn
                        {
                            Name = columnName,
                            SqlType = sqlType,
                            CSharpType = csharpType,
                            IsNullable = isNullable,
                            IsPrimaryKey = false, // Query results don't have primary keys
                            IsIdentity = false,
                            IsComputed = false,
                            MaxLength = maxLength,
                            Precision = null,
                            Scale = null,
                        }
                    );
                }
            }

            var metadata = new SqlQueryMetadata
            {
                Columns = columns.AsReadOnly(),
                SqlText = sqlQuery,
            };

            return new Result<SqlQueryMetadata, SqlError>.Success(metadata);
        }
        catch (SqliteException ex)
        {
            return new Result<SqlQueryMetadata, SqlError>.Failure(
                new SqlError("SQLite error during schema inspection", ex)
            );
        }
        catch (Exception ex)
        {
            return new Result<SqlQueryMetadata, SqlError>.Failure(
                new SqlError("Error analyzing SQL query", ex)
            );
        }
    }

    private static async Task<IReadOnlyList<string>> GetTableNamesAsync(SqliteConnection connection)
    {
        const string sql = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name
            """;

        using var command = new SqliteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        var tables = new List<string>();

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            tables.Add(reader.GetString(0));
        }

        return tables.AsReadOnly();
    }

    private static async Task<IReadOnlyList<DatabaseColumn>> GetColumnsAsync(
        SqliteConnection connection,
        string tableName
    )
    {
        // Get column information using PRAGMA table_info
        // Note: PRAGMA commands don't support parameters, but tableName comes from database schema
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
        var sql = string.Create(CultureInfo.InvariantCulture, $"PRAGMA table_info({tableName})");
        using var command = new SqliteCommand(sql, connection);
#pragma warning restore CA2100
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        var columns = new List<DatabaseColumn>();

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var columnName = reader.GetString(1); // name
            var dataType = reader.GetString(2); // type
            var isNullable = reader.GetInt32(3) == 0; // notnull (0 means nullable)
            var defaultValue = await reader.IsDBNullAsync(4).ConfigureAwait(false)
                ? null
                : reader.GetString(4); // dflt_value
            var isPrimaryKey = reader.GetInt32(5) == 1; // pk

            // SQLite doesn't have explicit identity columns, but we can check for INTEGER PRIMARY KEY
            var isIdentity =
                isPrimaryKey
                && dataType.Equals(SqliteInteger, StringComparison.OrdinalIgnoreCase)
                && columns.Count == 0; // Only first column can be ROWID alias

            var csharpType = SqliteTypeToCSharpType(dataType, isNullable);

            columns.Add(
                new DatabaseColumn
                {
                    Name = columnName,
                    SqlType = dataType,
                    CSharpType = csharpType,
                    IsNullable = isNullable,
                    IsPrimaryKey = isPrimaryKey,
                    IsIdentity = isIdentity,
                    IsComputed = false, // SQLite doesn't have computed columns in the traditional sense
                    MaxLength = ExtractMaxLength(dataType),
                    Precision = ExtractPrecision(dataType),
                    Scale = ExtractScale(dataType),
                }
            );
        }

        return columns.AsReadOnly();
    }

    private static string SqliteTypeToCSharpType(string sqlType, bool isNullable)
    {
        // SQLite has dynamic typing, but we can infer from type affinity
        var lowerType = sqlType.ToLowerInvariant();

        var csharpType = lowerType switch
        {
            var t when t.Contains("int", StringComparison.Ordinal) => "int",
            var t
                when t.Contains("real", StringComparison.Ordinal)
                    || t.Contains("float", StringComparison.Ordinal)
                    || t.Contains("double", StringComparison.Ordinal) => "double",
            var t
                when t.Contains("decimal", StringComparison.Ordinal)
                    || t.Contains("numeric", StringComparison.Ordinal) => "decimal",
            var t when t.Contains("bool", StringComparison.Ordinal) => "bool",
            var t
                when t.Contains("date", StringComparison.Ordinal)
                    || t.Contains("time", StringComparison.Ordinal) => "DateTime",
            var t when t.Contains("blob", StringComparison.Ordinal) => "byte[]",
            var t
                when t.Contains("text", StringComparison.Ordinal)
                    || t.Contains("char", StringComparison.Ordinal)
                    || t.Contains("varchar", StringComparison.Ordinal) => "string",
            _ => "string", // Default to string for unknown types
        };

        // Add nullability for value types
        if (isNullable && csharpType != "string" && csharpType != "byte[]")
        {
            csharpType += "?";
        }

        return csharpType;
    }

    private static int? ExtractMaxLength(string dataType)
    {
        // Extract length from types like VARCHAR(50)
        var openParen = dataType.IndexOf('(', StringComparison.Ordinal);
        var closeParen = dataType.IndexOf(')', StringComparison.Ordinal);

        if (openParen > 0 && closeParen > openParen)
        {
            var lengthStr = dataType.Substring(openParen + 1, closeParen - openParen - 1);
            if (int.TryParse(lengthStr, out var length))
            {
                return length;
            }
        }

        return null;
    }

    private static int? ExtractPrecision(string dataType)
    {
        // Extract precision from types like DECIMAL(10,2)
        var openParen = dataType.IndexOf('(', StringComparison.Ordinal);
        var comma = dataType.IndexOf(',', StringComparison.Ordinal);

        if (openParen > 0 && comma > openParen)
        {
            var precisionStr = dataType.Substring(openParen + 1, comma - openParen - 1);
            if (int.TryParse(precisionStr, out var precision))
            {
                return precision;
            }
        }

        return null;
    }

    private static int? ExtractScale(string dataType)
    {
        // Extract scale from types like DECIMAL(10,2)
        var comma = dataType.IndexOf(',', StringComparison.Ordinal);
        var closeParen = dataType.IndexOf(')', StringComparison.Ordinal);

        if (comma > 0 && closeParen > comma)
        {
            var scaleStr = dataType.Substring(comma + 1, closeParen - comma - 1);
            if (int.TryParse(scaleStr, out var scale))
            {
                return scale;
            }
        }

        return null;
    }
}
