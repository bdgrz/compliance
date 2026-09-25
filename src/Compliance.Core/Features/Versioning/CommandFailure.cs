namespace Bdgrz.Compliance.Features.Versioning;

public sealed record CommandFailure
{
    public CommandFailureCode Code { get; }
    public string? Message { get; }
    public VersionConflict? Version { get; }

    CommandFailure(CommandFailureCode code, string? message = null,
        VersionConflict? version = null)
    {
        Code = code;
        Message = message;
        Version = version;
    }

    public static CommandFailure MissingRecord(string message) =>
        WithMessage(CommandFailureCode.MissingRecord, message);

    public static CommandFailure StateConflict(string message) =>
        WithMessage(CommandFailureCode.StateConflict, message);

    public static CommandFailure InvalidContent(string message) =>
        WithMessage(CommandFailureCode.InvalidContent, message);

    public static CommandFailure ActorProhibited(string message) =>
        WithMessage(CommandFailureCode.ActorProhibited, message);

    public static CommandFailure ForVersion(VersionConflict conflict) =>
        new(CommandFailureCode.VersionConflict,
            version: conflict ?? throw new ArgumentNullException(nameof(conflict)));

    static CommandFailure WithMessage(CommandFailureCode code, string message) =>
        string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("A command failure needs a message.", nameof(message))
            : new CommandFailure(code, message);
}
