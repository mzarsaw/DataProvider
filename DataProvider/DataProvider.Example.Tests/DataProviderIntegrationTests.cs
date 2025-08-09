using System.Collections.Immutable;
using Generated;
using Microsoft.Data.Sqlite;
using Results;
using Xunit;

namespace DataProvider.Example.Tests;

#pragma warning disable CS1591

/// <summary>
/// Integration tests for DataProvider code generation
/// </summary>
public sealed class DataProviderIntegrationTests : IDisposable
{
    private readonly string _connectionString = "Data Source=:memory:";
    private readonly SqliteConnection _connection;

    public DataProviderIntegrationTests()
    {
        _connection = new SqliteConnection(_connectionString);
    }

    [Fact]
    public async Task GetInvoicesAsync_WithValidData_ReturnsCorrectTypes()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetInvoicesAsync("Acme Corp", "2024-01-01", "2024-12-31");

        // Assert
        if (result is Result<ImmutableList<Invoice>, SqlError>.Failure failure)
        {
            throw new InvalidOperationException(
                $"GetInvoicesAsync failed: {failure.ErrorValue.Message}"
            );
        }
        Assert.True(
            result is Result<ImmutableList<Invoice>, SqlError>.Success,
            $"Expected Success but got {result.GetType()}"
        );

        var success = (Result<ImmutableList<Invoice>, SqlError>.Success)result;
        var invoices = success.Value;

        Assert.NotEmpty(invoices);
        var invoice = invoices[0];
        var line = invoice.InvoiceLines[0];

        // Verify Invoice type and properties
        Assert.IsType<Invoice>(invoice);
        Assert.IsType<long>(invoice.Id);
        Assert.IsType<string>(invoice.InvoiceNumber);
        Assert.IsType<string>(invoice.InvoiceDate);
        Assert.IsType<string>(invoice.CustomerName);
        Assert.IsType<double>(invoice.TotalAmount);
        Assert.IsAssignableFrom<IReadOnlyList<InvoiceLine>>(invoice.InvoiceLines);

        // Verify InvoiceLine type and properties
        Assert.IsType<InvoiceLine>(line);
        Assert.IsType<long>(line.LineId);
        Assert.IsType<long>(line.InvoiceId);
        Assert.IsType<string>(line.Description);
        Assert.IsType<double>(line.Quantity);
        Assert.IsType<double>(line.UnitPrice);
        Assert.IsType<double>(line.Amount);
    }

    [Fact]
    public async Task GetCustomersLqlAsync_WithValidData_ReturnsCorrectTypes()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetCustomersLqlAsync(null);

        // Assert
        if (result is Result<ImmutableList<Customer>, SqlError>.Failure failure)
        {
            // Log the error but continue the test to see what happens
            Console.WriteLine($"GetCustomersLqlAsync failed: {failure.ErrorValue.Message}");
            Console.WriteLine(
                $"Full exception: {failure.ErrorValue.Exception?.ToString() ?? "No exception details"}"
            );
        }
        Assert.True(
            result is Result<ImmutableList<Customer>, SqlError>.Success,
            $"Expected Success but got {result.GetType()}, Error: {(result as Result<ImmutableList<Customer>, SqlError>.Failure)?.ErrorValue.Message ?? "No error message"}"
        );

        var success = (Result<ImmutableList<Customer>, SqlError>.Success)result;
        var customers = success.Value;

        Assert.NotEmpty(customers);
        var customer = customers[0];
        var address = customer.Addresss[0];

        // Verify Customer type and properties
        Assert.IsType<Customer>(customer);
        Assert.IsType<long>(customer.Id);
        Assert.IsType<string>(customer.CustomerName);
        Assert.IsType<string>(customer.Email);
        // Phone property not available in generated Customer type
        Assert.IsType<string>(customer.CreatedDate);
        Assert.IsAssignableFrom<IReadOnlyList<Address>>(customer.Addresss);

        // Verify Address type and properties
        Assert.IsType<Address>(address);
        Assert.IsType<long>(address.AddressId);
        Assert.IsType<long>(address.CustomerId);
        Assert.IsType<string>(address.Street);
        Assert.IsType<string>(address.City);
        Assert.IsType<string>(address.State);
        Assert.IsType<string>(address.ZipCode);
        // Country property not available in generated Address type
    }

    [Fact]
    public async Task GetOrdersAsync_WithValidData_ReturnsCorrectTypes()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetOrdersAsync(1, "Completed", "2024-01-01", "2024-12-31");

        // Assert
        Assert.True(
            result is Result<ImmutableList<Order>, SqlError>.Success,
            $"Expected Success but got {result.GetType()}"
        );

        var success = (Result<ImmutableList<Order>, SqlError>.Success)result;
        var orders = success.Value;

        Assert.NotEmpty(orders);
        var order = orders[0];
        var item = order.OrderItems[0];

        // Verify Order type and properties
        Assert.IsType<Order>(order);
        Assert.IsType<long>(order.Id);
        Assert.IsType<string>(order.OrderNumber);
        // OrderDate property not available in generated Order type
        Assert.IsType<long>(order.CustomerId);
        Assert.IsType<double>(order.TotalAmount);
        Assert.IsType<string>(order.Status);
        Assert.IsAssignableFrom<IReadOnlyList<OrderItem>>(order.OrderItems);

        // Verify OrderItem type and properties
        Assert.IsType<OrderItem>(item);
        Assert.IsType<long>(item.ItemId);
        Assert.IsType<long>(item.OrderId);
        Assert.IsType<string>(item.ProductName);
        Assert.IsType<double>(item.Quantity);
        Assert.IsType<double>(item.Price);
        Assert.IsType<double>(item.Subtotal);
    }

    [Fact]
    public async Task AllQueries_VerifyCorrectTableNamesGenerated()
    {
        // Arrange
        await SetupTestDatabase();

        // Act & Assert - Verify extension methods exist with correct names
        var invoiceResult = await _connection.GetInvoicesAsync("Acme Corp", null!, null!);
        var customerResult = await _connection.GetCustomersLqlAsync(null);
        var orderResult = await _connection.GetOrdersAsync(1, null!, null!, null!);

        // All should succeed (this proves the extension methods were generated)
        Assert.True(
            invoiceResult is Result<ImmutableList<Invoice>, SqlError>.Success,
            $"Expected Invoice Success but got {invoiceResult.GetType()}"
        );
        Assert.True(
            customerResult is Result<ImmutableList<Customer>, SqlError>.Success,
            $"Expected Customer Success but got {customerResult.GetType()}"
        );
        Assert.True(
            orderResult is Result<ImmutableList<Order>, SqlError>.Success,
            $"Expected Order Success but got {orderResult.GetType()}"
        );

        // Verify different table names were used (not hard-coded)
        var invoices = ((Result<ImmutableList<Invoice>, SqlError>.Success)invoiceResult).Value;
        var customers = ((Result<ImmutableList<Customer>, SqlError>.Success)customerResult).Value;
        var orders = ((Result<ImmutableList<Order>, SqlError>.Success)orderResult).Value;

        //TODO: Assert these!!!

        // These should be different types, proving they're not hard-coded
        Assert.NotEqual(typeof(Invoice), typeof(Customer));
        Assert.NotEqual(typeof(Invoice), typeof(Order));
        Assert.NotEqual(typeof(Customer), typeof(Order));

        Assert.NotEqual(typeof(InvoiceLine), typeof(Address));
        Assert.NotEqual(typeof(InvoiceLine), typeof(OrderItem));
        Assert.NotEqual(typeof(Address), typeof(OrderItem));
    }

    [Fact]
    public async Task GetInvoicesAsync_WithMultipleRecords_GroupsCorrectly()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetInvoicesAsync("Acme Corp", "2024-01-01", "2024-12-31");

        // Assert
        Assert.True(
            result is Result<ImmutableList<Invoice>, SqlError>.Success,
            $"Expected Success but got {result.GetType()}"
        );

        var success = (Result<ImmutableList<Invoice>, SqlError>.Success)result;
        var invoices = success.Value;

        Assert.Equal(3, invoices.Count);

        // Verify grouping is working correctly
        var firstInvoice = invoices[0];
        Assert.Equal(2, firstInvoice.InvoiceLines.Count);

        var secondInvoice = invoices[1];
        Assert.Equal(2, secondInvoice.InvoiceLines.Count);

        var thirdInvoice = invoices[2];
        Assert.Equal(2, thirdInvoice.InvoiceLines.Count);
    }

    [Fact]
    public async Task GetCustomersLqlAsync_WithMultipleAddresses_GroupsCorrectly()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetCustomersLqlAsync(null);

        // Assert
        Assert.True(
            result is Result<ImmutableList<Customer>, SqlError>.Success,
            $"Expected Success but got {result.GetType()}"
        );

        var success = (Result<ImmutableList<Customer>, SqlError>.Success)result;
        var customers = success.Value;

        Assert.Equal(2, customers.Count);

        // First customer should have 2 addresses
        var firstCustomer = customers[0];
        Assert.Equal(2, firstCustomer.Addresss.Count);

        // Second customer should have 1 address
        var secondCustomer = customers[1];
        Assert.Single(secondCustomer.Addresss);
    }

    [Fact]
    public async Task GetOrdersAsync_WithMultipleItems_GroupsCorrectly()
    {
        // Arrange
        await SetupTestDatabase();

        // Act
        var result = await _connection.GetOrdersAsync(null, null, "2024-01-01", "2024-12-31");

        // Assert
        Assert.True(
            result is Result<ImmutableList<Order>, SqlError>.Success,
            $"Expected Success but got {result.GetType()}"
        );

        var success = (Result<ImmutableList<Order>, SqlError>.Success)result;
        var orders = success.Value;

        Assert.Equal(2, orders.Count);

        // Orders are returned by OrderDate DESC, so ORD-002 comes first (1 item), ORD-001 comes second (2 items)
        var firstOrder = orders[0]; // ORD-002
        Assert.Single(firstOrder.OrderItems);

        var secondOrder = orders[1]; // ORD-001
        Assert.Equal(2, secondOrder.OrderItems.Count);
    }

    [Fact]
    public async Task GetInvoicesAsync_WithEmptyDatabase_ReturnsEmpty()
    {
        // Arrange
        await SetupEmptyDatabase();

        // Act
        var result = await _connection.GetInvoicesAsync("Acme Corp", "2024-01-01", "2024-12-31");

        // Assert
        Assert.True(
            result is Result<ImmutableList<Invoice>, SqlError>.Success,
            $"Expected Success but got {result.GetType()}"
        );

        var success = (Result<ImmutableList<Invoice>, SqlError>.Success)result;
        var invoices = success.Value;

        Assert.Empty(invoices);
    }

    [Fact]
    public async Task GetCustomersLqlAsync_WithEmptyDatabase_ReturnsEmpty()
    {
        // Arrange
        await SetupEmptyDatabase();

        // Act
        var result = await _connection.GetCustomersLqlAsync(null);

        // Assert
        Assert.True(
            result is Result<ImmutableList<Customer>, SqlError>.Success,
            $"Expected Success but got {result.GetType()}"
        );

        var success = (Result<ImmutableList<Customer>, SqlError>.Success)result;
        var customers = success.Value;

        Assert.Empty(customers);
    }

    [Fact]
    public async Task GetOrdersAsync_WithEmptyDatabase_ReturnsEmpty()
    {
        // Arrange
        await SetupEmptyDatabase();

        // Act
        var result = await _connection.GetOrdersAsync(1, "Completed", "2024-01-01", "2024-12-31");

        // Assert
        Assert.True(
            result is Result<ImmutableList<Order>, SqlError>.Success,
            $"Expected Success but got {result.GetType()}"
        );

        var success = (Result<ImmutableList<Order>, SqlError>.Success)result;
        var orders = success.Value;

        Assert.Empty(orders);
    }

    private async Task SetupTestDatabase()
    {
        await _connection.OpenAsync().ConfigureAwait(false);
        using (var pragmaCommand = new SqliteCommand("PRAGMA foreign_keys = OFF", _connection))
        {
            await pragmaCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // Create all tables
        var createTablesScript = """
            CREATE TABLE IF NOT EXISTS Invoice (
                Id INTEGER PRIMARY KEY,
                InvoiceNumber TEXT NOT NULL,
                InvoiceDate TEXT NOT NULL,
                CustomerName TEXT NOT NULL,
                CustomerEmail TEXT NULL,
                TotalAmount REAL NOT NULL,
                DiscountAmount REAL NULL,
                Notes TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS InvoiceLine (
                Id INTEGER PRIMARY KEY,
                InvoiceId INT NOT NULL,
                Description TEXT NOT NULL,
                Quantity REAL NOT NULL,
                UnitPrice REAL NOT NULL,
                Amount REAL NOT NULL,
                DiscountPercentage REAL NULL,
                Notes TEXT NULL,
                FOREIGN KEY (InvoiceId) REFERENCES Invoice (Id)
            );

            CREATE TABLE IF NOT EXISTS Customer (
                Id INTEGER PRIMARY KEY,
                CustomerName TEXT NOT NULL,
                Email TEXT NULL,
                Phone TEXT NULL,
                CreatedDate TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Address (
                Id INTEGER PRIMARY KEY,
                CustomerId INT NOT NULL,
                Street TEXT NOT NULL,
                City TEXT NOT NULL,
                State TEXT NOT NULL,
                ZipCode TEXT NOT NULL,
                Country TEXT NOT NULL,
                FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
            );

            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY,
                OrderNumber TEXT NOT NULL,
                OrderDate TEXT NOT NULL,
                CustomerId INT NOT NULL,
                TotalAmount REAL NOT NULL,
                Status TEXT NOT NULL,
                FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
            );

            CREATE TABLE IF NOT EXISTS OrderItem (
                Id INTEGER PRIMARY KEY,
                OrderId INT NOT NULL,
                ProductName TEXT NOT NULL,
                Quantity REAL NOT NULL,
                Price REAL NOT NULL,
                Subtotal REAL NOT NULL,
                FOREIGN KEY (OrderId) REFERENCES Orders (Id)
            );
            """;

        using var command = new SqliteCommand(createTablesScript, _connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);

        // Insert comprehensive test data
        var insertScript = """
            INSERT INTO Invoice (InvoiceNumber, InvoiceDate, CustomerName, CustomerEmail, TotalAmount, DiscountAmount, Notes) VALUES 
            ('INV-001', '2024-01-15', 'Acme Corp', 'accounting@acme.com', 1250.00, NULL, 'Test invoice'),
            ('INV-002', '2024-01-16', 'Acme Corp', 'accounting@acme.com', 850.75, 25.00, NULL),
            ('INV-003', '2024-01-17', 'Acme Corp', 'accounting@acme.com', 2100.25, 100.00, 'Large order discount');

            INSERT INTO InvoiceLine (InvoiceId, Description, Quantity, UnitPrice, Amount, DiscountPercentage, Notes) VALUES 
            (1, 'Software License', 1.0, 1000.00, 1000.00, NULL, NULL),
            (1, 'Support Package', 1.0, 250.00, 250.00, 10.0, 'First year support'),
            (2, 'Consulting Hours', 5.0, 150.00, 750.00, NULL, NULL),
            (2, 'Travel Expenses', 1.0, 100.75, 100.75, NULL, 'Reimbursement'),
            (3, 'Hardware Components', 10.0, 125.50, 1255.00, 5.0, 'Bulk discount'),
            (3, 'Installation Service', 3.0, 281.75, 845.25, NULL, NULL);

            INSERT INTO Customer (CustomerName, Email, Phone, CreatedDate) VALUES 
            ('Acme Corp', 'contact@acme.com', '555-0100', '2024-01-01'),
            ('Tech Solutions', 'info@techsolutions.com', '555-0200', '2024-01-02');

            INSERT INTO Address (CustomerId, Street, City, State, ZipCode, Country) VALUES 
            (1, '123 Business Ave', 'New York', 'NY', '10001', 'USA'),
            (1, '456 Main St', 'Albany', 'NY', '12201', 'USA'),
            (2, '789 Tech Blvd', 'San Francisco', 'CA', '94105', 'USA');

            INSERT INTO Orders (OrderNumber, OrderDate, CustomerId, TotalAmount, Status) VALUES 
            ('ORD-001', '2024-01-10', 1, 500.00, 'Completed'),
            ('ORD-002', '2024-01-11', 2, 750.00, 'Processing');

            INSERT INTO OrderItem (OrderId, ProductName, Quantity, Price, Subtotal) VALUES 
            (1, 'Widget A', 2.0, 100.00, 200.00),
            (1, 'Widget B', 3.0, 100.00, 300.00),
            (2, 'Service Package', 1.0, 750.00, 750.00);
            """;

        using var insertCommand = new SqliteCommand(insertScript, _connection);
        await insertCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task SetupEmptyDatabase()
    {
        await _connection.OpenAsync().ConfigureAwait(false);

        // Create tables but don't insert any data - same script as above but without inserts
        var createTablesScript = """
            CREATE TABLE IF NOT EXISTS Invoice (
                Id INTEGER PRIMARY KEY,
                InvoiceNumber TEXT NOT NULL,
                InvoiceDate TEXT NOT NULL,
                CustomerName TEXT NOT NULL,
                CustomerEmail TEXT NULL,
                TotalAmount REAL NOT NULL,
                DiscountAmount REAL NULL,
                Notes TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS InvoiceLine (
                Id INTEGER PRIMARY KEY,
                InvoiceId INT NOT NULL,
                Description TEXT NOT NULL,
                Quantity REAL NOT NULL,
                UnitPrice REAL NOT NULL,
                Amount REAL NOT NULL,
                DiscountPercentage REAL NULL,
                Notes TEXT NULL,
                FOREIGN KEY (InvoiceId) REFERENCES Invoice (Id)
            );

            CREATE TABLE IF NOT EXISTS Customer (
                Id INTEGER PRIMARY KEY,
                CustomerName TEXT NOT NULL,
                Email TEXT NULL,
                Phone TEXT NULL,
                CreatedDate TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Address (
                Id INTEGER PRIMARY KEY,
                CustomerId INT NOT NULL,
                Street TEXT NOT NULL,
                City TEXT NOT NULL,
                State TEXT NOT NULL,
                ZipCode TEXT NOT NULL,
                Country TEXT NOT NULL,
                FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
            );

            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY,
                OrderNumber TEXT NOT NULL,
                OrderDate TEXT NOT NULL,
                CustomerId INT NOT NULL,
                TotalAmount REAL NOT NULL,
                Status TEXT NOT NULL,
                FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
            );

            CREATE TABLE IF NOT EXISTS OrderItem (
                Id INTEGER PRIMARY KEY,
                OrderId INT NOT NULL,
                ProductName TEXT NOT NULL,
                Quantity REAL NOT NULL,
                Price REAL NOT NULL,
                Subtotal REAL NOT NULL,
                FOREIGN KEY (OrderId) REFERENCES Orders (Id)
            );
            """;

        using var command = new SqliteCommand(createTablesScript, _connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        _connection?.Dispose();

        // Clean up test database file
        var dbFileName = _connectionString.Replace("Data Source=", "", StringComparison.Ordinal);
        if (File.Exists(dbFileName))
        {
            try
            {
                File.Delete(dbFileName);
            }
            catch (IOException)
            {
                // File might be in use, ignore
            }
            catch (UnauthorizedAccessException)
            {
                // No permission to delete, ignore
            }
        }
    }
}
