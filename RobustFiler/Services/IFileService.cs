using System.Collections.Generic;
using System.Threading.Tasks;
using RobustFiler.Models;

namespace RobustFiler.Services;

public interface IFileService
{
    Task<IEnumerable<FileItem>> GetFilesAsync(string path);
    Task<bool> CreateFolderAsync(string parentPath, string folderName);
    Task<bool> DeleteToRecycleBinAsync(string path);
    Task<bool> RenameAsync(string oldPath, string newName);
    Task OpenFileAsync(string path, string arguments = "");
    string ResolveShortcut(string shortcutPath);
    Task CopyFilesAsync(IEnumerable<string> sourcePaths, string destinationPath);
    Task MoveFilesAsync(IEnumerable<string> sourcePaths, string destinationPath);

    Task<IEnumerable<FavoriteItem>> LoadFavoritesAsync();
    Task SaveFavoritesAsync(IEnumerable<FavoriteItem> favorites);

    Task<SessionState?> LoadSessionAsync();
    Task SaveSessionAsync(SessionState session);

    event System.EventHandler<string> DirectoryChanged;
    void StartWatch(string path);
    void StopWatch();
}
