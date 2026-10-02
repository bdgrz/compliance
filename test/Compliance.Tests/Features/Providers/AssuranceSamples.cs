using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Providers;

/// <summary>Valid authored assurance inputs; tests vary one fact at a time.</summary>
static class AssuranceSamples
{
    public static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    public static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder");

    public static ProviderSourceCitation Citation(string classification = "internal", Uuid? artifactId = null) =>
        new("soc2_report", "Annual SOC 2 Type 2", "2026-06-30", "report.pdf page 4", classification, artifactId);

    public static AssuranceReportContent Report(string classification = "internal") => new("soc2_type2", "Example Assurance LLP",
        "Hosting platform and support services", new DateOnly(2026, 6, 30), new DateOnly(2025, 7, 1), "unqualified",
        "Independent service auditor's report, section I", ["Hosting", "Support"], [], ["Customer reviews access quarterly"], [],
        null, Citation(classification));

    public static ProviderReviewContent Review(Uuid? reportId = null, string conclusion = "acceptable") => new(
        new DateOnly(2026, 9, 1), new DateOnly(2027, 9, 1), "soc2_type2", conclusion, "Report reviewed against our controls.",
        reportId, conclusion == "acceptable_with_exceptions" ? ["One exception on patch timeliness"] : [],
        reportId is null ? Citation() : null);

    public static ProviderContent MaterialProvider(string name = "Example Provider") => new(name, "Supplier",
        "material", ["customer_data"], "Processes customer data");
}
