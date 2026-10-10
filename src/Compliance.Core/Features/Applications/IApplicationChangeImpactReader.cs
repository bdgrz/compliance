using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationChangeImpactReader
{
    ValueTask<Result<ApplicationChangePreview>> ReadAsync(PreviewApplicationChange request,
        Uuid userId, CancellationToken ct);
}
