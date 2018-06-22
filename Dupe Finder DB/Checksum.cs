using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dupe_Finder_DB
{
    public enum ChecksumType
    {
        MD5 = 0,
        SHA1 = 1,
        SHA256 = 2
    }

    public class Checksum
    {
        [Key]
        public int ChecksumID { get; set; }
        public ChecksumType Type { get; set; }
        [StringLength(64)]
        public string Value { get; set; }

        public virtual ICollection<File> Files { get; set; }
    }
}
