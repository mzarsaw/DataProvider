namespace Selecta;

/// <summary>
/// Abstraction for parsing SQL and extracting comprehensive metadata
/// </summary>
public interface ISqlParser
{
    /// <summary>
    /// Parse SQL text and extract comprehensive metadata including SELECT list, tables, parameters, and joins
    /// </summary>
    /// <param name="sql">The SQL text to parse</param>
    /// <returns>The parsed SQL statement metadata</returns>
    SqlStatement ParseSql(string sql);
}
