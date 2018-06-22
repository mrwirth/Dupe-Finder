using Dupe_Finder_VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dupe_Finder_VM_Test
{
    class Program
    {
        static void Main(string[] args)
        {
            const string path = @"C:\Users\dreik\Desktop\Temp\DupeFinderTest";
            var result = Operations.GetBasicComparison(path);
        }
    }
}
