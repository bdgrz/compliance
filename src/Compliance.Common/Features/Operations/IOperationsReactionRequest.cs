using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>A request only Portia's trusted system actor may dispatch from an operations reactor.</summary>
public interface IOperationsReactionRequest : IRequestBase;
