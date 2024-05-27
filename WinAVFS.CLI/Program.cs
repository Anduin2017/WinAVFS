using WinAvfs.Core;

namespace WinAvfs.CLI
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine(@"Usage: WinAVFS.CLI.exe <path to archive> <mount point>");
                Console.WriteLine(@"Example: WinAVFS.CLI.exe D:\1.zip Z:\");
                return;
            }

            var fs = new ReadOnlyAvfs(new SevenZipProvider(args[0]));
            fs.Mount(args[1]);
        }
    }
}