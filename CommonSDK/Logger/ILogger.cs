namespace CommonSDK.Logger;

public interface ILogger
{
    void Log(LogType type, string msg);
    void LogInfo(string msg);
    void LogWarn(string msg);
    void LogError(string msg);
    void LogFatal(string msg);
}