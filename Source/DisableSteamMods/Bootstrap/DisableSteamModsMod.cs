using DisableSteamMods.Configuration;
using UnityEngine;
using Verse;

namespace DisableSteamMods.Bootstrap;

public sealed class DisableSteamModsMod : Mod
{
    private const float ModKeyTextAreaHeight = 72f;
    private const float TextAreaInset = 4f;
    private const float ScrollbarWidth = 16f;
    private const string WhitelistControlName = "DisableSteamMods_WhitelistTextArea";
    private const string BlacklistControlName = "DisableSteamMods_BlacklistTextArea";
    private readonly DisableSteamModsSettings settings;
    private Vector2 blacklistScrollPosition;
    private Vector2 whitelistScrollPosition;

    public DisableSteamModsMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<DisableSteamModsSettings>();
        DisableSteamModsSettings.SetCurrent(settings);
        Log.Message("[DisableSteamMods] Loaded Steam Workshop suppression support.");
    }

    public override string SettingsCategory()
    {
        return "DisableSteamMods_SettingsCategory".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Find.WindowStack.currentlyDrawnWindow.closeOnAccept = false;

        var listing = new Listing_Standard();
        listing.Begin(inRect);

        listing.CheckboxLabeled("DisableSteamMods_EnableWhitelist".Translate(), ref settings.WhitelistEnabled, "DisableSteamMods_EnableWhitelistTooltip".Translate());
        listing.Label("DisableSteamMods_WhitelistedModKeys".Translate());
        settings.WhitelistModKeys = DrawModKeyTextArea(listing.GetRect(ModKeyTextAreaHeight), WhitelistControlName, settings.WhitelistModKeys, ref whitelistScrollPosition);
        listing.Gap();

        listing.CheckboxLabeled("DisableSteamMods_EnableBlacklist".Translate(), ref settings.BlacklistEnabled, "DisableSteamMods_EnableBlacklistTooltip".Translate());
        listing.Label("DisableSteamMods_BlacklistedModKeys".Translate());
        settings.BlacklistModKeys = DrawModKeyTextArea(listing.GetRect(ModKeyTextAreaHeight), BlacklistControlName, settings.BlacklistModKeys, ref blacklistScrollPosition);
        listing.Gap();

        listing.CheckboxLabeled("DisableSteamMods_RemoveOnlyLocalDuplicates".Translate(), ref settings.RemoveOnlyWhenLocalVersionExists, "DisableSteamMods_RemoveOnlyLocalDuplicatesTooltip".Translate());
        listing.Gap();

        listing.Label("DisableSteamMods_ModKeyHelp".Translate());
        listing.Gap();

        if (listing.ButtonText("DisableSteamMods_RestartGame".Translate()))
        {
            settings.Write();
            GenCommandLine.Restart();
        }

        listing.End();

        settings.NotifyChanged();
    }

    private static string DrawModKeyTextArea(Rect rect, string controlName, string? value, ref Vector2 scrollPosition)
    {
        var text = value ?? string.Empty;
        Widgets.DrawBox(rect);
        var scrollRect = new Rect(
            rect.x + TextAreaInset,
            rect.y + TextAreaInset,
            rect.width - TextAreaInset * 2f,
            rect.height - TextAreaInset * 2f);

        var viewWidth = scrollRect.width - ScrollbarWidth;
        var viewHeight = Mathf.Max(scrollRect.height, Text.CalcHeight(text, viewWidth) + TextAreaInset * 2f);
        var viewRect = new Rect(0f, 0f, viewWidth, viewHeight);

        Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
        GUI.SetNextControlName(controlName);
        var result = Widgets.TextArea(viewRect, text, true);
        Widgets.EndScrollView();

        var currentEvent = Event.current;
        if (GUI.GetNameOfFocusedControl() == controlName &&
            currentEvent.type == EventType.KeyDown &&
            (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter))
        {
            currentEvent.Use();
        }

        return result;
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        DisableSteamModsSettings.SetCurrent(settings);
    }
}
