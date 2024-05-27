using System.Runtime.InteropServices;

namespace WinAvfs.Core
{
    public class FsTreeNode
    {
        public FsTreeNode Parent { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public string FullName { get; private set; } = string.Empty;

        public long Length { get; private set; }

        public long CompressedLength { get; private set; }

        public DateTime? CreationTime { get; internal set; }

        public DateTime? LastAccessTime { get; internal set; }

        public DateTime? LastWriteTime { get; internal set; }

        public Dictionary<string, FsTreeNode> Children { get; }

        public bool IsDirectory => Children != null;

        public object Context { get; internal set; }

        public IntPtr Buffer { get; internal set; } = IntPtr.Zero;

        private bool _extracted;

        public FsTreeNode() : this(false)
        {
        }

        public FsTreeNode(bool isDirectory)
        {
            if (isDirectory)
            {
                Children = new Dictionary<string, FsTreeNode>();
            }
        }

        public FsTreeNode GetOrAddChild(bool isDirectory, string name, long length = 0, long compressedLength = 0,
            object context = null)
        {
            if (Children == null)
            {
                return null;
            }

            var caseInsensitiveName = name.ToLower();
            if (Children.TryGetValue(caseInsensitiveName, out var addChild))
            {
                return addChild;
            }

            var child = new FsTreeNode(isDirectory)
            {
                Parent = this,
                Name = name,
                FullName = $"{FullName}\\{name}",
                Length = length,
                CompressedLength = compressedLength,
                Context = context,
            };
            Children[caseInsensitiveName] = child;

            if (!isDirectory)
            {
                var parent = this;
                while (parent != null)
                {
                    parent.Length += length;
                    parent.CompressedLength += compressedLength;
                    parent = parent.Parent;
                }
            }

            return child;
        }

        public void FillBuffer(Action<IntPtr> extractAction)
        {
            if (_extracted || IsDirectory)
            {
                return;
            }

            lock (this)
            {
                if (!_extracted)
                {
                    if (Buffer == IntPtr.Zero)
                    {
                        Buffer = Marshal.AllocHGlobal((IntPtr) Length);
                    }

                    try
                    {
                        extractAction(Buffer);
                        _extracted = true;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex.StackTrace);
                    }
                }
            }
        }
    }
}