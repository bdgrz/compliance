#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

case "${1:-}" in
  focused)
    if [[ $# -ne 2 || -z "$2" ]]; then
      echo "Usage: $0 focused '<dotnet test filter>'" >&2
      exit 2
    fi
    dotnet test Compliance.slnx --configuration Release --no-restore \
      -p:EnableAotAnalyzer=true \
      --filter "$2"
    ;;
  full)
    if [[ $# -ne 1 ]]; then
      echo "Usage: $0 full" >&2
      exit 2
    fi
    npm ci
    npm run client:check
    dotnet restore Compliance.slnx --locked-mode
    dotnet format Compliance.slnx --verify-no-changes --no-restore
    dotnet build Compliance.slnx --configuration Release --no-restore
    dotnet test Compliance.slnx --configuration Release --no-build --no-restore \
      --filter 'Category!=BrokerIntegration'
    dotnet test Compliance.slnx --configuration Release --no-build --no-restore \
      --filter 'Category=BrokerIntegration'
    ;;
  *)
    echo "Usage: $0 focused '<dotnet test filter>' | full" >&2
    exit 2
    ;;
esac
