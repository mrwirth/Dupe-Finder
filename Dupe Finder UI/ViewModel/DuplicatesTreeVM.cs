using Dupe_Finder_DB;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Dupe_Finder_UI.ViewModel
{
    public class DuplicatesTreeVM : BaseVM
    {
        #region Data
        #region Label data
        private string _status;
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged("Status");
            }
        }

        private string _folderPath;
        public string FolderPath
        {
            get => _folderPath;
            set
            {
                _folderPath = value;
                OnPropertyChanged("FolderPath");
            }
        }

        private long _duplicateItemCount;
        public long DuplicateItemCount
        {
            get => _duplicateItemCount;
            set
            {
                _duplicateItemCount = value;
                OnPropertyChanged("DuplicateItemCount");
                OnPropertyChanged("DuplicateItemCountText");
            }
        }
        public string DuplicateItemCountText => $"Extra Files: {DuplicateItemCount} files in {Children.Count().ToString("N0")} groups";

        private long _wastedSpace;
        public long WastedSpace
        {
            get => _wastedSpace;
            set
            {
                _wastedSpace = value;
                OnPropertyChanged("WastedSpace");
                OnPropertyChanged("WastedSpaceText");
            }
        }
        public string WastedSpaceText => $"Wasted Space: {WastedSpace.ToString("N0")} bytes";
        #endregion Label data

        #region Functional Data
        private bool _fullComparisonEnabled = false;
        public bool FullComparisonEnabled
        {
            get => _fullComparisonEnabled;
            set
            {
                _fullComparisonEnabled = value;
                OnPropertyChanged("FullComparisonEnabled");
            }
        }
        #endregion Functional Data

        #region Local data
        protected enum ChecksumType { md5, sha256 };
        protected enum StatusType { Done, Working };
        protected Dictionary<StatusType, string> StatusText = new Dictionary<StatusType, string>()
        {
            { StatusType.Done, "Done." },
            { StatusType.Working, "Working..." }
        };
        #endregion Local data
        #endregion Data

        #region Constructors
        #endregion Constructors

        #region Operations

        public async void LoadData(string path)
        {
            // Set status and clear old data.
            Status = StatusText[StatusType.Working];
            FullComparisonEnabled = false;
            Children.Clear();

            // Load all the basic info about the new directory into the database.
            FolderPath = path;
            await Task.Run(() => LoadDirectory(path));

            // Extract duplicates and add to viewmodel.
            var optionsBuilder = new DbContextOptionsBuilder<DupeFinderContext>();
            var connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["Test"];
            optionsBuilder.UseSqlite(connectionString.ConnectionString);
            using (var context = new DupeFinderContext(optionsBuilder.Options))
            {
                context.Database.EnsureCreated();

                // Extract duplicates.
                var tree = context.Files
                    .GroupBy(f => f.Size)
                    .Where(g => g.Count() > 1)
                    .OrderByDescending(g => g.Count() * g.Key.Size)
                    .AsEnumerable()
                    .Select(g =>
                    {
                        var result = new DupeGroupVM(g.Key.Size);
                        foreach (var file in g)
                        {
                            result.Children.Add(new DuplicateFileVM(file));
                        }
                        return result;
                    });

                // Fill in viewmodel data.
                foreach (var group in tree)
                {
                    Children.Add(group);
                }
                DuplicateItemCount = tree.Select(g => g.Children.Count - 1).Sum();
                WastedSpace = tree.Select(g => (long)g.Size * (g.Children.Count - 1)).Sum();
            }

            // Set status to finished and enable full comparison.
            Status = StatusText[StatusType.Done];
            FullComparisonEnabled = true;
        }

        public async void DoFullComparison()
        {
            // Set status, clear the entries, and compute checksums where necessary.
            Status = StatusText[StatusType.Working];
            FullComparisonEnabled = false;
            Children.Clear();
            await Task.Run(() => CompareFiles());

            // Extract duplicates and add to viewmodel.
            var optionsBuilder = new DbContextOptionsBuilder<DupeFinderContext>();
            var connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["Test"];
            optionsBuilder.UseSqlite(connectionString.ConnectionString);
            using (var context = new DupeFinderContext(optionsBuilder.Options))
            {
                context.Database.EnsureCreated();

                // Extract duplicates.
                var tree = context.Files
                    .GroupBy(f => new { f.ChecksumId, f.Size.Size })
                    .Where(g => g.Count() > 1)
                    .OrderByDescending(g => g.Count() * g.Key.Size)
                    .AsEnumerable()
                    .Select(g =>
                    {
                        var result = new DupeGroupVM(g.Key.Size);
                        foreach (var file in g)
                        {
                            result.Children.Add(new DuplicateFileVM(file));
                        }
                        return result;
                    });

                // Fill in viewmodel data.
                foreach (var group in tree)
                {
                    Children.Add(group);
                }
                DuplicateItemCount = tree.Select(g => g.Children.Count - 1).Sum();
                WastedSpace = tree.Select(g => (long)g.Size * (g.Children.Count - 1)).Sum();
            }

            // Set status to finished.
            Status = StatusText[StatusType.Done];
        }

        public void LoadDirectory(string path)
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
                        Path = filepath,
                        SizeId = sizeInfo.SizeId
                    };
                    context.Files.Add(file);
                }
                context.SaveChanges();
            }
        }

        public void CompareFiles()
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
                        // Let user see which file we're on.
                        Status = $"Reading file: {file.Path}";
                        var csum = GetChecksum(file.Path, ChecksumType.sha256);
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
        #endregion Operations
    }
}
