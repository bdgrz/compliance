using System.Globalization;
using System.Text;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

static class CriteriaPageCursor
{
    public static string Write(ListCriteriaCatalogEntries request, int nextIndex)
    {
        var content = string.Join('\n', request.TenantId.ToString(), request.EditionId.ToString(),
            request.Category ?? "", request.Kind ?? "", request.ParentIdentifier ?? "",
            request.Purpose ?? CriteriaTextOverlayPurpose.Display,
            nextIndex.ToString(CultureInfo.InvariantCulture));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(content))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryRead(ListCriteriaCatalogEntries request, out int index)
    {
        index = 0;
        try
        {
            var cursor = request.Cursor!;
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split('\n');
            var common = parts.Length >= 6 &&
                         parts[0] == request.TenantId.ToString() &&
                         parts[1] == request.EditionId.ToString() &&
                         parts[2] == (request.Category ?? "") &&
                         parts[3] == (request.Kind ?? "") &&
                         parts[4] == (request.ParentIdentifier ?? "");
            var legacyDisplayCursor = parts.Length == 6 &&
                                      (request.Purpose ?? CriteriaTextOverlayPurpose.Display) ==
                                      CriteriaTextOverlayPurpose.Display;
            var purposeBoundCursor = parts.Length == 7 &&
                                     parts[5] == (request.Purpose ?? CriteriaTextOverlayPurpose.Display);
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
}
