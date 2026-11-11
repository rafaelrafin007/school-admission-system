using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.Infrastructure.Persistence.Configurations;

public sealed class AdmissionHoldConfiguration : IEntityTypeConfiguration<AdmissionHold>
{
    public void Configure(EntityTypeBuilder<AdmissionHold> builder)
    {
        builder.ToTable("admission_holds", table =>
            table.HasCheckConstraint("ck_admission_holds_status",
                "status IN ('HELD', 'CONFIRMED', 'EXPIRED', 'CANCELLED')"));

        builder.HasKey(entity => entity.Id).HasName("pk_admission_holds");
        builder.Property(entity => entity.Id)
            .HasColumnName("id").HasColumnType("character varying(40)").HasMaxLength(40)
            .ValueGeneratedNever().IsRequired();
        builder.Property(entity => entity.ClassId)
            .HasColumnName("class_id").HasColumnType("character varying(40)").HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.StudentId)
            .HasColumnName("student_id").HasColumnType("character varying(17)").HasMaxLength(17).IsRequired();
        builder.Property(entity => entity.Status)
            .HasColumnName("status").HasColumnType("character varying(20)").HasMaxLength(20)
            .HasConversion(status => ToDatabase(status), value => FromDatabase(value)).IsRequired();
        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(entity => entity.ExpiresAt)
            .HasColumnName("expires_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(entity => entity.ConfirmedAt)
            .HasColumnName("confirmed_at").HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(entity => entity.CancelledAt)
            .HasColumnName("cancelled_at").HasColumnType("timestamp with time zone").IsRequired(false);

        builder.HasOne<AdmissionClass>().WithMany().HasForeignKey(entity => entity.ClassId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_admission_holds_admission_classes_class_id");

        builder.HasIndex(entity => new { entity.ClassId, entity.StudentId })
            .IsUnique().HasDatabaseName("ux_admission_holds_class_student_active")
            .HasFilter("status IN ('HELD', 'CONFIRMED')");
        builder.HasIndex(entity => new { entity.ClassId, entity.Status, entity.ExpiresAt })
            .HasDatabaseName("ix_admission_holds_class_status_expires_at");
        builder.HasIndex(entity => new { entity.StudentId, entity.CreatedAt })
            .HasDatabaseName("ix_admission_holds_student_created_at");
        builder.HasIndex(entity => entity.ExpiresAt)
            .HasDatabaseName("ix_admission_holds_held_expires_at").HasFilter("status = 'HELD'");
    }

    private static string ToDatabase(HoldStatus status) => status switch
    {
        HoldStatus.Held => "HELD",
        HoldStatus.Confirmed => "CONFIRMED",
        HoldStatus.Expired => "EXPIRED",
        HoldStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown hold status.")
    };

    private static HoldStatus FromDatabase(string status) => status switch
    {
        "HELD" => HoldStatus.Held,
        "CONFIRMED" => HoldStatus.Confirmed,
        "EXPIRED" => HoldStatus.Expired,
        "CANCELLED" => HoldStatus.Cancelled,
        _ => throw new InvalidOperationException("The database contains an unknown hold status.")
    };
}
