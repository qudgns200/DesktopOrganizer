using DesktopOrganizer.Models;
using Xunit;

namespace DesktopOrganizer.Tests.Models;

public class ContainerModelTests
{
    [Fact]
    public void Container_ShouldHaveUniqueId_WhenCreated()
    {
        var c1 = new Container();
        var c2 = new Container();
        Assert.NotEqual(c1.Id, c2.Id);
    }

    [Fact]
    public void Container_ShouldHaveDefaultDimensions()
    {
        var container = new Container();
        Assert.True(container.Width > 0);
        Assert.True(container.Height > 0);
    }

    [Fact]
    public void Container_DefaultSortMode_ShouldBeNameAsc()
    {
        var container = new Container();
        Assert.Equal(SortMode.NameAsc, container.SortMode);
    }
}
