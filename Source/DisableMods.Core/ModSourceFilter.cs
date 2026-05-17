using System.Collections.Generic;

namespace DisableMods.Core;

public static class ModSourceFilter
{
    public static bool Allows(ModSourceIdentity identity, ModSourceFilterPolicy policy)
    {
        if (identity.Source == ModSourceKind.Official)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(identity.PackageId) &&
            ModKeyMatcher.Matches(policy.AlwaysAllowedPackageIds, identity.PackageId))
        {
            return true;
        }

        return identity.Source switch
        {
            ModSourceKind.Local => policy.AllowLocalMods,
            ModSourceKind.Steam => policy.AllowSteamMods,
            _ => true
        };
    }
}

public readonly struct ModSourceFilterPolicy
{
    public ModSourceFilterPolicy(
        bool allowLocalMods,
        bool allowSteamMods,
        IEnumerable<string>? alwaysAllowedPackageIds = null)
    {
        AllowLocalMods = allowLocalMods;
        AllowSteamMods = allowSteamMods;
        AlwaysAllowedPackageIds = alwaysAllowedPackageIds ?? System.Array.Empty<string>();
    }

    public bool AllowLocalMods { get; }

    public bool AllowSteamMods { get; }

    public IEnumerable<string> AlwaysAllowedPackageIds { get; }
}

public readonly struct ModSourceIdentity
{
    public ModSourceIdentity(string packageId, ModSourceKind source)
    {
        PackageId = ModKeyMatcher.Normalize(packageId);
        Source = source;
    }

    public string PackageId { get; }

    public ModSourceKind Source { get; }
}

public enum ModSourceKind
{
    Unknown,
    Official,
    Local,
    Steam
}
