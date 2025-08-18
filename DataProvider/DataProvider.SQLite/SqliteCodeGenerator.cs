using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using DataProvider.CodeGeneration;
using DataProvider.SQLite.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
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
    /// Generates C# source for a SQL file using real database metadata.
    /// </summary>
    /// <param name="fileName">The SQL file name.</param>
    /// <param name="sql">The SQL content.</param>
    /// <param name="statement">The parsed SQL statement metadata.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="columnMetadata">Real column metadata from the database.</param>
    /// <param name="hasCustomImplementation">Whether a custom implementation exists.</param>
    /// <param name="groupingConfig">Optional grouping configuration for parent-child relationships.</param>
    /// <param name="config">Optional code generation configuration with custom functions.</param>
    /// <returns>Result with generated source or an error.</returns>
    public static Result<string, SqlError> GenerateCodeWithMetadata(
        string fileName,
        string sql,
        SqlStatement statement,
        string connectionString,
        IReadOnlyList<DatabaseColumn> columnMetadata,
        bool hasCustomImplementation,
        GroupingConfig? groupingConfig = null,
        CodeGenerationConfig? config = null
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

        // Use provided config or create default SQLite config
        var generationConfig = config ?? CreateDefaultSqliteConfig();

        // If grouping is configured, generate grouped version
        if (groupingConfig != null)
        {
            return GenerateGroupedVersionWithMetadata(
                fileName,
                sql,
                statement,
                columnMetadata,
                groupingConfig,
                generationConfig
            );
        }

        // Generate model type
        var modelResult = generationConfig.GenerateModelType(fileName, columnMetadata);
        if (modelResult is Result<string, SqlError>.Failure modelFailure)
            return modelFailure;

        // Generate data access method
        var className = $"{fileName}Extensions";
        var dataAccessResult = generationConfig.GenerateDataAccessMethod(
            className,
            fileName,
            sql,
            statement.Parameters,
            columnMetadata,
            generationConfig.ConnectionType
        );
        if (dataAccessResult is Result<string, SqlError>.Failure dataAccessFailure)
            return dataAccessFailure;

        // Generate complete source file
        return generationConfig.GenerateSourceFile(
            generationConfig.TargetNamespace,
            (modelResult as Result<string, SqlError>.Success)!.Value,
            (dataAccessResult as Result<string, SqlError>.Success)!.Value
        );
    }

    /// <summary>
    /// Creates default SQLite configuration for code generation
    /// </summary>
    private static CodeGenerationConfig CreateDefaultSqliteConfig()
    {
        var databaseEffects = new SqliteDatabaseEffects();
        var tableOperationGenerator = new DefaultTableOperationGenerator("SqliteConnection");

        return new CodeGenerationConfig(
            databaseEffects.GetColumnMetadataFromSqlAsync,
            tableOperationGenerator
        )
        {
            ConnectionType = "SqliteConnection",
            TargetNamespace = "Generated",
        };
    }

    /// <summary>
    /// Generate C# source code for Insert/Update operations based on table configuration
    /// </summary>
    /// <param name="tableOperationGenerator">The table generator</param>
    /// <param name="table">The database table metadata</param>
    /// <param name="config">The table configuration</param>
    /// <returns>The generated C# source code</returns>
    public static Result<string, SqlError> GenerateTableOperations(
        ITableOperationGenerator tableOperationGenerator,
        DatabaseTable table,
        TableConfig config
    ) => tableOperationGenerator.GenerateTableOperations(table, config);

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
        var databaseEffects = new SqliteDatabaseEffects();
        return await databaseEffects
            .GetColumnMetadataFromSqlAsync(connectionString, sql, parameters)
            .ConfigureAwait(false);
    }

    private static Result<string, SqlError> GenerateGroupedVersionWithMetadata(
        string fileName,
        string sql,
        SqlStatement statement,
        IReadOnlyList<DatabaseColumn> columnMetadata,
        GroupingConfig groupingConfig,
        CodeGenerationConfig config
    )
    {
        // Generate raw record type
        var rawRecordResult = config.GenerateRawRecordType($"{fileName}Raw", columnMetadata);
        if (rawRecordResult is Result<string, SqlError>.Failure rawFailure)
            return rawFailure;

        // Generate grouped query method
        var groupedMethodResult = config.GenerateGroupedQueryMethod(
            $"{fileName}Extensions",
            fileName,
            sql,
            statement.Parameters,
            columnMetadata,
            groupingConfig,
            config.ConnectionType
        );
        if (groupedMethodResult is Result<string, SqlError>.Failure methodFailure)
            return methodFailure;

        // Generate grouped model types
        var groupedModelsResult = config.GenerateGroupedModels(
            groupingConfig.ParentEntity.Name,
            groupingConfig.ChildEntity.Name,
            groupingConfig.ParentEntity.Columns,
            groupingConfig.ChildEntity.Columns,
            columnMetadata
        );
        if (groupedModelsResult is Result<string, SqlError>.Failure modelsFailure)
            return modelsFailure;

        // Combine all parts
        var rawRecord = (rawRecordResult as Result<string, SqlError>.Success)!.Value;
        var method = (groupedMethodResult as Result<string, SqlError>.Success)!.Value;
        var models = (groupedModelsResult as Result<string, SqlError>.Success)!.Value;

        return config.GenerateSourceFile(
            config.TargetNamespace,
            $"{rawRecord}\n\n{models}",
            method
        );
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
