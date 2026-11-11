using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Remotely.Agent.Services;

public interface IUpdateDownloader
{
    Task DownloadFile(string address, string fileName);
}

public class UpdateDownloader : IUpdateDownloader
{
    private readonly TimeSpan _timeout = TimeSpan.FromHours(6);
    private readonly IHttpClientFactory _clientFactory;

    public UpdateDownloader(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task DownloadFile(string downloadUrl, string filePath)
    {
        using var client = _clientFactory.CreateClient();
        client.Timeout = _timeout;

        using var downloadStream = await client.GetStreamAsync(downloadUrl);
        // CreateNew : jamais un fichier déjà présent (le chemin vient de UpdateWorkDirectory).
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }
        using var fileStream = new FileStream(filePath, options);

        await downloadStream.CopyToAsync(fileStream);
    }
}