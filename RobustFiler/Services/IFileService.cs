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

    event System.EventHandler<string> DirectoryChanged;
    void StartWatch(string path);
    void StopWatch();
}
