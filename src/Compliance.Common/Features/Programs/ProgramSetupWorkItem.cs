using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed record ProgramSetupWorkItem(string Code, string Detail,
    string SourceType, Uuid? SourceId);
