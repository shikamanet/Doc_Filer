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

    public async Task DeleteToRecycleBinAsync(string path)
    {
        await Task.Run(() =>
        {
            var shf = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path + '\0' + '\0', // Must be double-null terminated
                fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT)
            };
            int result = SHFileOperation(ref shf);
            if (result != 0 || shf.fAnyOperationsAborted)
            {
                throw new IOException($"ファイルまたはフォルダー '{Path.GetFileName(path)}' をごみ箱に移動できませんでした。(エラーコード: {result})");
            }
        });
    }

    public async Task RenameAsync(string oldPath, string newName)
    {
        await Task.Run(() =>
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
            else
            {
                throw new FileNotFoundException($"指定されたファイルまたはフォルダーが見つかりません。({oldPath})");
            }
        });
    }

    public async Task CreateFolderAsync(string parentPath, string folderName)
    {
        await Task.Run(() =>
        {
            var newPath = Path.Combine(parentPath, folderName);
            Directory.CreateDirectory(newPath);
        });
    }

    public async Task OpenFileAsync(string path, string arguments = "")
    {
        try
        {
            if (string.IsNullOrEmpty(arguments))
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
                var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetDirectoryName(path));
                var options = new Windows.System.LauncherOptions
                {
                    NeighboringFilesQuery = folder.CreateFileQuery()
                };
                await Windows.System.Launcher.LaunchFileAsync(file, options);
            }
            else
            {
                await Task.Run(() =>
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true,
                        Arguments = arguments,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(path) ?? string.Empty
                    };
                    System.Diagnostics.Process.Start(startInfo);
                });
            }
        }
        catch
        {
            // Fallback if Launcher fails (e.g., unsupported file type or access denied)
            await Task.Run(() =>
            {
                try
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    };
                    if (!string.IsNullOrEmpty(arguments))
                    {
                        startInfo.Arguments = arguments;
                    }
                    startInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
                    System.Diagnostics.Process.Start(startInfo);
                }
                catch { }
            });
        }
    }

    public string ResolveShortcut(string shortcutPath)
    {
        try
        {
            Type? t = Type.GetTypeFromProgID("WScript.Shell");
            if (t != null)
            {
                dynamic shell = Activator.CreateInstance(t)!;
                var shortcut = shell.CreateShortcut(shortcutPath);
                return shortcut.TargetPath;
            }
        }
        catch { }
        return string.Empty;
    }

    public async Task CopyFilesAsync(IEnumerable<string> sourcePaths, string destinationPath)
    {
        await Task.Run(() =>
        {
            var exceptions = new List<Exception>();
            foreach (var path in sourcePaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var dest = Path.Combine(destinationPath, Path.GetFileName(path));
                        File.Copy(path, dest, overwrite: true);
                    }
                    else if (Directory.Exists(path))
                    {
                        var dest = Path.Combine(destinationPath, Path.GetFileName(path));
                        if (string.Equals(path, dest, StringComparison.OrdinalIgnoreCase)) continue; // ignore
                        CopyDirectory(path, dest);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(new Exception($"'{Path.GetFileName(path)}' のコピー中にエラーが発生しました: {ex.Message}", ex));
                }
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }
        });
    }

    public async Task MoveFilesAsync(IEnumerable<string> sourcePaths, string destinationPath)
    {
        await Task.Run(() =>
        {
            var exceptions = new List<Exception>();
            foreach (var path in sourcePaths)
            {
                try
                {
                    var dest = Path.Combine(destinationPath, Path.GetFileName(path));
                    if (string.Equals(path, dest, StringComparison.OrdinalIgnoreCase)) continue; // skip if moving to same location

                    if (File.Exists(path))
                    {
                        File.Move(path, dest, overwrite: true);
                    }
                    else if (Directory.Exists(path))
                    {
                        Directory.Move(path, dest);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(new Exception($"'{Path.GetFileName(path)}' の移動中にエラーが発生しました: {ex.Message}", ex));
                }
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
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

    private string GetAppLocalFolder()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "RobustFiler");
        Directory.CreateDirectory(appFolder);
        return appFolder;
    }

    public async Task<IEnumerable<FavoriteItem>> LoadFavoritesAsync()
    {
        var filePath = Path.Combine(GetAppLocalFolder(), "favorites.json");
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
            var filePath = Path.Combine(GetAppLocalFolder(), "favorites.json");
            var json = JsonSerializer.Serialize(favorites);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch
        {
            // Ignored
        }
    }

    public async Task<SessionState?> LoadSessionAsync()
    {
        var filePath = Path.Combine(GetAppLocalFolder(), "session.json");
        if (!File.Exists(filePath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<SessionState>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveSessionAsync(SessionState session)
    {
        try
        {
            var filePath = Path.Combine(GetAppLocalFolder(), "session.json");
            var json = JsonSerializer.Serialize(session);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch
        {
            // Ignored
        }
    }
}
