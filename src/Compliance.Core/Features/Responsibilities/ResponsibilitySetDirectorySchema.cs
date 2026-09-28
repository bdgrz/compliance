using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

static class ResponsibilitySetDirectorySchema
{
    public static readonly KvDirectory<ResponsibilitySetView, Uuid> Sets = new(
        "responsibility_sets", ComplianceCoreJsonContext.Default.ResponsibilitySetView,
        static set => set.SetId, static id => [id.ToString()], []);
}
