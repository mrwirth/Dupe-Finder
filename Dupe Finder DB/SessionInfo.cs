using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dupe_Finder_DB
{
    public enum StateType
    {
        NoData = 0, // Intentional default value.
        Working,
        DirectoryOpened,
        ChecksumComparisonCompleted,
        BinaryComparisonCompleted
    }

    public class SessionInfo
    {
        [Key]
        public int Id { get; set; }
        public StateType State { get; set; }
        
        public virtual ICollection<SessionPath> SessionPaths { get; set; }
    }
}
