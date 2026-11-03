
using Catalog.Persistence.Write.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Persistence.Write;

public partial class CatalogWriteDbContext
{
    partial void OnModelCreatingPartial(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>()
            .Property(x => x.Version)
            .IsConcurrencyToken();
    }
}

