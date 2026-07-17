using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置本地用户唯一一份好友自主互动设置。
/// </summary>
public sealed class AutonomousInteractionSettingsConfiguration
    : IEntityTypeConfiguration<AutonomousInteractionSettings>
{
    public void Configure(
        EntityTypeBuilder<AutonomousInteractionSettings> builder)
    {
        builder.ToTable("AutonomousInteractionSettings", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_Singleton",
                $"\"Id\" = {AutonomousInteractionSettings.SingletonId}");
            tableBuilder.HasCheckConstraint(
                "CK_AutonomousInteractionSettings_Frequency",
                "\"Frequency\" IN ('Low', 'Normal', 'High')");
        });

        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.Id)
            .ValueGeneratedNever();

        builder.Property(settings => settings.IsEnabled)
            .IsRequired();

        builder.Property(settings => settings.Frequency)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(settings => settings.AllowPrivateChats)
            .IsRequired();

        builder.Property(settings => settings.AllowGroupChats)
            .IsRequired();
    }
}
