namespace Identity.Application.Common.Security;

public static class EmailMasker
{
    public static string Mask(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        string trimmed = email.Trim();
        int atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0 || atIndex >= trimmed.Length - 1)
        {
            return "***";
        }

        string localPart = trimmed[..atIndex];
        string domainPart = trimmed[(atIndex + 1)..];

        string visibleLocal = localPart.Length >= 3 ? localPart[..2] : localPart[..1];

        int lastDot = domainPart.LastIndexOf('.');
        string maskedDomain = lastDot > 0 && lastDot < domainPart.Length - 1
            ? $"{domainPart[..1]}***{domainPart[lastDot..]}"
            : "***";

        return $"{visibleLocal}***@{maskedDomain}";
    }
}
