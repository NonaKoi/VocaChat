using System;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VocaChat.Data;
using VocaChat.Tests.TestSupport;
using VocaChat.WebApi.Services;

namespace VocaChat.Tests.ApiIntegration;

/// <summary>
/// 为一个 HTTP 测试场景启动真实 Web API 管线和独立临时 SQLite 数据库。
/// </summary>
internal sealed class VocaChatWebApiFactory
    : WebApplicationFactory<VocaChat.WebApi.Program>
{
    private readonly SqliteTestDatabase _database = new(
        applyMigrations: false);
    private readonly string _mediaDirectory = Path.Combine(
        Path.GetTempPath(),
        "VocaChat.Tests",
        "Media",
        Guid.NewGuid().ToString("N"));

    public string DatabasePath => _database.DatabasePath;
    public string MediaDirectory => _mediaDirectory;

    /// <summary>
    /// 在 Host 构建前替换正式数据库工厂，使启动 Migration 只作用于测试数据库。
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<VocaChatDbContextFactory>();
            services.RemoveAll<LocalMediaStorageService>();
            services.AddSingleton(_database.CreateDbContextFactory());
            services.AddSingleton(
                new LocalMediaStorageService(_mediaDirectory));
        });
    }

    /// <summary>
    /// 使用 HTTPS 测试地址，避免 HTTPS 重定向干扰 TestServer 请求。
    /// </summary>
    public HttpClient CreateApiClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    /// <summary>
    /// 先释放 Test Host，再删除 SQLite 主文件和可能的临时文件。
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _database.Dispose();

            if (Directory.Exists(_mediaDirectory))
            {
                Directory.Delete(_mediaDirectory, recursive: true);
            }
        }
    }
}
