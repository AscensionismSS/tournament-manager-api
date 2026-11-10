namespace Tournaments.Application.Common.Utilities;

public static class SteamIdConverter
{
    private const ulong Steam64Base = 76561197960265728UL;

    public static long? ToAccountId(string? steamIdOrExternalUserId)
    {
        if (string.IsNullOrWhiteSpace(steamIdOrExternalUserId))
            return null;

        if (ulong.TryParse(steamIdOrExternalUserId.Trim(), out var parsed))
        {
            if (parsed > Steam64Base)
            {
                return (long)(parsed - Steam64Base);
            }

            return (long)parsed;
        }

        return null;
    }

    public static bool Matches(string? externalUserId, long steamId64Or32)
    {
        if (string.IsNullOrWhiteSpace(externalUserId))
            return false;

        var accId1 = ToAccountId(externalUserId);
        var accId2 = ToAccountId(steamId64Or32.ToString());

        return accId1.HasValue && accId2.HasValue && accId1.Value == accId2.Value;
    }
}
