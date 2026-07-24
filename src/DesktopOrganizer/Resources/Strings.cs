using System.Resources;
using System.Runtime.CompilerServices;

namespace DesktopOrganizer.Resources;

/// <summary>
/// F-024: Localized string accessor backed by Resources/Strings.resx (ko-KR only for now).
/// Hand-written rather than relying on the Visual Studio resx designer, so resource access
/// works identically under `dotnet build`/`dotnet publish` regardless of IDE.
/// Only System.Resources/System.Reflection are used — no PresentationFramework reference,
/// so Services/ (Core) can consume this without violating the Core→UI one-way dependency rule.
/// Adding a language later only requires a new Strings.{culture}.resx file with the same keys.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new(typeof(Strings));

    /// <summary>
    /// Looks up the resource named after the calling property (via CallerMemberName),
    /// so each property below needs no explicit key string. Falls back to the property
    /// name itself if the key is missing — never throws (F-024 acceptance criteria).
    /// Internal (not private) so DesktopOrganizer.Tests can verify the missing-key
    /// fallback path directly, via InternalsVisibleTo.
    /// </summary>
    internal static string Get([CallerMemberName] string? name = null)
    {
        try { return Manager.GetString(name!) ?? name ?? string.Empty; }
        catch (MissingManifestResourceException) { return name ?? string.Empty; }
    }

    // ── Tray menu (App.xaml.cs) ────────────────────────────────────
    public static string Tray_NewContainer      => Get();
    public static string Tray_RuleManager        => Get();
    public static string Tray_SaveLayout         => Get();
    public static string Tray_ManageLayout       => Get();
    public static string Tray_Settings           => Get();
    public static string Tray_WatcherPause       => Get();
    public static string Tray_WatcherResume      => Get();
    public static string Tray_OpenLog            => Get();
    public static string Tray_Exit               => Get();
    public static string App_AlreadyRunningMessage => Get();
    public static string App_AlreadyRunningTitle   => Get();
    public static string App_NoLogFileMessage      => Get();

    // ── Common button captions ──────────────────────────────────────
    public static string Common_OK     => Get();
    public static string Common_Cancel => Get();

    // ── F-023 Settings dialog ───────────────────────────────────────
    public static string Settings_Title                   => Get();
    public static string Settings_WatcherEnabled           => Get();
    public static string Settings_WatcherDebounceMsLabel   => Get();
    public static string Settings_IconSpacingPxLabel       => Get();
    public static string Settings_MaxContainersLabel       => Get();
    public static string Settings_LogLevelLabel            => Get();
    public static string Settings_ExcludedPathsLabel        => Get();
    public static string Settings_AddButton                => Get();
    public static string Settings_RemoveButton             => Get();
    public static string Settings_ErrorTitle               => Get();
    public static string Settings_DebounceRangeError       => Get();
    public static string Settings_SpacingRangeError        => Get();
    public static string Settings_MaxContainersRangeError  => Get();
    public static string Settings_PathAddedTitle           => Get();
    public static string Settings_PathNotExistMessage      => Get();
    public static string Settings_ConfirmExternalLinkLaunch => Get();

    // ── F-023 container-limit message ───────────────────────────────
    public static string Container_LimitTitle         => Get();
    public static string Container_LimitMessageFormat => Get();

    // ── F-025: external link launch confirmation ────────────────────
    public static string ExternalLink_ConfirmTitle         => Get();
    public static string ExternalLink_ConfirmMessageFormat => Get();
}
