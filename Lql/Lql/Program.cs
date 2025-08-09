using System.Runtime.InteropServices;
using Results;

// THIS IS NOT A TEST!!!
// THIS IS ONLY A SIMPLE EXAMPLE. LEAVE IT ALONE!!!

[assembly: ComVisible(false)]

namespace Lql;

internal sealed class Program
{
    static void Main(string[] _)
    {
        string lqlCode = """
            -- Join users + orders, filter only completed orders
            let joined =
                users
                |> join(orders, on = users.id = orders.user_id)
                |> filter(fn(row) => row.orders.status = 'completed')

            -- Union with archived users
            let all_users =
                joined
                |> select(users.id, users.name)
                |> union(
                    archived_users
                    |> select(archived_users.id, archived_users.name)
                )

            -- Insert result into report table
            all_users |> insert(report_table)
            """;

        /* EXPECTED OUTPUT:
        
        INSERT INTO report_table (id, name)
        SELECT id, name
        FROM (
            SELECT u.id, u.name
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id
            WHERE o.status = 'completed'
        
            UNION
        
            SELECT a.id, a.name
            FROM archived_users a
        ) AS all_users;
        */

        var statementResult = LqlStatementConverter.ToStatement(lqlCode);

        switch (statementResult)
        {
            case Result<LqlStatement, SqlError>.Success success:
                // Note: Extension methods for ToPostgreSql are in Lql.Postgres project
                // This example just shows that parsing works
                Console.WriteLine("LQL parsing successful");
                Console.WriteLine(
                    $"AST Node Type: {success.Value.AstNode?.GetType().Name ?? "null"}"
                );
                break;
            case Result<LqlStatement, SqlError>.Failure failure:
                Console.WriteLine($"Parse Error: {failure.ErrorValue.FormattedMessage}");
                break;
        }
    }
}
