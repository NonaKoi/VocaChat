using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置 AI 账号与其专有模型接口设置的一对一关系。
/// </summary>
public sealed class AiAccountModelConnectionSettingsConfiguration
    : IEntityTypeConfiguration<AiAccountModelConnectionSettings>
{
    public void Configure(
        EntityTypeBuilder<AiAccountModelConnectionSettings> builder)
    {
        builder.ToTable("AiAccountModelConnectionSettings");
        builder.HasKey(settings => settings.AiAccountId);
        builder.Property(settings => settings.AiAccountId)
            .ValueGeneratedNever();
        builder.Property(settings => settings.UseGlobalSettings)
            .HasDefaultValue(true)
            .IsRequired();
        builder.Property(settings => settings.BaseUrl)
            .HasMaxLength(AiModelConnectionSettingsService.MaximumBaseUrlLength)
            .IsRequired();
        builder.Property(settings => settings.Model)
            .HasMaxLength(AiModelConnectionSettingsService.MaximumModelLength)
            .IsRequired();
        builder.Property(settings => settings.ProtectedApiKey)
            .HasMaxLength(8192);

        builder.HasOne<AiAccount>()
            .WithOne()
            .HasForeignKey<AiAccountModelConnectionSettings>(settings =>
                settings.AiAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
