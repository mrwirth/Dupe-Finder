using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dupe_Finder_UI
{
    class WpfIOService : IOService
    {
        public string OpenFolderDialog()
        {
            var ofd = new OpenFileDialog
            {
                // Set validate names and check file exists to false otherwise windows will
                // not let you select "Folder Selection."
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                // Always default to Folder Selection.
                FileName = "Folder Selection."
            };

            if (ofd.ShowDialog() == true)
            {
                return Path.GetDirectoryName(ofd.FileName);
            }
            else
            {
                return null;
            }
        }

        public string OpenSavedSessionDialog()
        {
            var ofd = new OpenFileDialog
            {
                CheckFileExists = true,
                DefaultExt = "DupeFinderSession",
                Multiselect = false
            };

            if (ofd.ShowDialog() == true)
            {
                return ofd.FileName;
            }
            else
            {
                return null;
            }
        }

        public string SaveSessionFileDialog()
        {
            var sfd = new SaveFileDialog()
            {
                AddExtension = true,
                DefaultExt = "DupeFinderSession",
                OverwritePrompt = true
            };
            
            if (sfd.ShowDialog() == true)
            {
                return sfd.FileName;
            }
            else
            {
                return null;
            }
        }
    }
}
