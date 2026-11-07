using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using MlbbOverlay.Services;

namespace MlbbOverlay.ViewModels;

/// <summary>One image field (icon, splash or full body) in the hero editor.</summary>
public partial class ImageSlot : ObservableObject
{
    private string? _newSourcePath;
    private bool _cleared;

    public string Label { get; }
    public string Kind { get; }
    public string Hint { get; }

    /// <summary>Full path of the picture shown in the preview.</summary>
    [ObservableProperty]
    private string? _previewPath;

    public ImageSlot(string label, string kind, string hint)
    {
        Label = label;
        Kind = kind;
        Hint = hint;
    }

    public void LoadExisting(string? fileName)
    {
        PreviewPath = string.IsNullOrEmpty(fileName) ? null : Path.Combine(AppPaths.HeroImages, fileName);
    }

    public void SetNew(string sourcePath)
    {
        _newSourcePath = sourcePath;
        _cleared = false;
        PreviewPath = sourcePath;
    }

    public void Clear()
    {
        _newSourcePath = null;
        _cleared = true;
        PreviewPath = null;
    }

    public ImageChange ToChange() => new(_newSourcePath, _cleared && _newSourcePath == null);
}