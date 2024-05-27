
namespace WinAvfs.Core
{
    public interface IArchiveProvider : IDisposable
    {
        FsTree ReadFsTree();

        void ExtractFileUnmanaged(FsTreeNode node, IntPtr buffer);
    }
}