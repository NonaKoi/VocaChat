using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置角色世界字段、名称唯一性和默认现实世界。
/// </summary>
public sealed class CharacterWorldConfiguration
    : IEntityTypeConfiguration<CharacterWorld>
{
    private static readonly DateTime DefaultWorldCreatedAt =
        new(2026, 7, 23, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<CharacterWorld> builder)
    {
        builder.ToTable("CharacterWorlds", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_CharacterWorlds_Name_NotBlank",
                "length(trim(\"Name\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_CharacterWorlds_Name_MaxLength",
                $"length(\"Name\") <= {CharacterWorld.NameMaxLength}");
            tableBuilder.HasCheckConstraint(
                "CK_CharacterWorlds_Description_MaxLength",
                $"length(\"Description\") <= {CharacterWorld.DescriptionMaxLength}");
        });

        builder.HasKey(world => world.Id);

        builder.Property(world => world.Id)
            .ValueGeneratedNever();

        builder.Property(world => world.Name)
            .IsRequired()
            .HasMaxLength(CharacterWorld.NameMaxLength)
            .UseCollation("NOCASE");

        builder.Property(world => world.Description)
            .IsRequired()
            .HasMaxLength(CharacterWorld.DescriptionMaxLength);

        builder.Property(world => world.CreatedAt)
            .IsRequired();

        builder.Property(world => world.UpdatedAt)
            .IsRequired();

        builder.HasIndex(world => world.Name)
            .IsUnique();

        builder.HasData(new
        {
            Id = CharacterWorld.DefaultWorldId,
            Name = CharacterWorld.DefaultWorldName,
            Description = CharacterWorld.DefaultWorldDescription,
            CreatedAt = DefaultWorldCreatedAt,
            UpdatedAt = DefaultWorldCreatedAt
        });
    }
}
