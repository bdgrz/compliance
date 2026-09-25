namespace Bdgrz.Compliance.Tests.E2E;

public sealed record PortableBrokerBackupRestore(string SourceProject, string RestoredProject,
    string SourceVolume, string RestoredVolume, string BrokerImage,
    string RestoredBrokerImage, string EventReaderVersion, string ArchiveSha256);
