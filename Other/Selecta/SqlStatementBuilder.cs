namespace Selecta;

/// <summary>
/// Builder for constructing SqlStatement instances
/// </summary>
public sealed class SqlStatementBuilder
{
    private readonly List<ColumnInfo> _selectList = [];
    private readonly List<TableInfo> _tables = [];
    private readonly List<ParameterInfo> _parameters = [];
    private readonly JoinGraph _joinGraph = new();
    private readonly List<WhereCondition> _whereConditions = [];
    private readonly List<ColumnInfo> _groupByColumns = [];
    private readonly List<OrderByItem> _orderByItems = [];
    private readonly List<UnionOperation> _unions = [];
    private readonly List<string> _insertColumns = [];
    private string? _havingCondition;
    private string? _limit;
    private string? _offset;
    private bool _isDistinct;
    private string? _insertTable;
    private bool _forceTableAliases;
    private string _queryType = "SELECT";
    private string? _parseError;

    /// <summary>
    /// Sets the SELECT list columns
    /// </summary>
    /// <param name="columns">The columns to select</param>
    /// <param name="distinct">Whether to use DISTINCT</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder WithSelectColumns(
        IEnumerable<ColumnInfo> columns,
        bool distinct = false
    )
    {
        _selectList.Clear();
        _selectList.AddRange(columns);
        _isDistinct = distinct;
        return this;
    }

    /// <summary>
    /// Adds a SELECT column
    /// </summary>
    /// <param name="column">The column to add</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder AddSelectColumn(ColumnInfo column)
    {
        _selectList.Add(column);
        return this;
    }

    /// <summary>
    /// Adds a SELECT column by name
    /// </summary>
    /// <param name="name">The column name</param>
    /// <param name="alias">The column alias</param>
    /// <param name="tableAlias">The table alias</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder AddSelectColumn(
        string name,
        string? alias = null,
        string? tableAlias = null
    )
    {
        _selectList.Add(ColumnInfo.Named(name, tableAlias, alias));
        return this;
    }

    /// <summary>
    /// Adds a table to the FROM clause
    /// </summary>
    /// <param name="table">The table to add</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder AddTable(TableInfo table)
    {
        _tables.Add(table);
        return this;
    }

    /// <summary>
    /// Adds a table by name
    /// </summary>
    /// <param name="name">The table name</param>
    /// <param name="alias">The table alias</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder AddTable(string name, string? alias = null)
    {
        _tables.Add(new TableInfo(name, alias));
        return this;
    }

    /// <summary>
    /// Adds a join to the query
    /// </summary>
    /// <param name="leftTable">The left table</param>
    /// <param name="rightTable">The right table</param>
    /// <param name="condition">The join condition</param>
    /// <param name="joinType">The join type</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder AddJoin(
        string leftTable,
        string rightTable,
        string condition,
        string joinType = "INNER"
    )
    {
        _joinGraph.Add(leftTable, rightTable, condition, joinType);
        return this;
    }

    /// <summary>
    /// Adds a WHERE condition
    /// </summary>
    /// <param name="condition">The condition to add</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder AddWhereCondition(WhereCondition condition)
    {
        _whereConditions.Add(condition);
        return this;
    }

    /// <summary>
    /// Adds a WHERE condition from a string expression
    /// </summary>
    /// <param name="condition">The condition string to add</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder AddWhereCondition(string condition)
    {
        _whereConditions.Add(WhereCondition.FromExpression(condition));
        return this;
    }

    /// <summary>
    /// Adds GROUP BY columns
    /// </summary>
    /// <param name="columns">The columns to group by</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder AddGroupBy(IEnumerable<ColumnInfo> columns)
    {
        _groupByColumns.AddRange(columns);
        return this;
    }

    /// <summary>
    /// Adds an ORDER BY item
    /// </summary>
    /// <param name="column">The column to order by</param>
    /// <param name="direction">The order direction</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder AddOrderBy(string column, string direction)
    {
        _orderByItems.Add(new OrderByItem(column, direction));
        return this;
    }

    /// <summary>
    /// Sets the HAVING condition
    /// </summary>
    /// <param name="condition">The having condition</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder WithHaving(string condition)
    {
        _havingCondition = condition;
        return this;
    }

    /// <summary>
    /// Sets the LIMIT
    /// </summary>
    /// <param name="limit">The limit value</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder WithLimit(string limit)
    {
        _limit = limit;
        return this;
    }

    /// <summary>
    /// Sets the OFFSET
    /// </summary>
    /// <param name="offset">The offset value</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder WithOffset(string offset)
    {
        _offset = offset;
        return this;
    }

    /// <summary>
    /// Sets whether to use DISTINCT
    /// </summary>
    /// <param name="distinct">Whether to use DISTINCT</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder WithDistinct(bool distinct = true)
    {
        _isDistinct = distinct;
        return this;
    }

    /// <summary>
    /// Adds a UNION operation
    /// </summary>
    /// <param name="query">The query to union with</param>
    /// <param name="isUnionAll">Whether this is UNION ALL</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder AddUnion(string query, bool isUnionAll = false)
    {
        _unions.Add(new UnionOperation(query, isUnionAll));
        return this;
    }

    /// <summary>
    /// Sets the INSERT target
    /// </summary>
    /// <param name="table">The target table</param>
    /// <param name="columns">The columns to insert</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder WithInsertTarget(string table, IEnumerable<string> columns)
    {
        _insertTable = table;
        _insertColumns.Clear();
        _insertColumns.AddRange(columns);
        _queryType = "INSERT";
        return this;
    }

    /// <summary>
    /// Sets whether to force table aliases
    /// </summary>
    /// <param name="force">Whether to force table aliases</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder WithForceTableAliases(bool force = true)
    {
        _forceTableAliases = force;
        return this;
    }

    /// <summary>
    /// Sets the query type
    /// </summary>
    /// <param name="queryType">The query type</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder WithQueryType(string queryType)
    {
        _queryType = queryType;
        return this;
    }

    /// <summary>
    /// Sets the parse error
    /// </summary>
    /// <param name="parseError">The parse error</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder WithParseError(string parseError)
    {
        _parseError = parseError;
        return this;
    }

    /// <summary>
    /// Adds a parameter
    /// </summary>
    /// <param name="parameter">The parameter to add</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder AddParameter(ParameterInfo parameter)
    {
        _parameters.Add(parameter);
        return this;
    }

    /// <summary>
    /// Adds a parameter by name and type
    /// </summary>
    /// <param name="name">The parameter name</param>
    /// <param name="sqlType">The SQL type</param>
    /// <returns>This builder instance</returns>
    public SqlStatementBuilder AddParameter(string name, string sqlType = "NVARCHAR")
    {
        _parameters.Add(new ParameterInfo(name, sqlType));
        return this;
    }

    /// <summary>
    /// Builds the SqlStatement
    /// </summary>
    /// <returns>The constructed SqlStatement</returns>
    public SqlStatement Build() =>
        new()
        {
            SelectList = _selectList.AsReadOnly(),
            Tables = _tables.AsReadOnly(),
            Parameters = _parameters.AsReadOnly(),
            JoinGraph = _joinGraph,
            WhereConditions = _whereConditions.AsReadOnly(),
            GroupByColumns = _groupByColumns.AsReadOnly(),
            OrderByItems = _orderByItems.AsReadOnly(),
            HavingCondition = _havingCondition,
            Limit = _limit,
            Offset = _offset,
            IsDistinct = _isDistinct,
            Unions = _unions.AsReadOnly(),
            InsertTable = _insertTable,
            InsertColumns = _insertColumns.AsReadOnly(),
            ForceTableAliases = _forceTableAliases,
            QueryType = _queryType,
            ParseError = _parseError,
        };
}
