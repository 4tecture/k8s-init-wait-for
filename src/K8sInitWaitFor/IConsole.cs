namespace K8sInitWaitFor;

public interface IConsole
{
    void Info(string message);

    void Debug(string message);

    void Error(string message);
}

public sealed class SystemConsole : IConsole
{
    public void Info(string message) => Console.WriteLine(message);

    public void Debug(string message) => Console.Error.WriteLine(message);

    public void Error(string message) => Console.Error.WriteLine(message);
}
