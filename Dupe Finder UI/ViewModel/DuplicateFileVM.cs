using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
