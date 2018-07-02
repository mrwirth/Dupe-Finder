using Dupe_Finder_DB;
using Microsoft.Data.Sqlite;
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
    public class DupeFinderVM : BaseVM, IDataErrorInfo
    {
        #region Data
        #region Tree Data
        public ObservableCollection<DupeGroupVM> Children { get; } = new ObservableCollection<DupeGroupVM>();
        #endregion Tree Data

        #region Status Data
        protected enum StatusType { Done, Working, SavingSession, OpeningSession };
        protected Dictionary<StatusType, string> StatusText = new Dictionary<StatusType, string>()
        {
            { StatusType.Done, "Done." },
            { StatusType.Working, "Working..." },
            { StatusType.SavingSession, "Saving session..." },
            { StatusType.OpeningSession, "Opening session..." }
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
        private List<string> CurrentFolders = new List<string>();
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
        #region Connection Data
        protected SqliteConnection Connection;
        protected DbContextOptions<DupeFinderContext> Options;
        #endregion Connection Data

        protected enum ChecksumType { md5, sha256 };
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
        public DupeFinderVM(IOService ioService)
        {
            IOService = ioService;
            Connection = new SqliteConnection(System.Configuration.ConfigurationManager.ConnectionStrings["InMemory"].ConnectionString);
            Connection.Open();
            Options = new DbContextOptionsBuilder<DupeFinderContext>().UseSqlite(Connection).Options;
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
                    if (FolderPath != null && FolderPath != ""
                        && (FolderPath == "{Multiple Folders}" && CurrentFolders.Count() > 1) == false
                        && Directory.Exists(FolderPath) == false)
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
            // We can always open a new folder as long as we're not already working on something.
            return State != StateType.Working;
        }
        protected async Task ExecuteOpenFolder(object param)
        {
            // Save last state so we can return to it if no path given,
            // then set current state to "Working".
            var LastState = State;
            State = StateType.Working;
            Status = StatusText[StatusType.Working];
            // Try to get path from user, returning to previous state if cancelled.
            var path = IOService.OpenFolderDialog();
            if (path != null)
            {
                // Set the new location as current folder and
                // Set the current path property to the newly received path.
                CurrentFolders = new List<string>() { path };
                SetFolderPath();
                // Load file info from the directory into the database
                // and then select the relevant info into the dupes tree.
                await OpenDirectory(path);
                // Set the state to indicate that we've got a directory loaded, but no further comparisons done.
                State = StateType.DirectoryOpened;
            }
            else
            {
                // Revert to previous state.
                State = LastState;
            }
            // Set status to finished.
            Status = StatusText[StatusType.Done];
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
            // Save last state so we can return to it if no path given,
            // then set current state to "Working".
            var LastState = State;
            State = StateType.Working;
            Status = StatusText[StatusType.Working];
            // Check if the new folder path is valid, returning to previous state if not.
            var path = FolderPath;
            if (path != null && Directory.Exists(path))
            {
                // Set the new location as current folder.
                CurrentFolders = new List<string>() { path };
                // Load file info from the directory into the database
                // and then select the relevant info into the dupes tree.
                await OpenDirectory(path);
                // Set the state to indicate that we've got a directory loaded, but no further comparisons done.
                State = StateType.DirectoryOpened;
            }
            else
            {
                // Revert to previous state.
                State = LastState;
            }
            // Set status to finished.
            Status = StatusText[StatusType.Done];
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

        #region AddFolderCommand
        protected bool CanAddFolder(object param)
        {
            // We can always add a new folder unless there's nothing already opened,
            // or we're already working on something.
            return State != StateType.Working && State != StateType.NoData;
        }
        protected async Task ExecuteAddFolder(object param)
        {
            // Save last state so we can return to it if no path given,
            // then set current state to "Working".
            var LastState = State;
            State = StateType.Working;
            Status = StatusText[StatusType.Working];
            // Try to get path from user, returning to previous state if cancelled or the path is already loaded.
            var path = IOService.OpenFolderDialog();
            if (path != null && CurrentFolders.Contains(path) == false
                && CurrentFolders.Any(s => path.StartsWith(s)) == false)
            {
                if (CurrentFolders.Any(s => s.StartsWith(path)) == false)
                {
                    // Add the new location to current folders and
                    // set the current path property to indicate we've got multiple folders loaded.
                    CurrentFolders.Add(path);
                    SetFolderPath();
                    // Load file info from the directory into the database
                    // and then select the relevant info into the dupes tree.
                    await AddDirectory(path);
                    // Set the state to indicate that we've got a directory loaded, but no further comparisons done.
                    State = StateType.DirectoryOpened;
                }
                else if (CurrentFolders.Any(s => s.StartsWith(path)))
                {
                    // Add only the subfolders not already included.  
                    throw new NotImplementedException();
                }
            }
            else
            {
                // Revert to previous state.
                State = LastState;
            }
            // Set status to finished.
            Status = StatusText[StatusType.Done];
        }
        private ICommand _addFolder;
        public ICommand AddFolder
        {
            get
            {
                if (_addFolder == null)
                {
                    _addFolder = new RelayCommandAsync(
                        param => ExecuteAddFolder(param),
                        param => CanAddFolder(param)
                        );
                }
                return _addFolder;
            }
        }
        #endregion AddFolderCommand

        #region SaveSessionAsCommand
        protected bool CanSaveSessionAs(object param)
        {
            return true;
        }
        protected async Task ExecuteSaveSessionAs(object param)
        {
            // Save last state so we can return to it if no path given,
            // then set current state to "Working".
            var LastState = State;
            State = StateType.Working;
            Status = StatusText[StatusType.SavingSession];
            // Try to get path from user, returning to previous state if cancelled.
            var path = IOService.SaveSessionFileDialog();
            if (path != null)
            {
                await CopyDatabaseToFile(path, LastState);
            }
            // Revert to previous state.
            State = LastState;
            // Set status to finished.
            Status = StatusText[StatusType.Done];
        }
        private ICommand _saveSessionAs;
        public ICommand SaveSessionAs
        {
            get
            {
                if (_saveSessionAs == null)
                {
                    _saveSessionAs = new RelayCommandAsync(
                        param => ExecuteSaveSessionAs(param),
                        param => CanSaveSessionAs(param)
                        );
                }
                return _saveSessionAs;
            }
        }
        #endregion SaveSessionAsCommand

        #region OpenSavedSessionCommand
        protected bool CanOpenSavedSession(object param)
        {
            return true;
        }
        protected async Task ExecuteOpenSavedSession(object param)
        {
            // Save last state so we can return to it if no path given,
            // then set current state to "Working".
            var LastState = State;
            State = StateType.Working;
            Status = StatusText[StatusType.OpeningSession];
            // Try to get path from user, returning to previous state if cancelled.
            var path = IOService.OpenSavedSessionDialog();
            if (path != null)
            {
                LastState = await OpenDatabaseFromFile(path);
            }
            // Revert to previous state.
            State = LastState;
            // Set status to finished.
            Status = StatusText[StatusType.Done];
        }
        private ICommand _openSavedSession;
        public ICommand OpenSavedSession
        {
            get
            {
                if (_openSavedSession == null)
                {
                    _openSavedSession = new RelayCommandAsync(
                        param => ExecuteOpenSavedSession(param),
                        param => CanOpenSavedSession(param)
                        );
                }
                return _openSavedSession;
            }
        }
        #endregion SaveSessionAsCommand

        #region ResetFolderPathCommand
        protected bool CanResetFolderPath(object param)
        {
            return true;
        }
        protected void ExecuteResetFolderPath(object param)
        {
            SetFolderPath();
        }
        private ICommand _resetFolderPath;
        public ICommand ResetFolderPath
        {
            get
            {
                if (_resetFolderPath == null)
                {
                    _resetFolderPath = new RelayCommand(
                        param => ExecuteResetFolderPath(param),
                        param => CanResetFolderPath(param)
                        );
                }
                return _resetFolderPath;
            }
        }
        #endregion ResetFolderPathCommand

        #region DoChecksumComparisonCommand
        protected bool CanDoChecksumComparison(object param)
        {
            return State != StateType.Working && State != StateType.NoData;
        }
        protected async Task ExecuteDoChecksumComparison(object param)
        {
            State = StateType.Working;
            Status = StatusText[StatusType.Working];
            await ChecksumComparison();
            State = StateType.BinaryComparisonCompleted;
            // Set status to finished.
            Status = StatusText[StatusType.Done];
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
        protected async Task AddDirectory(string path)
        {
            // Invalidate current data
            InvalidateAggregates();
            // Clear the dupes tree, since we're adding to and then reloading the set.
            Children.Clear();

            // Load all the basic info about the new directory into the database.
            await Task.Run(() => AddDirectoryToDatabase(path));

            // Extract duplicates and add to viewmodel.
            LoadDatabaseInfoToViewModel(StateType.DirectoryOpened);
        }

        protected async Task OpenDirectory(string path)
        {
            ResetDatabase();
            await AddDirectory(path);
        }

        protected void LoadDatabaseInfoToViewModel(StateType resultState)
        {
            if (resultState == StateType.DirectoryOpened)
            {
                using (var context = new DupeFinderContext(Options))
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
                            var result = new DupeGroupVM(g.Key.Size, this);
                            foreach (var file in g)
                            {
                                result.Children.Add(new DuplicateFileVM(file, result));
                            }
                            return result;
                        });
                    
                    // Fill in viewmodel data.
                    foreach (var group in tree)
                    {
                        Children.Add(group);
                    }
                }
                CalculateAggregates();
            }
            else if (resultState == StateType.ChecksumComparisonCompleted)
            {
                using (var context = new DupeFinderContext(Options))
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
                            var result = new DupeGroupVM(g.Key.Size, this);
                            foreach (var file in g)
                            {
                                result.Children.Add(new DuplicateFileVM(file, result));
                            }
                            return result;
                        });

                    // Fill in viewmodel data.
                    foreach (var group in tree)
                    {
                        Children.Add(group);
                    }
                }
                CalculateAggregates();
            }
            else if (resultState == StateType.BinaryComparisonCompleted)
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new ArgumentException("resultState");
            }
        }

        protected async Task CopyDatabaseToFile(string targetPath, StateType databaseState)
        {
            var connectionString = "Data Source=" + targetPath;
            var targetOptions = new DbContextOptionsBuilder<DupeFinderContext>().UseSqlite(connectionString).Options;
            using (var workingDatabase = new DupeFinderContext(Options))
            using (var targetDatabase = new DupeFinderContext(targetOptions))
            {
                await workingDatabase.Database.EnsureCreatedAsync();
                await targetDatabase.Database.EnsureDeletedAsync();
                await targetDatabase.Database.EnsureCreatedAsync();

                await targetDatabase.AddRangeAsync(workingDatabase.Checksums);
                await targetDatabase.AddRangeAsync(workingDatabase.SizeInfos);
                await targetDatabase.AddRangeAsync(workingDatabase.Files);

                //var sessionPaths = new List<SessionPath>();
                //foreach (var path in CurrentFolders)
                //{
                //    var sessionPath = new SessionPath() { Path = path };
                //    targetDatabase.SessionPaths.Add(sessionPath);
                //    sessionPaths.Add(sessionPath);
                //}
                var sessionInfo = new SessionInfo()
                {
                    State = databaseState,
                    SessionPaths = CurrentFolders.Select(f => new SessionPath() { Path = f }).ToList()
                };
                targetDatabase.SessionInfo.Add(sessionInfo);

                await targetDatabase.SaveChangesAsync();
            }
        }

        protected async Task<StateType> OpenDatabaseFromFile(string sourcePath)
        {
            var connectionString = "Data Source=" + sourcePath;
            var sourceOptions = new DbContextOptionsBuilder<DupeFinderContext>().UseSqlite(connectionString).Options;
            StateType finalState;
            using (var workingDatabase = new DupeFinderContext(Options))
            using (var sourceDatabase = new DupeFinderContext(sourceOptions))
            {
                await workingDatabase.Database.EnsureCreatedAsync();
                await sourceDatabase.Database.EnsureCreatedAsync();

                await workingDatabase.AddRangeAsync(sourceDatabase.Checksums);
                await workingDatabase.AddRangeAsync(sourceDatabase.SizeInfos);
                await workingDatabase.AddRangeAsync(sourceDatabase.Files);
                await workingDatabase.SaveChangesAsync();
                CurrentFolders.AddRange(sourceDatabase.SessionPaths.Select(sp => sp.Path));
                SetFolderPath();

                finalState = (await sourceDatabase.SessionInfo.FirstAsync()).State;
                LoadDatabaseInfoToViewModel(finalState);
                return finalState;
            }
        }

        public async Task ChecksumComparison()
        {
            // Invalidate current data
            InvalidateAggregates();
            // Clear the dupes tree, since we're re-evaluating it with new info and then reloading the set.
            Children.Clear();

            // Calculate checksums for all files that have the same size as another file and don't already have a checksum value.
            await Task.Run(() => CalculateNewChecksums());

            // Extract duplicates and add to viewmodel.
            LoadDatabaseInfoToViewModel(StateType.ChecksumComparisonCompleted);
        }

        protected void AddDirectoryToDatabase(string path)
        {
            using (var context = new DupeFinderContext(Options))
            {
                context.Database.EnsureCreated();

                var sizes = context.SizeInfos.ToDictionary(si => si.Size);
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

        protected void CalculateNewChecksums()
        {
            using (var context = new DupeFinderContext(Options))
            {
                var files = context.Files
                    .GroupBy(f => f.Size)
                    .Where(g => g.Count() > 1)
                    .OrderBy(g => g.Count() * g.Key.Size);
                
                var checksums = context.Checksums.ToDictionary(cs => cs.Value);
                foreach (var group in files)
                {
                    foreach (var file in group.Where(f => f.ChecksumId == null))
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

        public async Task DeleteFileFromDatabase(DuplicateFileVM fileVM)
        {
            using (var context = new DupeFinderContext(Options))
            {
                context.Files.Remove(fileVM.File);
                await context.SaveChangesAsync();
            }
            CalculateAggregates();
        }

        public void DeleteGroup(DupeGroupVM group)
        {
            Children.Remove(group);
            CalculateAggregates();
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

        protected void ResetDatabase()
        {
            // Since the Sqlite :memory: database requires we keep an open connection,
            // we can't use `EnsureDeleted()` and instead have to close and open the connection to reset.
            Connection.Close();
            Connection.Open();
        }

        protected void SetFolderPath()
        {
            if (CurrentFolders.Count() == 0)
            {
                FolderPath = null;
            }
            else if (CurrentFolders.Count() == 1)
            {
                FolderPath = CurrentFolders[0];
            }
            else
            {
                FolderPath = "{Multiple Folders}";
            }
        }

        protected void CalculateAggregates()
        {
            DuplicateItemCount = Children.Select(g => g.Children.Count - 1).Sum();
            WastedSpace = Children.Select(g => g.Size * (g.Children.Count - 1)).Sum();
        }

        protected void InvalidateAggregates()
        {
            DuplicateItemCountIsValid = false;
            WastedSpaceIsValid = false;
        }
        #endregion Utility Functions
    }
}
