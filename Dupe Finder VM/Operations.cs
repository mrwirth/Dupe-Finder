using Dupe_Finder_DB;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Dupe_Finder_VM
{
    public class Operations
    {
        private enum ChecksumType { md5, sha256 };

        public static async Task<ComparisonResult> GetBasicComparison(string path)
        {
            await Task.Run(() => LoadDirectory(path));

            var optionsBuilder = new DbContextOptionsBuilder<DupeFinderContext>();
            var connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["Test"];
            optionsBuilder.UseSqlite(connectionString.ConnectionString);
            using (var context = new DupeFinderContext(optionsBuilder.Options))
            {
                context.Database.EnsureCreated();

                var tree = context.Files
                    .GroupBy(f => f.Size)
                    .Where(g => g.Count() > 1)
                    .OrderBy(g => g.Count() * g.Key.Size)
                    .AsEnumerable()
                    .Select(g =>
                    {
                        var result = new TreeViewItem()
                        {
                            Header = $"{g.Count()} @ {g.Key.Size.ToString("N0")} bytes",
                            Tag = g.Key.Size
                        };
                        foreach (var file in g)
                        {
                            result.Items.Add(new TreeViewItem()
                            {
                                Header = Path.Combine(file.Directory, file.Filename)
                            });
                        }
                        return result;
                    });

                return new ComparisonResult()
                {
                    TreeViewItems = tree.ToList(),
                    DuplicateItemCount = tree.Select(g => g.Items.Count - 1).Sum(),
                    WastedSpace = tree.Select(g => (long)g.Tag * (g.Items.Count - 1)).Sum()
                };
            }
        }

        public static async Task<ComparisonResult> GetFullComparison()
        {
            await Task.Run(() => CompareFiles());

            var optionsBuilder = new DbContextOptionsBuilder<DupeFinderContext>();
            var connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["Test"];
            optionsBuilder.UseSqlite(connectionString.ConnectionString);
            using (var context = new DupeFinderContext(optionsBuilder.Options))
            {
                context.Database.EnsureCreated();

                var tree = context.Files
                    .GroupBy(f => new { f.ChecksumId, f.Size.Size })
                    .Where(g => g.Count() > 1)
                    .OrderBy(g => g.Count() * g.Key.Size)
                    .AsEnumerable()
                    .Select(g =>
                    {
                        var result = new TreeViewItem()
                        {
                            Header = $"{g.Count()} @ {g.Key.Size.ToString("N0")} bytes",
                            Tag = g.Key.Size
                        };
                        foreach (var file in g)
                        {
                            result.Items.Add(new TreeViewItem()
                            {
                                Header = Path.Combine(file.Directory, file.Filename)
                            });
                        }
                        return result;
                    });

                return new ComparisonResult()
                {
                    TreeViewItems = tree.ToList(),
                    DuplicateItemCount = tree.Select(g => g.Items.Count - 1).Sum(),
                    WastedSpace = tree.Select(g => (long)g.Tag * (g.Items.Count - 1)).Sum()
                };
            }
        }

        public static void LoadDirectory(string path)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DupeFinderContext>();
            var connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["Test"];
            optionsBuilder.UseSqlite(connectionString.ConnectionString);
            using (var context = new DupeFinderContext(optionsBuilder.Options))
            {
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();

                var sizes = new Dictionary<long, SizeInfo>();
                var filepaths = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                foreach (var filepath in filepaths)
                {
                    var fileInfo = new FileInfo(filepath);

                    // Figure out if we have a duplicate size or not.
                    if (sizes.TryGetValue(fileInfo.Length, out var sizeInfo) == false)
                    {
                        sizeInfo = new SizeInfo()
                        {
                            Size = fileInfo.Length
                        };
                        sizes.Add(fileInfo.Length, sizeInfo);
                        context.SizeInfos.Add(sizeInfo);
                    }

                    var file = new Dupe_Finder_DB.File()
                    {
                        Directory = Path.GetDirectoryName(filepath),
                        Filename = Path.GetFileName(filepath),
                        SizeId = sizeInfo.SizeId
                    };
                    context.Files.Add(file);
                }
                context.SaveChanges();
            }
        }

        public static void CompareFiles()
        {
            var optionsBuilder = new DbContextOptionsBuilder<DupeFinderContext>();
            var connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["Test"];
            optionsBuilder.UseSqlite(connectionString.ConnectionString);
            using (var context = new DupeFinderContext(optionsBuilder.Options))
            {
                var files = context.Files
                    .GroupBy(f => f.Size)
                    .Where(g => g.Count() > 1)
                    .OrderBy(g => g.Count() * g.Key.Size);

                var checksums = new Dictionary<string, Checksum>();
                foreach (var group in files)
                {
                    foreach (var file in group)
                    {
                        var csum = GetChecksum(Path.Combine(file.Directory, file.Filename), ChecksumType.sha256);
                        if (checksums.TryGetValue(csum, out var checksum) == false)
                        {
                            checksum = new Checksum()
                            {
                                Type = Dupe_Finder_DB.ChecksumType.SHA256,
                                Value = csum
                            };
                            checksums.Add(csum, checksum);
                            context.Checksums.Add(checksum);
                        }
                        file.ChecksumId = checksum.ChecksumID;
                    }
                }
                context.SaveChanges();
            }
        }

        static string GetChecksum(string filename, ChecksumType type)
        {
            string computed;
            switch (type)
            {
                case ChecksumType.md5:
                    computed = Md5SumFile(filename);
                    break;
                case ChecksumType.sha256:
                    computed = Sha256SumFile(filename);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return computed;
        }

        static string Md5SumFile(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = System.IO.File.OpenRead(filename))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                }
            }
        }

        static string Sha256SumFile(string filename)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = System.IO.File.OpenRead(filename))
                {
                    return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLower();
                }
            }
        }
    }
}
