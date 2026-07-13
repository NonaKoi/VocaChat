using Microsoft.EntityFrameworkCore;

namespace VocaChat.ConsoleApp.Data;

/// <summary>
/// VocaChat 的 EF Core 数据库上下文，集中管理后续需要持久化的实体和关系配置。
/// 当前功能单元只建立基础环境，业务实体会在后续单元中逐步加入。
/// </summary>
public sealed class VocaChatDbContext : DbContext
{
    public VocaChatDbContext(DbContextOptions<VocaChatDbContext> options)
        : base(options)
    {
    }
}
