using Microsoft.EntityFrameworkCore;
using VocaChat.Data.Configurations;
using VocaChat.Models;

namespace VocaChat.Data;

/// <summary>
/// VocaChat 的 EF Core 数据库上下文，集中管理当前持久化实体和关系配置。
/// </summary>
public sealed class VocaChatDbContext : DbContext
{
    public DbSet<AiAccount> AiAccounts => Set<AiAccount>();
    public DbSet<AiAccountTag> AiAccountTags => Set<AiAccountTag>();
    public DbSet<ContactGroup> ContactGroups => Set<ContactGroup>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<PrivateChat> PrivateChats => Set<PrivateChat>();
    public DbSet<PrivateMessage> PrivateMessages => Set<PrivateMessage>();
    public DbSet<GroupChat> GroupChats => Set<GroupChat>();
    public DbSet<GroupMessage> GroupMessages => Set<GroupMessage>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostImage> PostImages => Set<PostImage>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<PostComment> PostComments => Set<PostComment>();

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
        modelBuilder.ApplyConfiguration(new AiAccountTagConfiguration());
        modelBuilder.ApplyConfiguration(new ContactGroupConfiguration());
        modelBuilder.ApplyConfiguration(new ContactConfiguration());
        modelBuilder.ApplyConfiguration(new PrivateChatConfiguration());
        modelBuilder.ApplyConfiguration(new PrivateMessageConfiguration());
        modelBuilder.ApplyConfiguration(new GroupChatConfiguration());
        modelBuilder.ApplyConfiguration(new GroupMessageConfiguration());
        modelBuilder.ApplyConfiguration(new PostConfiguration());
        modelBuilder.ApplyConfiguration(new PostImageConfiguration());
        modelBuilder.ApplyConfiguration(new PostLikeConfiguration());
        modelBuilder.ApplyConfiguration(new PostCommentConfiguration());
    }
}
