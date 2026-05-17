using System;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Verse;

namespace DisableMods.Core;

public static class OwnModFinder
{
    public static OwnModInfo FindOwnMod(Assembly assembly, string logPrefix)
    {
        var assemblyPath = assembly.Location;
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
                Log.Warning(logPrefix + " Could not read own package ID from " + aboutPath + ". " + exception.Message);
                return new OwnModInfo(directory.Name, string.Empty);
            }
        }

        Log.Warning(logPrefix + " Could not find own About.xml.");
        return OwnModInfo.Empty;
    }
}

public readonly struct OwnModInfo
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
