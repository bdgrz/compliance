namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A service record and its classification under one exact independence rule-set version.</summary>
public sealed record IndependenceServiceAssessment(NonattestServiceRecord Service,
    IndependenceServiceClassification? Classification);
