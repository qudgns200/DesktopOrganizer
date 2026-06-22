using DesktopOrganizer.Models;
using DesktopOrganizer.ViewModels;
using Xunit;

namespace DesktopOrganizer.Tests.ViewModels;

public class StyleEditorViewModelTests
{
    private static ContainerStyle DefaultStyle() => new()
    {
        BackgroundColor   = "#44000000",
        BackgroundOpacity = 0.8,
        BorderColor       = "#CCFFFFFF",
        BorderThickness   = 1.0,
        BorderStyle       = BorderStyle.Solid,
        ShowTitle         = true,
        TitleFontSize     = 12.0,
        TitleFontColor    = "#FFFFFFFF",
        CornerRadius      = 4.0
    };

    // ── Constructor ───────────────────────────────────────────────

    [Fact]
    public void Constructor_CopiesAllStyleProperties()
    {
        var src = DefaultStyle();
        var vm  = new StyleEditorViewModel(src);

        Assert.Equal(src.BackgroundColor,   vm.BackgroundColor);
        Assert.Equal(src.BackgroundOpacity, vm.BackgroundOpacity);
        Assert.Equal(src.BorderColor,       vm.BorderColor);
        Assert.Equal(src.BorderThickness,   vm.BorderThickness);
        Assert.Equal(src.BorderStyle,       vm.BorderStyle);
        Assert.Equal(src.ShowTitle,         vm.ShowTitle);
        Assert.Equal(src.TitleFontSize,     vm.TitleFontSize);
        Assert.Equal(src.TitleFontColor,    vm.TitleFontColor);
        Assert.Equal(src.CornerRadius,      vm.CornerRadius);
    }

    // ── Clamp validation ──────────────────────────────────────────

    [Theory]
    [InlineData(-0.1, 0.0)]
    [InlineData(1.1, 1.0)]
    [InlineData(0.5, 0.5)]
    public void BackgroundOpacity_ClampsToZeroOne(double input, double expected)
    {
        var vm = new StyleEditorViewModel(DefaultStyle()) { BackgroundOpacity = input };
        Assert.Equal(expected, vm.BackgroundOpacity, precision: 5);
    }

    [Fact]
    public void BorderThickness_NegativeValue_ClampsToZero()
    {
        var vm = new StyleEditorViewModel(DefaultStyle()) { BorderThickness = -5 };
        Assert.Equal(0, vm.BorderThickness);
    }

    [Theory]
    [InlineData(-1.0, 0.0)]
    [InlineData(41.0, 40.0)]
    [InlineData(10.0, 10.0)]
    public void CornerRadius_ClampsToRange(double input, double expected)
    {
        var vm = new StyleEditorViewModel(DefaultStyle()) { CornerRadius = input };
        Assert.Equal(expected, vm.CornerRadius, precision: 5);
    }

    [Theory]
    [InlineData(7.0,  8.0)]
    [InlineData(25.0, 24.0)]
    [InlineData(14.0, 14.0)]
    public void TitleFontSize_ClampsToRange(double input, double expected)
    {
        var vm = new StyleEditorViewModel(DefaultStyle()) { TitleFontSize = input };
        Assert.Equal(expected, vm.TitleFontSize, precision: 5);
    }

    // ── Property change ───────────────────────────────────────────

    [Fact]
    public void BackgroundColor_SetValue_RaisesPropertyChanged()
    {
        var vm      = new StyleEditorViewModel(DefaultStyle());
        var raised  = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StyleEditorViewModel.BackgroundColor))
                raised = true;
        };

        vm.BackgroundColor = "#FF112233";

        Assert.True(raised);
        Assert.Equal("#FF112233", vm.BackgroundColor);
    }

    [Fact]
    public void ShowTitle_Toggle_RaisesPropertyChanged()
    {
        var vm     = new StyleEditorViewModel(DefaultStyle());
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StyleEditorViewModel.ShowTitle))
                raised = true;
        };

        vm.ShowTitle = false;

        Assert.True(raised);
    }

    // ── ToContainerStyle ─────────────────────────────────────────

    [Fact]
    public void ToContainerStyle_ReflectsCurrentState()
    {
        var vm = new StyleEditorViewModel(DefaultStyle())
        {
            BackgroundColor = "#AABBCCDD",
            CornerRadius    = 8.0,
            ShowTitle       = false
        };

        var result = vm.ToContainerStyle();

        Assert.Equal("#AABBCCDD", result.BackgroundColor);
        Assert.Equal(8.0,         result.CornerRadius);
        Assert.False(result.ShowTitle);
    }

    [Fact]
    public void ToContainerStyle_IsNewInstance()
    {
        var vm = new StyleEditorViewModel(DefaultStyle());
        Assert.NotSame(vm.ToContainerStyle(), vm.ToContainerStyle());
    }

    // ── BorderStyleOptions ───────────────────────────────────────

    [Fact]
    public void BorderStyleOptions_ContainsAllEnumValues()
    {
        var vm      = new StyleEditorViewModel(DefaultStyle());
        var expected = Enum.GetValues<BorderStyle>();
        Assert.Equal(expected.Length, vm.BorderStyleOptions.Count);
        foreach (var style in expected)
            Assert.Contains(style, vm.BorderStyleOptions);
    }
}
