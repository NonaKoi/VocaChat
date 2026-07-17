using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

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

        builder.Property(aiAccount => aiAccount.VcNumber)
            .IsRequired()
            .HasMaxLength(AiAccount.VcNumberMaxLength)
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

        builder.Property(aiAccount => aiAccount.Signature)
            .IsRequired()
            .HasMaxLength(AiAccount.SignatureMaxLength);

        builder.Property(aiAccount => aiAccount.Birthday);

        builder.Property(aiAccount => aiAccount.Gender)
            .IsRequired();

        builder.Property(aiAccount => aiAccount.Location)
            .IsRequired()
            .HasMaxLength(AiAccount.LocationMaxLength);

        builder.Property(aiAccount => aiAccount.Occupation)
            .IsRequired()
            .HasMaxLength(AiAccount.OccupationMaxLength);

        builder.Property(aiAccount => aiAccount.Hometown)
            .IsRequired()
            .HasMaxLength(AiAccount.HometownMaxLength);

        builder.Property(aiAccount => aiAccount.OnlineStatus)
            .IsRequired();

        builder.Property(aiAccount => aiAccount.AvatarMediaId)
            .HasMaxLength(AiAccount.MediaIdMaxLength);

        builder.Property(aiAccount => aiAccount.ProfileCoverMediaId)
            .HasMaxLength(AiAccount.MediaIdMaxLength);

        builder.Property(aiAccount => aiAccount.CreatedAt)
            .IsRequired();

        builder.HasIndex(aiAccount => aiAccount.Nickname)
            .IsUnique();

        builder.HasIndex(aiAccount => aiAccount.VcNumber)
            .IsUnique();

        builder.Navigation(aiAccount => aiAccount.Tags)
            .HasField("_tags")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
