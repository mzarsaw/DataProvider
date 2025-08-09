using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Data.Sqlite;
using Results;
using Selecta;

namespace DataProvider.SQLite;

/// <summary>
/// SQLite specific code generator implementation and incremental source generator entrypoint
/// </summary>
[Generator]
public sealed class SqliteCodeGenerator : IIncrementalGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Generates code for SQL queries without metadata
    /// </summary>
    /// <param name="fileName">The SQL file name.</param>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="statement">The parsed SQL statement.</param>
    /// <param name="hasCustomImplementation">Whether a custom implementation exists.</param>
    /// <param name="groupingConfig">Optional grouping configuration.</param>
    /// <returns>A result containing the generated code or an error.</returns>
    public static Result<string, SqlError> GenerateCode(
        string fileName,
        string sql,
        SqlStatement statement,
        bool hasCustomImplementation,
        GroupingConfig? groupingConfig = null
    )
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return new Result<string, SqlError>.Failure(
                new SqlError("fileName cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<string, SqlError>.Failure(
                new SqlError("sql cannot be null or empty")
            );

        if (statement == null)
            return new Result<string, SqlError>.Failure(new SqlError("statement cannot be null"));

        _ = hasCustomImplementation; // Suppress unused parameter warning
        _ = groupingConfig; // Suppress unused parameter warning

        // This is the fallback method - should not be used when database metadata is available
        return new Result<string, SqlError>.Failure(
            new SqlError(
                "Use GenerateCodeWithMetadata instead for proper database-driven code generation"
            )
        );
    }

    /// <summary>
    /// Generates C# source for a SQL file using real database metadata.
    /// </summary>
    /// <param name="fileName">The SQL file name.</param>
    /// <param name="sql">The SQL content.</param>
    /// <param name="statement">The parsed SQL statement metadata.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="columnMetadata">Real column metadata from the database.</param>
    /// <param name="hasCustomImplementation">Whether a custom implementation exists.</param>
    /// <param name="groupingConfig">Optional grouping configuration for parent-child relationships.</param>
    /// <returns>Result with generated source or an error.</returns>
    public static Result<string, SqlError> GenerateCodeWithMetadata(
        string fileName,
        string sql,
        SqlStatement statement,
        string connectionString,
        IReadOnlyList<DatabaseColumn> columnMetadata,
        bool hasCustomImplementation,
        GroupingConfig? groupingConfig = null
    )
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return new Result<string, SqlError>.Failure(
                new SqlError("fileName cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<string, SqlError>.Failure(
                new SqlError("sql cannot be null or empty")
            );

        if (statement == null)
            return new Result<string, SqlError>.Failure(new SqlError("statement cannot be null"));

        if (string.IsNullOrWhiteSpace(connectionString))
            return new Result<string, SqlError>.Failure(
                new SqlError("connectionString cannot be null or empty")
            );

        if (columnMetadata == null)
            return new Result<string, SqlError>.Failure(
                new SqlError("columnMetadata cannot be null")
            );

        _ = hasCustomImplementation; // Suppress unused parameter warning

        // If grouping is configured, generate grouped version
        if (groupingConfig != null)
        {
            var groupedResult = GenerateGroupedVersionWithMetadata(
                fileName,
                sql,
                statement,
                columnMetadata,
                groupingConfig
            );
            return groupedResult;
        }

        var className = string.Create(CultureInfo.InvariantCulture, $"{fileName}Extensions");
        var parameterList = GenerateParameterList(statement.Parameters);

        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Microsoft.Data.Sqlite;");
        sb.AppendLine("using Results;");
        sb.AppendLine();
        sb.AppendLine("namespace Generated;");
        sb.AppendLine();

        // Generate extension class
        sb.AppendLine("/// <summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"/// Extension methods for '{fileName}'.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"public static partial class {className}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Executes '{fileName}.sql' and maps results."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"connection\">Open SQLite connection.</param>");
        foreach (var p in statement.Parameters)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    /// <param name=\"{p.Name}\">SQL parameter '@{p.Name}'.</param>"
            );
        }
        sb.AppendLine("    /// <returns>Result with the materialized rows.</returns>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<ImmutableList<{fileName}>, SqlError>> {fileName}Async(this SqliteConnection connection, {parameterList})"
        );
        sb.AppendLine("    {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        const string sql = @\"{sql.Replace("\"", "\"\"", StringComparison.Ordinal)}\";"
        );
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            var results = ImmutableList.CreateBuilder<{fileName}>();"
        );
        sb.AppendLine();
        sb.AppendLine("            using (var command = new SqliteCommand(sql, connection))");
        sb.AppendLine("            {");

        // Add parameters
        foreach (var parameter in statement.Parameters)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                command.Parameters.AddWithValue(\"@{parameter.Name}\", {parameter.Name});"
            );
        }

        sb.AppendLine();
        sb.AppendLine(
            "                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))"
        );
        sb.AppendLine("                {");
        sb.AppendLine("                    while (await reader.ReadAsync().ConfigureAwait(false))");
        sb.AppendLine("                    {");

        // Generate record constructor using real column metadata
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                        var item = new {fileName}("
        );

        for (int i = 0; i < columnMetadata.Count; i++)
        {
            var column = columnMetadata[i];
            var isLast = i == columnMetadata.Count - 1;
            var comma = isLast ? "" : ",";

            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                            reader.IsDBNull({i}) ? ({column.CSharpType}){(column.IsNullable ? "null" : "default")} : ({column.CSharpType})reader.GetValue({i}){comma}"
            );
        }

        sb.AppendLine("                        );");
        sb.AppendLine("                        results.Add(item);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new Result<ImmutableList<{fileName}>, SqlError>.Success(results.ToImmutable());"
        );
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new Result<ImmutableList<{fileName}>, SqlError>.Failure(new SqlError(\"Database error\", ex));"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Generate record based on actual database columns with XML docs
        sb.AppendLine("/// <summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"/// Result row for '{fileName}' query.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"public record {fileName}");
        sb.AppendLine("{");
        for (int i = 0; i < columnMetadata.Count; i++)
        {
            var column = columnMetadata[i];
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    /// <summary>Column '{column.Name}'.</summary>"
            );
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    public {column.CSharpType} {column.Name} {{ get; init; }}"
            );
            sb.AppendLine();
        }
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <summary>Initializes a new instance of {fileName}.</summary>"
        );
        // Constructor signature
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public {fileName}(");
        for (int i = 0; i < columnMetadata.Count; i++)
        {
            var column = columnMetadata[i];
            var isLast = i == columnMetadata.Count - 1;
            var comma = isLast ? "" : ",";
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        {column.CSharpType} {column.Name}{comma}"
            );
        }
        sb.AppendLine("    )");
        sb.AppendLine("    {");
        for (int i = 0; i < columnMetadata.Count; i++)
        {
            var column = columnMetadata[i];
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        this.{column.Name} = {column.Name};"
            );
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new Result<string, SqlError>.Success(sb.ToString());
    }

    /// <summary>
    /// Generate C# source code for Insert/Update operations based on table configuration
    /// </summary>
    /// <param name="table">The database table metadata</param>
    /// <param name="config">The table configuration</param>
    /// <returns>The generated C# source code</returns>
    public static Result<string, SqlError> GenerateTableOperations(
        DatabaseTable table,
        TableConfig config
    )
    {
        if (table == null)
            return new Result<string, SqlError>.Failure(new SqlError("table cannot be null"));

        if (config == null)
            return new Result<string, SqlError>.Failure(new SqlError("config cannot be null"));

        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Microsoft.Data.Sqlite;");
        sb.AppendLine("using Results;");
        sb.AppendLine();
        sb.AppendLine("namespace Generated");
        sb.AppendLine("{");

        var className = string.Create(CultureInfo.InvariantCulture, $"{table.Name}Extensions");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public static partial class {className}");
        sb.AppendLine("    {");

        if (config.GenerateInsert)
        {
            GenerateInsertMethod(sb, table);
        }

        if (config.GenerateUpdate)
        {
            GenerateUpdateMethod(sb, table);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new Result<string, SqlError>.Success(sb.ToString());
    }

    private static void GenerateInsertMethod(StringBuilder sb, DatabaseTable table)
    {
        var insertableColumns = table.InsertableColumns;
        if (insertableColumns.Count == 0)
        {
            return;
        }

        var parameterList = string.Join(
            ", ",
            insertableColumns.Select(c =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{c.CSharpType} {c.Name.ToLowerInvariant()}"
                )
            )
        );

        sb.AppendLine();
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        public static async Task<Result<long, SqlError>> Insert{table.Name}Async(this SqliteConnection connection, {parameterList})"
        );
        sb.AppendLine("        {");

        // Generate INSERT SQL
        var columnNames = string.Join(", ", insertableColumns.Select(c => c.Name));
        var parameterNames = string.Join(", ", insertableColumns.Select(c => $"@{c.Name}"));

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            const string sql = \"INSERT INTO {table.Name} ({columnNames}) VALUES ({parameterNames}); SELECT last_insert_rowid()\";"
        );
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                using (var command = new SqliteCommand(sql, connection))");
        sb.AppendLine("                {");

        // Add parameters
        foreach (var column in insertableColumns)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                    command.Parameters.AddWithValue(\"@{column.Name}\", {column.Name.ToLowerInvariant()});"
            );
        }

        sb.AppendLine();
        sb.AppendLine(
            "                    var result = await command.ExecuteScalarAsync().ConfigureAwait(false);"
        );
        sb.AppendLine(
            "                    var newId = Convert.ToInt64(result, CultureInfo.InvariantCulture);"
        );
        sb.AppendLine("                    return new Result<long, SqlError>.Success(newId);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine(
            "                return new Result<long, SqlError>.Failure(new SqlError(\"Insert failed\", ex));"
        );
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    private static void GenerateUpdateMethod(StringBuilder sb, DatabaseTable table)
    {
        var updateableColumns = table.UpdateableColumns;
        var primaryKeyColumns = table.PrimaryKeyColumns;

        if (updateableColumns.Count == 0 || primaryKeyColumns.Count == 0)
        {
            return;
        }

        var allColumns = primaryKeyColumns.Concat(updateableColumns).ToList();
        var parameterList = string.Join(
            ", ",
            allColumns.Select(c =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{c.CSharpType} {c.Name.ToLowerInvariant()}"
                )
            )
        );

        sb.AppendLine();
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        public static async Task<Result<int, SqlError>> Update{table.Name}Async(this SqliteConnection connection, {parameterList})"
        );
        sb.AppendLine("        {");

        // Generate UPDATE SQL
        var setClause = string.Join(", ", updateableColumns.Select(c => $"{c.Name} = @{c.Name}"));
        var whereClause = string.Join(
            " AND ",
            primaryKeyColumns.Select(c => $"{c.Name} = @{c.Name}")
        );

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            const string sql = \"UPDATE {table.Name} SET {setClause} WHERE {whereClause}\";"
        );
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                using (var command = new SqliteCommand(sql, connection))");
        sb.AppendLine("                {");

        // Add parameters
        foreach (var column in allColumns)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                    command.Parameters.AddWithValue(\"@{column.Name}\", {column.Name.ToLowerInvariant()});"
            );
        }

        sb.AppendLine();
        sb.AppendLine(
            "                    var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);"
        );
        sb.AppendLine(
            "                    return new Result<int, SqlError>.Success(rowsAffected);"
        );
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex)");
        sb.AppendLine("            {");
        sb.AppendLine(
            "                return new Result<int, SqlError>.Failure(new SqlError(\"Update failed\", ex));"
        );
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Gets column metadata by executing the SQL query against the database.
    /// This is the proper way to get column types - by executing the query and checking metadata.
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="sql">SQL query to execute</param>
    /// <param name="parameters">Query parameters</param>
    /// <returns>List of database columns with their metadata</returns>
    public static async Task<
        Result<IReadOnlyList<DatabaseColumn>, SqlError>
    > GetColumnMetadataFromSqlAsync(
        string connectionString,
        string sql,
        IEnumerable<ParameterInfo> parameters
    )
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Failure(
                new SqlError("connectionString cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Failure(
                new SqlError("sql cannot be null or empty")
            );

        if (parameters == null)
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Failure(
                new SqlError("parameters cannot be null")
            );

        var columns = new List<DatabaseColumn>();

        try
        {
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            // The SQL is provided by the developer at compile time, not user input
# pragma warning disable CA2100
            using var command = new SqliteCommand(sql, connection);
# pragma warning restore CA2100

            // Add parameters with dummy values to get schema
            foreach (var param in parameters)
            {
                var dummyValue = GetDummyValueForParameter(param);
                command.Parameters.AddWithValue($"@{param.Name}", dummyValue);
            }

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

            // Get column schema from the reader
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var fieldType = reader.GetFieldType(i);
                var dataTypeName = reader.GetDataTypeName(i);

                var csharpType = MapSqliteTypeToCSharpType(fieldType);

                columns.Add(
                    new DatabaseColumn
                    {
                        Name = columnName,
                        SqlType = dataTypeName,
                        CSharpType = csharpType,
                        IsNullable =
                            !fieldType.IsValueType || Nullable.GetUnderlyingType(fieldType) != null,
                        IsPrimaryKey = false, // Cannot determine from query result
                        IsIdentity = false, // Cannot determine from query result
                        IsComputed = false, // Cannot determine from query result
                    }
                );
            }
        }
        catch (SqliteException ex)
        {
            // If we can't execute the query, return error
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Failure(
                new SqlError("Failed to get column metadata", ex)
            );
        }
        catch (Exception ex)
        {
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Failure(
                new SqlError("Unexpected error getting column metadata", ex)
            );
        }

        return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Success(columns.AsReadOnly());
    }

    /// <summary>
    /// Maps SQLite data types to C# types
    /// </summary>
    /// <param name="fieldType">The .NET field type</param>
    /// <returns>C# type name</returns>
    private static string MapSqliteTypeToCSharpType(Type fieldType)
    {
        // Use the .NET type first as it's more accurate
        if (fieldType == typeof(int) || fieldType == typeof(int?))
            return fieldType == typeof(int?) ? "int?" : "int";

        if (fieldType == typeof(long) || fieldType == typeof(long?))
            return fieldType == typeof(long?) ? "long?" : "long";

        if (fieldType == typeof(double) || fieldType == typeof(double?))
            return fieldType == typeof(double?) ? "double?" : "double";

        if (fieldType == typeof(decimal) || fieldType == typeof(decimal?))
            return fieldType == typeof(decimal?) ? "decimal?" : "decimal";

        if (fieldType == typeof(bool) || fieldType == typeof(bool?))
            return fieldType == typeof(bool?) ? "bool?" : "bool";

        if (fieldType == typeof(DateTime) || fieldType == typeof(DateTime?))
            return fieldType == typeof(DateTime?) ? "DateTime?" : "DateTime";

        if (fieldType == typeof(string))
            return "string";

        if (fieldType == typeof(byte[]))
            return "byte[]";

        if (fieldType == typeof(Guid) || fieldType == typeof(Guid?))
            return fieldType == typeof(Guid?) ? "Guid?" : "Guid";

        // Fall back to string for unknown types
        return "string";
    }

    /// <summary>
    /// Gets a dummy value for a parameter based on its name for schema discovery
    /// </summary>
    /// <param name="parameter">Parameter info</param>
    /// <returns>Dummy value</returns>
    private static object GetDummyValueForParameter(ParameterInfo parameter)
    {
        var lowerName = parameter.Name.ToLowerInvariant();

        return lowerName switch
        {
            var name when name.Contains("id", StringComparison.Ordinal) => 1,
            var name when name.Contains("count", StringComparison.Ordinal) => 1,
            var name when name.Contains("quantity", StringComparison.Ordinal) => 1,
            var name when name.Contains("amount", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("price", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("total", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("percentage", StringComparison.Ordinal) => 1.0m,
            var name when name.Contains("date", StringComparison.Ordinal) => DateTime.UtcNow,
            var name when name.Contains("time", StringComparison.Ordinal) => DateTime.UtcNow,
            var name when name.Contains("created", StringComparison.Ordinal) => DateTime.UtcNow,
            var name when name.Contains("updated", StringComparison.Ordinal) => DateTime.UtcNow,
            _ => "dummy_value",
        };
    }

    private static string GenerateParameterList(IReadOnlyList<ParameterInfo> parameters)
    {
        if (parameters.Count == 0)
        {
            return "";
        }

        return string.Join(", ", parameters.Select(p => $"object {p.Name}"));
    }

    private static Result<string, SqlError> GenerateGroupedVersionWithMetadata(
        string fileName,
        string sql,
        SqlStatement statement,
        IReadOnlyList<DatabaseColumn> columnMetadata,
        GroupingConfig groupingConfig
    )
    {
        try
        {
            var className = string.Create(CultureInfo.InvariantCulture, $"{fileName}Extensions");
            var parameterList = GenerateParameterList(statement.Parameters);

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Collections.Immutable;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using Microsoft.Data.Sqlite;");
            sb.AppendLine("using Results;");
            sb.AppendLine();
            sb.AppendLine("namespace Generated;");
            sb.AppendLine();

            // Generate extension class with XML docs
            sb.AppendLine("/// <summary>");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"/// Extension methods for '{fileName}' grouped query."
            );
            sb.AppendLine("/// </summary>");
            sb.AppendLine(CultureInfo.InvariantCulture, $"public static partial class {className}");
            sb.AppendLine("{");

            // Generate method that returns grouped results
            sb.AppendLine("    /// <summary>");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    /// Executes the '{fileName}' query and groups rows into '{groupingConfig.ParentEntity.Name}' with '{groupingConfig.ChildEntity.Name}' children."
            );
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"connection\">The open SQLite connection.</param>");
            if (!string.IsNullOrWhiteSpace(parameterList))
            {
                foreach (var p in statement.Parameters)
                {
                    sb.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"    /// <param name=\"{p.Name}\">Query parameter.</param>"
                    );
                }
            }
            sb.AppendLine("    /// <returns>Result of grouped records or SQL error.</returns>");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    public static async Task<Result<ImmutableList<{groupingConfig.ParentEntity.Name}>, SqlError>> {fileName}Async(this SqliteConnection connection, {parameterList})"
            );
            sb.AppendLine("    {");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        const string sql = @\"{sql.Replace("\"", "\"\"", StringComparison.Ordinal)}\";"
            );
            sb.AppendLine();
            sb.AppendLine("        try");
            sb.AppendLine("        {");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"            var rawResults = ImmutableList.CreateBuilder<{fileName}Raw>();"
            );
            sb.AppendLine();
            sb.AppendLine("            using (var command = new SqliteCommand(sql, connection))");
            sb.AppendLine("            {");

            // Add parameters
            foreach (var parameter in statement.Parameters)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"                command.Parameters.AddWithValue(\"@{parameter.Name}\", {parameter.Name} ?? (object)DBNull.Value);"
                );
            }

            sb.AppendLine();
            sb.AppendLine(
                "                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))"
            );
            sb.AppendLine("                {");
            sb.AppendLine(
                "                    while (await reader.ReadAsync().ConfigureAwait(false))"
            );
            sb.AppendLine("                    {");

            // Generate raw record constructor using real column metadata
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                        var item = new {fileName}Raw("
            );

            for (int i = 0; i < columnMetadata.Count; i++)
            {
                var column = columnMetadata[i];
                var isLast = i == columnMetadata.Count - 1;
                var comma = isLast ? "" : ",";

                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"                            reader.IsDBNull({i}) ? ({column.CSharpType}){(column.IsNullable ? "null" : "default")} : ({column.CSharpType})reader.GetValue({i}){comma}"
                );
            }

            sb.AppendLine("                        );");
            sb.AppendLine("                        rawResults.Add(item);");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            // Group the raw results into parent-child structure");
            sb.AppendLine("            var grouped = GroupResults(rawResults.ToImmutable());");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"            return new Result<ImmutableList<{groupingConfig.ParentEntity.Name}>, SqlError>.Success(grouped);"
            );
            sb.AppendLine("        }");
            sb.AppendLine("        catch (Exception ex)");
            sb.AppendLine("        {");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"            return new Result<ImmutableList<{groupingConfig.ParentEntity.Name}>, SqlError>.Failure(new SqlError(\"Database error\", ex));"
            );
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Generate grouping method
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    private static ImmutableList<{groupingConfig.ParentEntity.Name}> GroupResults(ImmutableList<{fileName}Raw> rawResults)"
            );
            sb.AppendLine("    {");
            sb.AppendLine("        // Group raw results by parent key columns");
            sb.AppendLine("        var parentGroups = rawResults");
            sb.AppendLine("            .GroupBy(r => new { ");

            // Generate grouping key based on parent key columns
            for (int i = 0; i < groupingConfig.ParentEntity.KeyColumns.Count; i++)
            {
                var keyColumn = groupingConfig.ParentEntity.KeyColumns[i];
                var isLast = i == groupingConfig.ParentEntity.KeyColumns.Count - 1;
                var comma = isLast ? "" : ",";
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"                {keyColumn} = r.{keyColumn}{comma}"
                );
            }

            sb.AppendLine("            })");
            sb.AppendLine("            .ToList();");
            sb.AppendLine();
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        var result = new List<{groupingConfig.ParentEntity.Name}>();"
            );
            sb.AppendLine("        foreach (var group in parentGroups)");
            sb.AppendLine("        {");
            sb.AppendLine("            var firstItem = group.First();");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"            var parent = new {groupingConfig.ParentEntity.Name}("
            );

            // Generate parent properties
            for (int i = 0; i < groupingConfig.ParentEntity.Columns.Count; i++)
            {
                var column = groupingConfig.ParentEntity.Columns[i];
                var isLast = i == groupingConfig.ParentEntity.Columns.Count - 1;
                var comma = isLast ? "" : ",";
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"                firstItem.{column}{comma}"
                );
            }
            // Ensure comma between last parent arg and child collection argument
            sb.AppendLine("                ,");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                group.Select(item => new {groupingConfig.ChildEntity.Name}("
            );

            // Generate child properties
            for (int i = 0; i < groupingConfig.ChildEntity.Columns.Count; i++)
            {
                var column = groupingConfig.ChildEntity.Columns[i];
                var isLast = i == groupingConfig.ChildEntity.Columns.Count - 1;
                var comma = isLast ? "" : ",";
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"                    item.{column}{comma}"
                );
            }
            sb.AppendLine("                )).ToList()");
            sb.AppendLine("            );");
            sb.AppendLine("            result.Add(parent);");
            sb.AppendLine("        }");
            sb.AppendLine("        return result.ToImmutableList();");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Generate raw data record using real column metadata
            sb.AppendLine(CultureInfo.InvariantCulture, $"internal record {fileName}Raw(");
            for (int i = 0; i < columnMetadata.Count; i++)
            {
                var column = columnMetadata[i];
                var isLast = i == columnMetadata.Count - 1;
                var comma = isLast ? "" : ",";

                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    {column.CSharpType} {column.Name}{comma}"
                );
            }
            sb.AppendLine(");");
            sb.AppendLine();

            // Generate parent entity with children (property-based record with XML docs)
            var childCollectionName = string.Create(
                CultureInfo.InvariantCulture,
                $"{groupingConfig.ChildEntity.Name}s"
            );
            sb.AppendLine("/// <summary>");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"/// Represents a '{groupingConfig.ParentEntity.Name}' with '{childCollectionName}'."
            );
            sb.AppendLine("/// </summary>");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"public record {groupingConfig.ParentEntity.Name}"
            );
            sb.AppendLine("{");
            foreach (var col in groupingConfig.ParentEntity.Columns)
            {
                var columnMetadataItem = columnMetadata.FirstOrDefault(c => c.Name == col);
                var csharpType = columnMetadataItem?.CSharpType ?? "object";
                sb.AppendLine("    /// <summary>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    /// Gets the '{col}'.");
                sb.AppendLine("    /// </summary>");
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    public {csharpType} {col} {{ get; init; }}"
                );
            }
            sb.AppendLine("    /// <summary>");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    /// Gets the related '{childCollectionName}'."
            );
            sb.AppendLine("    /// </summary>");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    public IReadOnlyList<{groupingConfig.ChildEntity.Name}> {childCollectionName} {{ get; init; }}"
            );
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    /// Initializes a new instance of <see cref=\"{groupingConfig.ParentEntity.Name}\"/>."
            );
            sb.AppendLine("    /// </summary>");
            foreach (var col in groupingConfig.ParentEntity.Columns)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    /// <param name=\"{col}\">The '{col}'.</param>"
                );
            }
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    /// <param name=\"{childCollectionName}\">The related '{childCollectionName}'.</param>"
            );
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    public {groupingConfig.ParentEntity.Name}("
            );
            for (int i = 0; i < groupingConfig.ParentEntity.Columns.Count; i++)
            {
                var column = groupingConfig.ParentEntity.Columns[i];
                var columnMetadataItem = columnMetadata.FirstOrDefault(c => c.Name == column);
                var csharpType = columnMetadataItem?.CSharpType ?? "object";
                sb.AppendLine(CultureInfo.InvariantCulture, $"        {csharpType} {column},");
            }
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        IReadOnlyList<{groupingConfig.ChildEntity.Name}> {childCollectionName}"
            );
            sb.AppendLine("    )");
            sb.AppendLine("    {");
            foreach (var col in groupingConfig.ParentEntity.Columns)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        this.{col} = {col};");
            }
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        this.{childCollectionName} = {childCollectionName};"
            );
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            // Generate child entity (property-based with XML docs)
            sb.AppendLine("/// <summary>");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"/// Represents a '{groupingConfig.ChildEntity.Name}'."
            );
            sb.AppendLine("/// </summary>");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"public record {groupingConfig.ChildEntity.Name}"
            );
            sb.AppendLine("{");
            foreach (var col in groupingConfig.ChildEntity.Columns)
            {
                var columnMetadataItem = columnMetadata.FirstOrDefault(c => c.Name == col);
                var csharpType = columnMetadataItem?.CSharpType ?? "object";
                sb.AppendLine("    /// <summary>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    /// Gets the '{col}'.");
                sb.AppendLine("    /// </summary>");
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    public {csharpType} {col} {{ get; init; }}"
                );
            }
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    /// Initializes a new instance of <see cref=\"{groupingConfig.ChildEntity.Name}\"/>."
            );
            sb.AppendLine("    /// </summary>");
            foreach (var col in groupingConfig.ChildEntity.Columns)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    /// <param name=\"{col}\">The '{col}'.</param>"
                );
            }
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    public {groupingConfig.ChildEntity.Name}("
            );
            for (int i = 0; i < groupingConfig.ChildEntity.Columns.Count; i++)
            {
                var column = groupingConfig.ChildEntity.Columns[i];
                var columnMetadataItem = columnMetadata.FirstOrDefault(c => c.Name == column);
                var csharpType = columnMetadataItem?.CSharpType ?? "object";
                var comma = i == groupingConfig.ChildEntity.Columns.Count - 1 ? string.Empty : ",";
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"        {csharpType} {column}{comma}"
                );
            }
            sb.AppendLine("    )");
            sb.AppendLine("    {");
            foreach (var col in groupingConfig.ChildEntity.Columns)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        this.{col} = {col};");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return new Result<string, SqlError>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return new Result<string, SqlError>.Failure(
                new SqlError("Error generating grouped version", ex)
            );
        }
    }

    // =============================
    // Incremental generator wiring
    // =============================

    /// <summary>
    /// Initializes the incremental generator for SQLite. Collects .sql files, grouping config, and DataProvider.json
    /// and registers the output step.
    /// </summary>
    /// <param name="context">The initialization context</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additional = context.AdditionalTextsProvider;

        var sqlFiles = additional.Where(at =>
            at.Path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
        );
        var configFiles = additional.Where(at =>
            at.Path.EndsWith("DataProvider.json", StringComparison.OrdinalIgnoreCase)
        );
        var groupingFiles = additional.Where(at =>
            at.Path.EndsWith(".grouping.json", StringComparison.OrdinalIgnoreCase)
        );

        var sqlCollected = sqlFiles.Collect();
        var configCollected = configFiles.Collect();
        var groupingCollected = groupingFiles.Collect();

        var left = sqlCollected.Combine(configCollected);
        var all = left.Combine(groupingCollected);

        context.RegisterSourceOutput(all, GenerateCodeForAllSqlFiles);
    }

    private static void GenerateCodeForAllSqlFiles(
        SourceProductionContext context,
        (
            (
                ImmutableArray<AdditionalText> SqlFiles,
                ImmutableArray<AdditionalText> ConfigFiles
            ) Left,
            ImmutableArray<AdditionalText> GroupingFiles
        ) data
    )
    {
        var (left, groupingFiles) = data;
        var (sqlFiles, configFiles) = left;

        // Read configuration
        SourceGeneratorDataProviderConfiguration? config = null;
        if (configFiles.Length > 0)
        {
            var configContent = configFiles[0].GetText()?.ToString();
            if (!string.IsNullOrEmpty(configContent))
            {
                try
                {
                    config = JsonSerializer.Deserialize<SourceGeneratorDataProviderConfiguration>(
                        configContent!,
                        JsonOptions
                    );
                }
                catch (JsonException ex)
                {
                    var diag = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DataProvider002",
                            "Configuration parsing failed",
                            "Failed to parse DataProvider.json: {0}",
                            "DataProvider",
                            DiagnosticSeverity.Error,
                            true
                        ),
                        Location.None,
                        ex.Message
                    );
                    context.ReportDiagnostic(diag);
                    return;
                }
            }
        }

        if (config == null || string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            var diag = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "DataProvider003",
                    "Configuration missing",
                    "DataProvider.json with ConnectionString is required for code generation",
                    "DataProvider",
                    DiagnosticSeverity.Error,
                    true
                ),
                Location.None
            );
            context.ReportDiagnostic(diag);
            return;
        }

        // Build lookup for grouping configs by base filename (strip ".grouping.json")
        var groupingByBase = groupingFiles
            .Select(g =>
            {
                var fileName = Path.GetFileName(g.Path);
                var baseName = fileName.EndsWith(
                    ".grouping.json",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? fileName[..^".grouping.json".Length]
                    : Path.GetFileNameWithoutExtension(fileName);
                return new { Text = g, Base = baseName };
            })
            .ToLookup(x => x.Base, x => x.Text);

        var parser = new Parsing.SqliteAntlrParser();

        foreach (var sqlFile in sqlFiles)
        {
            try
            {
                var sqlText =
                    sqlFile.GetText(context.CancellationToken)?.ToString() ?? string.Empty;
                var baseName = Path.GetFileNameWithoutExtension(sqlFile.Path);
                // Normalize base name to ignore optional ".generated" suffix from intermediate files
                if (baseName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase))
                {
                    baseName = baseName[..^".generated".Length];
                }

                if (string.IsNullOrWhiteSpace(sqlText))
                {
                    continue;
                }

                // Parse SQL (attach any unexpected parser errors to the SQL file)
                var statement = parser.ParseSql(sqlText);

                // Discover real column metadata by executing the SQL against the DB
                var columnsResult = GetColumnMetadataFromSqlAsync(
                        config.ConnectionString,
                        sqlText,
                        statement.Parameters
                    )
                    .GetAwaiter()
                    .GetResult();

                if (
                    columnsResult
                    is not Result<IReadOnlyList<DatabaseColumn>, SqlError>.Success colSuccess
                )
                {
                    var err = (
                        columnsResult as Result<IReadOnlyList<DatabaseColumn>, SqlError>.Failure
                    )!.ErrorValue;

                    // Attach the diagnostic to the start of the SQL file so IDEs show it inline
                    var text =
                        sqlFile.GetText(context.CancellationToken)
                        ?? SourceText.From(sqlText, Encoding.UTF8);
                    var span = new TextSpan(0, Math.Min(1, text.Length));
                    var lineSpan = new LinePositionSpan(
                        new LinePosition(0, 0),
                        new LinePosition(0, Math.Min(1, text.Length))
                    );
                    var location = Location.Create(sqlFile.Path, span, lineSpan);

                    var diagCol = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DL0002",
                            "Database metadata error",
                            "Failed to get column metadata for {0}: {1}",
                            "DataProvider",
                            DiagnosticSeverity.Error,
                            true
                        ),
                        location,
                        baseName,
                        err.DetailedMessage
                    );
                    context.ReportDiagnostic(diagCol);
                    continue;
                }

                // Optional grouping config for this SQL file
                GroupingConfig? groupingConfig = null;
                if (groupingByBase.Contains(baseName))
                {
                    var groupingText = groupingByBase[baseName].First().GetText()?.ToString();
                    if (!string.IsNullOrWhiteSpace(groupingText))
                    {
                        try
                        {
                            groupingConfig = JsonSerializer.Deserialize<GroupingConfig>(
                                groupingText!,
                                JsonOptions
                            );
                        }
                        catch (JsonException ex)
                        {
                            var diagGrp = Diagnostic.Create(
                                new DiagnosticDescriptor(
                                    "DataProvider004",
                                    "Grouping parsing failed",
                                    "Failed to parse {0}.grouping.json: {1}",
                                    "DataProvider",
                                    DiagnosticSeverity.Error,
                                    true
                                ),
                                Location.None,
                                baseName,
                                ex.Message
                            );
                            context.ReportDiagnostic(diagGrp);
                        }
                    }
                }

                var sourceResult = GenerateCodeWithMetadata(
                    baseName,
                    sqlText,
                    statement,
                    config.ConnectionString,
                    colSuccess.Value,
                    hasCustomImplementation: false,
                    groupingConfig
                );

                if (sourceResult is Result<string, SqlError>.Success success)
                {
                    context.AddSource(
                        baseName + ".g.cs",
                        SourceText.From(success.Value, Encoding.UTF8)
                    );
                }
                else if (sourceResult is Result<string, SqlError>.Failure failure)
                {
                    // Attach the diagnostic to the SQL file as well
                    var text =
                        sqlFile.GetText(context.CancellationToken)
                        ?? SourceText.From(sqlText, Encoding.UTF8);
                    var span = new TextSpan(0, Math.Min(1, text.Length));
                    var lineSpan = new LinePositionSpan(
                        new LinePosition(0, 0),
                        new LinePosition(0, Math.Min(1, text.Length))
                    );
                    var location = Location.Create(sqlFile.Path, span, lineSpan);

                    var diagGen = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DataProvider005",
                            "Code generation failed",
                            "Failed to generate code for {0}: {1}",
                            "DataProvider",
                            DiagnosticSeverity.Error,
                            true
                        ),
                        location,
                        baseName,
                        failure.ErrorValue.DetailedMessage
                    );
                    context.ReportDiagnostic(diagGen);
                }
            }
            catch (Exception ex)
            {
                // Attach unexpected errors to the SQL file so they are visible inline
                var text =
                    sqlFile.GetText(context.CancellationToken)
                    ?? SourceText.From(string.Empty, Encoding.UTF8);
                var span = new TextSpan(0, Math.Min(1, text.Length));
                var lineSpan = new LinePositionSpan(
                    new LinePosition(0, 0),
                    new LinePosition(0, Math.Min(1, text.Length))
                );
                var location = Location.Create(sqlFile.Path, span, lineSpan);

                var diag = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DataProvider006",
                        "Unexpected error",
                        "Unexpected error while generating for file '{0}': {1}",
                        "DataProvider",
                        DiagnosticSeverity.Error,
                        true
                    ),
                    location,
                    sqlFile.Path,
                    ex.Message
                );
                context.ReportDiagnostic(diag);
            }
        }
    }
}
