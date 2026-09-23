namespace Bdgrz.Compliance.Features.Snapshots;

sealed class SnapshotContentException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
