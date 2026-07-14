using Microsoft.EntityFrameworkCore;
using VocaChat.ConsoleApp.Data.Configurations;
using VocaChat.ConsoleApp.Models;

namespace VocaChat.ConsoleApp.Data;

/// <summary>
/// VocaChat 的 EF Core 数据库上下文，集中管理后续需要持久化的实体和关系配置。
/// 当前功能单元只建立基础环境，业务实体会在后续单元中逐步加入。
/// </summary>
public sealed class VocaChatDbContext : DbContext
{
    public DbSet<AiAccount> AiAccounts => Set<AiAccount>();
    public DbSet<GroupChat> GroupChats => Set<GroupChat>();

    public VocaChatDbContext(DbContextOptions<VocaChatDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// 应用当前已经迁移到数据库的实体配置。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new AiAccountConfiguration());
        modelBuilder.ApplyConfiguration(new GroupChatConfiguration());
    }
}
