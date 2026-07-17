using k8s.Models;

namespace K8sInitWaitFor;

public sealed class ResourceWaiter(IKubernetesResourceClient client, IConsole console)
{
    public async Task WaitAsync(WaitOptions options, CancellationToken cancellationToken)
    {
        while (true)
        {
            var status = options.ResourceKind switch
            {
                ResourceKind.Pod => await GetPodStatusAsync(options.Query, options.FailureMode, cancellationToken).ConfigureAwait(false),
                ResourceKind.Service => await GetServiceStatusAsync(options.Query, cancellationToken).ConfigureAwait(false),
                ResourceKind.Job => await GetJobStatusAsync(options.Query, options.FailureMode, cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unknown resource kind: {options.ResourceKind}")
            };

            if (options.DebugLevel > 0)
            {
                console.Debug(status.Detail);
            }

            if (status.IsReady)
            {
                console.Info($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {options.ResourceKind.ToString().ToLowerInvariant()} {options.Query.DisplayValue} is ready.");
                return;
            }

            console.Info($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] Waiting for {options.ResourceKind.ToString().ToLowerInvariant()} {options.Query.DisplayValue}...");
            await Task.Delay(options.PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<WaitStatus> GetPodStatusAsync(ResourceQuery query, FailureMode failureMode, CancellationToken cancellationToken)
    {
        var pods = await client.GetPodsAsync(query, cancellationToken).ConfigureAwait(false);
        return EvaluatePods(pods, failureMode);
    }

    public async Task<WaitStatus> GetServiceStatusAsync(ResourceQuery query, CancellationToken cancellationToken)
    {
        var services = await client.GetServicesAsync(query, cancellationToken).ConfigureAwait(false);
        if (services.Count == 0)
        {
            return WaitStatus.NotReady("No services found.");
        }

        var serviceStatuses = new List<WaitStatus>();
        foreach (var service in services)
        {
            var selector = service.Spec?.Selector;
            if (selector is null || selector.Count == 0)
            {
                serviceStatuses.Add(WaitStatus.NotReady($"Service {service.Metadata?.Name} has no selector."));
                continue;
            }

            var selectorQuery = new LabelSelectorQuery(string.Join(",", selector.OrderBy(static item => item.Key).Select(static item => $"{item.Key}={item.Value}")));
            var pods = await client.GetPodsAsync(selectorQuery, cancellationToken).ConfigureAwait(false);
            var podStatus = EvaluatePods(pods, FailureMode.RequireSuccess);
            serviceStatuses.Add(podStatus with { Detail = $"Service {service.Metadata?.Name}: {podStatus.Detail}" });
        }

        return serviceStatuses.All(static status => status.IsReady)
            ? WaitStatus.Ready(string.Join(Environment.NewLine, serviceStatuses.Select(static status => status.Detail)))
            : WaitStatus.NotReady(string.Join(Environment.NewLine, serviceStatuses.Select(static status => status.Detail)));
    }

    public async Task<WaitStatus> GetJobStatusAsync(ResourceQuery query, FailureMode failureMode, CancellationToken cancellationToken)
    {
        var jobs = await client.GetJobsAsync(query, cancellationToken).ConfigureAwait(false);
        if (jobs.Count == 0)
        {
            return WaitStatus.NotReady("No jobs found.");
        }

        var statuses = jobs.Select(job => EvaluateJob(job, failureMode)).ToArray();
        return statuses.All(static status => status.IsReady)
            ? WaitStatus.Ready(string.Join(Environment.NewLine, statuses.Select(static status => status.Detail)))
            : WaitStatus.NotReady(string.Join(Environment.NewLine, statuses.Select(static status => status.Detail)));
    }

    public static WaitStatus EvaluatePods(IReadOnlyList<V1Pod> pods, FailureMode failureMode)
    {
        if (pods.Count == 0)
        {
            return WaitStatus.NotReady("No pods found.");
        }

        var statuses = pods.Select(pod => new PodReadiness(pod.Metadata?.Name ?? "<unknown>", IsPodReady(pod), IsPodError(pod))).ToArray();
        var isReady = failureMode switch
        {
            FailureMode.RequireSuccess => statuses.All(static status => status.IsReady),
            FailureMode.TreatErrorsAsReady => statuses.All(static status => status.IsReady || status.IsError),
            FailureMode.RequireAnySuccess => statuses.Any(static status => status.IsReady),
            _ => false
        };

        return isReady
            ? WaitStatus.Ready(FormatPodDetails(statuses))
            : WaitStatus.NotReady(FormatPodDetails(statuses));
    }

    public static WaitStatus EvaluateJob(V1Job job, FailureMode failureMode)
    {
        var name = job.Metadata?.Name ?? "<unknown>";
        var status = job.Status;
        var active = status?.Active ?? 0;
        var succeeded = status?.Succeeded ?? 0;
        var failed = status?.Failed ?? 0;
        var hasCompleteCondition = HasCondition(status, "Complete", "True");
        var hasFailedCondition = HasCondition(status, "Failed", "True");

        var isReady = failureMode switch
        {
            FailureMode.RequireSuccess => active == 0 && succeeded > 0 && hasCompleteCondition && !hasFailedCondition,
            FailureMode.TreatErrorsAsReady => active == 0 && (succeeded > 0 || failed > 0 || hasCompleteCondition || hasFailedCondition),
            FailureMode.RequireAnySuccess => active == 0 && succeeded > 0,
            _ => false
        };

        var detail = $"Job {name}: active={active}, succeeded={succeeded}, failed={failed}, complete={hasCompleteCondition}, failedCondition={hasFailedCondition}";
        return isReady ? WaitStatus.Ready(detail) : WaitStatus.NotReady(detail);
    }

    private static bool IsPodReady(V1Pod pod)
    {
        return pod.Status?.Conditions?.Any(static condition =>
            string.Equals(condition.Type, "Ready", StringComparison.Ordinal)
            && string.Equals(condition.Status, "True", StringComparison.Ordinal)) == true;
    }

    private static bool IsPodError(V1Pod pod)
    {
        return pod.Status?.ContainerStatuses?.Any(static container =>
            string.Equals(container.State?.Terminated?.Reason, "Error", StringComparison.Ordinal)) == true;
    }

    private static bool HasCondition(V1JobStatus? status, string type, string conditionStatus)
    {
        return status?.Conditions?.Any(condition =>
            string.Equals(condition.Type, type, StringComparison.Ordinal)
            && string.Equals(condition.Status, conditionStatus, StringComparison.Ordinal)) == true;
    }

    private static string FormatPodDetails(IEnumerable<PodReadiness> statuses)
    {
        return string.Join(", ", statuses.Select(static status => $"{status.Name}: ready={status.IsReady}, error={status.IsError}"));
    }

    private sealed record PodReadiness(string Name, bool IsReady, bool IsError);
}

public sealed record WaitStatus(bool IsReady, string Detail)
{
    public static WaitStatus Ready(string detail) => new(true, detail);

    public static WaitStatus NotReady(string detail) => new(false, detail);
}
