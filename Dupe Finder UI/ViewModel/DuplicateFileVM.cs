using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public string Path { get; }
        #endregion Data

        #region Constructors
        public DuplicateFileVM(string path)
        {
            Path = path;
        }

        public DuplicateFileVM(Dupe_Finder_DB.File file)
        {
            Path = file.Path;
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
        #endregion Commands
    }
}
