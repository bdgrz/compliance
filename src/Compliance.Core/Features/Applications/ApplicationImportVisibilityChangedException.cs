namespace Bdgrz.Compliance.Features.Applications;

sealed class ApplicationImportVisibilityChangedException()
    : Exception("Application import visibility changed; restart paging.");
