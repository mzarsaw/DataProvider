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
    InvoiceId SMALLINT NOT NULL,
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
    CustomerId SMALLINT NOT NULL,
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
    CustomerId SMALLINT NOT NULL,
    TotalAmount REAL NOT NULL,
    Status TEXT NOT NULL,
    FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
);

CREATE TABLE IF NOT EXISTS OrderItem (
    Id INTEGER PRIMARY KEY,
    OrderId SMALLINT NOT NULL,
    ProductName TEXT NOT NULL,
    Quantity REAL NOT NULL,
    Price REAL NOT NULL,
    Subtotal REAL NOT NULL,
    FOREIGN KEY (OrderId) REFERENCES Orders (Id)
);
