using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Derives the stable identity of a control's exact version line.</summary>
public static class ControlVersionIds
{
    /// <summary>The version identity shared by the initial draft and its approved version.</summary>
    public static Uuid Initial(Uuid controlId) =>
        Uuid.CreateVersion5(controlId, "control-version-1");
}
