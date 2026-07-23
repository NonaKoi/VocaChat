using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置模型调用 Token 记录及按消息批次查询所需的索引。
/// </summary>
public sealed class AiModelInvocationUsageConfiguration
    : IEntityTypeConfiguration<AiModelInvocationUsage>
{
    public void Configure(EntityTypeBuilder<AiModelInvocationUsage> builder)
    {
        builder.ToTable("AiModelInvocationUsages", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AiModelInvocationUsages_AttemptNumber_Positive",
                "\"AttemptNumber\" > 0");
            tableBuilder.HasCheckConstraint(
                "CK_AiModelInvocationUsages_Tokens_NonNegative",
                "(\"PromptTokens\" IS NULL OR \"PromptTokens\" >= 0) "
                + "AND (\"CompletionTokens\" IS NULL OR \"CompletionTokens\" >= 0) "
                + "AND (\"TotalTokens\" IS NULL OR \"TotalTokens\" >= 0) "
                + "AND (\"PromptCacheHitTokens\" IS NULL OR \"PromptCacheHitTokens\" >= 0) "
                + "AND (\"PromptCacheMissTokens\" IS NULL OR \"PromptCacheMissTokens\" >= 0) "
                + "AND (\"ReasoningTokens\" IS NULL OR \"ReasoningTokens\" >= 0)");
            tableBuilder.HasCheckConstraint(
                "CK_AiModelInvocationUsages_UsageReported",
                "\"UsageReported\" = 0 OR ("
                + "\"PromptTokens\" IS NOT NULL "
                + "AND \"CompletionTokens\" IS NOT NULL "
                + "AND \"TotalTokens\" IS NOT NULL)");
        });

        builder.HasKey(usage => usage.Id);
        builder.Property(usage => usage.Id).ValueGeneratedNever();
        builder.Property(usage => usage.Stage)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(usage => usage.ModelName)
            .HasMaxLength(AiModelInvocationUsage.ModelNameMaxLength)
            .IsRequired();
        builder.Property(usage => usage.AttemptNumber).IsRequired();
        builder.Property(usage => usage.UsageReported).IsRequired();
        builder.Property(usage => usage.RecordedAt).IsRequired();

        builder.HasIndex(usage => usage.InteractionBatchId);
        builder.HasIndex(usage => usage.AiResponseBatchId);
        builder.HasIndex(usage => new
        {
            usage.GroupChatId,
            usage.RecordedAt
        });
        builder.HasIndex(usage => new
        {
            usage.PrivateChatId,
            usage.RecordedAt
        });
        builder.HasIndex(usage => usage.AutonomousPrivateChatSessionId);
        builder.HasIndex(usage => usage.AutonomousGroupChatSessionId);
    }
}
