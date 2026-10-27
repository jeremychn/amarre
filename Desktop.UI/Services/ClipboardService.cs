using Avalonia.Input.Platform;
using Remotely.Desktop.Shared.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Remotely.Desktop.UI.Services;

public class ClipboardService : IClipboardService
{
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<ClipboardService> _logger;
    private Task? _watcherTask;

    public event EventHandler<string>? ClipboardTextChanged;

    public ClipboardService(
        IUiDispatcher dispatcher,
        ILogger<ClipboardService> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    private string ClipboardText { get; set; } = string.Empty;

    public void BeginWatching()
    {
        if (_watcherTask?.Status == TaskStatus.Running)
        {
            return;
        }

        _watcherTask = Task.Run(
            async () => await WatchClipboard(_dispatcher.ApplicationExitingToken),
            _dispatcher.ApplicationExitingToken);
    }

    public async Task SetText(string clipboardText)
    {
        try
        {
            // Pendant une prise de main, Avalonia n'a pas de fenêtre : Clipboard est null.
            // Sous Windows on passe directement par l'API Win32.
            if (OperatingSystem.IsWindows())
            {
                Win32Clipboard.SetText(clipboardText);
                ClipboardText = clipboardText;
                return;
            }

            if (_dispatcher?.Clipboard is null)
            {
                _logger.LogWarning("Clipboard is null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(clipboardText))
            {
                await _dispatcher.Clipboard.ClearAsync();
            }
            else
            {
                await _dispatcher.Clipboard.SetTextAsync(clipboardText);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while setting text.");
        }
    }

    private async Task WatchClipboard(CancellationToken cancelToken)
    {
        while (
            !cancelToken.IsCancellationRequested &&
            !Environment.HasShutdownStarted)
        {
            try
            {
                string? currentText;
                if (OperatingSystem.IsWindows())
                {
                    currentText = Win32Clipboard.GetText();
                }
                else if (_dispatcher?.Clipboard is null)
                {
                    continue;
                }
                else
                {
                    currentText = await _dispatcher.Clipboard.TryGetTextAsync();
                }

                if (!string.IsNullOrEmpty(currentText) && currentText != ClipboardText)
                {
                    ClipboardText = currentText;
                    ClipboardTextChanged?.Invoke(this, ClipboardText);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error while reading clipboard.");
            }
            finally
            {
                Thread.Sleep(500);
            }
        }
    }
}

internal static class Win32Clipboard
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr hMem);

    // Le presse-papier peut être verrouillé un court instant par une autre appli.
    private static bool TryOpen()
    {
        for (var i = 0; i < 10; i++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                return true;
            }
            Thread.Sleep(50);
        }
        return false;
    }

    public static string? GetText()
    {
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT) || !TryOpen())
        {
            return null;
        }

        try
        {
            var handle = GetClipboardData(CF_UNICODETEXT);
            var ptr = handle == IntPtr.Zero ? IntPtr.Zero : GlobalLock(handle);
            if (ptr == IntPtr.Zero)
            {
                return null;
            }
            try
            {
                return Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    public static void SetText(string? text)
    {
        if (!TryOpen())
        {
            throw new InvalidOperationException("OpenClipboard a échoué.");
        }

        try
        {
            EmptyClipboard();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var handle = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)((text.Length + 1) * 2));
            if (handle == IntPtr.Zero)
            {
                throw new OutOfMemoryException("GlobalAlloc a échoué.");
            }

            var ptr = GlobalLock(handle);
            try
            {
                Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
                Marshal.WriteInt16(ptr, text.Length * 2, 0);
            }
            finally
            {
                GlobalUnlock(handle);
            }

            if (SetClipboardData(CF_UNICODETEXT, handle) == IntPtr.Zero)
            {
                GlobalFree(handle);
                throw new InvalidOperationException("SetClipboardData a échoué.");
            }
        }
        finally
        {
            CloseClipboard();
        }
    }
}
