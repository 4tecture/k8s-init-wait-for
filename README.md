# k8s-init-wait-for

Rootless Kubernetes init-container helper for waiting until jobs, pods, or services are ready.

This is a .NET 10 Native AOT executable using the official Kubernetes C# client. The container image is built from Microsoft's chiseled AOT runtime-deps image and runs as the image's non-root application user.

## Image

```text
ghcr.io/4tecture/k8s-init-wait-for:latest
```

Released tags are also published as `ghcr.io/4tecture/k8s-init-wait-for:<version>`.

## Usage

```bash
k8s-init-wait-for job <job-name>
k8s-init-wait-for pod <pod-name>
k8s-init-wait-for service <service-name>
k8s-init-wait-for pod -lapp=proauth
k8s-init-wait-for pod --selector=app=proauth --namespace proauth
```

Compatibility command names from `groundnuty/k8s-wait-for` are supported:

```bash
k8s-init-wait-for pod-we -lapp=proauth
k8s-init-wait-for pod-wr -lapp=proauth
k8s-init-wait-for job-we db-migration
k8s-init-wait-for job-wr db-migration
```

The default polling interval is 2 seconds. Override it with:

```bash
WAIT_TIME=5
```

Set `DEBUG=1` or `DEBUG=2` for additional status output.

## Kubernetes permissions

The container needs read-only access to the resources it waits on. For the full feature set, grant namespaced `get`, `list`, and `watch` permissions for:

- `pods`
- `services`
- `jobs.batch`

## Building locally

```bash
dotnet test tests/K8sInitWaitFor.Tests/K8sInitWaitFor.Tests.csproj
docker buildx build --load --platform linux/arm64 -t k8s-init-wait-for:local .
```

## License

MIT
