#!/usr/bin/env bash
# Prints the dotnet test filter for one shard of the broker integration suite.
# Usage: scripts/broker-test-filter.sh <shard-index> <shard-count>
#
# Each broker test class owns its own compose stack (BrokerStackFixture), so classes can run on
# separate CI runners. Classes are assigned round-robin in name order so a shard's membership only
# changes when classes are added or removed.
set -euo pipefail

index=${1:?shard index (0-based)}
count=${2:?shard count}
root=$(cd "$(dirname "$0")/.." && pwd)

classes=()
while IFS= read -r line; do classes+=("$line"); done < <(
  grep -rl --include='*.cs' 'Trait("Category", "BrokerIntegration")' "$root/test" |
    xargs -n1 awk '
      /^namespace / { ns = $2; sub(/;$/, "", ns) }
      /^(public |internal )?(sealed |static |abstract )*(partial )?class / {
        for (i = 1; i <= NF; i++) if ($i == "class") { name = $(i + 1); break }
        sub(/[(<:].*/, "", name)
        print ns "." name
        exit
      }' |
    sort -u
)

if [ "${#classes[@]}" -eq 0 ]; then
  echo "No broker test classes found." >&2
  exit 1
fi

filter=""
for i in "${!classes[@]}"; do
  if [ $((i % count)) -eq "$index" ]; then
    clause="FullyQualifiedName~${classes[$i]}."
    filter=${filter:+$filter|}$clause
  fi
done

if [ -z "$filter" ]; then
  echo "Shard $index of $count has no classes." >&2
  exit 1
fi

echo "Category=BrokerIntegration&($filter)"
