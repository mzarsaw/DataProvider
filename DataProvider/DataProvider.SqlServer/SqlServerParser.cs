using DataProvider.SqlServer.SqlParsing;
using Selecta;

namespace DataProvider.SqlServer;

/// <summary>
/// SQL Server specific parser implementation using SqlParserCS
/// </summary>
public sealed class SqlServerParser : ISqlParser
{
    private readonly SqlParserCsImplementation _parser = new();

    /// <summary>
    /// Parses the specified SQL into a <see cref="SqlStatement"/> using the SqlParserCS backend.
    /// </summary>
    /// <param name="sql">The SQL text to parse.</param>
    /// <returns>A <see cref="SqlStatement"/> describing parameters and query type.</returns>
    public SqlStatement ParseSql(string sql) =>
        // Use the SqlParserCS library which supports multiple SQL dialects
        _parser.ParseSql(sql);
}
