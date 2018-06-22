using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dupe_Finder_DB
{
    public class SizeInfo
    {
        [Key]
        public int SizeId { get; set; }
        public long Size { get; set; }

        public virtual ICollection<File> Files { get; set; }
    }
}
