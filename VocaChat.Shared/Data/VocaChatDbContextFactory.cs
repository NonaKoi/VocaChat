using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VocaChat.Data;

/// <summary>
/// 统一创建短生命周期的 <see cref="VocaChatDbContext" />。
/// 同时供程序后续的数据操作和 EF Core 设计时工具使用。
/// </summary>
public sealed class VocaChatDbContextFactory : IDesignTimeDbContextFactory<VocaChatDbContext>
{
    private const string ApplicationDirectoryName = "VocaChat";
    private const string DatabaseFileName = "vocachat.db";

    private readonly string _connectionString;

    /// <summary>
    /// 使用本地应用数据目录中的正式开发数据库。
    /// </summary>
    public VocaChatDbContextFactory()
        : this(CreateDefaultConnectionString())
    {
    }

    /// <summary>
    /// 使用指定连接字符串，便于测试使用独立的 SQLite 数据库。
    /// </summary>
    public VocaChatDbContextFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("数据库连接字符串不能为空。", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    /// <summary>
    /// 为一次明确的数据操作创建新的 DbContext；调用方负责在操作完成后释放它。
    /// </summary>
    public VocaChatDbContext CreateDbContext()
    {
        DbContextOptions<VocaChatDbContext> options =
            new DbContextOptionsBuilder<VocaChatDbContext>()
                .UseSqlite(_connectionString)
                .Options;

        return new VocaChatDbContext(options);
    }

    /// <summary>
    /// 供 dotnet ef 等设计时工具创建 DbContext。
    /// </summary>
    public VocaChatDbContext CreateDbContext(string[] args)
    {
        return CreateDbContext();
    }

    /// <summary>
    /// 将正式数据库放在当前用户的本地应用数据目录，避免把个人数据写入仓库。
    /// </summary>
    private static string CreateDefaultConnectionString()
    {
        string localApplicationDataDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        string applicationDirectory = Path.Combine(
            localApplicationDataDirectory,
            ApplicationDirectoryName);

        Directory.CreateDirectory(applicationDirectory);

        string databasePath = Path.Combine(applicationDirectory, DatabaseFileName);
        SqliteConnectionStringBuilder connectionStringBuilder = new()
        {
            DataSource = databasePath,
            ForeignKeys = true
        };

        return connectionStringBuilder.ToString();
    }
}
