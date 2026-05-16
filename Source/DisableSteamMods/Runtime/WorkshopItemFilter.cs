using DisableSteamMods.Configuration;
using System;
using System.IO;
using System.Xml.Linq;
using Verse;
using Verse.Steam;

namespace DisableSteamMods.Runtime;

public static class WorkshopItemFilter
{
    public static bool Allows(WorkshopItem? item)
    {
        if (item == null)
        {
            return false;
        }

        return DisableSteamModsSettings.Current.Allows(new WorkshopModIdentity(ReadPackageId(item.Directory)));
    }

    private static string ReadPackageId(DirectoryInfo directory)
    {
        if (directory == null)
        {
            return string.Empty;
        }

        var aboutPath = Path.Combine(directory.FullName, "About", "About.xml");
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
            Log.Warning("[DisableSteamMods] Could not read Workshop mod package ID from " + aboutPath + ". " + exception.Message);
            return string.Empty;
        }
    }
}
