using System.Data;
using Results;

namespace DataProvider;

/// <summary>
/// Static extension methods for IDbTransaction following FP patterns
/// </summary>
public static class DbTransactionExtensions
{
    /// <summary>
    /// Execute a query within a transaction and return results
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="transaction">The database transaction</param>
    /// <param name="sql">The SQL query</param>
    /// <param name="parameters">Optional parameters</param>
    /// <param name="mapper">Function to map from IDataReader to T</param>
    /// <returns>Result with list of T or error</returns>
    public static Result<IReadOnlyList<T>, SqlError> Query<T>(
        this IDbTransaction transaction,
        string sql,
        IEnumerable<IDataParameter>? parameters = null,
        Func<IDataReader, T>? mapper = null
    )
    {
        if (transaction?.Connection == null)
            return new Result<IReadOnlyList<T>, SqlError>.Failure(
                SqlError.Create("Transaction or connection is null")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<IReadOnlyList<T>, SqlError>.Failure(
                SqlError.Create("SQL is null or empty")
            );

        try
        {
            using var command = transaction.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }
            }

            var results = new List<T>();
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                if (mapper != null)
                {
                    results.Add(mapper(reader));
                }
            }

            return new Result<IReadOnlyList<T>, SqlError>.Success(results.AsReadOnly());
        }
        catch (Exception ex)
        {
            return new Result<IReadOnlyList<T>, SqlError>.Failure(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Execute a non-query command within a transaction
    /// </summary>
    /// <param name="transaction">The database transaction</param>
    /// <param name="sql">The SQL command</param>
    /// <param name="parameters">Optional parameters</param>
    /// <returns>Result with rows affected or error</returns>
    public static Result<int, SqlError> Execute(
        this IDbTransaction transaction,
        string sql,
        IEnumerable<IDataParameter>? parameters = null
    )
    {
        if (transaction?.Connection == null)
            return new Result<int, SqlError>.Failure(
                SqlError.Create("Transaction or connection is null")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<int, SqlError>.Failure(SqlError.Create("SQL is null or empty"));

        try
        {
            using var command = transaction.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }
            }

            var rowsAffected = command.ExecuteNonQuery();
            return new Result<int, SqlError>.Success(rowsAffected);
        }
        catch (Exception ex)
        {
            return new Result<int, SqlError>.Failure(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Execute a scalar command within a transaction
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="transaction">The database transaction</param>
    /// <param name="sql">The SQL command</param>
    /// <param name="parameters">Optional parameters</param>
    /// <returns>Result with scalar value or error</returns>
    public static Result<T?, SqlError> Scalar<T>(
        this IDbTransaction transaction,
        string sql,
        IEnumerable<IDataParameter>? parameters = null
    )
    {
        if (transaction?.Connection == null)
            return new Result<T?, SqlError>.Failure(
                SqlError.Create("Transaction or connection is null")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<T?, SqlError>.Failure(SqlError.Create("SQL is null or empty"));

        try
        {
            using var command = transaction.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }
            }

            var result = command.ExecuteScalar();
            return new Result<T?, SqlError>.Success(result is T value ? value : default);
        }
        catch (Exception ex)
        {
            return new Result<T?, SqlError>.Failure(SqlError.FromException(ex));
        }
    }
}
