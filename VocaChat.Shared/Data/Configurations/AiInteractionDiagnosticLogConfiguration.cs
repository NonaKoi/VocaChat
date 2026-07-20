using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocaChat.Models;

namespace VocaChat.Data.Configurations;

public sealed class AiInteractionDiagnosticLogConfiguration
    : IEntityTypeConfiguration<AiInteractionDiagnosticLog>
{
    public void Configure(EntityTypeBuilder<AiInteractionDiagnosticLog> builder)
    {
        builder.ToTable("AiInteractionDiagnosticLogs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedNever();
        builder.Property(log => log.OccurredAt).IsRequired();
        builder.Property(log => log.Severity)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(log => log.Code)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(log => log.Scenario)
            .HasMaxLength(AiInteractionDiagnosticLog.ScenarioMaxLength)
            .IsRequired();
        builder.Property(log => log.Summary)
            .HasMaxLength(AiInteractionDiagnosticLog.SummaryMaxLength)
            .IsRequired();
        builder.Property(log => log.Detail)
            .HasMaxLength(AiInteractionDiagnosticLog.DetailMaxLength)
            .IsRequired();
        builder.Property(log => log.WasRecovered).IsRequired();
        builder.HasIndex(log => log.OccurredAt);
        builder.HasIndex(log => log.AiAccountId);
    }
}

