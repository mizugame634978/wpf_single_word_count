using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VocabApp.Infrastructure.Persistence;

public class VocabDbContextFactory : IDesignTimeDbContextFactory<VocabDbContext>
{
    public VocabDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VocabDbContext>()
            .UseSqlite("Data Source=vocab.design.db")
            .Options;
        return new VocabDbContext(options);
    }
}
