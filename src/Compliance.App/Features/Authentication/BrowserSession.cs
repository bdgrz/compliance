namespace Bdgrz.Compliance.Features.Authentication;

sealed record BrowserSession(string Id, string? EmailAddress, bool EmailAddressVerified);
