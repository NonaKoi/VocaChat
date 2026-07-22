using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;
using VocaChat.Services;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置本地用户唯一一份全局模型接口设置。
/// </summary>
public sealed class AiModelConnectionSettingsConfiguration
    : IEntityTypeConfiguration<AiModelConnectionSettings>
{
    public void Configure(EntityTypeBuilder<AiModelConnectionSettings> builder)
    {
        builder.ToTable("AiModelConnectionSettings", tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_AiModelConnectionSettings_Singleton",
                $"\"Id\" = {AiModelConnectionSettings.SingletonId}"));

        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.BaseUrl)
            .HasMaxLength(AiModelConnectionSettingsService.MaximumBaseUrlLength)
            .IsRequired();
        builder.Property(settings => settings.Model)
            .HasMaxLength(AiModelConnectionSettingsService.MaximumModelLength)
            .IsRequired();
        builder.Property(settings => settings.ProtectedApiKey)
            .HasMaxLength(8192);
    }
}
