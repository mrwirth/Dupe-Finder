using System.ComponentModel.DataAnnotations;

namespace Dupe_Finder_DB
{
    public class SessionPath
    {
        [Key]
        public int Id { get; set; }
        public string Path { get; set; }
    }
}