using Microsoft.EntityFrameworkCore;
using VocabApp.Core.Models;

namespace VocabApp.Infrastructure.Persistence;

public class VocabDbContext : DbContext
{
    public VocabDbContext(DbContextOptions<VocabDbContext> options) : base(options)
    {
    }

    public DbSet<Word> Words => Set<Word>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<TestSession> TestSessions => Set<TestSession>();

    public DbSet<TestAnswer> TestAnswers => Set<TestAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Word>(entity =>
        {
            entity.Property(w => w.Text).IsRequired().HasMaxLength(128);
            entity.Property(w => w.Meaning).IsRequired().HasMaxLength(512);
            entity.Property(w => w.Example).HasMaxLength(1024);
            entity.Property(w => w.Notes).HasMaxLength(1024);
            entity.Property(w => w.PartOfSpeech).HasConversion<string?>().HasMaxLength(32);
            entity.HasIndex(w => new { w.Text, w.PartOfSpeech });
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.Property(t => t.Name).IsRequired().HasMaxLength(64);
            entity.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<Word>()
            .HasMany(w => w.Tags)
            .WithMany(t => t.Words)
            .UsingEntity(j => j.ToTable("WordTags"));

        modelBuilder.Entity<TestSession>(entity =>
        {
            entity.Property(s => s.Mode).HasConversion<string>().HasMaxLength(64);
            entity.HasMany(s => s.Answers)
                .WithOne(a => a.TestSession!)
                .HasForeignKey(a => a.TestSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TestAnswer>(entity =>
        {
            entity.Property(a => a.UserInput).HasMaxLength(512);
            entity.HasOne(a => a.Word)
                .WithMany()
                .HasForeignKey(a => a.WordId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
