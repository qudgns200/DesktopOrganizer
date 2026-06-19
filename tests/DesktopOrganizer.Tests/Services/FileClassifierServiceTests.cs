using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using Xunit;

namespace DesktopOrganizer.Tests.Services;

public class FileClassifierServiceTests
{
    private readonly FileClassifierService _sut = new();

    private static IconInfo MakeIcon(string fileName, IconType type = IconType.File)
    {
        var ext = type == IconType.Folder ? string.Empty : Path.GetExtension(fileName).ToLowerInvariant();
        return new IconInfo
        {
            FileName = fileName,
            FullPath = $@"C:\Users\Test\Desktop\{fileName}",
            IconType = type,
            Extension = ext
        };
    }

    // ── Documents ───────────────────────────────────────────
    [Theory]
    [InlineData("report.pdf")]
    [InlineData("budget.xlsx")]
    [InlineData("notes.txt")]
    [InlineData("presentation.pptx")]
    [InlineData("document.hwp")]
    [InlineData("document.hwpx")]
    [InlineData("spreadsheet.csv")]
    [InlineData("readme.md")]
    public void Classify_DocumentExtensions_ReturnsDocument(string fileName)
        => Assert.Equal(FileCategory.Document, _sut.Classify(MakeIcon(fileName)));

    // ── Images ──────────────────────────────────────────────
    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("screenshot.png")]
    [InlineData("design.psd")]
    [InlineData("icon.ico")]
    [InlineData("photo.heic")]
    public void Classify_ImageExtensions_ReturnsImage(string fileName)
        => Assert.Equal(FileCategory.Image, _sut.Classify(MakeIcon(fileName)));

    // ── Videos ──────────────────────────────────────────────
    [Theory]
    [InlineData("movie.mp4")]
    [InlineData("clip.avi")]
    [InlineData("recording.mkv")]
    [InlineData("stream.webm")]
    public void Classify_VideoExtensions_ReturnsVideo(string fileName)
        => Assert.Equal(FileCategory.Video, _sut.Classify(MakeIcon(fileName)));

    // ── Audio ───────────────────────────────────────────────
    [Theory]
    [InlineData("song.mp3")]
    [InlineData("track.flac")]
    [InlineData("audio.wav")]
    [InlineData("voice.opus")]
    public void Classify_AudioExtensions_ReturnsAudio(string fileName)
        => Assert.Equal(FileCategory.Audio, _sut.Classify(MakeIcon(fileName)));

    // ── Archives ────────────────────────────────────────────
    [Theory]
    [InlineData("archive.zip")]
    [InlineData("backup.7z")]
    [InlineData("package.rar")]
    [InlineData("source.tar")]
    public void Classify_ArchiveExtensions_ReturnsArchive(string fileName)
        => Assert.Equal(FileCategory.Archive, _sut.Classify(MakeIcon(fileName)));

    // ── Executables ─────────────────────────────────────────
    [Theory]
    [InlineData("setup.exe")]
    [InlineData("install.msi")]
    [InlineData("script.bat")]
    [InlineData("run.ps1")]
    [InlineData("macro.vbs")]
    public void Classify_ExecutableExtensions_ReturnsExecutable(string fileName)
        => Assert.Equal(FileCategory.Executable, _sut.Classify(MakeIcon(fileName)));

    // ── Shortcuts ───────────────────────────────────────────
    [Fact]
    public void Classify_LnkFile_ReturnsShortcut()
        => Assert.Equal(FileCategory.Shortcut, _sut.Classify(MakeIcon("app.lnk", IconType.Shortcut)));

    [Fact]
    public void Classify_UrlFile_ReturnsShortcut()
    {
        var icon = MakeIcon("site.url");
        icon.IconType = IconType.File; // .url is a file, not Shortcut type
        Assert.Equal(FileCategory.Shortcut, _sut.Classify(icon));
    }

    // ── Folders ─────────────────────────────────────────────
    [Fact]
    public void Classify_Folder_ReturnsFolder()
        => Assert.Equal(FileCategory.Folder, _sut.Classify(MakeIcon("MyFolder", IconType.Folder)));

    [Fact]
    public void Classify_FolderAlwaysReturnsFolder_RegardlessOfName()
    {
        var icon = MakeIcon("report.pdf", IconType.Folder);
        icon.Extension = string.Empty; // folder has no extension
        Assert.Equal(FileCategory.Folder, _sut.Classify(icon));
    }

    // ── Other / edge cases ──────────────────────────────────
    [Fact]
    public void Classify_UnknownExtension_ReturnsOther()
        => Assert.Equal(FileCategory.Other, _sut.Classify(MakeIcon("data.xyz")));

    [Fact]
    public void Classify_NoExtension_ReturnsOther()
        => Assert.Equal(FileCategory.Other, _sut.Classify(MakeIcon("noextension")));

    [Fact]
    public void Classify_ExtensionIsCaseInsensitive()
    {
        var icon = MakeIcon("IMAGE.JPG");
        icon.Extension = ".JPG"; // uppercase
        Assert.Equal(FileCategory.Image, _sut.Classify(icon));
    }

    // ── ClassifyAll ─────────────────────────────────────────
    [Fact]
    public void ClassifyAll_SetsAllCategories()
    {
        var icons = new List<IconInfo>
        {
            MakeIcon("doc.pdf"),
            MakeIcon("img.png"),
            MakeIcon("vid.mp4"),
            MakeIcon("folder", IconType.Folder),
        };
        _sut.ClassifyAll(icons);

        Assert.Equal(FileCategory.Document, icons[0].Category);
        Assert.Equal(FileCategory.Image,    icons[1].Category);
        Assert.Equal(FileCategory.Video,    icons[2].Category);
        Assert.Equal(FileCategory.Folder,   icons[3].Category);
    }

    [Fact]
    public void ClassifyAll_EmptyList_DoesNotThrow()
    {
        var icons = new List<IconInfo>();
        var ex = Record.Exception(() => _sut.ClassifyAll(icons));
        Assert.Null(ex);
    }
}
