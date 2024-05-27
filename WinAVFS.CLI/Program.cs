using DokanNet;
using DokanNet.Logging;
using WinAvfs.Core;

namespace WinAvfs.CLI
{
    public abstract class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine(@"Usage: WinAVFS.CLI.exe <path to archive> <mount point>");
                Console.WriteLine(@"Example: WinAVFS.CLI.exe D:\1.zip Z:\");
                return;
            }

            var pathToArchive = args[0];
            var mountPoint = args[1];

            var provider = new ZipArchiveProvider(pathToArchive);
            
            var mre = new ManualResetEvent(false);
            var dokanLogger = new NullLogger();
            using var dokan = new Dokan(dokanLogger);
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                mre.Set();
            };

            var sampleFs = new ReadOnlyAvfs(provider, provider.ReadFsTree());
            var dokanBuilder = new DokanInstanceBuilder(dokan)
                .ConfigureOptions(options =>
                {
                    options.Options = DokanOptions.WriteProtection | DokanOptions.MountManager;
                    options.MountPoint = mountPoint;
                });
            using (dokanBuilder.Build(sampleFs))
            {
                mre.WaitOne();
            }
        }
    }
}