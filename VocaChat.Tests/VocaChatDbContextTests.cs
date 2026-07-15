using Microsoft.EntityFrameworkCore;
using VocaChat.Data;

namespace VocaChat.Tests;

/// <summary>
/// 验证 DbContext 基础创建方式，不接触正式开发数据库文件。
/// </summary>
public class VocaChatDbContextTests
{
    [Fact]
    public void CreateDbContext_WithTestConnectionString_UsesSqliteProvider()
    {
        VocaChatDbContextFactory factory = new("Data Source=:memory:");

        using VocaChatDbContext context = factory.CreateDbContext();

        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);
        Assert.Equal(":memory:", context.Database.GetDbConnection().DataSource);
        Assert.True(context.Database.CanConnect());
    }
}
