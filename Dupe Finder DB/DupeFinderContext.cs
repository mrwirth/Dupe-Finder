using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dupe_Finder_DB
{
    public class DupeFinderContext : DbContext
    {
        public DupeFinderContext(DbContextOptions<DupeFinderContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Make sure each checksum is unique.
            builder.Entity<Checksum>()
                .HasIndex(c => new { c.Type, c.Value })
                .IsUnique();

            // Make sure each File is unique.
            builder.Entity<File>()
                .HasIndex(f => new { f.Path })
                .IsUnique();
        }

        public DbSet<File> Files { get; set; }
        public DbSet<Checksum> Checksums { get; set; }
        public DbSet<SizeInfo> SizeInfos { get; set; }
    }
}
