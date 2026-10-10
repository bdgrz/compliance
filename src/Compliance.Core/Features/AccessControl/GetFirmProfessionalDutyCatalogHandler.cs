using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetFirmProfessionalDutyCatalogHandler(IAggregateReader reader)
    : IRequestHandler<GetFirmProfessionalDutyCatalog, FirmProfessionalDutyCatalogView>
{
    public async ValueTask<Result<FirmProfessionalDutyCatalogView>> HandleAsync(
        IRequestContext<GetFirmProfessionalDutyCatalog> context, CancellationToken ct)
    {
        var catalog = await reader.HydrateAsync(new FirmProfessionalDutyCatalog(), ct).ConfigureAwait(false);
        return Result<FirmProfessionalDutyCatalogView>.Success(catalog.View());
    }
}
