using System.CommandLine;
using System.Text.Json;
using DataProvider.SQLite.Parsing;
using Results;

#pragma warning disable CA1849 // Call async methods when in an async method

namespace DataProvider.SQLite.Cli;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<int> Main(string[] args)
    {
        var projectDir = new Option<DirectoryInfo>(
            "--project-dir",
            description: "Project directory containing sql, grouping, and DataProvider.json"
        )
        {
            IsRequired = true,
        };
        var config = new Option<FileInfo>("--config", description: "Path to DataProvider.json")
        {
            IsRequired = true,
        };
        var outDir = new Option<DirectoryInfo>(
            "--out",
            description: "Output directory for generated .g.cs files"
        )
        {
            IsRequired = true,
        };
        var schema = new Option<FileInfo?>(
            "--schema",
            description: "Optional schema SQL file to execute before generation"
        )
        {
            IsRequired = false,
        };

        var root = new RootCommand("DataProvider.SQLite codegen CLI")
        {
            projectDir,
            config,
            outDir,
            schema,
        };
        root.SetHandler(
            async (DirectoryInfo proj, FileInfo cfg, DirectoryInfo output, FileInfo? schemaFile) =>
            {
                var exit = await RunAsync(proj, cfg, output, schemaFile).ConfigureAwait(false);
                Environment.Exit(exit);
            },
            projectDir,
            config,
            outDir,
            schema
        );

        return await root.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task<int> RunAsync(
        DirectoryInfo projectDir,
        FileInfo configFile,
        DirectoryInfo outDir,
        FileInfo? schemaFile
    )
    {
        try
        {
            if (!configFile.Exists)
            {
                Console.WriteLine($"❌ Config not found: {configFile.FullName}");
                return 1;
            }

            if (!outDir.Exists)
                outDir.Create();

            var cfgText = await File.ReadAllTextAsync(configFile.FullName).ConfigureAwait(false);
            var cfg = JsonSerializer.Deserialize<SourceGeneratorDataProviderConfiguration>(
                cfgText,
                JsonOptions
            );
            if (cfg is null || string.IsNullOrWhiteSpace(cfg.ConnectionString))
            {
                Console.WriteLine("❌ DataProvider.json ConnectionString is required");
                return 1;
            }

            // Ensure DB exists and schema applied if provided
            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(cfg.ConnectionString);
                await conn.OpenAsync().ConfigureAwait(false);
                if (schemaFile is not null && schemaFile.Exists)
                {
                    var schemaSql = await File.ReadAllTextAsync(schemaFile.FullName)
                        .ConfigureAwait(false);
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = schemaSql;
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to prepare database: {ex.Message}");
                return 1;
            }

            // Gather files
            var sqlFiles = Directory.GetFiles(
                projectDir.FullName,
                "*.sql",
                SearchOption.AllDirectories
            );
            var groupingFiles = Directory
                .GetFiles(projectDir.FullName, "*.grouping.json", SearchOption.AllDirectories)
                .Select(p =>
                {
                    var fileName = Path.GetFileName(p);
                    var baseName = fileName.EndsWith(
                        ".grouping.json",
                        StringComparison.OrdinalIgnoreCase
                    )
                        ? fileName[..^".grouping.json".Length]
                        : Path.GetFileNameWithoutExtension(fileName);
                    return new { Path = p, Base = baseName };
                })
                .ToLookup(x => x.Base, x => x.Path);

            var parser = new SqliteAntlrParser();

            var hadErrors = false;

            foreach (var sqlPath in sqlFiles)
            {
                try
                {
                    var sql = await File.ReadAllTextAsync(sqlPath).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(sql))
                        continue;

                    var baseName = Path.GetFileNameWithoutExtension(sqlPath);
                    // Normalize base name to ignore optional ".generated" suffix from intermediate files
                    if (baseName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = baseName[..^".generated".Length];
                    }
                    if (string.Equals(baseName, "schema", StringComparison.OrdinalIgnoreCase))
                    {
                        // Skip schema file; it's only for DB initialization
                        continue;
                    }
                    var stmt = parser.ParseSql(sql);

                    var colsResult = await SqliteCodeGenerator
                        .GetColumnMetadataFromSqlAsync(cfg.ConnectionString, sql, stmt.Parameters)
                        .ConfigureAwait(false);
                    if (
                        colsResult
                        is not Result<IReadOnlyList<DatabaseColumn>, SqlError>.Success cols
                    )
                    {
                        var err = (
                            colsResult as Result<IReadOnlyList<DatabaseColumn>, SqlError>.Failure
                        )!.ErrorValue;
                        var prettyMeta = FormatSqliteMetadataMessage(err.DetailedMessage);
                        Console.WriteLine($"❌ {prettyMeta}");
                        // Also emit an MSBuild-formatted error so IDE Problem Matchers pick it up
                        Console.Error.WriteLine($"{sqlPath}(1,1): error DP0001: {prettyMeta}");
                        // Emit a compile-time error file so MSBuild surfaces the exact problem
                        var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
                        var content =
                            $"// Auto-generated due to SQL error in {sqlPath}\n#error {EscapeForPreprocessor(prettyMeta)}\n";
                        await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
                        hadErrors = true;
                        continue;
                    }

                    GroupingConfig? grouping = null;
                    if (groupingFiles.Contains(baseName))
                    {
                        var gpath = groupingFiles[baseName].First();
                        var gtext = await File.ReadAllTextAsync(gpath).ConfigureAwait(false);
                        grouping = JsonSerializer.Deserialize<GroupingConfig>(gtext, JsonOptions);
                    }

                    var gen = SqliteCodeGenerator.GenerateCodeWithMetadata(
                        baseName,
                        sql,
                        stmt,
                        cfg.ConnectionString,
                        cols.Value,
                        hasCustomImplementation: false,
                        grouping
                    );
                    if (gen is Result<string, SqlError>.Success success)
                    {
                        var target = Path.Combine(outDir.FullName, baseName + ".g.cs");
                        await File.WriteAllTextAsync(target, success.Value).ConfigureAwait(false);
                        Console.WriteLine($"✅ Generated {target}");
                    }
                    else if (gen is Result<string, SqlError>.Failure failure)
                    {
                        var prettyGen = FormatSqliteMetadataMessage(
                            failure.ErrorValue.DetailedMessage
                        );
                        Console.WriteLine($"❌ {prettyGen}");
                        // Also emit an MSBuild-formatted error so IDE Problem Matchers pick it up
                        Console.Error.WriteLine($"{sqlPath}(1,1): error DL0002: {prettyGen}");
                        // Emit a compile-time error file so MSBuild surfaces the exact problem
                        var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
                        var content =
                            $"// Auto-generated due to SQL generation error in {sqlPath}\n#error {EscapeForPreprocessor(prettyGen)}\n";
                        await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
                        hadErrors = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing {sqlPath}: {ex.Message}");
                    // Emit a compile-time error file so MSBuild surfaces the exact problem
                    var baseName = Path.GetFileNameWithoutExtension(sqlPath);
                    if (baseName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = baseName[..^".generated".Length];
                    }
                    var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
                    var content =
                        $"// Auto-generated due to unexpected error in {sqlPath}\n#error {EscapeForPreprocessor(ex.Message)}\n";
                    Console.Error.WriteLine($"{sqlPath}(1,1): error DL0003: {ex.Message}");
                    await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
                    hadErrors = true;
                }
            }

            return hadErrors ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error: {ex}");
            return 1;
        }
    }

    private static string EscapeForPreprocessor(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;
        // Preprocessor directives cannot span multiple lines; replace newlines and quotes
        var oneLine = message.Replace('\r', ' ').Replace('\n', ' ');
        return oneLine.Replace('"', '\'');
    }

    private static string FormatSqliteMetadataMessage(string detailed)
    {
        if (string.IsNullOrWhiteSpace(detailed))
            return detailed;

        var msg = detailed.Trim();

        const string sqlitePrefix = "SQLite Error ";
        var idx = msg.IndexOf(sqlitePrefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return msg; // Not a known SQLite pattern; return as-is

        // Extract code
        var codeStart = idx + sqlitePrefix.Length;
        var colonAfterCode = msg.IndexOf(':', codeStart);
        if (colonAfterCode < 0)
            return msg;
        var codeText = msg[codeStart..colonAfterCode].Trim();

        // Extract inner quoted detail after colon, between single quotes if present
        var firstQuote = msg.IndexOf('\'', colonAfterCode + 1);
        var lastQuote = firstQuote >= 0 ? msg.IndexOf('\'', firstQuote + 1) : -1;
        string detail =
            lastQuote > firstQuote && firstQuote >= 0
                ? msg.Substring(firstQuote + 1, lastQuote - firstQuote - 1)
                : msg[(colonAfterCode + 1)..].Trim().Trim('.');

        // If detail starts with "no such column: ", rephrase
        const string noSuch = "no such column: ";
        string head;
        if (detail.StartsWith(noSuch, StringComparison.OrdinalIgnoreCase))
        {
            var col = detail[noSuch.Length..].Trim();
            head = $"No such column {col}.";
        }
        else
        {
            // Capitalize first letter and ensure trailing period
            if (detail.Length > 0)
            {
                var first = char.ToUpperInvariant(detail[0]);
                var rest = detail.Length > 1 ? detail[1..] : string.Empty;
                detail = first + rest;
            }
            head = detail.EndsWith('.') ? detail : detail + ".";
        }

        // Keep the leading context up to the sqlite prefix (e.g., "Failed to get column metadata: ")
        var contextPart = msg[..idx].Trim();
        if (contextPart.EndsWith(':'))
            contextPart = contextPart[..^1].TrimEnd();

        var tail = string.IsNullOrEmpty(contextPart) ? string.Empty : $" {contextPart}";
        var final = string.IsNullOrEmpty(tail)
            ? head
            : $"{head}{tail} (SQLite generation Error {codeText})";
        return final;
    }
}
