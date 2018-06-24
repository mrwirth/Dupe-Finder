using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dupe_Finder_DB
{
    public class File
    {
        [Key]
        public int Id { get; set; }
        public string Path { get; set; }

        public int SizeId { get; set; }
        public virtual SizeInfo Size { get; set; }

        public int? ChecksumId { get; set; }
        public virtual Checksum Checksum { get; set; }
    }
}
