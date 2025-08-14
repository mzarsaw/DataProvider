using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lql.Browser.Models;
using Lql.SQLite;
using Microsoft.Data.Sqlite;
using Results;

namespace Lql.Browser.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private string _resultsHeader = "FirstName";

    [ObservableProperty]
    private string _executionTime = "46 ms";

    [ObservableProperty]
    private string _rowCount = "50 rows";

    [ObservableProperty]
    private string _connectionStatusText = "Connected";

    [ObservableProperty]
    private string _connectionStatusColor = "#228B22";

    [ObservableProperty]
    private string _databasePath = "/path/to/database.db";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private ObservableCollection<QueryResultRow>? _queryResults;

    [ObservableProperty]
    private ObservableCollection<string>? _columnNames;

    [ObservableProperty]
    private FileTab? _activeTab;

    public ObservableCollection<FileTab> FileTabs { get; } = [];

    public ObservableCollection<string> DatabaseTables { get; } = [];
    public ObservableCollection<string> DatabaseViews { get; } = [];

    private SqliteConnection? _connection;
    private DataTable? _currentDataTable;
    private int _nextTabNumber = 1;

    public ICommand ConnectDatabaseCommand { get; }
    public ICommand NewFileCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand SaveFileCommand { get; }
    public ICommand SaveAsFileCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand SwitchTabCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand SelectTableCommand { get; }
    public ICommand ExecuteQueryCommand { get; }

    public MainWindowViewModel()
    {
        ConnectDatabaseCommand = new AsyncRelayCommand(ConnectDatabaseAsync);
        NewFileCommand = new RelayCommand<string>(NewFile);
        OpenFileCommand = new AsyncRelayCommand(OpenFileAsync);
        SaveFileCommand = new AsyncRelayCommand(SaveFileAsync);
        SaveAsFileCommand = new AsyncRelayCommand(SaveAsFileAsync);
        CloseTabCommand = new RelayCommand<FileTab>(CloseTab);
        SwitchTabCommand = new RelayCommand<FileTab>(SwitchTab);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync);
        ExportJsonCommand = new AsyncRelayCommand(ExportJsonAsync);
        SelectTableCommand = new RelayCommand<string>(SelectTable);
        ExecuteQueryCommand = new AsyncRelayCommand(ExecuteQueryAsync);

        ConnectionStatusText = "Disconnected";
        ConnectionStatusColor = "#9CA3AF";
        DatabasePath = "No database selected";

        // Create initial LQL tab
        NewFile("lql");
    }

    private async Task ConnectDatabaseAsync()
    {
        try
        {
            var dialog = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select SQLite Database",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("SQLite Database")
                    {
                        Patterns = ["*.db", "*.sqlite", "*.sqlite3"],
                    },
                ],
            };

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(dialog);
                if (files.Count > 0)
                {
                    await ConnectToDatabase(files[0].Path.LocalPath);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private async Task ConnectToDatabase(string databasePath)
    {
        try
        {
            Console.WriteLine($"=== Connecting to database: {databasePath} ===");
            _connection?.Close();

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
            }.ToString();

            Console.WriteLine($"Connection string: {connectionString}");
            _connection = new SqliteConnection(connectionString);
            await _connection.OpenAsync();
            Console.WriteLine("Database connection opened successfully");

            DatabasePath = databasePath;
            ConnectionStatusText = "Connected";
            ConnectionStatusColor = "#228B22";
            StatusMessage = "Database connected successfully";

            await LoadDatabaseSchema();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"=== Database connection failed ===");
            Console.WriteLine($"Exception: {ex}");
            ConnectionStatusText = "Error";
            ConnectionStatusColor = "#FF0000";
            StatusMessage = $"Connection failed: {ex.Message}";
        }
    }

    private async Task LoadDatabaseSchema()
    {
        if (_connection == null)
            return;

        try
        {
            DatabaseTables.Clear();
            DatabaseViews.Clear();

            var command = _connection.CreateCommand();
            command.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DatabaseTables.Add(reader.GetString(0));
            }

            command.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'view' ORDER BY name";
            using var viewReader = await command.ExecuteReaderAsync();
            while (await viewReader.ReadAsync())
            {
                DatabaseViews.Add(viewReader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading schema: {ex.Message}";
        }
    }

    private void NewFile(string? fileType)
    {
        var tab = FileOperations.CreateNewTab(fileType ?? "lql", _nextTabNumber);
        FileTabs.Add(tab);
        FileOperations.SwitchActiveTab(FileTabs, tab);
        ActiveTab = tab;
        _nextTabNumber++;
    }

    private async Task OpenFileAsync()
    {
        try
        {
            var dialog = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Open File",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("LQL Files")
                    {
                        Patterns = ["*.lql"],
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("SQL Files")
                    {
                        Patterns = ["*.sql"],
                    },
                ],
            };

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(dialog);
                if (files.Count > 0)
                {
                    var filePath = files[0].Path.LocalPath;
                    var contentResult = await FileOperations.ReadFileAsync(filePath);

                    if (contentResult is Result<string, string>.Success success)
                    {
                        var tab = FileOperations.CreateTabFromFile(filePath, success.Value);
                        FileTabs.Add(tab);
                        FileOperations.SwitchActiveTab(FileTabs, tab);
                        ActiveTab = tab;
                    }
                    else if (contentResult is Result<string, string>.Failure failure)
                    {
                        StatusMessage = failure.ErrorValue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening file: {ex.Message}";
        }
    }

    private async Task SaveFileAsync()
    {
        if (ActiveTab == null)
            return;

        if (string.IsNullOrEmpty(ActiveTab.FilePath))
        {
            await SaveAsFileAsync();
            return;
        }

        var result = await FileOperations.WriteFileAsync(ActiveTab.FilePath, ActiveTab.Content);
        if (result is Result<Unit, string>.Success)
        {
            ActiveTab.IsModified = false;
            StatusMessage = $"Saved {ActiveTab.FileName}";
        }
        else if (result is Result<Unit, string>.Failure failure)
        {
            StatusMessage = failure.ErrorValue;
        }
    }

    private async Task SaveAsFileAsync()
    {
        if (ActiveTab == null)
            return;

        try
        {
            var dialog = new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save File As",
                SuggestedFileName = ActiveTab.FileName,
                FileTypeChoices =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("LQL Files")
                    {
                        Patterns = ["*.lql"],
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("SQL Files")
                    {
                        Patterns = ["*.sql"],
                    },
                ],
            };

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(dialog);
                if (file != null)
                {
                    var filePath = file.Path.LocalPath;
                    var result = await FileOperations.WriteFileAsync(filePath, ActiveTab.Content);

                    if (result is Result<Unit, string>.Success)
                    {
                        FileOperations.UpdateFileType(ActiveTab, filePath);
                        ActiveTab.IsModified = false;
                        StatusMessage = $"Saved {ActiveTab.FileName}";
                    }
                    else if (result is Result<Unit, string>.Failure failure)
                    {
                        StatusMessage = failure.ErrorValue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving file: {ex.Message}";
        }
    }

    private void CloseTab(FileTab? tab)
    {
        if (tab == null)
            return;

        var newActiveTab = FileOperations.RemoveTab(FileTabs, tab);

        if (ActiveTab == tab)
        {
            ActiveTab = newActiveTab;
            if (newActiveTab != null)
            {
                FileOperations.SwitchActiveTab(FileTabs, newActiveTab);
            }
        }

        if (FileTabs.Count == 0)
        {
            NewFile("lql");
        }
    }

    private void SwitchTab(FileTab? tab)
    {
        if (tab == null)
            return;

        FileOperations.SwitchActiveTab(FileTabs, tab);
        ActiveTab = tab;
    }

    private async Task ExportCsvAsync()
    {
        if (_currentDataTable == null || _currentDataTable.Rows.Count == 0)
        {
            StatusMessage = "No data to export";
            return;
        }

        try
        {
            var dialog = new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Export CSV",
                SuggestedFileName = "export.csv",
                FileTypeChoices =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("CSV Files")
                    {
                        Patterns = ["*.csv"],
                    },
                ],
            };

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(dialog);
                if (file != null)
                {
                    var result = await DataExport.ExportToCsvAsync(
                        _currentDataTable,
                        file.Path.LocalPath
                    );
                    if (result is Result<Unit, string>.Success)
                    {
                        StatusMessage = $"Exported {_currentDataTable.Rows.Count} rows to CSV";
                    }
                    else if (result is Result<Unit, string>.Failure failure)
                    {
                        StatusMessage = failure.ErrorValue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting CSV: {ex.Message}";
        }
    }

    private async Task ExportJsonAsync()
    {
        if (_currentDataTable == null || _currentDataTable.Rows.Count == 0)
        {
            StatusMessage = "No data to export";
            return;
        }

        try
        {
            var dialog = new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Export JSON",
                SuggestedFileName = "export.json",
                FileTypeChoices =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("JSON Files")
                    {
                        Patterns = ["*.json"],
                    },
                ],
            };

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(dialog);
                if (file != null)
                {
                    var result = await DataExport.ExportToJsonAsync(
                        _currentDataTable,
                        file.Path.LocalPath
                    );
                    if (result is Result<Unit, string>.Success)
                    {
                        StatusMessage = $"Exported {_currentDataTable.Rows.Count} rows to JSON";
                    }
                    else if (result is Result<Unit, string>.Failure failure)
                    {
                        StatusMessage = failure.ErrorValue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting JSON: {ex.Message}";
        }
    }

    private void SelectTable(string? tableName)
    {
        if (string.IsNullOrEmpty(tableName))
            return;

        // Create a new LQL tab with the table query
        var newTab = FileOperations.CreateNewTab("lql", _nextTabNumber);
        newTab.Content = $"{tableName} |> select(*) |> limit(50)";
        newTab.IsModified = true;
        
        FileTabs.Add(newTab);
        FileOperations.SwitchActiveTab(FileTabs, newTab);
        ActiveTab = newTab;
        _nextTabNumber++;
    }

    private async Task ExecuteQueryAsync()
    {
        Console.WriteLine("=== ExecuteQueryAsync Started ===");

        if (_connection == null)
        {
            Console.WriteLine("ERROR: No database connection");
            StatusMessage = "No database connection";
            return;
        }

        if (ActiveTab == null || string.IsNullOrWhiteSpace(ActiveTab.Content))
        {
            Console.WriteLine("ERROR: No query to execute");
            StatusMessage = "No query to execute";
            return;
        }

        Console.WriteLine($"Query Text: {ActiveTab.Content}");
        Console.WriteLine($"Is LQL Mode: {ActiveTab.FileType == FileType.Lql}");

        try
        {
            var stopwatch = Stopwatch.StartNew();
            StatusMessage = "Executing query...";

            string sqlToExecute;

            if (ActiveTab.FileType == FileType.Lql)
            {
                Console.WriteLine("Converting LQL to SQL...");
                var lqlStatement = LqlStatementConverter.ToStatement(ActiveTab.Content);
                if (lqlStatement is Result<LqlStatement, SqlError>.Success lqlSuccess)
                {
                    Console.WriteLine("LQL parsed successfully");
                    var sqlResult = lqlSuccess.Value.ToSQLite();
                    if (sqlResult is Result<string, SqlError>.Success sqlSuccess)
                    {
                        sqlToExecute = sqlSuccess.Value;
                        Console.WriteLine($"LQL converted to SQL: {sqlToExecute}");
                        StatusMessage = $"LQL converted to SQL: {sqlToExecute}";
                    }
                    else if (sqlResult is Result<string, SqlError>.Failure sqlFailure)
                    {
                        Console.WriteLine($"LQL conversion error: {sqlFailure.ErrorValue.Message}");
                        StatusMessage = $"LQL conversion error: {sqlFailure.ErrorValue.Message}";
                        return;
                    }
                    else
                    {
                        Console.WriteLine("Unknown LQL conversion result");
                        StatusMessage = "Unknown LQL conversion result";
                        return;
                    }
                }
                else if (lqlStatement is Result<LqlStatement, SqlError>.Failure lqlFailure)
                {
                    Console.WriteLine($"LQL parse error: {lqlFailure.ErrorValue.Message}");
                    StatusMessage = $"LQL parse error: {lqlFailure.ErrorValue.Message}";
                    return;
                }
                else
                {
                    Console.WriteLine("Unknown LQL parse result");
                    StatusMessage = "Unknown LQL parse result";
                    return;
                }
            }
            else
            {
                sqlToExecute = ActiveTab.Content;
                Console.WriteLine($"Using SQL directly: {sqlToExecute}");
            }

            Console.WriteLine($"Executing SQL: {sqlToExecute}");

            using var command = _connection.CreateCommand();
            command.CommandText = sqlToExecute;

            Console.WriteLine("Created command, executing reader...");
            using var reader = await command.ExecuteReaderAsync();
            Console.WriteLine("Reader created, loading data...");

            _currentDataTable?.Dispose();
            _currentDataTable = new DataTable();
            _currentDataTable.Load(reader);
            Console.WriteLine(
                $"Data loaded: {_currentDataTable.Rows.Count} rows, {_currentDataTable.Columns.Count} columns"
            );

            // Convert DataTable to QueryResultRow collection
            var results = new ObservableCollection<QueryResultRow>();
            var columnNames = new ObservableCollection<string>();

            // Get column names
            foreach (DataColumn column in _currentDataTable.Columns)
            {
                columnNames.Add(column.ColumnName);
            }

            // Convert rows
            foreach (DataRow row in _currentDataTable.Rows)
            {
                var resultRow = new QueryResultRow();
                foreach (DataColumn column in _currentDataTable.Columns)
                {
                    var value = row[column] == DBNull.Value ? null : row[column];
                    resultRow[column.ColumnName] = value;
                    Console.WriteLine($"  {column.ColumnName}: {value}");
                }
                results.Add(resultRow);
            }

            stopwatch.Stop();

            ColumnNames = columnNames;
            QueryResults = results;
            Console.WriteLine($"QueryResults set with {results.Count} QueryResultRow items");

            ExecutionTime = $"{stopwatch.ElapsedMilliseconds} ms";
            RowCount = $"{_currentDataTable.Rows.Count} rows";
            ResultsHeader =
                _currentDataTable.Columns.Count > 0
                    ? _currentDataTable.Columns[0].ColumnName
                    : "Results";
            StatusMessage = $"Query executed successfully in {stopwatch.ElapsedMilliseconds} ms";

            Console.WriteLine("=== ExecuteQueryAsync Completed Successfully ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"=== ERROR in ExecuteQueryAsync ===");
            Console.WriteLine($"Exception: {ex}");
            StatusMessage = $"Query execution error: {ex.Message}";
            QueryResults = null;
            ExecutionTime = "";
            RowCount = "";
            ResultsHeader = "Error";
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _currentDataTable?.Dispose();
    }
}
