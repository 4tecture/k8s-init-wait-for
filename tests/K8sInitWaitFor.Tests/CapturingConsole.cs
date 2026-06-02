using K8sInitWaitFor;

namespace K8sInitWaitFor.Tests;

internal sealed class CapturingConsole : IConsole
{
    private readonly List<string> _errors = [];

    public string ErrorOutput => string.Join(Environment.NewLine, _errors);

    public void Info(string message)
    {
    }

    public void Debug(string message)
    {
    }

    public void Error(string message) => _errors.Add(message);
}
