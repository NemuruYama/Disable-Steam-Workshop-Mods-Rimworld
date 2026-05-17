using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DisableMods.Core;
using Verse;

namespace DisableSteamMods.Configuration;

public sealed class DisableSteamModsSettings : ModSettings
{
    private const string DisableSteamModsPackageId = "nemuruyama.disablesteammods";
    private const string PrepatcherPackageId = "zetrith.prepatcher";
    private const string ModHandleName = "DisableSteamModsMod";
    private static readonly OwnModInfo OwnMod = OwnModFinder.FindOwnMod(
        typeof(DisableSteamModsSettings).Assembly,
        "[DisableSteamMods]"
    );
    private static DateTime cachedLocalPackageIdsWriteTimeUtc;
    private static HashSet<string>? cachedLocalPackageIds;
    private static DisableSteamModsSettings? current;

    public bool WhitelistEnabled = true;
    public bool BlacklistEnabled;
    public bool RemoveOnlyWhenLocalVersionExists;
    public string WhitelistModKeys = DefaultWhitelistModKeys;
    public string BlacklistModKeys = string.Empty;

    private List<string>? whitelist;
    private List<string>? blacklist;

    private static string SelfPackageIdForDefaultWhitelist =>
        string.IsNullOrWhiteSpace(OwnMod.PackageId) ? DisableSteamModsPackageId : OwnMod.PackageId;

    public static string DefaultWhitelistModKeys => string.Join(
        Environment.NewLine,
        new[] { SelfPackageIdForDefaultWhitelist, PrepatcherPackageId }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(ModKeyMatcher.Normalize)
            .Distinct());

    public static DisableSteamModsSettings Current => current ??= ReadSettingsForEarlyFilter();

    public static void SetCurrent(DisableSteamModsSettings settings)
    {
        settings.InvalidateParsedIds();
        current = settings;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref WhitelistEnabled, "whitelistEnabled", true);
        Scribe_Values.Look(ref BlacklistEnabled, "blacklistEnabled", false);
        Scribe_Values.Look(ref RemoveOnlyWhenLocalVersionExists, "removeOnlyWhenLocalVersionExists", false);
        Scribe_Values.Look(ref WhitelistModKeys, "whitelistModKeys", DefaultWhitelistModKeys);
        Scribe_Values.Look(ref BlacklistModKeys, "blacklistModKeys", string.Empty);
        InvalidateParsedIds();
    }

    public bool Allows(WorkshopModIdentity mod)
    {
        if (string.IsNullOrWhiteSpace(mod.PackageId))
        {
            return RemoveOnlyWhenLocalVersionExists;
        }

        var packageId = ModKeyMatcher.Normalize(mod.PackageId);
        if (RemoveOnlyWhenLocalVersionExists && !LocalPackageIds.Contains(packageId))
        {
            return true;
        }

        if (!WhitelistEnabled && !BlacklistEnabled)
        {
            return false;
        }

        if (WhitelistEnabled && !ModKeyMatcher.Matches(Whitelist, packageId))
        {
            return false;
        }

        return !BlacklistEnabled || !ModKeyMatcher.Matches(Blacklist, packageId);
    }

    public void NotifyChanged()
    {
        InvalidateParsedIds();
        SetCurrent(this);
    }

    private List<string> Whitelist => whitelist ??= ParseModKeys(WhitelistModKeys);

    private List<string> Blacklist => blacklist ??= ParseModKeys(BlacklistModKeys);

    private static HashSet<string> LocalPackageIds
    {
        get
        {
            var modsFolderPath = GenFilePaths.ModsFolderPath;
            var writeTimeUtc = Directory.Exists(modsFolderPath)
                ? Directory.GetLastWriteTimeUtc(modsFolderPath)
                : DateTime.MinValue;

            if (cachedLocalPackageIds == null || cachedLocalPackageIdsWriteTimeUtc != writeTimeUtc)
            {
                cachedLocalPackageIds = LoadLocalPackageIds(modsFolderPath);
                cachedLocalPackageIdsWriteTimeUtc = writeTimeUtc;
            }

            return cachedLocalPackageIds;
        }
    }

    private static DisableSteamModsSettings ReadSettingsForEarlyFilter()
    {
        if (string.IsNullOrWhiteSpace(OwnMod.FolderName))
        {
            return new DisableSteamModsSettings();
        }

        try
        {
            return LoadedModManager.ReadModSettings<DisableSteamModsSettings>(OwnMod.FolderName, ModHandleName) ??
                new DisableSteamModsSettings();
        }
        catch (Exception exception)
        {
            Log.Warning("[DisableSteamMods] Could not read RimWorld mod settings early; using defaults. " + exception.Message);
            return new DisableSteamModsSettings();
        }
    }

    private void InvalidateParsedIds()
    {
        whitelist = null;
        blacklist = null;
    }

    private static List<string> ParseModKeys(string rawKeys) => ModKeyMatcher.ParsePatterns(rawKeys).ToList();

    private static HashSet<string> LoadLocalPackageIds(string modsFolderPath)
    {
        if (!Directory.Exists(modsFolderPath))
        {
            return new HashSet<string>();
        }

        return Directory.GetDirectories(modsFolderPath)
            .Select(ReadPackageId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(ModKeyMatcher.Normalize)
            .ToHashSet();
    }

    private static string ReadPackageId(string modDirectory)
    {
        var aboutPath = Path.Combine(modDirectory, "About", "About.xml");
        if (!File.Exists(aboutPath))
        {
            return string.Empty;
        }

        try
        {
            return XDocument.Load(aboutPath).Root?.Element("packageId")?.Value ?? string.Empty;
        }
        catch (Exception exception)
        {
            Log.Warning("[DisableSteamMods] Could not read local mod package ID from " + aboutPath + ". " + exception.Message);
            return string.Empty;
        }
    }

}

public readonly struct WorkshopModIdentity
{
    public WorkshopModIdentity(string packageId)
    {
        PackageId = packageId;
    }

    public string PackageId { get; }
}
