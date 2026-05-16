using Verse;

namespace DisableSteamMods.Bootstrap;

public sealed class DisableSteamModsMod : Mod
{
    public DisableSteamModsMod(ModContentPack content) : base(content)
    {
        Log.Message("[DisableSteamMods] Loaded Steam Workshop suppression support.");
    }
}
