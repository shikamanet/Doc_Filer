namespace RobustFiler.Messages;

public class FileSystemChangedMessage
{
    public string[] AffectedPaths { get; }
    public FileSystemChangedMessage(params string[] affectedPaths)
    {
        AffectedPaths = affectedPaths;
    }
}