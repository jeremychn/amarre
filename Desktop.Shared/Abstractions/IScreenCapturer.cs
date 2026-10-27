using Remotely.Shared.Models.Dtos;
using Remotely.Shared.Primitives;
using SkiaSharp;
using System.Drawing;

namespace Remotely.Desktop.Shared.Abstractions;

public interface IScreenCapturer : IDisposable
{
    event EventHandler<Rectangle> ScreenChanged;

    bool CaptureFullscreen { get; set; }
    Rectangle CurrentScreenBounds { get; }
    bool IsGpuAccelerated { get; }
    string SelectedScreen { get; }
    IEnumerable<string> GetDisplayNames();
    IEnumerable<DisplayLayoutDto> GetDisplayLayouts();
    SKRect GetFrameDiffArea();

    Result<SKBitmap> GetImageDiff();

    Result<SKBitmap> GetNextFrame();

    Result<SKBitmap> GetDisplayThumbnail(string displayName);

    int GetScreenCount();

    Rectangle GetVirtualScreenBounds();

    void Init();

    void SetSelectedScreen(string displayName);
}
