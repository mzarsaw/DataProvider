# DataProvider

A .NET source generator that creates compile-time safe database extension methods from SQL queries. DataProvider eliminates runtime SQL errors by validating queries at compile time and generating strongly-typed C# code.

## Features

- **Compile-Time Safety** - SQL queries are validated during compilation, catching errors before runtime
- **Auto-Generated Extensions** - Creates extension methods on `IDbConnection` and `IDbTransaction`
- **Schema Inspection** - Automatically inspects database schema to generate appropriate types
- **Result Type Pattern** - All operations return `Result<T>` types for explicit error handling
- **Multi-Database Support** - Currently supports SQLite and SQL Server
- **LQL Integration** - Seamlessly works with Lambda Query Language files

## How It Works

1. **Define SQL Queries** - Place `.sql` or `.lql` files in your project
2. **Configure Generation** - Set up `DataProvider.json` configuration
3. **Build Project** - Source generators create extension methods during compilation
4. **Use Generated Code** - Call type-safe methods with full IntelliSense support

## Installation

### SQLite
```xml
<PackageReference Include="DataProvider.SQLite" Version="*" />
```

### SQL Server
```xml
<PackageReference Include="DataProvider.SqlServer" Version="*" />
```

## Configuration

Create a `DataProvider.json` file in your project root:

```json
{
  "ConnectionString": "Data Source=mydatabase.db",
  "Namespace": "MyApp.DataAccess",
  "OutputDirectory": "Generated",
  "Queries": [
    {
      "Name": "GetCustomers",
      "SqlFile": "Queries/GetCustomers.sql"
    },
    {
      "Name": "GetOrders",
      "SqlFile": "Queries/GetOrders.lql"
    }
  ]
}
```

## Usage Examples

### Simple Query

SQL file (`GetCustomers.sql`):
```sql
SELECT Id, Name, Email 
FROM Customers 
WHERE IsActive = @isActive
```

Generated C# usage:
```csharp
using var connection = new SqliteConnection(connectionString);
var result = await connection.GetCustomersAsync(isActive: true);

if (result.IsSuccess)
{
    foreach (var customer in result.Value)
    {
        Console.WriteLine($"{customer.Name}: {customer.Email}");
    }
}
else
{
    Console.WriteLine($"Error: {result.Error.Message}");
}
```

### With LQL

LQL file (`GetOrders.lql`):
```lql
Order
|> join(Customer, on = Order.CustomerId = Customer.Id)
|> filter(fn(row) => row.Order.OrderDate >= @startDate)
|> select(Order.Id, Order.Total, Customer.Name)
```

This automatically generates:
```csharp
var orders = await connection.GetOrdersAsync(
    startDate: DateTime.Now.AddDays(-30)
);
```

### Transaction Support

```csharp
using var connection = new SqliteConnection(connectionString);
connection.Open();
using var transaction = connection.BeginTransaction();

var insertResult = await transaction.InsertCustomerAsync(
    name: "John Doe",
    email: "john@example.com"
);

if (insertResult.IsSuccess)
{
    transaction.Commit();
}
else
{
    transaction.Rollback();
}
```

## Grouping Configuration

For complex result sets with joins, configure grouping in a `.grouping.json` file:

```json
{
  "PrimaryKey": "Id",
  "GroupBy": ["Id"],
  "Collections": {
    "Addresses": {
      "ForeignKey": "CustomerId",
      "Properties": ["Street", "City", "State"]
    }
  }
}
```

## Architecture

DataProvider follows functional programming principles:

- **No Classes** - Uses records and static extension methods
- **No Exceptions** - Returns `Result<T>` types for all operations
- **Pure Functions** - Static methods with no side effects
- **Expression-Based** - Prefers expressions over statements

## Project Structure

```
DataProvider/
├── DataProvider/              # Core library and base types
├── DataProvider.SQLite/       # SQLite implementation
│   ├── Parsing/              # ANTLR grammar and parsers
│   └── SchemaInspection/     # Schema discovery
├── DataProvider.SqlServer/    # SQL Server implementation
│   └── SchemaInspection/
├── DataProvider.Example/      # Example usage
└── DataProvider.Tests/        # Unit tests
```

## Testing

Run tests with:
```bash
dotnet test DataProvider.Tests/DataProvider.Tests.csproj
```

## Performance

- **Zero Runtime Overhead** - All SQL parsing and validation happens at compile time
- **Minimal Allocations** - Uses value types and expressions where possible
- **Async/Await** - Full async support for all database operations

## Error Handling

All methods return `Result<T>` types:

```csharp
var result = await connection.ExecuteQueryAsync();

var output = result switch
{
    { IsSuccess: true } => ProcessData(result.Value),
    { Error: SqlError error } => HandleError(error),
    _ => "Unknown error"
};
```

## Contributing

1. Follow the functional programming style (no classes, no exceptions)
2. Keep files under 450 lines
3. All public members must have XML documentation
4. Run `dotnet csharpier .` before committing
5. Ensure all tests pass

## License

MIT License

## Author

MelbourneDeveloper - [ChristianFindlay.com](https://christianfindlay.com)