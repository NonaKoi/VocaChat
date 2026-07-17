using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置好友分组及固定默认分组。
/// </summary>
public sealed class ContactGroupConfiguration
    : IEntityTypeConfiguration<ContactGroup>
{
    public void Configure(EntityTypeBuilder<ContactGroup> builder)
    {
        builder.ToTable("ContactGroups", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_ContactGroups_Name_NotBlank",
                "length(trim(\"Name\")) > 0");
            tableBuilder.HasCheckConstraint(
                "CK_ContactGroups_Name_MaxLength",
                $"length(\"Name\") <= {ContactGroup.NameMaxLength}");
        });

        builder.HasKey(group => group.Id);
        builder.Property(group => group.Id).ValueGeneratedNever();
        builder.Property(group => group.Name)
            .IsRequired()
            .HasMaxLength(ContactGroup.NameMaxLength)
            .UseCollation("NOCASE");
        builder.Property(group => group.SortOrder).IsRequired();
        builder.Property(group => group.CreatedAt).IsRequired();
        builder.HasIndex(group => group.Name).IsUnique();

        builder.HasData(new
        {
            Id = ContactGroup.DefaultGroupId,
            Name = ContactGroup.DefaultGroupName,
            SortOrder = 0,
            CreatedAt = new DateTime(2026, 7, 17, 0, 0, 0)
        });
    }
}
