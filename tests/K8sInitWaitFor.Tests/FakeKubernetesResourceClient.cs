using K8sInitWaitFor;
using k8s.Models;

namespace K8sInitWaitFor.Tests;

internal sealed class FakeKubernetesResourceClient : IKubernetesResourceClient
{
    public IReadOnlyList<V1Service> Services { get; init; } = [];

    public Dictionary<string, IReadOnlyList<V1Pod>> PodsBySelector { get; } = [];

    public Task<IReadOnlyList<V1Pod>> GetPodsAsync(ResourceQuery query, CancellationToken cancellationToken)
    {
        var key = ((LabelSelectorQuery)query).Selector;
        return Task.FromResult(PodsBySelector.GetValueOrDefault(key, []));
    }

    public Task<IReadOnlyList<V1Service>> GetServicesAsync(ResourceQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(Services);
    }

    public Task<IReadOnlyList<V1Job>> GetJobsAsync(ResourceQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<V1Job>>([]);
    }
}
