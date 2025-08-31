# DataProvider Current Functionality Specification

## Overview
DataProvider is a source generator that creates compile-time safe database extension methods from SQL queries, providing zero-overhead data access with full type safety.

## Core Features

### 1. Source Generation
- **Incremental Source Generators**: Uses Roslyn source generators for compile-time code generation
- **SQL File Processing**: Processes `.sql` files in the project to generate C# extension methods
- **LQL Integration**: Automatically processes `.lql` files through transpilation to SQL
- **Schema Inspection**: Inspects database schema at build time to generate appropriate types

### 2. Database Support
- **SQLite**: Full implementation with ANTLR parsing and schema inspection
- **SQL Server**: Implementation with schema inspection and query parsing
- **PostgreSQL**: Planned (in roadmap)

### 3. Type Safety
- **Result Type Pattern**: All operations return `Result<T>` for explicit error handling (no exceptions)
- **Nullable Reference Types**: Full support for nullable reference types
- **Compile-Time Validation**: SQL queries validated during compilation against actual schema
- **AOT Compatible**: Full ahead-of-time compilation support with no runtime reflection

### 4. Generated Code Architecture
- **Extension Methods**: Generates static extension methods on `IDbConnection` and `IDbTransaction`
- **Functional Style**: Pure static methods with no classes (only records)
- **Expression-Based**: Prefers expressions over statements
- **No Side Effects**: Pure functions with predictable behavior

### 5. Query Features
- **Parameterized Queries**: Full support for SQL parameters with type safety
- **CRUD Operations**: Auto-generated Insert/Update/Delete/Select methods
- **Complex Joins**: Support for multi-table joins with result grouping
- **Transactions**: Full transaction support through `IDbTransaction` extensions
- **Async/Await**: All database operations support async patterns

### 6. LINQ Integration (Recent Addition)
- **LINQ Select Expressions**: Support for LINQ-style select queries
- **Expression Trees**: Converts LINQ expressions to SQL
- **Type-Safe Queries**: Compile-time checking of LINQ queries
- **Predicate Building**: Complex WHERE clause construction

### 7. Code Generation Templates
- **ICodeTemplate Interface**: Basic template abstraction for code generation
- **DefaultCodeTemplate**: Standard implementation for generating extension methods
- **Database-Specific Templates**: SQLite and SQL Server specific code generation

### 8. Schema Features
- **Table Discovery**: Automatic discovery of database tables
- **Column Type Mapping**: Maps SQL types to C# types
- **Primary Key Detection**: Identifies primary keys for CRUD operations
- **Foreign Key Recognition**: Understands relationships for joins

### 9. Configuration
- **DataProvider.json**: Configuration file for specifying queries and settings
- **Grouping Configuration**: `.grouping.json` files for complex result set mapping
- **Namespace Control**: Configurable namespace generation

### 10. Testing Support
- **Integration Tests**: Comprehensive test suite for database operations
- **Sample Data Seeding**: Built-in data seeding for testing
- **Multiple Test Databases**: Support for testing against different database engines

## Implementation Details

### Parser Implementation
- **ANTLR Grammar**: Uses ANTLR for SQL parsing (SQLite)
- **Custom SQL Parsers**: Database-specific parsing implementations
- **Parameter Extraction**: Automatic detection and typing of SQL parameters

### Code Generation Pipeline
1. SQL file discovery during build
2. Database schema inspection
3. SQL parsing and validation
4. C# code generation
5. Compilation integration

### Error Handling
- **Result<T,E> Pattern**: All operations return success/failure results
- **No Exceptions**: Exception-free architecture for predictable behavior
- **Detailed Error Messages**: Comprehensive error information for debugging

## File Structure
```
DataProvider/
├── CodeGeneration/
│   ├── ICodeTemplate.cs
│   ├── DefaultCodeTemplate.cs
│   └── DataAccessGenerator.cs
├── DbConnectionExtensions.cs
├── ICodeGenerator.cs
└── Database-specific implementations
```

## Usage Pattern
```csharp
// Generated extension method from SQL file
var result = await connection.GetCustomersAsync(isActive: true);
if (result.IsSuccess)
{
    foreach (var customer in result.Value)
    {
        // Type-safe access to customer properties
    }
}
```