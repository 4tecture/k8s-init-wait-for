# syntax=docker/dockerfile:1

# Native AOT needs a linker for the target architecture. Buildx runs each
# platform build on its target SDK image, avoiding cross-compiler setup.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble-aot AS build
ARG TARGETARCH
WORKDIR /src

COPY global.json ./
COPY K8sInitWaitFor.slnx ./
COPY src/K8sInitWaitFor/K8sInitWaitFor.csproj src/K8sInitWaitFor/
RUN dotnet restore src/K8sInitWaitFor/K8sInitWaitFor.csproj -a $TARGETARCH

COPY src/K8sInitWaitFor/ src/K8sInitWaitFor/
RUN dotnet publish src/K8sInitWaitFor/K8sInitWaitFor.csproj \
    -c Release \
    -a $TARGETARCH \
    --no-restore \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
ARG TARGETARCH
ARG VERSION=dev
ARG REVISION=unknown

LABEL org.opencontainers.image.title="k8s-init-wait-for" \
      org.opencontainers.image.description="Rootless Kubernetes init-container helper for waiting on pods, services, and jobs." \
      org.opencontainers.image.source="https://github.com/4tecture/k8s-init-wait-for" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.version="$VERSION" \
      org.opencontainers.image.revision="$REVISION"

WORKDIR /app
COPY --from=build /app/publish/k8s-init-wait-for /app/k8s-init-wait-for

USER $APP_UID
ENTRYPOINT ["/app/k8s-init-wait-for"]
