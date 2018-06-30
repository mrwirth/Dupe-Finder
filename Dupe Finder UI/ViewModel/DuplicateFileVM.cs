using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.VisualBasic.FileIO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Dupe_Finder_UI.ViewModel
{
    public class DuplicateFileVM : BaseVM
    {
        #region Data
        #region Tree Data
        public DupeGroupVM Parent { get; }
        #endregion Tree Data

        public Dupe_Finder_DB.File File { get; }
        public string Path => File.Path;
        public bool HasChecksum => File.ChecksumId != null;
        #endregion Data

        #region Constructors

        public DuplicateFileVM(Dupe_Finder_DB.File file, DupeGroupVM parent)
        {
            File = file;
            Parent = parent;
        }
        #endregion Constructors

        #region Commands
        #region CopyPathCommand
        protected bool CanCopyPath(object param)
        {
            return true;
        }
        protected void ExecuteCopyPath(object param)
        {
            Clipboard.SetDataObject(Path);
        }
        private ICommand _copyPath;
        public ICommand CopyPath
        {
            get
            {
                if (_copyPath == null)
                {
                    _copyPath = new RelayCommand(
                        param => ExecuteCopyPath(param),
                        param => CanCopyPath(param)
                        );
                }
                return _copyPath;
            }
        }
        #endregion OpenFolderCommand


        #region DeleteFileCommand
        protected bool CanDeleteFile(object param)
        {
            return true;
        }
        protected async Task ExecuteDeleteFile(object param)
        {
            FileSystem.DeleteFile(Path, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
            await Parent.DeleteFile(this);
        }
        private ICommand _deleteFile;
        public ICommand DeleteFile
        {
            get
            {
                if (_deleteFile == null)
                {
                    _deleteFile = new RelayCommandAsync(
                        param => ExecuteDeleteFile(param),
                        param => CanDeleteFile(param)
                        );
                }
                return _deleteFile;
            }
        }
        #endregion OpenFolderCommand
        #endregion Commands
    }
}
