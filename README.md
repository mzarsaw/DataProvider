# DataProvider & Lambda Query Language (LQL)

This repository contains two complementary projects that work together to provide compile-time safe database access and a functional query language for .NET applications.

## Projects Overview

### 1. DataProvider
A source generator that creates compile-time safe extension methods for database operations from SQL files. It generates strongly-typed C# code based on your SQL queries and database schema, ensuring type safety and eliminating runtime SQL errors.

**Key Features:**
- Compile-time SQL validation
- Auto-generated extension methods on `IDbConnection` and `IDbTransaction`
- Support for SQLite and SQL Server
- Schema inspection and code generation
- Result type pattern for error handling

[View DataProvider Documentation →](./DataProvider/README.md)

### 2. Lambda Query Language (LQL)
A functional pipeline-style DSL that transpiles to SQL. LQL provides a more intuitive and composable way to write database queries using lambda expressions and pipeline operators.

**Key Features:**
- Functional pipeline syntax using `|>` operator
- Lambda expressions for filtering and transformations
- Cross-database support (PostgreSQL, SQLite, SQL Server)
- VS Code extension with syntax highlighting
- CLI tools for transpilation

[View LQL Documentation →](./Lql/README.md)

## How They Work Together

DataProvider and LQL integrate seamlessly:

1. **Write queries in LQL** - Use the intuitive pipeline syntax to express your database queries
2. **Transpile to SQL** - LQL files (`.lql`) are automatically converted to SQL files
3. **Generate type-safe code** - DataProvider source generators create extension methods from the SQL
4. **Use in your application** - Call the generated methods with full IntelliSense and compile-time safety

### Example Workflow

```lql
// GetCustomers.lql
Customer
|> join(Address, on = Customer.Id = Address.CustomerId)
|> filter(fn(row) => row.Customer.IsActive = true)
|> select(Customer.Id, Customer.Name, Address.City)
```

This LQL query gets transpiled to SQL and DataProvider generates:

```csharp
// Auto-generated extension method
var customers = await connection.GetCustomersAsync(cancellationToken);
```

## Getting Started

### Prerequisites
- .NET 8.0 or later
- Visual Studio 2022 or VS Code
- Database (SQLite, SQL Server, or PostgreSQL)

### Installation

1. Clone the repository:
```bash
git clone https://github.com/MelbourneDeveloper/DataProvider.git
```

2. Build the solution:
```bash
dotnet build DataProvider.sln
```

3. Run tests:
```bash
dotnet test
```

## Repository Structure

```
DataProvider/
├── DataProvider/           # Core DataProvider projects
│   ├── DataProvider/       # Core library
│   ├── DataProvider.SQLite/
│   ├── DataProvider.SqlServer/
│   └── DataProvider.Example/
├── Lql/                    # Lambda Query Language projects
│   ├── Lql/                # Core LQL library
│   ├── Lql.SQLite/
│   ├── Lql.SqlServer/
│   ├── Lql.Postgres/
│   ├── LqlCli.SQLite/      # CLI tool
│   └── LqlExtension/       # VS Code extension
└── Other/
    ├── Results/            # Result type implementation
    └── Selecta/            # SQL parsing utilities

```

## License

MIT License - See individual project folders for details.

## Author

MelbourneDeveloper - [ChristianFindlay.com](https://christianfindlay.com)

## Contributing

Contributions are welcome! Please read the contributing guidelines in each project's README before submitting pull requests.