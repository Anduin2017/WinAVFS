using SevenZip;

namespace WinAvfs.Core
{
    public class SevenZipProvider : IArchiveProvider
    {
        private readonly ConcurrentObjectPool<SevenZipExtractor> _extractorPool;

        public SevenZipProvider(string path)
        {
            Console.WriteLine($"Loading archive {path} with 7z.dll");
            _extractorPool = new ConcurrentObjectPool<SevenZipExtractor>(() => new SevenZipExtractor(path));
        }

        public void Dispose()
        {
            foreach (var archive in _extractorPool.GetAll())
            {
                archive.Dispose();
            }
        }

        public FsTree ReadFsTree()
        {
            var extractor = _extractorPool.Get();
            var root = new FsTreeNode(true);
            foreach (var entry in extractor.ArchiveFileData)
            {
                // Console.WriteLine($"Loading {entry.FileName} into FS tree");
                var paths = entry.FileName.Split('/', '\\');
                var node = root;
                for (var i = 0; i < paths.Length - 1; i++)
                {
                    node = node.GetOrAddChild(true, paths[i]);
                }

                if (!string.IsNullOrEmpty(paths[^1]))
                {
                    node = node.GetOrAddChild(entry.IsDirectory, paths[^1], (long) entry.Size,
                        (long) entry.Size, entry.Index);
                    node.CreationTime = entry.CreationTime;
                    node.LastAccessTime = entry.LastAccessTime;
                    node.LastWriteTime = entry.LastWriteTime;
                    // if (!node.IsDirectory && node.Buffer == IntPtr.Zero)
                    // {
                    //     node.Buffer = Marshal.AllocHGlobal((IntPtr) node.Length);
                    // }
                }
            }

            Console.WriteLine($"Loaded {extractor.FilesCount} entries from archive");
            _extractorPool.Put(extractor);
            return new FsTree {Root = root};
        }

        public void ExtractFileUnmanaged(FsTreeNode node, IntPtr buffer)
        {
            if (!(node.Context is int index))
            {
                throw new ArgumentException();
            }

            unsafe
            {
                using var target = new UnmanagedMemoryStream((byte*) buffer.ToPointer(), node.Length, node.Length,
                    FileAccess.Write);
                var extractor = _extractorPool.Get();
                extractor.ExtractFile(index, target);
                _extractorPool.Put(extractor);
            }
        }
    }
}