using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Dupe_Finder_VM
{
    public class ComparisonResult
    {
        public IEnumerable<TreeViewItem> TreeViewItems;
        public long DuplicateItemCount;
        public long WastedSpace;
    }
}
