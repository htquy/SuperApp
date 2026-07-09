using Microsoft.EntityFrameworkCore;

namespace sh_backend_core
{
    public class HrmDbContext : DbContext
    {
        public HrmDbContext(DbContextOptions<HrmDbContext> options) : base(options)
        {
        }

        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Department).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Active).HasDefaultValue(true);
            });

            modelBuilder.Entity<OutboxMessage>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.EventType).IsRequired().HasMaxLength(100);
                entity.Property(m => m.Content).IsRequired();
                entity.Property(m => m.CreatedAt).IsRequired();
            });
        }
    }
}
