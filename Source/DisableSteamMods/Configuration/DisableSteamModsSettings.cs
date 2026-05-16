using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Verse;

namespace DisableSteamMods.Configuration;

public sealed class DisableSteamModsSettings : ModSettings
{
    private const string DisableSteamModsPackageId = "nemuruyama.disablesteammods";
    private const string PrepatcherPackageId = "zetrith.prepatcher";
    private const string ModHandleName = "DisableSteamModsMod";
    private static readonly OwnModInfo OwnMod = FindOwnMod();
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
            .Select(NormalizeModKey)
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

        var packageId = NormalizeModKey(mod.PackageId);
        if (RemoveOnlyWhenLocalVersionExists && !LocalPackageIds.Contains(packageId))
        {
            return true;
        }

        if (!WhitelistEnabled && !BlacklistEnabled)
        {
            return false;
        }

        if (WhitelistEnabled && !MatchesModKey(Whitelist, packageId))
        {
            return false;
        }

        return !BlacklistEnabled || !MatchesModKey(Blacklist, packageId);
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

    private static List<string> ParseModKeys(string rawKeys)
    {
        var separators = new[] { ',', ';', ' ', '\r', '\n', '\t' };
        return (rawKeys ?? string.Empty)
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeModKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .ToList();
    }

    private static bool MatchesModKey(IEnumerable<string> patterns, string packageId)
    {
        return patterns.Any(pattern => MatchesModKey(pattern, packageId));
    }

    private static bool MatchesModKey(string pattern, string packageId)
    {
        if (pattern == packageId || pattern == "*")
        {
            return true;
        }

        var wildcardIndex = pattern.IndexOf('*');
        if (wildcardIndex < 0)
        {
            return false;
        }

        var currentIndex = 0;
        var firstSegment = true;
        foreach (var segment in pattern.Split(new[] { '*' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var segmentIndex = packageId.IndexOf(segment, currentIndex, StringComparison.Ordinal);
            if (segmentIndex < 0)
            {
                return false;
            }

            if (firstSegment && wildcardIndex > 0 && segmentIndex != 0)
            {
                return false;
            }

            currentIndex = segmentIndex + segment.Length;
            firstSegment = false;
        }

        if (!pattern.EndsWith("*", StringComparison.Ordinal) && currentIndex != packageId.Length)
        {
            return false;
        }

        return true;
    }

    private static HashSet<string> LoadLocalPackageIds(string modsFolderPath)
    {
        if (!Directory.Exists(modsFolderPath))
        {
            return new HashSet<string>();
        }

        return Directory.GetDirectories(modsFolderPath)
            .Select(ReadPackageId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeModKey)
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

    private static OwnModInfo FindOwnMod()
    {
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return OwnModInfo.Empty;
        }

        for (var directory = Directory.GetParent(assemblyPath); directory != null; directory = directory.Parent)
        {
            var aboutPath = Path.Combine(directory.FullName, "About", "About.xml");
            if (!File.Exists(aboutPath))
            {
                continue;
            }

            try
            {
                var packageId = XDocument.Load(aboutPath).Root?.Element("packageId")?.Value ?? string.Empty;
                return new OwnModInfo(directory.Name, packageId);
            }
            catch (Exception exception)
            {
                Log.Warning("[DisableSteamMods] Could not read own package ID from " + aboutPath + ". " + exception.Message);
                return new OwnModInfo(directory.Name, string.Empty);
            }
        }

        Log.Warning("[DisableSteamMods] Could not find own About.xml while building the default whitelist.");
        return OwnModInfo.Empty;
    }

    private static string NormalizeModKey(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private readonly struct OwnModInfo
    {
        public static readonly OwnModInfo Empty = new(string.Empty, string.Empty);

        public OwnModInfo(string folderName, string packageId)
        {
            FolderName = folderName;
            PackageId = packageId;
        }

        public string FolderName { get; }

        public string PackageId { get; }
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
