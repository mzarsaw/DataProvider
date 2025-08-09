using Lql.Parsing;
using Results;

namespace Lql;

/// <summary>
/// Converts LQL code to LqlStatement and provides PostgreSQL generation
/// </summary>
public static class LqlStatementConverter
{
    /// <summary>
    /// Converts LQL code to a LqlStatement using the Antlr parser
    /// </summary>
    /// <param name="lqlCode">The LQL code to convert</param>
    /// <returns>A Result containing either a LqlStatement or a SqlError</returns>
    public static Result<LqlStatement, SqlError> ToStatement(string lqlCode)
    {
        var parseResult = LqlCodeParser.Parse(lqlCode);

        return parseResult switch
        {
            Result<INode, SqlError>.Success success => new Result<LqlStatement, SqlError>.Success(
                new LqlStatement { AstNode = success.Value }
            ),
            Result<INode, SqlError>.Failure failure => new Result<LqlStatement, SqlError>.Failure(
                failure.ErrorValue
            ),
            _ => new Result<LqlStatement, SqlError>.Failure(
                new SqlError("Unknown parse result type")
            ),
        };
    }
}
