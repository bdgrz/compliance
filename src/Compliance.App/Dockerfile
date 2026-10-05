FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble-aot@sha256:88446bc2708f037046d48ef2bd77c437e981ca01229d7005ac2016ec42e05347 AS build

WORKDIR /source

ARG TARGETARCH

COPY .editorconfig Directory.Build.props Directory.Packages.props NuGet.Config global.json ./
COPY src/Directory.Build.props src/
COPY src/Compliance.Common/Compliance.Common.csproj src/Compliance.Common/packages.lock.json src/Compliance.Common/
COPY src/Compliance.Core/Compliance.Core.csproj src/Compliance.Core/packages.lock.json src/Compliance.Core/
COPY src/Compliance.App/Compliance.App.csproj src/Compliance.App/packages.lock.json src/Compliance.App/

ARG GITHUB_ACTOR
RUN --mount=type=secret,id=github_token,required=true \
    GITHUB_ACTOR="$GITHUB_ACTOR" \
    GITHUB_TOKEN="$(cat /run/secrets/github_token)" \
    sh -c 'case "$TARGETARCH" in \
      amd64) compliance_rid=linux-x64 ;; \
      arm64) compliance_rid=linux-arm64 ;; \
      *) echo "Unsupported target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac; \
    dotnet restore src/Compliance.App/Compliance.App.csproj \
      --runtime "$compliance_rid" \
      --force-evaluate \
      /p:PublishAot=true \
      /p:RestorePackagesWithLockFile=true'

COPY src/ src/

RUN case "$TARGETARCH" in \
      amd64) compliance_rid=linux-x64 ;; \
      arm64) compliance_rid=linux-arm64 ;; \
      *) echo "Unsupported target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac; \
    dotnet publish src/Compliance.App/Compliance.App.csproj \
      --configuration Release \
      --no-restore \
      --runtime "$compliance_rid" \
      --self-contained true \
      --output /app/publish \
      /p:PublishAot=true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.12-noble-chiseled-extra@sha256:566a1d5613279cf96a70bc456b85a5e1dda0cc53f394209680d54b55e4d13cf7 AS runtime

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish ./

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["./Bdgrz.Compliance.App"]
