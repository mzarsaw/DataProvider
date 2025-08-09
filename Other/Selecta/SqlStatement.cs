namespace Selecta;

//TODO: make record, or at least immutable

/// <summary>
/// Represents a parsed SQL statement with extracted metadata that is generic for all SQL flavors
/// </summary>
public sealed class SqlStatement
{
    /// <summary>
    /// Gets or sets the SELECT list columns
    /// </summary>
    public IReadOnlyList<ColumnInfo> SelectList { get; init; } =
        new List<ColumnInfo>().AsReadOnly();

    /// <summary>
    /// Gets or sets the tables in the FROM clause
    /// </summary>
    public IReadOnlyList<TableInfo> Tables { get; init; } = new List<TableInfo>().AsReadOnly();

    /// <summary>
    /// Gets or sets the query parameters
    /// </summary>
    public IReadOnlyList<ParameterInfo> Parameters { get; init; } =
        new List<ParameterInfo>().AsReadOnly();

    /// <summary>
    /// Gets or sets the join graph
    /// </summary>
    public JoinGraph JoinGraph { get; init; } = new();

    /// <summary>
    /// Gets or sets the WHERE conditions
    /// </summary>
    public IReadOnlyList<WhereCondition> WhereConditions { get; init; } =
        new List<WhereCondition>().AsReadOnly();

    /// <summary>
    /// Gets or sets the GROUP BY columns
    /// </summary>
    public IReadOnlyList<ColumnInfo> GroupByColumns { get; init; } =
        new List<ColumnInfo>().AsReadOnly();

    /// <summary>
    /// Gets or sets the ORDER BY items
    /// </summary>
    public IReadOnlyList<OrderByItem> OrderByItems { get; init; } =
        new List<OrderByItem>().AsReadOnly();

    /// <summary>
    /// Gets or sets the HAVING condition
    /// </summary>
    public string? HavingCondition { get; init; }

    /// <summary>
    /// Gets or sets the LIMIT value
    /// </summary>
    public string? Limit { get; init; }

    /// <summary>
    /// Gets or sets the OFFSET value
    /// </summary>
    public string? Offset { get; init; }

    /// <summary>
    /// Gets or sets whether the query uses DISTINCT
    /// </summary>
    public bool IsDistinct { get; init; }

    /// <summary>
    /// Gets or sets the UNION operations
    /// </summary>
    public IReadOnlyList<UnionOperation> Unions { get; init; } =
        new List<UnionOperation>().AsReadOnly();

    /// <summary>
    /// Gets or sets the INSERT target table
    /// </summary>
    public string? InsertTable { get; init; }

    /// <summary>
    /// Gets or sets the INSERT columns
    /// </summary>
    public IReadOnlyList<string> InsertColumns { get; init; } = new List<string>().AsReadOnly();

    /// <summary>
    /// Gets or sets whether to force table aliases
    /// </summary>
    public bool ForceTableAliases { get; init; }

    /// <summary>
    /// Gets or sets the query type
    /// </summary>
    public string QueryType { get; init; } = "SELECT";

    /// <summary>
    /// Gets a value indicating whether the query has one-to-many joins
    /// </summary>
    public bool HasOneToManyJoins => JoinGraph.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the query has joins
    /// </summary>
    public bool HasJoins => JoinGraph.Count > 0;

    /// <summary>
    /// Gets or sets the parse error message if parsing failed
    /// </summary>
    public string? ParseError { get; init; }
}
