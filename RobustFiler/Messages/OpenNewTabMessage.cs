namespace RobustFiler.Messages;

public class OpenNewTabMessage
{
    public string Path { get; }
    public OpenNewTabMessage(string path)
    {
        Path = path;
    }
}
