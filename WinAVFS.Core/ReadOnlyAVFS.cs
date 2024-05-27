using System.Collections.Concurrent;
using System.Security.AccessControl;
using DokanNet;
using DokanNet.Logging;
using FileAccess = DokanNet.FileAccess;

namespace WinAvfs.Core
{
    public class ReadOnlyAvfs(IArchiveProvider archiveProvider) : IDokanOperationsUnsafe
    {
        private FsTree _fsTree;

        public void Mount(string mountPoint)
        {
            Unmount(mountPoint);

            _fsTree = archiveProvider.ReadFsTree();
            this.Mount(mountPoint, DokanOptions.WriteProtection | DokanOptions.MountManager, new NullLogger());
        }

        public void Unmount(string mountPoint)
        {
            Dokan.RemoveMountPoint(mountPoint);
            _fsTree = null;
        }

        #region Private helper methods

        private static readonly FileInformation[] EmptyFileInformation = [];
        private readonly DateTime _defaultTime = DateTime.Now;

        private readonly ConcurrentDictionary<string, FsTreeNode> _nodeCache =
            new ConcurrentDictionary<string, FsTreeNode>();

        private FsTreeNode GetNode(string fileName, IDokanFileInfo info = null)
        {
            if (info?.Context != null)
            {
                return (FsTreeNode) info.Context;
            }

            fileName = fileName.ToLower();
            if (_nodeCache.TryGetValue(fileName, out var nodeFromCache))
            {
                return nodeFromCache;
            }

            var paths = fileName.Split('\\');
            var node = _fsTree.Root;
            foreach (var path in paths.Where(y => !string.IsNullOrEmpty(y)))
            {
                if (!node.IsDirectory || !node.Children.TryGetValue(path, out var child))
                {
                    return null;
                }

                node = child;
            }

            _nodeCache.TryAdd(fileName, node);
            return node;
        }

        #endregion

        #region Dokan filesystem implementation

        public NtStatus CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode,
            FileOptions options, FileAttributes attributes, IDokanFileInfo info)
        {
            if (mode != FileMode.Open && mode != FileMode.OpenOrCreate)
            {
                return NtStatus.AccessDenied;
            }

            if (info.Context == null)
            {
                var node = GetNode(fileName);
                if (node == null)
                {
                    if (mode == FileMode.OpenOrCreate)
                    {
                        return NtStatus.AccessDenied;
                    }

                    return NtStatus.ObjectPathNotFound;
                }

                info.IsDirectory = node.IsDirectory;
                info.Context = node;
            }

            return NtStatus.Success;
        }

        public void Cleanup(string fileName, IDokanFileInfo info)
        {
            // No-op
        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {
            // No-op
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            bytesRead = 0;
            return NtStatus.NotImplemented;
        }

        public NtStatus ReadFile(string fileName, IntPtr buffer, uint bufferLength, out int bytesRead, long offset,
            IDokanFileInfo info)
        {
            bytesRead = 0;
            var node = GetNode(fileName, info);
            if (node == null)
            {
                return NtStatus.ObjectPathNotFound;
            }

            node.FillBuffer(buf => archiveProvider.ExtractFileUnmanaged(node, buf));

            unsafe
            {
                bytesRead = (int) Math.Min(bufferLength, node.Length - offset);
                Buffer.MemoryCopy((byte*) node.Buffer.ToPointer() + offset, buffer.ToPointer(), bufferLength,
                    bytesRead);
            }

            return NtStatus.Success;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset,
            IDokanFileInfo info)
        {
            bytesWritten = 0;
            return NtStatus.AccessDenied;
        }

        public NtStatus WriteFile(string fileName, IntPtr buffer, uint bufferLength, out int bytesWritten, long offset,
            IDokanFileInfo info)
        {
            bytesWritten = 0;
            return NtStatus.AccessDenied;
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            return NtStatus.NotImplemented;
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            fileInfo = new FileInformation();
            var node = GetNode(fileName, info);
            if (node == null)
            {
                return NtStatus.ObjectPathNotFound;
            }

            fileInfo.FileName = node.FullName;
            fileInfo.Attributes = node.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal;
            fileInfo.CreationTime = node.CreationTime ?? _defaultTime;
            fileInfo.LastAccessTime = node.LastAccessTime ?? _defaultTime;
            fileInfo.LastWriteTime = node.LastWriteTime ?? _defaultTime;
            fileInfo.Length = node.Length;
            return NtStatus.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = EmptyFileInformation;
            var node = GetNode(fileName, info);
            if (node == null)
            {
                return NtStatus.ObjectPathNotFound;
            }

            files = node.Children.Select(child => new FileInformation
            {
                FileName = child.Value.Name,
                Attributes = child.Value.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal,
                CreationTime = child.Value.CreationTime ?? _defaultTime,
                LastAccessTime = child.Value.LastAccessTime ?? _defaultTime,
                LastWriteTime = child.Value.LastWriteTime ?? _defaultTime,
                Length = child.Value.Length
            }).ToList();
            return NtStatus.Success;
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files,
            IDokanFileInfo info)
        {
            files = EmptyFileInformation;
            return NtStatus.NotImplemented;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
        {
            return NtStatus.AccessDenied;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
            DateTime? lastWriteTime, IDokanFileInfo info)
        {
            return NtStatus.AccessDenied;
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            return NtStatus.AccessDenied;
        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            return NtStatus.AccessDenied;
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            return NtStatus.AccessDenied;
        }

        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            return NtStatus.AccessDenied;
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            return NtStatus.AccessDenied;
        }

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return NtStatus.AccessDenied;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return NtStatus.AccessDenied;
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes,
            out long totalNumberOfFreeBytes, IDokanFileInfo info)
        {
            freeBytesAvailable = totalNumberOfFreeBytes = totalNumberOfBytes = _fsTree.Root.Length;
            return NtStatus.Success;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features,
            out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
        {
            volumeLabel = "AVFS";
            fileSystemName = "exFAT";
            features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.UnicodeOnDisk |
                       FileSystemFeatures.VolumeIsCompressed | FileSystemFeatures.ReadOnlyVolume;
            maximumComponentLength = 260;
            return NtStatus.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security,
            AccessControlSections sections, IDokanFileInfo info)
        {
            security = null;
            return NtStatus.NotImplemented;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections,
            IDokanFileInfo info)
        {
            return NtStatus.AccessDenied;
        }

        public NtStatus Mounted(IDokanFileInfo info)
        {
            Console.WriteLine($"Mounted readonly filesystem");
            return NtStatus.Success;
        }

        public NtStatus Unmounted(IDokanFileInfo info)
        {
            Console.WriteLine($"Unmounted readonly filesystem");
            return NtStatus.Success;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            streams = EmptyFileInformation;
            return NtStatus.NotImplemented;
        }

        #endregion
    }
}