using K8sInitWaitFor;
using k8s;

var console = new SystemConsole();

try
{
    if (!WaitOptionsParser.TryParse(args, console, out var options))
    {
        return IsHelp(args) ? 0 : 1;
    }

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };

    var configuration = KubernetesClientConfiguration.IsInCluster()
        ? KubernetesClientConfiguration.InClusterConfig()
        : KubernetesClientConfiguration.BuildConfigFromConfigFile();

    using var kubernetes = new Kubernetes(configuration);
    var client = new KubernetesResourceClient(kubernetes, options.Namespace);
    var waiter = new ResourceWaiter(client, console);

    await waiter.WaitAsync(options, cancellation.Token).ConfigureAwait(false);
    return 0;
}
catch (OperationCanceledException)
{
    console.Error("Wait cancelled.");
    return 130;
}
catch (Exception ex)
{
    console.Error(optionsDebugEnabled(args) ? ex.ToString() : ex.Message);
    return 1;
}

static bool optionsDebugEnabled(string[] args)
{
    var value = Environment.GetEnvironmentVariable("DEBUG");
    return int.TryParse(value, out var debug) && debug > 0 || args.Contains("--debug");
}

static bool IsHelp(string[] args) => args.Length > 0 && args[0] is "-h" or "--help" or "help";
