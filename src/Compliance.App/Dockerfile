FROM node:26.8-slim@sha256:14bf3eac4bf209d906d3c41256597d3ab1f926b2e93a79e9bdfe1efd32454239 AS client-build

WORKDIR /source

COPY package.json package-lock.json ./
COPY src/Compliance.App/ClientApp/package.json src/Compliance.App/ClientApp/
RUN --mount=type=cache,target=/root/.npm npm ci

COPY src/Compliance.App/ClientApp/ src/Compliance.App/ClientApp/
RUN npm run client:build

FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble-aot@sha256:1a069a730888d278b5ac00e3e238707387d20d2b65f0fc6866b6180df0a767e0 AS build

WORKDIR /source

ARG TARGETARCH

COPY .editorconfig Directory.Build.props Directory.Packages.props NuGet.Config global.json ./
COPY src/Directory.Build.props src/
COPY src/Compliance.Common/Compliance.Common.csproj src/Compliance.Common/packages.lock.json src/Compliance.Common/
COPY src/Compliance.Core/Compliance.Core.csproj src/Compliance.Core/packages.lock.json src/Compliance.Core/
COPY src/Compliance.App/Compliance.App.csproj src/Compliance.App/packages.lock.json src/Compliance.App/
COPY src/Compliance.App/ClientApp/Compliance.App.Client.esproj \
    src/Compliance.App/ClientApp/package.json \
    src/Compliance.App/ClientApp/

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
COPY --from=client-build /source/src/Compliance.App/ClientApp/dist \
    src/Compliance.App/ClientApp/dist

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
      /p:PublishAot=true \
      /p:ShouldRunNpmInstall=false \
      /p:BuildCommand=true \
      /p:PublishCommand=true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.12-noble-chiseled-extra@sha256:9a3e4e315a3eae3be20b73739ae84fa7e4ffb2a47a234f3bef30825f0543fe1b AS runtime

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish ./

USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["./Bdgrz.Compliance.App"]
