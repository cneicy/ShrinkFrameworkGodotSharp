namespace CommonSDK.Logger;

public class LogHelper(string prefix) : ILogger
{
    public void Log(LogType type, string msg)
    {
        Console.WriteLine($"[{DateTime.Now}][{prefix}][{type}]: {msg}");
    }

    public void LogInfo(string msg)
    {
        Console.WriteLine($"[{DateTime.Now}][{prefix}][{LogType.Info}]: {msg}");
    }

    public void LogWarn(string msg)
    {
        Console.WriteLine($"[{DateTime.Now}][{prefix}][{LogType.Warn}]: {msg}");
    }

    public void LogError(string msg)
    {
        Console.WriteLine($"[{DateTime.Now}][{prefix}][{LogType.Error}]: {msg}");
    }

    public void LogDebug(string msg)
    {
        Console.WriteLine($"[{DateTime.Now}][{prefix}][{LogType.Debug}]: {msg}");
    }

    public void LogFatal(string msg)
    {
        Console.WriteLine($"[{DateTime.Now}][{prefix}][{LogType.Fatal}]: {msg}");
    }
}