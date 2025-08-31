# LQL (Lambda Query Language) Current Functionality Specification

## Overview
LQL is a functional pipeline-style DSL that transpiles to procedural SQL, providing an intuitive and composable way to write database queries using lambda expressions and pipeline operators. While it provides a cross platform way to write SQL queries, it really shines as a way of writing procedural SQL code in a more functional way. 

## Core Features

### 1. Pipeline Syntax
- **Pipeline Operator (`|>`)**: Chain operations in a functional style
- **Composition**: Build complex queries from simple operations
- **Readability**: Linear flow matches thought process
- **Immutability**: Each operation returns a new query state

### 2. Database Support
- **SQLite**: Full transpilation support with C# function generation
- **SQL Server**: Complete dialect implementation
- **PostgreSQL**: Full support with PostgreSQL-specific features
- **Cross-Database**: Write once, transpile to multiple SQL dialects

### 3. Query Operations

#### Basic Operations
- **select()**: Column selection with aliasing support
- **filter()**: WHERE clause with lambda expressions
- **distinct()**: Remove duplicate rows
- **limit()**: Row limiting
- **offset()**: Row skipping for pagination

#### Join Operations
- **join()**: Inner joins with ON conditions
- **left_join()**: Left outer joins
- **right_join()**: Right outer joins (dialect-specific)
- **cross_join()**: Cartesian product

#### Grouping & Aggregation
- **group_by()**: Group rows by columns
- **having()**: Filter grouped results
- **Aggregate Functions**: COUNT, SUM, AVG, MIN, MAX

#### Set Operations
- **union()**: Combine queries (distinct)
- **union_all()**: Combine queries (with duplicates)
- **intersect()**: Common rows between queries
- **except()**: Rows in first query not in second

#### Ordering
- **order_by()**: Sort results (ASC/DESC)
- **Multiple columns**: Support for complex sorting

### 4. Lambda Expression Support
- **Filter Predicates**: `fn(row) => row.column = value`
- **Complex Conditions**: AND, OR, NOT operators
- **Comparison Operators**: =, !=, <, >, <=, >=
- **Pattern Matching**: LIKE, NOT LIKE
- **IN Operator**: Check membership in lists
- **NULL Handling**: IS NULL, IS NOT NULL

### 5. Function Support

#### String Functions
- UPPER(), LOWER()
- LENGTH(), CONCAT()
- SUBSTRING(), TRIM()

#### Date Functions
- NOW(), DATE()
- YEAR(), MONTH(), DAY()
- Date arithmetic

#### Mathematical Functions
- Basic arithmetic: +, -, *, /
- Modulo operator: %
- Mathematical functions per dialect

#### Conditional Functions
- CASE WHEN expressions
- COALESCE()
- NULLIF()

### 6. Advanced Features

#### Subqueries
- **IN subqueries**: Filter based on subquery results
- **Scalar subqueries**: Single value subqueries
- **Correlated subqueries**: Reference outer query

#### Variables & Let Bindings
- **let statements**: Define reusable query fragments
- **Variable references**: Use defined queries in expressions

#### CTEs (Common Table Expressions)
- **WITH clause**: Define temporary named result sets
- **Recursive CTEs**: Planned feature

### 7. Tooling

#### CLI Tool (LqlCli)
- **Transpilation**: Convert .lql to .sql files
- **Validation**: Syntax checking without transpilation
- **Console Output**: Quick testing of queries
- **File Processing**: Batch processing of LQL files

#### VS Code Extension
- **Syntax Highlighting**: LQL-specific syntax colors
- **IntelliSense**: Auto-completion for keywords
- **Error Diagnostics**: Real-time syntax validation
- **Snippets**: Common query patterns
- **Format on Save**: Code formatting

#### Browser Playground
- **Interactive Editor**: Write and test LQL in browser
- **Live Transpilation**: See SQL output in real-time
- **Multiple Dialects**: Switch between SQL targets
- **Schema Explorer**: Browse database structure
- **Example Queries**: Learn from examples

### 8. Parser Implementation

#### ANTLR Grammar
- **Lexer Rules**: Token definitions
- **Parser Rules**: Grammar structure
- **Visitor Pattern**: AST traversal and transformation

#### AST (Abstract Syntax Tree)
- **Statement Types**: Query, expression, operation nodes
- **Type System**: Basic type inference
- **Validation**: Semantic analysis during parsing

### 9. Transpilation Pipeline
1. **Lexical Analysis**: Tokenize LQL input
2. **Parsing**: Build AST from tokens
3. **Semantic Analysis**: Validate and type-check
4. **Optimization**: Query optimization passes
5. **SQL Generation**: Dialect-specific SQL output

### 10. Integration with DataProvider
- **Automatic Processing**: .lql files processed during build
- **Code Generation**: Extension methods from LQL queries
- **Type Safety**: Compile-time validation
- **Parameter Binding**: Automatic parameter detection

## Implementation Architecture

### Core Components
```
Lql/
├── Parsing/
│   ├── Lql.g4 (ANTLR grammar)
│   ├── LqlLexer.cs
│   ├── LqlParser.cs
│   └── LqlToAstVisitor.cs
├── AST/
│   ├── Statement types
│   └── Expression types
├── PipelineProcessor.cs
└── Dialect implementations
```

### Dialect Structure
Each dialect (SQLite, SQL Server, PostgreSQL) provides:
- SQL generation context
- Function mappings
- Type mappings
- Dialect-specific features

## Current Limitations
- No window functions
- Limited CTE support
- No stored procedure generation
- No trigger generation (planned)
- No database-specific optimizations
- Limited type inference

## Usage Examples

### Simple Query
```lql
users |> select(id, name, email)
```

### Complex Query
```lql
let active_orders = Order
|> filter(fn(row) => row.status = 'active')
|> select(*)

Customer
|> join(active_orders, on = Customer.Id = active_orders.CustomerId)
|> group_by(Customer.Id, Customer.Name)
|> having(fn(row) => COUNT(*) > 5)
|> select(Customer.Name, COUNT(*) AS OrderCount)
|> order_by(OrderCount DESC)
```

## Transpilation Output
LQL queries are transpiled to optimized, dialect-specific SQL maintaining semantic equivalence while leveraging database-specific features where appropriate.