using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置 AI 个人记忆的账号归属、有效状态和常用召回索引。
/// </summary>
public sealed class AiSelfMemoryConfiguration
    : IEntityTypeConfiguration<AiSelfMemory>
{
    public void Configure(EntityTypeBuilder<AiSelfMemory> builder)
    {
        builder.ToTable("AiSelfMemories", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AiSelfMemories_Type",
                $"\"Type\" BETWEEN {(int)AiSelfMemoryType.PersonalFact} "
                + $"AND {(int)AiSelfMemoryType.Preference}");
            tableBuilder.HasCheckConstraint(
                "CK_AiSelfMemories_Source",
                $"\"Source\" BETWEEN {(int)AiSelfMemorySource.User} "
                + $"AND {(int)AiSelfMemorySource.Director}");
            tableBuilder.HasCheckConstraint(
                "CK_AiSelfMemories_Status",
                $"\"Status\" BETWEEN {(int)AiSelfMemoryStatus.Active} "
                + $"AND {(int)AiSelfMemoryStatus.Archived}");
            tableBuilder.HasCheckConstraint(
                "CK_AiSelfMemories_Summary",
                "length(trim(\"Summary\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_AiSelfMemories_Salience",
                $"\"Salience\" BETWEEN {AiSelfMemory.MinimumSalience} "
                + $"AND {AiSelfMemory.MaximumSalience}");
            tableBuilder.HasCheckConstraint(
                "CK_AiSelfMemories_Validity",
                "\"ValidFrom\" IS NULL OR \"ValidUntil\" IS NULL "
                + "OR \"ValidUntil\" >= \"ValidFrom\"");
        });

        builder.HasKey(memory => memory.Id);
        builder.Property(memory => memory.Id).ValueGeneratedNever();
        builder.Property(memory => memory.Type).IsRequired();
        builder.Property(memory => memory.Summary)
            .IsRequired()
            .HasMaxLength(AiSelfMemory.SummaryMaxLength)
            .UseCollation("NOCASE");
        builder.Property(memory => memory.Source).IsRequired();
        builder.Property(memory => memory.Status).IsRequired();
        builder.Property(memory => memory.Salience).IsRequired();
        builder.Property(memory => memory.IsUserLocked).IsRequired();
        builder.Property(memory => memory.CreatedAt).IsRequired();
        builder.Property(memory => memory.UpdatedAt).IsRequired();

        builder.HasIndex(memory => new
        {
            memory.AiAccountId,
            memory.Type,
            memory.Summary
        })
            .IsUnique()
            .HasFilter($"\"Status\" = {(int)AiSelfMemoryStatus.Active}");
        builder.HasIndex(memory => new
        {
            memory.AiAccountId,
            memory.Status,
            memory.Salience,
            memory.UpdatedAt
        });
        builder.HasIndex(memory => memory.SourceMessageId);
        builder.HasIndex(memory => memory.SourceConversationId);
        builder.HasIndex(memory => new
        {
            memory.AiAccountId,
            memory.SourceMessageId,
            memory.Type,
            memory.Summary
        })
            .IsUnique()
            .HasFilter(
                $"\"Source\" = {(int)AiSelfMemorySource.Director} "
                + "AND \"SourceMessageId\" IS NOT NULL");

        builder.HasOne(memory => memory.AiAccount)
            .WithMany()
            .HasForeignKey(memory => memory.AiAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
