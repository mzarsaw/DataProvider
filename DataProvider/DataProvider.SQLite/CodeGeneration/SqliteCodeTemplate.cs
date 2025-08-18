using System.Globalization;
using DataProvider.CodeGeneration;
using Results;
using Selecta;

namespace DataProvider.SQLite.CodeGeneration;

/// <summary>
/// SQLite-specific code generation template
/// </summary>
public class SqliteCodeTemplate : DefaultCodeTemplate
{
    /// <summary>
    /// Generates the data access extension method for SQLite
    /// </summary>
    public override Result<string, SqlError> GenerateDataAccessMethod(
        string methodName,
        string returnTypeName,
        string sql,
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<DatabaseColumn> columns
    )
    {
        var className = string.Create(CultureInfo.InvariantCulture, $"{methodName}Extensions");
        return DataAccessGenerator.GenerateQueryMethod(
            className,
            methodName,
            returnTypeName,
            sql,
            parameters,
            columns,
            "SqliteConnection"
        );
    }
}
