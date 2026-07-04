using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text.Json;
using RobustFiler.Models;

namespace RobustFiler.Services;

public class LocalFileService : IFileService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);
    
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_NOERRORUI = 0x0400;
    private const ushort FOF_SILENT = 0x0004;

    private FileSystemWatcher? _watcher;
    public event EventHandler<string>? DirectoryChanged;

    public void StartWatch(string path)
    {
        StopWatch();
        if (Directory.Exists(path))
        {
            _watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _watcher.Created += (s, e) => DirectoryChanged?.Invoke(this, path);
            _watcher.Deleted += (s, e) => DirectoryChanged?.Invoke(this, path);
            _watcher.Renamed += (s, e) => DirectoryChanged?.Invoke(this, path);
        }
    }

    public void StopWatch()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    public async Task<IEnumerable<FileItem>> GetFilesAsync(string path)
    {
        return await Task.Run(() =>
        {
            var results = new List<FileItem>();
            try
            {
                var directoryInfo = new DirectoryInfo(path);
                
                // Get directories
                foreach (var dir in directoryInfo.GetDirectories())
                {
                    results.Add(new FileItem
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        IsDirectory = true,
                        DateModified = dir.LastWriteTime
                    });
                }
                
                // Get files
                foreach (var file in directoryInfo.GetFiles())
                {
                    results.Add(new FileItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        Size = file.Length,
                        DateModified = file.LastWriteTime
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Ignored - empty list will be returned
            }
            catch (Exception)
            {
                // Ignored for phase 1 - prevent crash
            }
            return results;
        });
    }

    public async Task<bool> DeleteToRecycleBinAsync(string path)
    {
        return await Task.Run(() =>
        {
            try
            {
                var shf = new SHFILEOPSTRUCT
                {
                    wFunc = FO_DELETE,
                    pFrom = path + '\0' + '\0', // Must be double-null terminated
                    fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT)
                };
                int result = SHFileOperation(ref shf);
                return result == 0 && !shf.fAnyOperationsAborted;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<bool> RenameAsync(string oldPath, string newName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var parent = Path.GetDirectoryName(oldPath);
                var newPath = Path.Combine(parent ?? "", newName);
                if (Directory.Exists(oldPath))
                {
                    Directory.Move(oldPath, newPath);
                }
                else if (File.Exists(oldPath))
                {
                    File.Move(oldPath, newPath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<bool> CreateFolderAsync(string parentPath, string folderName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var newPath = Path.Combine(parentPath, folderName);
                Directory.CreateDirectory(newPath);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task OpenFileAsync(string path)
    {
        await Task.Run(() =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch
            {
                throw;
            }
        });
    }

    public async Task<bool> CopyFilesAsync(IEnumerable<string> sourcePaths, string destinationPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                foreach (var path in sourcePaths)
                {
                    if (File.Exists(path))
                    {
                        var dest = Path.Combine(destinationPath, Path.GetFileName(path));
                        File.Copy(path, dest, overwrite: true);
                    }
                    else if (Directory.Exists(path))
                    {
                        var dest = Path.Combine(destinationPath, Path.GetFileName(path));
                        CopyDirectory(path, dest);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<bool> MoveFilesAsync(IEnumerable<string> sourcePaths, string destinationPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                foreach (var path in sourcePaths)
                {
                    var dest = Path.Combine(destinationPath, Path.GetFileName(path));
                    if (File.Exists(path))
                    {
                        File.Move(path, dest, overwrite: true);
                    }
                    else if (Directory.Exists(path))
                    {
                        Directory.Move(path, dest);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }

    public async Task<IEnumerable<FavoriteItem>> LoadFavoritesAsync()
    {
        var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        var filePath = Path.Combine(localFolder, "favorites.json");
        if (!File.Exists(filePath))
        {
            return new List<FavoriteItem>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var favorites = JsonSerializer.Deserialize<List<FavoriteItem>>(json);
            return favorites ?? new List<FavoriteItem>();
        }
        catch
        {
            return new List<FavoriteItem>();
        }
    }

    public async Task SaveFavoritesAsync(IEnumerable<FavoriteItem> favorites)
    {
        try
        {
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var filePath = Path.Combine(localFolder, "favorites.json");
            var json = JsonSerializer.Serialize(favorites);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch
        {
            // Ignored
        }
    }
}
