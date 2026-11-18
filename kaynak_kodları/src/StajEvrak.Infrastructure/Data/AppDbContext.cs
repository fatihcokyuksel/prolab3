using Microsoft.EntityFrameworkCore;
using StajEvrak.Application.Abstractions;
using StajEvrak.Domain.Applications;
using StajEvrak.Domain.Dataset;
using StajEvrak.Domain.Profiles;
using StajEvrak.Domain.Users;

namespace StajEvrak.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<DatasetStudent> DatasetStudents => Set<DatasetStudent>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<InternshipApplication> Applications => Set<InternshipApplication>();
    public DbSet<UploadedDocument> Documents => Set<UploadedDocument>();

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken ct) => await SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Kullanıcılar: Tek tablo kalıtımı (TPH) — Student ve Admin aynı tabloda, "UserType" ayırt edici sütunu ile.
        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasDiscriminator<string>("UserType")
                .HasValue<Student>("Student")
                .HasValue<Admin>("Admin");
            e.Property(u => u.UserName).HasMaxLength(30).IsRequired();
            e.Property(u => u.Email).HasMaxLength(200).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
            e.HasIndex(u => u.UserName).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.Ignore(u => u.Role);
            e.Ignore(u => u.HomePath);
            e.Ignore(u => u.CanReviewApplications);
        });

        b.Entity<Student>(e =>
        {
            e.Property(s => s.StudentNo).HasMaxLength(20);
            e.Property(s => s.FullName).HasMaxLength(150);
            e.HasIndex(s => s.StudentNo).IsUnique().HasFilter("\"StudentNo\" IS NOT NULL");
            e.HasOne(s => s.DatasetStudent).WithMany().HasForeignKey(s => s.DatasetStudentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(s => s.Profile).WithOne().HasForeignKey<StudentProfile>(p => p.StudentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LoginAttempt>(e =>
        {
            e.Property(a => a.Identifier).HasMaxLength(200);
            e.Property(a => a.IpAddress).HasMaxLength(64);
            e.HasIndex(a => new { a.IpAddress, a.Time });
        });

        b.Entity<DatasetStudent>(e =>
        {
            e.Property(d => d.TcKimlikNo).HasMaxLength(11).IsRequired();
            e.HasIndex(d => d.TcKimlikNo).IsUnique();
            e.HasIndex(d => d.OgrenciNo);
        });

        b.Entity<StudentProfile>(e =>
        {
            e.HasIndex(p => p.StudentId).IsUnique();
            e.HasMany(p => p.Values).WithOne().HasForeignKey("ProfileId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            e.Navigation(p => p.Values).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<ProfileFieldValue>(e =>
        {
            e.ToTable("ProfileFieldValues");
            e.Property(v => v.FieldCode).HasMaxLength(40);
        });

        b.Entity<InternshipApplication>(e =>
        {
            e.ToTable("Applications");
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(a => a.StudentId).IsUnique(); // her öğrencinin tek başvurusu, belgeler versiyonlanır
            e.HasOne(a => a.Student).WithMany().HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(a => a.Documents).WithOne().HasForeignKey(d => d.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(a => a.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Ignore(a => a.State);
            e.Ignore(a => a.LatestDocument);
        });

        b.Entity<UploadedDocument>(e =>
        {
            e.ToTable("Documents");
            e.Property(d => d.OriginalFileName).HasMaxLength(255);
            e.Property(d => d.StoredPath).HasMaxLength(300);
            e.Property(d => d.Extension).HasMaxLength(10);
            e.HasMany(d => d.ExtractedFields).WithOne().HasForeignKey(f => f.DocumentId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(d => d.ExtractedFields).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Entity<ExtractedField>(e =>
        {
            e.ToTable("ExtractedFields");
            e.Property(f => f.FieldCode).HasMaxLength(40);
        });
    }
}
