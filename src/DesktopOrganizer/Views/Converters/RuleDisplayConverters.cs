using System.Globalization;
using System.Windows.Data;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Views.Converters;

/// <summary>Maps ConditionType enum values to Korean display names.</summary>
[ValueConversion(typeof(ConditionType), typeof(string))]
public class ConditionTypeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ConditionType ct ? ct switch
        {
            ConditionType.FileNamePattern   => "파일명 패턴",
            ConditionType.Extension         => "확장자",
            ConditionType.FileCategory      => "파일 종류",
            ConditionType.CreatedDateRange  => "생성일 범위",
            ConditionType.ModifiedDateRange => "수정일 범위",
            _                              => value.ToString() ?? string.Empty
        } : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps PatternMatchType enum values to Korean display names.</summary>
[ValueConversion(typeof(PatternMatchType), typeof(string))]
public class PatternMatchTypeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is PatternMatchType pt ? pt switch
        {
            PatternMatchType.Contains   => "포함",
            PatternMatchType.StartsWith => "시작 문자",
            PatternMatchType.EndsWith   => "끝 문자",
            PatternMatchType.Regex      => "정규식(Regex)",
            _                          => value.ToString() ?? string.Empty
        } : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps AppLogLevel enum values to Korean display names (F-023 Settings dialog).</summary>
[ValueConversion(typeof(AppLogLevel), typeof(string))]
public class AppLogLevelDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is AppLogLevel level ? level switch
        {
            AppLogLevel.Disabled  => "비활성화",
            AppLogLevel.ErrorOnly => "오류만 (WARN+ERROR)",
            AppLogLevel.Info      => "정보 (기본값)",
            AppLogLevel.Debug     => "디버그 (상세)",
            _                    => value.ToString() ?? string.Empty
        } : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps FileCategory enum values to Korean display names.</summary>
[ValueConversion(typeof(FileCategory), typeof(string))]
public class FileCategoryDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is FileCategory fc ? fc switch
        {
            FileCategory.Document   => "문서 (PDF·Word·Excel 등)",
            FileCategory.Image      => "이미지 (JPG·PNG·PSD 등)",
            FileCategory.Video      => "동영상 (MP4·AVI·MKV 등)",
            FileCategory.Audio      => "오디오 (MP3·WAV·FLAC 등)",
            FileCategory.Archive    => "압축 파일 (ZIP·RAR·7Z 등)",
            FileCategory.Executable => "실행 파일 (EXE·MSI·BAT 등)",
            FileCategory.Shortcut   => "바로가기 (LNK·URL)",
            FileCategory.Folder     => "폴더",
            FileCategory.Other      => "기타",
            _                      => value.ToString() ?? string.Empty
        } : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
