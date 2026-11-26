using Remotely.Shared.Helpers;
using Vortice.Direct3D11;
using Vortice.DXGI;
using System.Drawing;

namespace Remotely.Desktop.Win.Models;

public class DirectXOutput : IDisposable
{
    public DirectXOutput(ID3D11Device device,
        IDXGIOutputDuplication outputDuplication,
        ID3D11Texture2D texture2D,
        ModeRotation rotation)
    {
        Device = device;
        OutputDuplication = outputDuplication;
        Texture2D = texture2D;
        Rotation = rotation;
        Bounds = new Rectangle(0, 0, (int)texture2D.Description.Width, (int)texture2D.Description.Height);
    }

    public Rectangle Bounds { get; set; }
    public ID3D11Device Device { get; }
    public IDXGIOutputDuplication OutputDuplication { get; }
    public ModeRotation Rotation { get; }
    public ID3D11Texture2D Texture2D { get; }
    public void Dispose()
    {
        OutputDuplication.ReleaseFrame();
        Disposer.TryDisposeAll(OutputDuplication, Texture2D, Device);
        GC.SuppressFinalize(this);
    }
}
