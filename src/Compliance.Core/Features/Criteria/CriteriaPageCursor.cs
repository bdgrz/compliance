using System.Globalization;
using System.Text;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

static class CriteriaPageCursor
{
    public static string Write(Uuid tenantId, Uuid editionId, string? category, string? kind,
        string? parentIdentifier, CriteriaTextOverlayPurpose purpose, int nextIndex)
    {
        var content = string.Join('\n', tenantId.ToString(), editionId.ToString(),
            category ?? "", kind ?? "", parentIdentifier ?? "", PurposeName(purpose),
            nextIndex.ToString(CultureInfo.InvariantCulture));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(content))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryRead(Uuid tenantId, Uuid editionId, string? category, string? kind,
        string? parentIdentifier, string? cursor, CriteriaTextOverlayPurpose purpose, out int index)
    {
        index = 0;
        try
        {
            var base64 = cursor!.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split('\n');
            var common = parts.Length >= 6 &&
                         parts[0] == tenantId.ToString() &&
                         parts[1] == editionId.ToString() &&
                         parts[2] == (category ?? "") &&
                         parts[3] == (kind ?? "") &&
                         parts[4] == (parentIdentifier ?? "");
            var legacyDisplayCursor = parts.Length == 6 &&
                                      purpose == CriteriaTextOverlayPurpose.Display;
            var purposeBoundCursor = parts.Length == 7 &&
                                     parts[5] == PurposeName(purpose);
            var indexPart = purposeBoundCursor ? parts[6] : legacyDisplayCursor ? parts[5] : null;
            return common && indexPart is not null &&
                   int.TryParse(indexPart, NumberStyles.None, CultureInfo.InvariantCulture,
                       out index) && index >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    static string PurposeName(CriteriaTextOverlayPurpose purpose) =>
        purpose == CriteriaTextOverlayPurpose.Export ? "export" : "display";
}
