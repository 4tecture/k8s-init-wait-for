using k8s;
using k8s.Models;

namespace K8sInitWaitFor;

public interface IKubernetesResourceClient
{
    Task<IReadOnlyList<V1Pod>> GetPodsAsync(ResourceQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<V1Service>> GetServicesAsync(ResourceQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<V1Job>> GetJobsAsync(ResourceQuery query, CancellationToken cancellationToken);
}

public sealed class KubernetesResourceClient(IKubernetes kubernetes, string namespaceName) : IKubernetesResourceClient
{
    public async Task<IReadOnlyList<V1Pod>> GetPodsAsync(ResourceQuery query, CancellationToken cancellationToken)
    {
        if (query is NamedResourceQuery named)
        {
            try
            {
                return [await kubernetes.CoreV1.ReadNamespacedPodAsync(named.Name, namespaceName, cancellationToken: cancellationToken).ConfigureAwait(false)];
            }
            catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return [];
            }
        }

        var selector = ((LabelSelectorQuery)query).Selector;
        var list = await kubernetes.CoreV1.ListNamespacedPodAsync(namespaceName, labelSelector: selector, cancellationToken: cancellationToken).ConfigureAwait(false);
        return list.Items.ToArray();
    }

    public async Task<IReadOnlyList<V1Service>> GetServicesAsync(ResourceQuery query, CancellationToken cancellationToken)
    {
        if (query is NamedResourceQuery named)
        {
            try
            {
                return [await kubernetes.CoreV1.ReadNamespacedServiceAsync(named.Name, namespaceName, cancellationToken: cancellationToken).ConfigureAwait(false)];
            }
            catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return [];
            }
        }

        var selector = ((LabelSelectorQuery)query).Selector;
        var list = await kubernetes.CoreV1.ListNamespacedServiceAsync(namespaceName, labelSelector: selector, cancellationToken: cancellationToken).ConfigureAwait(false);
        return list.Items.ToArray();
    }

    public async Task<IReadOnlyList<V1Job>> GetJobsAsync(ResourceQuery query, CancellationToken cancellationToken)
    {
        if (query is NamedResourceQuery named)
        {
            try
            {
                return [await kubernetes.BatchV1.ReadNamespacedJobAsync(named.Name, namespaceName, cancellationToken: cancellationToken).ConfigureAwait(false)];
            }
            catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return [];
            }
        }

        var selector = ((LabelSelectorQuery)query).Selector;
        var list = await kubernetes.BatchV1.ListNamespacedJobAsync(namespaceName, labelSelector: selector, cancellationToken: cancellationToken).ConfigureAwait(false);
        return list.Items.ToArray();
    }
}
