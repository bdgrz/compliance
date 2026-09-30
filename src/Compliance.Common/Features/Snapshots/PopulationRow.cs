using System.Text.Json;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed record PopulationRow(string Key, JsonElement Content);
