using Remotely.Shared.Models;

namespace Remotely.Desktop.Shared.Abstractions;

public interface ICursorIconWatcher
{
    event EventHandler<CursorInfo> OnChange;

    CursorInfo GetCurrentCursor();
}
