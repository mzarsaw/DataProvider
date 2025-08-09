using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Selecta;

namespace DataProvider.SQLite.Parsing;

/// <summary>
/// SQLite parser implementation using ANTLR grammar
/// </summary>
public sealed class SqliteAntlrParser : ISqlParser
{
    /// <summary>
    /// Parses the specified SQL text and returns a <see cref="SqlStatement"/> containing parameters and query type.
    /// </summary>
    /// <param name="sql">The SQL text to parse.</param>
    /// <returns>A parsed <see cref="SqlStatement"/> including discovered parameters and query type.</returns>
    public SqlStatement ParseSql(string sql)
    {
        try
        {
            var inputStream = new AntlrInputStream(sql);
            var lexer = new SQLiteLexer(inputStream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new SQLiteParser(tokens);

            // Parse the SQL
            var parseTree = parser.parse();

            // Extract parameters using visitor
            var parameterExtractor = new SqliteParameterExtractor();
            var parameters = SqliteParameterExtractor.ExtractParameters(parseTree);
            var parameterInfos = parameters.Select(p => new ParameterInfo(p)).ToList();

            // Determine query type
            var queryType = DetermineQueryType(parseTree);

            return new SqlStatement
            {
                Parameters = parameterInfos.AsReadOnly(),
                QueryType = queryType,
            };
        }
        catch (Exception ex)
        {
            return new SqlStatement
            {
                Parameters = new List<ParameterInfo>().AsReadOnly(),
                QueryType = "UNKNOWN",
                ParseError = ex.ToString(),
            };
        }
    }

    /// <summary>
    /// Determines the SQL operation type by walking the parse tree.
    /// </summary>
    /// <param name="parseTree">The ANTLR parse tree.</param>
    /// <returns>The inferred query type such as SELECT, INSERT, UPDATE, or DELETE.</returns>
    private static string DetermineQueryType(IParseTree parseTree)
    {
        // Walk the parse tree to determine the query type
        var walker = new ParseTreeWalker();
        var queryTypeListener = new SqliteQueryTypeListener();
        walker.Walk(queryTypeListener, parseTree);
        return queryTypeListener.QueryType;
    }
}
