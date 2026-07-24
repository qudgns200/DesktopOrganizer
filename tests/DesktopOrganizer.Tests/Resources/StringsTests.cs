using DesktopOrganizer.Resources;
using Xunit;

namespace DesktopOrganizer.Tests.Resources;

/// <summary>
/// F-024: Verifies the hand-written ResourceManager wiring in Strings.cs actually resolves
/// to Strings.resx at runtime (not just that it compiles). A base-name mismatch would cause
/// every property to silently fall back to the property name instead of throwing, so these
/// tests assert the REAL Korean text rather than merely "non-null" to catch that failure mode.
/// </summary>
public class StringsTests
{
    [Fact]
    public void TrayNewContainer_ResolvesActualResourceValue()
    {
        Assert.Equal("새 Container", Strings.Tray_NewContainer);
    }

    [Theory]
    [InlineData("Tray_RuleManager",   "Rule 관리...")]
    [InlineData("Tray_Settings",      "설정...")]
    [InlineData("Tray_WatcherPause",  "감시 일시정지")]
    [InlineData("Tray_WatcherResume", "감시 재개")]
    [InlineData("Tray_Exit",          "종료")]
    [InlineData("Common_OK",          "확인")]
    [InlineData("Common_Cancel",      "취소")]
    [InlineData("Settings_Title",     "설정")]
    public void KnownKeys_ResolveExpectedKoreanText(string propertyName, string expected)
    {
        var value = propertyName switch
        {
            "Tray_RuleManager"   => Strings.Tray_RuleManager,
            "Tray_Settings"      => Strings.Tray_Settings,
            "Tray_WatcherPause"  => Strings.Tray_WatcherPause,
            "Tray_WatcherResume" => Strings.Tray_WatcherResume,
            "Tray_Exit"          => Strings.Tray_Exit,
            "Common_OK"          => Strings.Common_OK,
            "Common_Cancel"      => Strings.Common_Cancel,
            "Settings_Title"     => Strings.Settings_Title,
            _ => throw new InvalidOperationException()
        };
        Assert.Equal(expected, value);
    }

    [Fact]
    public void ContainerLimitMessageFormat_FormatsWithCount()
    {
        var message = string.Format(Strings.Container_LimitMessageFormat, 42);
        Assert.Equal("Container는 최대 42개까지 생성할 수 있습니다.", message);
    }

    // ── F-025 ─────────────────────────────────────────────────────

    [Fact]
    public void ExternalLinkConfirmTitle_ResolvesActualResourceValue()
    {
        Assert.Equal("외부 링크 열기", Strings.ExternalLink_ConfirmTitle);
    }

    [Fact]
    public void ExternalLinkConfirmMessageFormat_FormatsWithFileName()
    {
        var message = string.Format(Strings.ExternalLink_ConfirmMessageFormat, "example.url");
        Assert.Equal("'example.url'을(를) 열면 외부 웹사이트로 이동합니다. 계속하시겠습니까?", message);
    }

    [Fact]
    public void SettingsConfirmExternalLinkLaunch_ResolvesActualResourceValue()
    {
        Assert.Equal("외부 링크 실행 전 확인 (.url 바로가기)", Strings.Settings_ConfirmExternalLinkLaunch);
    }

    // ── Missing-key fallback (F-024 acceptance criteria: never crashes) ─

    [Fact]
    public void MissingKey_FallsBackToKeyName_DoesNotThrow()
    {
        var ex = Record.Exception(() => Strings.Get("NonExistentKey_XYZ"));
        Assert.Null(ex);
    }

    [Fact]
    public void MissingKey_FallsBackToKeyNameValue()
    {
        Assert.Equal("NonExistentKey_XYZ", Strings.Get("NonExistentKey_XYZ"));
    }
}
