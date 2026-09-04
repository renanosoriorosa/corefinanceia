using CoreFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoreFinance.Infra.Data.Configurations;

public class FixedAccountConfiguration : IEntityTypeConfiguration<FixedAccount>
{
    public void Configure(EntityTypeBuilder<FixedAccount> builder)
    {
        builder.ToTable("FixedAccounts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(255);

        builder.Property(x => x.IsRequiredMonthly)
            .IsRequired();

        builder.Property(x => x.Active)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.HasIndex(x => x.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
