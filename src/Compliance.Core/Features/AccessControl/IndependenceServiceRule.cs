namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record IndependenceServiceRule(string ServiceType,
    IndependenceServiceClassification Classification,
    IndependenceServiceClassification ManagementFunctionsClassification);
