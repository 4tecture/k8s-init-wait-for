using K8sInitWaitFor;
using k8s.Models;

namespace K8sInitWaitFor.Tests;

public sealed class ResourceWaiterTests
{
    [Fact]
    public void PodRequireSuccessNeedsAllPodsReady()
    {
        var status = ResourceWaiter.EvaluatePods(
            [Pod("ready", ready: true), Pod("waiting", ready: false)],
            FailureMode.RequireSuccess);

        Assert.False(status.IsReady);
    }

    [Fact]
    public void PodTreatErrorsAsReadyAllowsTerminatedError()
    {
        var status = ResourceWaiter.EvaluatePods(
            [Pod("ready", ready: true), Pod("failed", ready: false, error: true)],
            FailureMode.TreatErrorsAsReady);

        Assert.True(status.IsReady);
    }

    [Fact]
    public void PodRequireAnySuccessAllowsOneReadyPod()
    {
        var status = ResourceWaiter.EvaluatePods(
            [Pod("ready", ready: true), Pod("failed", ready: false, error: true)],
            FailureMode.RequireAnySuccess);

        Assert.True(status.IsReady);
    }

    [Fact]
    public void JobRequireSuccessNeedsCompletedJobWithoutFailures()
    {
        var status = ResourceWaiter.EvaluateJob(Job("db", active: 0, succeeded: 1, failed: 0, complete: true), FailureMode.RequireSuccess);

        Assert.True(status.IsReady);
    }

    [Fact]
    public void JobRequireSuccessDoesNotAllowFailedPods()
    {
        var status = ResourceWaiter.EvaluateJob(Job("db", active: 0, succeeded: 1, failed: 1, complete: true), FailureMode.RequireSuccess);

        Assert.False(status.IsReady);
    }

    [Fact]
    public void JobTreatErrorsAsReadyAllowsFailedJob()
    {
        var status = ResourceWaiter.EvaluateJob(Job("db", active: 0, succeeded: 0, failed: 1, failedCondition: true), FailureMode.TreatErrorsAsReady);

        Assert.True(status.IsReady);
    }

    [Fact]
    public void JobRequireAnySuccessAllowsSucceededJobWithFailures()
    {
        var status = ResourceWaiter.EvaluateJob(Job("db", active: 0, succeeded: 1, failed: 1), FailureMode.RequireAnySuccess);

        Assert.True(status.IsReady);
    }

    [Fact]
    public async Task ServiceWaitsForPodsSelectedByEveryService()
    {
        var client = new FakeKubernetesResourceClient
        {
            Services =
            [
                new V1Service
                {
                    Metadata = new V1ObjectMeta { Name = "api" },
                    Spec = new V1ServiceSpec { Selector = new Dictionary<string, string> { ["app"] = "api" } }
                }
            ],
            PodsBySelector =
            {
                ["app=api"] = [Pod("api-1", ready: true)]
            }
        };
        var waiter = new ResourceWaiter(client, new CapturingConsole());

        var status = await waiter.GetServiceStatusAsync(new NamedResourceQuery("api"), CancellationToken.None);

        Assert.True(status.IsReady);
    }

    private static V1Pod Pod(string name, bool ready, bool error = false)
    {
        return new V1Pod
        {
            Metadata = new V1ObjectMeta { Name = name },
            Status = new V1PodStatus
            {
                Conditions =
                [
                    new V1PodCondition { Type = "Ready", Status = ready ? "True" : "False" }
                ],
                ContainerStatuses =
                [
                    new V1ContainerStatus
                    {
                        Name = "app",
                        Image = "example",
                        ImageID = "example",
                        Ready = ready,
                        RestartCount = 0,
                        State = error
                            ? new V1ContainerState { Terminated = new V1ContainerStateTerminated { Reason = "Error" } }
                            : new V1ContainerState()
                    }
                ]
            }
        };
    }

    private static V1Job Job(string name, int active, int succeeded, int failed, bool complete = false, bool failedCondition = false)
    {
        var conditions = new List<V1JobCondition>();
        if (complete)
        {
            conditions.Add(new V1JobCondition { Type = "Complete", Status = "True" });
        }

        if (failedCondition)
        {
            conditions.Add(new V1JobCondition { Type = "Failed", Status = "True" });
        }

        return new V1Job
        {
            Metadata = new V1ObjectMeta { Name = name },
            Status = new V1JobStatus
            {
                Active = active,
                Succeeded = succeeded,
                Failed = failed,
                Conditions = conditions
            }
        };
    }
}
