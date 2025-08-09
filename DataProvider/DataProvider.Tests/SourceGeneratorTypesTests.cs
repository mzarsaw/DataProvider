using Selecta;
using Xunit;

namespace DataProvider.Tests;

/// <summary>
/// Tests for source generator types
/// </summary>
public sealed class SourceGeneratorTypesTests
{
    [Fact]
    public void ColumnInfo_CanBeCreatedWithBasicProperties()
    {
        // Arrange & Act
        var column = ColumnInfo.Named("Id");

        // Assert
        var namedColumn = (NamedColumn)column;
        Assert.Equal("Id", namedColumn.Name);
        Assert.Null(column.Alias);
        Assert.Null(namedColumn.TableAlias);
    }

    [Fact]
    public void ColumnInfo_CanBeCreatedWithAllProperties()
    {
        // Arrange & Act
        var column = ColumnInfo.Named("Name", "c", "CustomerName");

        // Assert
        var namedColumn = (NamedColumn)column;
        Assert.Equal("Name", namedColumn.Name);
        Assert.Equal("CustomerName", column.Alias);
        Assert.Equal("c", namedColumn.TableAlias);
    }

    [Fact]
    public void ColumnInfo_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var column1 = ColumnInfo.Named("Id", "i", "InvoiceId");
        var column2 = ColumnInfo.Named("Id", "i", "InvoiceId");
        var column3 = ColumnInfo.Named("Name", "i", "InvoiceId");

        // Act & Assert
        Assert.Equal(column1, column2);
        Assert.NotEqual(column1, column3);
    }

    [Fact]
    public void TableInfo_CanBeCreatedWithNameOnly()
    {
        // Arrange & Act
        var table = new TableInfo("Invoice", null);

        // Assert
        Assert.Equal("Invoice", table.Name);
        Assert.Null(table.Alias);
    }

    [Fact]
    public void TableInfo_CanBeCreatedWithAlias()
    {
        // Arrange & Act
        var table = new TableInfo("Invoice", "i");

        // Assert
        Assert.Equal("Invoice", table.Name);
        Assert.Equal("i", table.Alias);
    }

    [Fact]
    public void TableInfo_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var table1 = new TableInfo("Invoice", "i");
        var table2 = new TableInfo("Invoice", "i");
        var table3 = new TableInfo("InvoiceLine", "i");

        // Act & Assert
        Assert.Equal(table1, table2);
        Assert.NotEqual(table1, table3);
    }

    [Fact]
    public void ParameterInfo_CanBeCreatedWithNameOnly()
    {
        // Arrange & Act
        var parameter = new ParameterInfo("id");

        // Assert
        Assert.Equal("id", parameter.Name);
        Assert.Equal("NVARCHAR", parameter.SqlType); // Default
    }

    [Fact]
    public void ParameterInfo_CanBeCreatedWithCustomSqlType()
    {
        // Arrange & Act
        var parameter = new ParameterInfo("id", "INT");

        // Assert
        Assert.Equal("id", parameter.Name);
        Assert.Equal("INT", parameter.SqlType);
    }

    [Fact]
    public void ParameterInfo_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var param1 = new ParameterInfo("id", "INT");
        var param2 = new ParameterInfo("id", "INT");
        var param3 = new ParameterInfo("name", "INT");

        // Act & Assert
        Assert.Equal(param1, param2);
        Assert.NotEqual(param1, param3);
    }

    [Fact]
    public void SqlStatement_CanBeCreatedWithDefaults()
    {
        // Arrange & Act
        var statement = new SqlStatement();

        // Assert
        Assert.NotNull(statement.SelectList);
        Assert.Empty(statement.SelectList);
        Assert.NotNull(statement.Tables);
        Assert.Empty(statement.Tables);
        Assert.NotNull(statement.Parameters);
        Assert.Empty(statement.Parameters);
        Assert.NotNull(statement.JoinGraph);
        Assert.Equal(0, statement.JoinGraph.Count);
        Assert.Equal("SELECT", statement.QueryType);
        Assert.False(statement.HasOneToManyJoins);
        Assert.Null(statement.ParseError);
    }

    [Fact]
    public void SqlStatement_CanBeCreatedWithCustomProperties()
    {
        // Arrange
        var selectList = new List<ColumnInfo>
        {
            ColumnInfo.Named("Id", "i"),
            ColumnInfo.Named("Name", "c", "CustomerName"),
        }.AsReadOnly();

        var tables = new List<TableInfo> { new("Invoice", "i"), new("Customer", "c") }.AsReadOnly();

        var parameters = new List<ParameterInfo>
        {
            new("id", "INT"),
            new("name", "NVARCHAR"),
        }.AsReadOnly();

        var joinGraph = new JoinGraph();
        joinGraph.Add("Invoice", "Customer", "i.CustomerId = c.Id");

        // Act
        var statement = new SqlStatement
        {
            SelectList = selectList,
            Tables = tables,
            Parameters = parameters,
            JoinGraph = joinGraph,
            QueryType = "UPDATE",
            ParseError = "Test error",
        };

        // Assert
        Assert.Equal(2, statement.SelectList.Count);
        Assert.Equal(2, statement.Tables.Count);
        Assert.Equal(2, statement.Parameters.Count);
        Assert.Equal(1, statement.JoinGraph.Count);
        Assert.Equal("UPDATE", statement.QueryType);
        Assert.True(statement.HasOneToManyJoins);
        Assert.Equal("Test error", statement.ParseError);
    }

    [Fact]
    public void SqlStatement_HasOneToManyJoins_ReturnsTrueWhenJoinsExist()
    {
        // Arrange
        var joinGraph = new JoinGraph();
        joinGraph.Add("Invoice", "InvoiceLine", "i.Id = l.InvoiceId");

        var statement = new SqlStatement { JoinGraph = joinGraph };

        // Act & Assert
        Assert.True(statement.HasOneToManyJoins);
    }

    [Fact]
    public void SqlStatement_HasOneToManyJoins_ReturnsFalseWhenNoJoins()
    {
        // Arrange
        var statement = new SqlStatement();

        // Act & Assert
        Assert.False(statement.HasOneToManyJoins);
    }

    [Fact]
    public void SqlStatement_CanHaveParseError()
    {
        // Arrange & Act
        var statement = new SqlStatement { ParseError = "Syntax error near line 5" };

        // Assert
        Assert.Equal("Syntax error near line 5", statement.ParseError);
    }

    [Fact]
    public void SqlStatement_Collections_AreImmutable()
    {
        // Arrange
        var selectList = new List<ColumnInfo> { ColumnInfo.Named("Id") }.AsReadOnly();
        var tables = new List<TableInfo> { new("Invoice", null) }.AsReadOnly();
        var parameters = new List<ParameterInfo> { new("id") }.AsReadOnly();

        var statement = new SqlStatement
        {
            SelectList = selectList,
            Tables = tables,
            Parameters = parameters,
        };

        // Act & Assert - These should be read-only collections
        Assert.IsAssignableFrom<IReadOnlyList<ColumnInfo>>(statement.SelectList);
        Assert.IsAssignableFrom<IReadOnlyList<TableInfo>>(statement.Tables);
        Assert.IsAssignableFrom<IReadOnlyList<ParameterInfo>>(statement.Parameters);
    }

    [Fact]
    public void JoinRelationship_CanBeCreatedWithBasicProperties()
    {
        // Arrange & Act
        var relationship = new JoinRelationship("Invoice", "InvoiceLine", "i.Id = l.InvoiceId");

        // Assert
        Assert.Equal("Invoice", relationship.LeftTable);
        Assert.Equal("InvoiceLine", relationship.RightTable);
        Assert.Equal("i.Id = l.InvoiceId", relationship.Condition);
        Assert.Equal("INNER", relationship.JoinType); // Default
    }

    [Fact]
    public void JoinRelationship_CanBeCreatedWithCustomJoinType()
    {
        // Arrange & Act
        var relationship = new JoinRelationship(
            "Invoice",
            "Customer",
            "i.CustomerId = c.Id",
            "LEFT"
        );

        // Assert
        Assert.Equal("Invoice", relationship.LeftTable);
        Assert.Equal("Customer", relationship.RightTable);
        Assert.Equal("i.CustomerId = c.Id", relationship.Condition);
        Assert.Equal("LEFT", relationship.JoinType);
    }

    [Fact]
    public void JoinRelationship_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var join1 = new JoinRelationship("Invoice", "InvoiceLine", "i.Id = l.InvoiceId", "INNER");
        var join2 = new JoinRelationship("Invoice", "InvoiceLine", "i.Id = l.InvoiceId", "INNER");
        var join3 = new JoinRelationship("Invoice", "Customer", "i.Id = l.InvoiceId", "INNER");

        // Act & Assert
        Assert.Equal(join1, join2);
        Assert.NotEqual(join1, join3);
    }
}
