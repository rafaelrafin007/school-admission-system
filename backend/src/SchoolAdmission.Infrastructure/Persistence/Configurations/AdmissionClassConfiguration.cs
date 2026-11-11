using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Infrastructure.Persistence.Configurations;

public sealed class AdmissionClassConfiguration : IEntityTypeConfiguration<AdmissionClass>
{
    public void Configure(EntityTypeBuilder<AdmissionClass> builder)
    {
        builder.ToTable("admission_classes", table =>
            table.HasCheckConstraint("ck_admission_classes_total_seats_positive", "total_seats > 0"));

        builder.HasKey(entity => entity.Id).HasName("pk_admission_classes");
        builder.Property(entity => entity.Id)
            .HasColumnName("id").HasColumnType("character varying(40)").HasMaxLength(40)
            .ValueGeneratedNever().IsRequired();
        builder.Property(entity => entity.Name)
            .HasColumnName("name").HasColumnType("character varying(160)").HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.TotalSeats)
            .HasColumnName("total_seats").HasColumnType("integer").IsRequired();
        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
    }
}
