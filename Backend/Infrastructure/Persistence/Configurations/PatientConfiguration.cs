using Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patients", table =>
        {
            table.HasCheckConstraint("ck_patients_document_type", "document_type in (1, 2)");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.DocumentType).HasConversion<short>();
        builder.Property(p => p.DocumentNumber).HasMaxLength(20).IsRequired();
        builder.Property(p => p.FirstName).HasMaxLength(80).IsRequired();
        builder.Property(p => p.LastName).HasMaxLength(80).IsRequired();
        builder.Property(p => p.BirthDate);
        builder.Property(p => p.Phone).HasMaxLength(20);
        builder.Property(p => p.Email).HasMaxLength(150);
        builder.Property(p => p.Address).HasMaxLength(200);
        builder.Property(p => p.GuardianName).HasMaxLength(160);
        builder.Property(p => p.GuardianPhone).HasMaxLength(20);
        builder.Property(p => p.IsActive);

        // RN-10: the document is unique per type.
        builder.HasIndex(p => new { p.DocumentType, p.DocumentNumber }).IsUnique();
    }
}