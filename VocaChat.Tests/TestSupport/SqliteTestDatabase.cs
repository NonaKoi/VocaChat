using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VocaChat.ConsoleApp.Data;

namespace VocaChat.Tests.TestSupport;

/// <summary>
/// 为单个测试提供独立的临时 SQLite 文件，并应用正式 Migration。
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public SqliteTestDatabase()
    {
        string testDatabaseDirectory = Path.Combine(
            Path.GetTempPath(),
            "VocaChat.Tests");
        Directory.CreateDirectory(testDatabaseDirectory);

        _databasePath = Path.Combine(
            testDatabaseDirectory,
            $"{Guid.NewGuid():N}.db");

        SqliteConnectionStringBuilder connectionStringBuilder = new()
        {
            DataSource = _databasePath,
            ForeignKeys = true,
            Pooling = false
        };
        _connectionString = connectionStringBuilder.ToString();

        using VocaChatDbContext dbContext = CreateDbContextFactory().CreateDbContext();
        dbContext.Database.Migrate();
    }

    /// <summary>
    /// 创建与同一个测试数据库相连的新工厂，用于模拟程序重新启动。
    /// </summary>
    public VocaChatDbContextFactory CreateDbContextFactory()
    {
        return new VocaChatDbContextFactory(_connectionString);
    }

    /// <summary>
    /// 删除当前测试生成的数据库及 SQLite 临时文件。
    /// </summary>
    public void Dispose()
    {
        DeleteIfExists(_databasePath);
        DeleteIfExists($"{_databasePath}-shm");
        DeleteIfExists($"{_databasePath}-wal");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
