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
                "CK_AiSelfMemories_FactNature",
                $"\"FactNature\" BETWEEN {(int)AiSelfMemoryFactNature.Objective} "
                + $"AND {(int)AiSelfMemoryFactNature.Narrative}");
            tableBuilder.HasCheckConstraint(
                "CK_AiSelfMemories_Mutability",
                $"\"Mutability\" BETWEEN {(int)AiSelfMemoryMutability.Immutable} "
                + $"AND {(int)AiSelfMemoryMutability.Ephemeral}");
            tableBuilder.HasCheckConstraint(
                "CK_AiSelfMemories_TrustLevel",
                $"\"TrustLevel\" BETWEEN {(int)AiSelfMemoryTrustLevel.UserCanon} "
                + $"AND {(int)AiSelfMemoryTrustLevel.SubjectiveState}");
            tableBuilder.HasCheckConstraint(
                "CK_AiSelfMemories_Summary",
                "length(trim(\"Summary\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_AiSelfMemories_FactKey",
                "length(trim(\"FactKey\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_AiSelfMemories_FactKey_MaxLength",
                $"length(\"FactKey\") <= {AiSelfMemory.FactKeyMaxLength}");
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
        builder.Property(memory => memory.FactKey)
            .IsRequired()
            .HasMaxLength(AiSelfMemory.FactKeyMaxLength)
            .UseCollation("NOCASE");
        builder.Property(memory => memory.FactNature).IsRequired();
        builder.Property(memory => memory.Mutability).IsRequired();
        builder.Property(memory => memory.TrustLevel).IsRequired();
        builder.Property(memory => memory.CharacterWorldId).IsRequired();
        builder.Property(memory => memory.Source).IsRequired();
        builder.Property(memory => memory.Status).IsRequired();
        builder.Property(memory => memory.Salience).IsRequired();
        builder.Property(memory => memory.IsUserLocked).IsRequired();
        builder.Property(memory => memory.CreatedAt).IsRequired();
        builder.Property(memory => memory.UpdatedAt).IsRequired();

        builder.HasIndex(memory => new
        {
            memory.AiAccountId,
            memory.CharacterWorldId,
            memory.FactKey
        })
            .IsUnique()
            .HasFilter($"\"Status\" = {(int)AiSelfMemoryStatus.Active}");
        builder.HasIndex(memory => new
        {
            memory.AiAccountId,
            memory.CharacterWorldId,
            memory.Type,
            memory.Summary
        })
            .IsUnique()
            .HasFilter($"\"Status\" = {(int)AiSelfMemoryStatus.Active}");
        builder.HasIndex(memory => new
        {
            memory.AiAccountId,
            memory.CharacterWorldId,
            memory.Status,
            memory.Salience,
            memory.UpdatedAt
        });
        builder.HasIndex(memory => memory.SourceMessageId);
        builder.HasIndex(memory => memory.SourceConversationId);
        builder.HasIndex(memory => memory.SupersedesMemoryId)
            .IsUnique()
            .HasFilter("\"SupersedesMemoryId\" IS NOT NULL");
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
        builder.HasOne(memory => memory.CharacterWorld)
            .WithMany()
            .HasForeignKey(memory => memory.CharacterWorldId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(memory => memory.SupersedesMemory)
            .WithMany()
            .HasForeignKey(memory => memory.SupersedesMemoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
