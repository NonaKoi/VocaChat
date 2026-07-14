using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.ConsoleApp.Models;

namespace VocaChat.ConsoleApp.Data.Configurations;

/// <summary>
/// 配置 AI 账号的表结构、字段限制和昵称唯一规则。
/// </summary>
public sealed class AiAccountConfiguration : IEntityTypeConfiguration<AiAccount>
{
    public void Configure(EntityTypeBuilder<AiAccount> builder)
    {
        builder.ToTable("AiAccounts", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_AiAccounts_Nickname_NotBlank",
                "length(trim(\"Nickname\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_AiAccounts_Nickname_MaxLength",
                $"length(\"Nickname\") <= {AiAccount.NicknameMaxLength}");
            tableBuilder.HasCheckConstraint(
                "CK_AiAccounts_IdentityDescription_MaxLength",
                $"length(\"IdentityDescription\") <= {AiAccount.IdentityDescriptionMaxLength}");
            tableBuilder.HasCheckConstraint(
                "CK_AiAccounts_Personality_MaxLength",
                $"length(\"Personality\") <= {AiAccount.PersonalityMaxLength}");
            tableBuilder.HasCheckConstraint(
                "CK_AiAccounts_SpeakingStyle_MaxLength",
                $"length(\"SpeakingStyle\") <= {AiAccount.SpeakingStyleMaxLength}");
        });

        builder.HasKey(aiAccount => aiAccount.Id);

        builder.Property(aiAccount => aiAccount.Id)
            .ValueGeneratedNever();

        builder.Property(aiAccount => aiAccount.Nickname)
            .IsRequired()
            .HasMaxLength(AiAccount.NicknameMaxLength)
            .UseCollation("NOCASE");

        builder.Property(aiAccount => aiAccount.IdentityDescription)
            .IsRequired()
            .HasMaxLength(AiAccount.IdentityDescriptionMaxLength);

        builder.Property(aiAccount => aiAccount.Personality)
            .IsRequired()
            .HasMaxLength(AiAccount.PersonalityMaxLength);

        builder.Property(aiAccount => aiAccount.SpeakingStyle)
            .IsRequired()
            .HasMaxLength(AiAccount.SpeakingStyleMaxLength);

        builder.Property(aiAccount => aiAccount.CreatedAt)
            .IsRequired();

        builder.HasIndex(aiAccount => aiAccount.Nickname)
            .IsUnique();
    }
}
