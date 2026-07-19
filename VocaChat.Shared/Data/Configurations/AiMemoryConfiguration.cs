using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置方向记忆的来源、有效范围、幂等约束和候选查询索引。
/// </summary>
public sealed class AiMemoryConfiguration
    : IEntityTypeConfiguration<AiMemory>
{
    public void Configure(EntityTypeBuilder<AiMemory> builder)
    {
        builder.ToTable("AiMemories", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AiMemories_DifferentAccounts",
                "\"OwnerAiAccountId\" <> \"SubjectAiAccountId\"");
            tableBuilder.HasCheckConstraint(
                "CK_AiMemories_Type",
                $"\"Type\" BETWEEN {(int)AiMemoryType.ImportantEvent} "
                + $"AND {(int)AiMemoryType.PersonalFact}");
            tableBuilder.HasCheckConstraint(
                "CK_AiMemories_Summary",
                "length(trim(\"Summary\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_AiMemories_Salience",
                $"\"Salience\" BETWEEN {AiMemory.MinimumSalience} "
                + $"AND {AiMemory.MaximumSalience}");
        });

        builder.HasKey(memory => memory.Id);
        builder.Property(memory => memory.Id).ValueGeneratedNever();
        builder.Property(memory => memory.Type).IsRequired();
        builder.Property(memory => memory.Summary)
            .IsRequired()
            .HasMaxLength(AiMemory.SummaryMaxLength);
        builder.Property(memory => memory.Salience).IsRequired();
        builder.Property(memory => memory.OccurredAt).IsRequired();
        builder.Property(memory => memory.IsActive).IsRequired();
        builder.Property(memory => memory.CreatedAt).IsRequired();

        builder.HasIndex(memory => new
        {
            memory.SourceSessionId,
            memory.OwnerAiAccountId,
            memory.SubjectAiAccountId,
            memory.Type,
            memory.Summary
        })
            .IsUnique();
        builder.HasIndex(memory => new
        {
            memory.OwnerAiAccountId,
            memory.SubjectAiAccountId,
            memory.IsActive,
            memory.Salience,
            memory.OccurredAt
        });
        builder.HasIndex(memory => memory.SubjectAiAccountId);
        builder.HasIndex(memory => memory.SourcePrivateChatId);

        builder.HasOne(memory => memory.OwnerAiAccount)
            .WithMany()
            .HasForeignKey(memory => memory.OwnerAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(memory => memory.SubjectAiAccount)
            .WithMany()
            .HasForeignKey(memory => memory.SubjectAiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(memory => memory.SourcePrivateChat)
            .WithMany()
            .HasForeignKey(memory => memory.SourcePrivateChatId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(memory => memory.SourceSession)
            .WithMany()
            .HasForeignKey(memory => memory.SourceSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
