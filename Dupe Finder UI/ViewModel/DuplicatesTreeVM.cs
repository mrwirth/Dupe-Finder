using Dupe_Finder_DB;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Dupe_Finder_UI.ViewModel
{
    public class DuplicatesTreeVM : BaseVM, IDataErrorInfo
    {
        #region Data
        #region Status Data
        protected enum StatusType { Done, Working };
        protected Dictionary<StatusType, string> StatusText = new Dictionary<StatusType, string>()
        {
            { StatusType.Done, "Done." },
            { StatusType.Working, "Working..." }
        };

        private string _status = "Nothing loaded yet.";
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged("Status");
            }
        }
        #endregion Status Data

        #region Results Data
        private long _duplicateItemCount;
        public long DuplicateItemCount
        {
            get => _duplicateItemCount;
            set
            {
                _duplicateItemCount = value;
                OnPropertyChanged("DuplicateItemCount");
                OnPropertyChanged("DuplicateItemCountText");
                DuplicateItemCountIsValid = true;
            }
        }
        public string DuplicateItemCountText => $"Extra Files: {DuplicateItemCount} files in {Children.Count().ToString("N0")} groups";

        private bool _duplicateItemCountIsValid;
        public bool DuplicateItemCountIsValid
        {
            get => _duplicateItemCountIsValid;
            set
            {
                _duplicateItemCountIsValid = value;
                OnPropertyChanged("DuplicateItemCountIsValid");
            }
        }

        private long _wastedSpace;
        public long WastedSpace
        {
            get => _wastedSpace;
            set
            {
                _wastedSpace = value;
                OnPropertyChanged("WastedSpace");
                OnPropertyChanged("WastedSpaceText");
                WastedSpaceIsValid = true;
            }
        }
        public string WastedSpaceText => $"Wasted Space: {WastedSpace.ToString("N0")} bytes";

        private bool _wastedSpaceIsValid;
        public bool WastedSpaceIsValid
        {
            get => _wastedSpaceIsValid;
            set
            {
                _wastedSpaceIsValid = value;
                OnPropertyChanged("WastedSpaceIsValid");
            }
        }
        #endregion Results Data

        #region Path Data
        // Last item is the currently loaded path.
        private Stack<string> FolderPathHistory = new Stack<string>();
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
        #endregion Path Data

        #region Local Data
        protected enum ChecksumType { md5, sha256 };

        protected enum StateType
        {
            NoData = 0, // Intentional default value.
            Working,
            DirectoryOpened,
            ChecksumComparisonCompleted,
            BinaryComparisonCompleted
        }
        private StateType _state;
        protected StateType State
        {
            get => _state;
            set
            {
                _state = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion Local Data

        #region IOService
        protected IOService IOService;
        #endregion IOService
        #endregion Data

        #region Constructors
        public DuplicatesTreeVM(IOService ioService)
        {
            IOService = ioService;
        }
        #endregion Constructors

        #region IDataErrorInfo Members
        public string Error => null;

        public string this[string columnName]
        {
            get
            {
                var result = string.Empty;
                if (columnName == "FolderPath")
                {
                    if (FolderPath != null && FolderPath != "" && Directory.Exists(FolderPath) == false)
                    {
                        result = "Directory does not exist.";
                    }
                }
                return result;
            }
        }
        #endregion IDataErrorInfo Members

        #region Commands
        #region OpenFolderCommand
        protected bool CanOpenFolder(object param)
        {
            return State != StateType.Working;
        }
        protected async Task ExecuteOpenFolder(object param)
        {
            var LastState = State;
            State = StateType.Working;
            var path = IOService.OpenFolderDialog();
            if (path != null)
            {
                FolderPath = path;
                await LoadData(path);
                FolderPathHistory.Push(path);
                State = StateType.DirectoryOpened;
            }
            else
            {
                // Revert to previous state.
                State = LastState;
            }
        }
        private ICommand _openFolder;
        public ICommand OpenFolder
        {
            get
            {
                if (_openFolder == null)
                {
                    _openFolder = new RelayCommandAsync(
                        param => ExecuteOpenFolder(param),
                        param => CanOpenFolder(param)
                        );
                }
                return _openFolder;
            }
        }
        #endregion OpenFolderCommand

        #region OpenFolderPathCommand
        protected bool CanOpenFolderPath(object param)
        {
            return true;
        }
        protected async Task ExecuteOpenFolderPath(object param)
        {
            var LastState = State;
            State = StateType.Working;
            var path = FolderPath;
            if (path != null && Directory.Exists(path))
            {
                await LoadData(path);
                FolderPathHistory.Push(path);
                State = StateType.DirectoryOpened;
            }
            else
            {
                // Revert to previous state.
                State = LastState;
            }
        }
        private ICommand _openFolderPath;
        public ICommand OpenFolderPath
        {
            get
            {
                if (_openFolderPath == null)
                {
                    _openFolderPath = new RelayCommandAsync(
                        param => ExecuteOpenFolderPath(param),
                        param => CanOpenFolderPath(param)
                        );
                }
                return _openFolderPath;
            }
        }
        #endregion OpenFolderPathCommand

        #region DoChecksumComparisonCommand
        protected bool CanDoChecksumComparison(object param)
        {
            return State != StateType.Working && State != StateType.NoData;
        }
        protected async Task ExecuteDoChecksumComparison(object param)
        {
            State = StateType.Working;
            await ChecksumComparison();
            State = StateType.BinaryComparisonCompleted;
        }
        private ICommand _doChecksumComparison;
        public ICommand DoChecksumComparison
        {
            get
            {
                if (_doChecksumComparison == null)
                {
                    _doChecksumComparison = new RelayCommandAsync(
                        param => ExecuteDoChecksumComparison(param),
                        param => CanDoChecksumComparison(param)
                        );
                }
                return _doChecksumComparison;
            }
        }
        #endregion DoChecksumComparisonCommand
        #endregion Commands

        #region Operations

        public async Task LoadData(string path)
        {
            // Set status and clear old data.
            Status = StatusText[StatusType.Working];
            DuplicateItemCountIsValid = false;
            WastedSpaceIsValid = false;
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
        }

        public async Task ChecksumComparison()
        {
            // Set status, clear the entries, and compute checksums where necessary.
            Status = StatusText[StatusType.Working];
            DuplicateItemCountIsValid = false;
            WastedSpaceIsValid = false;
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
        #endregion Operations

        #region Utility Functions
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
        #endregion Utility Functions
    }
}
