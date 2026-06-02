using K8sInitWaitFor;

namespace K8sInitWaitFor.Tests;

public sealed class WaitOptionsParserTests
{
    [Fact]
    public void ParsesJobName()
    {
        var console = new CapturingConsole();

        var parsed = WaitOptionsParser.TryParse(["job", "db-migration"], console, out var options);

        Assert.True(parsed);
        Assert.Equal(ResourceKind.Job, options.ResourceKind);
        Assert.Equal(FailureMode.RequireSuccess, options.FailureMode);
        Assert.Equal("db-migration", Assert.IsType<NamedResourceQuery>(options.Query).Name);
    }

    [Fact]
    public void ParsesSelectorAndNamespace()
    {
        var console = new CapturingConsole();

        var parsed = WaitOptionsParser.TryParse(["pod-wr", "-lapp=proauth", "--namespace", "tenant-a"], console, out var options);

        Assert.True(parsed);
        Assert.Equal(ResourceKind.Pod, options.ResourceKind);
        Assert.Equal(FailureMode.RequireAnySuccess, options.FailureMode);
        Assert.Equal("app=proauth", Assert.IsType<LabelSelectorQuery>(options.Query).Selector);
        Assert.Equal("tenant-a", options.Namespace);
    }

    [Fact]
    public void RejectsUnsupportedKubectlArguments()
    {
        var console = new CapturingConsole();

        var parsed = WaitOptionsParser.TryParse(["pod", "web", "--context", "prod"], console, out _);

        Assert.False(parsed);
        Assert.Contains("Unsupported kubectl-style argument", console.ErrorOutput);
    }
}
