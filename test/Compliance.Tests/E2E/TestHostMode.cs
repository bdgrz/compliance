namespace Bdgrz.Compliance.Tests.E2E;

/// <summary>
///     The host mode a test's next <c>CreateClient()</c> starts in. It flows with the test's async
///     context instead of the process environment, so broker tests can run in parallel.
/// </summary>
static class TestHostMode
{
    static readonly AsyncLocal<string?> Mode = new();

    public static string? Current => Mode.Value;

    public static void Set(string? mode) => Mode.Value = mode;
}
