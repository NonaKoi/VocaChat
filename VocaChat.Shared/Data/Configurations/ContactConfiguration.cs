using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

/// <summary>
/// 配置好友关系、账号引用和所属分组。
/// </summary>
public sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("Contacts");
        builder.HasKey(contact => contact.Id);
        builder.Property(contact => contact.Id).ValueGeneratedNever();
        builder.Property(contact => contact.CreatedAt).IsRequired();

        builder.HasOne(contact => contact.AiAccount)
            .WithOne()
            .HasForeignKey<Contact>(contact => contact.AiAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(contact => contact.ContactGroup)
            .WithMany()
            .HasForeignKey(contact => contact.ContactGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(contact => contact.AiAccountId).IsUnique();
        builder.HasIndex(contact => contact.ContactGroupId);
    }
}
