using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Results;
using Selecta;

namespace DataProvider;

/// <summary>
/// Base source generator for SQL files that uses database-specific parsers
/// </summary>
public abstract class SqlFileGeneratorBase : IIncrementalGenerator
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlFileGeneratorBase"/> class.
    /// </summary>
    /// <param name="SqlParser">The database-specific SQL parser used by the generator.</param>
    protected SqlFileGeneratorBase(ISqlParser SqlParser) => this.SqlParser = SqlParser;

    readonly ISqlParser SqlParser;

    /// <summary>
    /// Creates the code generation functions for the specific database
    /// </summary>
    /// <returns>Tuple containing the code generation functions</returns>
    protected abstract (
        CodeGenerators.GenerateCodeFunc GenerateCode,
        CodeGenerators.GenerateCodeWithMetadataFunc GenerateCodeWithMetadata,
        CodeGenerators.GenerateTableOperationsFunc GenerateTableOperations
    ) CreateCodeGenerators();

    /// <summary>
    /// Gets column metadata from the database by executing the SQL query
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="sql">SQL query to execute</param>
    /// <param name="parameters">Query parameters</param>
    /// <returns>Result with column metadata from the database or error</returns>
    protected abstract Task<Result<IReadOnlyList<DatabaseColumn>, SqlError>> GetColumnMetadataAsync(
        string connectionString,
        string sql,
        IEnumerable<ParameterInfo> parameters
    );

    /// <summary>
    /// Initializes the incremental generator
    /// </summary>
    /// <param name="context">The initialization context</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var sqlFiles = context.AdditionalTextsProvider.Where(t =>
            t.Path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
        );

        var jsonFiles = context.AdditionalTextsProvider.Where(t =>
            t.Path.EndsWith("DataProvider.json", StringComparison.OrdinalIgnoreCase)
        );

        var groupingFiles = context.AdditionalTextsProvider.Where(t =>
            t.Path.EndsWith(".grouping.json", StringComparison.OrdinalIgnoreCase)
        );

        // Combine SQL files with JSON configuration and grouping configurations
        var sqlWithConfig = sqlFiles
            .Combine(jsonFiles.Collect())
            .Combine(groupingFiles.Collect())
            .Select(
                (combined, _) =>
                    new
                    {
                        SqlFile = combined.Left.Left,
                        JsonFiles = combined.Left.Right,
                        GroupingFiles = combined.Right,
                    }
            );

        context.RegisterSourceOutput(sqlWithConfig, GenerateWithDatabaseMetadata);
        context.RegisterSourceOutput(jsonFiles, GenerateFromConfig);
    }

    /// <summary>
    /// Generates source code for a SQL file using real database metadata
    /// </summary>
    /// <param name="context">The source production context</param>
    /// <param name="input">The combined SQL file, JSON config, and grouping configurations</param>
    private void GenerateWithDatabaseMetadata(SourceProductionContext context, dynamic input)
    {
        var sqlFile = (AdditionalText)input.SqlFile;
        var jsonFiles = (ImmutableArray<AdditionalText>)input.JsonFiles;
        var groupingFiles = (ImmutableArray<AdditionalText>)input.GroupingFiles;

        var sqlText = sqlFile.GetText(context.CancellationToken)!.ToString();
        var fileName = Path.GetFileNameWithoutExtension(sqlFile.Path);

        // Parse SQL to get parameters
        var statement = SqlParser.ParseSql(sqlText);

        // Get connection string from DataProvider.json
        string? connectionString = null;
        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                var jsonText = jsonFile.GetText(context.CancellationToken)!.ToString();
                var config = System.Text.Json.JsonSerializer.Deserialize<DataProviderConfig>(
                    jsonText
                );
                if (!string.IsNullOrEmpty(config?.ConnectionString))
                {
                    connectionString = config.ConnectionString;
                    break;
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                ReportConfigurationError(context, "Invalid JSON configuration", ex);
                return;
            }
        }

        // CRITICAL: Connection string is required
        if (string.IsNullOrEmpty(connectionString))
        {
            ReportConfigurationError(
                context,
                "Connection string is required in DataProvider.json",
                null
            );
            return;
        }

        // Get real column metadata from database
        var metadataResult = GetColumnMetadataAsync(connectionString, sqlText, statement.Parameters)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        if (metadataResult is Result<IReadOnlyList<DatabaseColumn>, SqlError>.Failure failure)
        {
            ReportDatabaseConnectionError(
                context,
                connectionString,
                failure.ErrorValue.Exception
                    ?? new InvalidOperationException(failure.ErrorValue.Message)
            );
            return;
        }

        var columnMetadata = (
            (Result<IReadOnlyList<DatabaseColumn>, SqlError>.Success)metadataResult
        ).Value;

        // Must have column metadata to generate code
        if (columnMetadata.Count == 0)
        {
            ReportDatabaseConnectionError(
                context,
                connectionString,
                new InvalidOperationException(
                    "Query returned no columns. Check your SQL syntax and database connection."
                )
            );
            return;
        }

        // Look for corresponding grouping configuration file
        GroupingConfig? groupingConfig = null;
        var expectedGroupingFileName = $"{fileName}.grouping.json";
        AdditionalText? groupingFile = null;

        foreach (var file in groupingFiles)
        {
            if (
                Path.GetFileName(file.Path)
                    .Equals(expectedGroupingFileName, StringComparison.OrdinalIgnoreCase)
            )
            {
                groupingFile = file;
                break;
            }
        }

        if (groupingFile != null)
        {
            try
            {
                var groupingText = groupingFile.GetText(context.CancellationToken)!.ToString();
                groupingConfig = System.Text.Json.JsonSerializer.Deserialize<GroupingConfig>(
                    groupingText
                );
            }
            catch (System.Text.Json.JsonException)
            {
                // If grouping config is malformed, treat as no grouping
                groupingConfig = null;
            }
        }

        // For now, assume no custom file exists since we can't do file I/O in source generators
        var hasCustom = false;

        var (GenerateCode, GenerateCodeWithMetadata, GenerateTableOperations) =
            CreateCodeGenerators();

        // Use the new method that accepts real database metadata
        var sourceResult = GenerateCodeWithMetadata(
            fileName,
            sqlText,
            statement,
            connectionString,
            columnMetadata,
            hasCustom,
            groupingConfig
        );

        if (sourceResult is Result<string, SqlError>.Failure sourceFailure)
        {
            ReportConfigurationError(
                context,
                $"Code generation failed: {sourceFailure.ErrorValue.Message}",
                sourceFailure.ErrorValue.Exception
            );
            return;
        }

        var source = ((Result<string, SqlError>.Success)sourceResult).Value;
        context.AddSource($"{fileName}.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>
    /// Generates source code from JSON configuration
    /// </summary>
    /// <param name="context">The source production context</param>
    /// <param name="jsonFile">The JSON configuration file</param>
    private void GenerateFromConfig(SourceProductionContext context, AdditionalText jsonFile)
    {
        try
        {
            var jsonText = jsonFile.GetText(context.CancellationToken)!.ToString();
            var config = System.Text.Json.JsonSerializer.Deserialize<DataProviderConfig>(jsonText);

            if (config?.Tables == null || config.Tables.Count == 0)
            {
                return;
            }

            // Connection string is required for table operations too
            if (string.IsNullOrEmpty(config.ConnectionString))
            {
                ReportConfigurationError(
                    context,
                    "Connection string is required in DataProvider.json for table operations",
                    null
                );
                return;
            }

            // Note: In a real implementation, you would use the connection string to inspect the schema
            // For now, we'll generate stub methods that show the structure
            var (_, _, GenerateTableOperations) = CreateCodeGenerators();
            var sources = new List<string>();

            foreach (var tableConfig in config.Tables)
            {
                // Create a stub DatabaseTable for demonstration
                var table = new DatabaseTable
                {
                    Schema = tableConfig.Schema,
                    Name = tableConfig.Name,
                    Columns = new List<DatabaseColumn>
                    {
                        new()
                        {
                            Name = "Id",
                            CSharpType = "int",
                            IsPrimaryKey = true,
                            IsIdentity = true,
                        },
                        new()
                        {
                            Name = "Name",
                            CSharpType = "string",
                            IsNullable = false,
                        },
                        new()
                        {
                            Name = "CreatedAt",
                            CSharpType = "DateTime",
                            IsNullable = false,
                        },
                    }.AsReadOnly(),
                };

                var sourceResult = GenerateTableOperations(table, tableConfig);
                if (sourceResult is Result<string, SqlError>.Success success)
                {
                    sources.Add(success.Value);
                }
                else if (sourceResult is Result<string, SqlError>.Failure failure)
                {
                    ReportConfigurationError(
                        context,
                        $"Table operation generation failed for {tableConfig.Name}: {failure.ErrorValue.Message}",
                        failure.ErrorValue.Exception
                    );
                }
            }

            // Combine all table operations into a single file
            var combinedSource = string.Join("\n\n", sources);
            context.AddSource(
                "TableOperations.g.cs",
                SourceText.From(combinedSource, Encoding.UTF8)
            );
        }
        catch (System.Text.Json.JsonException ex)
        {
            ReportConfigurationError(context, "Failed to parse DataProvider.json", ex);
        }
    }

    /// <summary>
    /// Reports a database connection error as a compiler error
    /// </summary>
    /// <param name="context">Source production context</param>
    /// <param name="connectionString">Connection string that failed</param>
    /// <param name="exception">The exception that occurred</param>
    private static void ReportDatabaseConnectionError(
        SourceProductionContext context,
        string connectionString,
        Exception exception
    )
    {
        var diagnostic = Diagnostic.Create(
            new DiagnosticDescriptor(
                "DL0002",
                "Database Connection Required",
                "Failed to connect to database at compile time. Connection string: '{0}'. Error: {1}. "
                    + "The generator must connect to the database to generate strongly-typed extensions.",
                "DataProvider",
                DiagnosticSeverity.Error,
                true
            ),
            Location.None,
            connectionString,
            exception.Message
        );
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    /// Reports a configuration error as a compiler error
    /// </summary>
    /// <param name="context">Source production context</param>
    /// <param name="message">Error message</param>
    /// <param name="exception">Optional exception</param>
    private static void ReportConfigurationError(
        SourceProductionContext context,
        string message,
        Exception? exception
    )
    {
        var diagnostic = Diagnostic.Create(
            new DiagnosticDescriptor(
                "DL0001",
                "Configuration Error",
                "{0}{1}",
                "DataProvider",
                DiagnosticSeverity.Error,
                true
            ),
            Location.None,
            message,
            exception != null ? $" Details: {exception.Message}" : ""
        );
        context.ReportDiagnostic(diagnostic);
    }
}
