#!/usr/bin/env bash
# Prints the dotnet test filter for one shard of the broker integration suite.
# Usage: scripts/broker-test-filter.sh <shard-index> <shard-count>
#
# Classes are balanced across shards by their observed duration
# (test/Compliance.Tests/E2E/broker-test-seconds.txt): longest first, each to the currently
# lightest shard. Classes without an observed duration count as 90 seconds.
set -euo pipefail

index=${1:?shard index (0-based)}
count=${2:?shard count}
root=$(cd "$(dirname "$0")/.." && pwd)

python3 - "$root" "$index" "$count" <<'PY'
import pathlib
import re
import sys

root, index, count = pathlib.Path(sys.argv[1]), int(sys.argv[2]), int(sys.argv[3])

classes = set()
for path in (root / "test").rglob("*.cs"):
    text = path.read_text(encoding="utf-8")
    if 'Trait("Category", "BrokerIntegration")' not in text:
        continue
    namespace = re.search(r"^namespace ([\w.]+);", text, re.M)
    declared = re.search(r"^(?:public |internal )?(?:sealed |static |abstract )*(?:partial )?class (\w+)", text, re.M)
    if namespace and declared:
        classes.add(f"{namespace.group(1)}.{declared.group(1)}")
if not classes:
    sys.exit("No broker test classes found.")

observed = {}
durations = root / "test/Compliance.Tests/E2E/broker-test-seconds.txt"
for line in durations.read_text(encoding="utf-8").splitlines():
    if line.strip() and not line.startswith("#"):
        name, seconds = line.split()
        observed[name] = int(seconds)

loads = [0] * count
shards = [[] for _ in range(count)]
for name in sorted(classes, key=lambda c: (-observed.get(c.rsplit(".", 1)[1], 90), c)):
    target = loads.index(min(loads))
    shards[target].append(name)
    loads[target] += observed.get(name.rsplit(".", 1)[1], 90)

mine = shards[index]
if not mine:
    sys.exit(f"Shard {index} of {count} has no classes.")
print(f"Shard {index}: ~{loads[index]}s", file=sys.stderr)
print("Category=BrokerIntegration&(" + "|".join(f"FullyQualifiedName~{c}." for c in mine) + ")")
PY
