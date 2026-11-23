using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table =>
        {
            table.HasCheckConstraint("ck_users_role", "role in (1, 2, 3)");
        });

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        // The domain stores the email in lower case, so a plain unique index is enough.
        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(User.FullNameMaxLength).IsRequired();

        // Unbounded text so the column does not depend on the hash format chosen by the hasher.
        builder.Property(u => u.PasswordHash).HasColumnType("text").IsRequired();

        builder.Property(u => u.Role).HasConversion<short>();
        builder.Property(u => u.IsActive);

        builder.HasIndex(u => u.Email).IsUnique();
    }
}
