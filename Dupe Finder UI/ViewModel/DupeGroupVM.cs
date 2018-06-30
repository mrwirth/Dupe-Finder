using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dupe_Finder_UI.ViewModel
{
    public class DupeGroupVM : BaseVM
    {
        #region Data
        #region Tree Data
        public DupeFinderVM Parent { get; }
        public ObservableCollection<DuplicateFileVM> Children { get; } = new ObservableCollection<DuplicateFileVM>();
        #endregion Tree Data

        public long Size { get; }
        public int Count => Children.Count();
        public string Description => $"{Count} @ {Size.ToString("N0")} bytes ({(Size * (Count - 1)).ToString("N0")} bytes wasted)";
        #endregion Data

        #region Constructors
        public DupeGroupVM(long size, DupeFinderVM parent)
        {
            Size = size;
            Parent = parent;
        }
        #endregion Constructors

        #region Operations
        public async Task DeleteFile(DuplicateFileVM fileVM)
        {
            Children.Remove(fileVM);
            await Parent.DeleteFileFromDatabase(fileVM);
            if (Children.Count < 2)
            {
                Parent.DeleteGroup(this);
            }
        }
        #endregion Operations
    }
}
