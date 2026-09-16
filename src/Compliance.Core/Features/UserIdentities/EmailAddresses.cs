using System.Net.Mail;
using System.Text;

namespace Bdgrz.Compliance.Features.UserIdentities;

static class EmailAddresses
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > 254)
        {
            return false;
        }

        try
        {
            var parsed = new MailAddress(normalized);
            return string.Equals(parsed.Address, normalized, StringComparison.Ordinal) &&
                   normalized.Contains('@', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
