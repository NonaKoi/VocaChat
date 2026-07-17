using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置 AI 账号与其自主互动专有设置的一对一关系。
/// </summary>
public sealed class AiAccountAutonomySettingsConfiguration
    : IEntityTypeConfiguration<AiAccountAutonomySettings>
{
    public void Configure(EntityTypeBuilder<AiAccountAutonomySettings> builder)
    {
        builder.ToTable("AiAccountAutonomySettings", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AiAccountAutonomySettings_InitiativeLevel",
                "\"InitiativeLevel\" IN ('Low', 'Normal', 'High')");
        });

        builder.HasKey(settings => settings.AiAccountId);

        builder.Property(settings => settings.AiAccountId)
            .ValueGeneratedNever();

        builder.Property(settings => settings.IsEnabled)
            .IsRequired();

        builder.Property(settings => settings.InitiativeLevel)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(settings => settings.CanInitiatePrivateChats)
            .IsRequired();

        builder.Property(settings => settings.CanInitiateGroupChats)
            .IsRequired();

        builder.Property(settings => settings.CanJoinGroupChats)
            .IsRequired();

        builder.HasOne<AiAccount>()
            .WithOne()
            .HasForeignKey<AiAccountAutonomySettings>(settings =>
                settings.AiAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
