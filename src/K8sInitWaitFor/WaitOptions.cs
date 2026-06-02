namespace K8sInitWaitFor;

public sealed record WaitOptions(
    ResourceKind ResourceKind,
    FailureMode FailureMode,
    ResourceQuery Query,
    string Namespace,
    TimeSpan PollInterval,
    int DebugLevel);

public enum ResourceKind
{
    Pod,
    Service,
    Job
}

public enum FailureMode
{
    RequireSuccess,
    TreatErrorsAsReady,
    RequireAnySuccess
}

public abstract record ResourceQuery
{
    public abstract string DisplayValue { get; }
}

public sealed record NamedResourceQuery(string Name) : ResourceQuery
{
    public override string DisplayValue => Name;
}

public sealed record LabelSelectorQuery(string Selector) : ResourceQuery
{
    public override string DisplayValue => "-l" + Selector;
}

public static class WaitOptionsParser
{
    private const string DefaultNamespace = "default";

    public static bool TryParse(string[] args, IConsole console, out WaitOptions options)
    {
        options = default!;

        if (args.Length < 2 || IsHelp(args[0]))
        {
            PrintUsage(console);
            return false;
        }

        if (!TryParseResourceKind(args[0], out var kind, out var failureMode))
        {
            console.Error($"ERROR: Unknown resource type: {args[0]}");
            PrintUsage(console);
            return false;
        }

        var namespaceName = ReadNamespaceFromServiceAccount() ?? DefaultNamespace;
        var queryToken = args[1];
        var remaining = args.Skip(2).ToArray();
        var debugLevel = GetIntEnvironmentVariable("DEBUG", 0);
        var pollInterval = TimeSpan.FromSeconds(GetIntEnvironmentVariable("WAIT_TIME", 2));

        var query = ParseQuery(queryToken);
        for (var index = 0; index < remaining.Length; index++)
        {
            var current = remaining[index];

            if (current is "-n" or "--namespace")
            {
                if (index + 1 >= remaining.Length)
                {
                    console.Error($"{current} requires a namespace value.");
                    return false;
                }

                namespaceName = remaining[++index];
                continue;
            }

            if (current.StartsWith("--namespace=", StringComparison.Ordinal))
            {
                namespaceName = current["--namespace=".Length..];
                continue;
            }

            if (current is "-l" or "--selector")
            {
                if (index + 1 >= remaining.Length)
                {
                    console.Error($"{current} requires a selector value.");
                    return false;
                }

                query = new LabelSelectorQuery(remaining[++index]);
                continue;
            }

            if (current.StartsWith("--selector=", StringComparison.Ordinal))
            {
                query = new LabelSelectorQuery(current["--selector=".Length..]);
                continue;
            }

            if (current == "--debug")
            {
                debugLevel = Math.Max(debugLevel, 1);
                continue;
            }

            console.Error($"Unsupported kubectl-style argument: {current}");
            console.Error("Supported extra arguments are -n/--namespace, -l/--selector, and --debug.");
            return false;
        }

        if (pollInterval <= TimeSpan.Zero)
        {
            console.Error("WAIT_TIME must be a positive integer number of seconds.");
            return false;
        }

        options = new WaitOptions(kind, failureMode, query, namespaceName, pollInterval, debugLevel);
        return true;
    }

    private static bool TryParseResourceKind(string value, out ResourceKind kind, out FailureMode failureMode)
    {
        failureMode = FailureMode.RequireSuccess;

        var resource = value;
        if (value.EndsWith("-we", StringComparison.Ordinal))
        {
            resource = value[..^3];
            failureMode = FailureMode.TreatErrorsAsReady;
        }
        else if (value.EndsWith("-wr", StringComparison.Ordinal))
        {
            resource = value[..^3];
            failureMode = FailureMode.RequireAnySuccess;
        }

        kind = resource switch
        {
            "pod" => ResourceKind.Pod,
            "service" => ResourceKind.Service,
            "job" => ResourceKind.Job,
            _ => default
        };

        return resource is "pod" or "service" or "job"
            && (resource != "service" || failureMode == FailureMode.RequireSuccess);
    }

    private static ResourceQuery ParseQuery(string value)
    {
        if (value.StartsWith("-l", StringComparison.Ordinal) && value.Length > 2)
        {
            return new LabelSelectorQuery(value[2..]);
        }

        if (value.StartsWith("--selector=", StringComparison.Ordinal))
        {
            return new LabelSelectorQuery(value["--selector=".Length..]);
        }

        return new NamedResourceQuery(value);
    }

    private static int GetIntEnvironmentVariable(string name, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out var value) ? value : defaultValue;
    }

    private static string? ReadNamespaceFromServiceAccount()
    {
        const string namespaceFile = "/var/run/secrets/kubernetes.io/serviceaccount/namespace";

        try
        {
            return File.Exists(namespaceFile)
                ? File.ReadAllText(namespaceFile).Trim()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsHelp(string arg) => arg is "-h" or "--help" or "help";

    private static void PrintUsage(IConsole console)
    {
        console.Error("""
        This tool waits until a Kubernetes job, pod or service enters ready state.

        k8s-init-wait-for job [<job name> | -l<selector>] [--namespace <namespace>]
        k8s-init-wait-for pod [<pod name> | -l<selector>] [--namespace <namespace>]
        k8s-init-wait-for service [<service name> | -l<selector>] [--namespace <namespace>]

        Compatibility aliases:
          pod-we   Wait for pods to become Ready or terminate with Error
          pod-wr   Wait until at least one selected pod is Ready
          job-we   Wait for jobs to complete successfully or fail
          job-wr   Wait until at least one selected job pod succeeds

        Environment:
          WAIT_TIME  Poll interval in seconds. Defaults to 2.
          DEBUG      Set to 1 or 2 for extra status output.
        """);
    }
}
