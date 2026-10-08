using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>A governed inventory mutation, requiring both ordinary grants and the client management independence wall.</summary>
public interface ITechnologyInventoryWriteRequest : ITechnologyInventoryRequest, IClientManagementMutationRequest;
