#nullable enable

using MessagePack;
using Remotely.Shared.Models;
using Remotely.Shared.Primitives;
using System.Buffers.Binary;
using System.Text;

namespace Remotely.Shared.Services;

public interface IEmbeddedServerDataProvider
{
    /// <summary>Copie du client avec l'adresse du serveur et l'organisation inscrites à la fin du fichier.</summary>
    byte[] Embed(byte[] file, EmbeddedServerData serverData);
    Result<EmbeddedServerData> TryGetEmbeddedData(string filePath);
}

/// <summary>
/// Les données sont ajoutées à la fin du client : [bourrage][MessagePack][longueur sur 4 octets]["AMARRE01"].
/// Exe signé (Authenticode) : la table de certificats finit déjà le fichier, on l'allonge pour qu'elle couvre
/// les données. Cette table est exclue du condensé signé : la signature reste valide (même principe que le
/// « tagging » des installeurs Chrome). Le nom du fichier est donc libre.
/// Limite : un poste qui active EnableCertPaddingCheck (désactivé par défaut sous Windows) verrait la
/// signature invalide.
/// </summary>
public class EmbeddedServerDataProvider : IEmbeddedServerDataProvider
{
    private static readonly byte[] _magic = Encoding.ASCII.GetBytes("AMARRE01");
    private const int TrailerLength = 12; // longueur + magic

    public static EmbeddedServerDataProvider Instance { get; } = new();

    public byte[] Embed(byte[] file, EmbeddedServerData serverData)
    {
        var payload = MessagePackSerializer.Serialize(serverData);
        var length = payload.Length + TrailerLength;
        // La table de certificats doit rester alignée sur 8 octets.
        var padding = (8 - length % 8) % 8;
        length += padding;

        var result = new byte[file.Length + length];
        file.CopyTo(result, 0);
        var tail = result.AsSpan(file.Length);
        payload.CopyTo(tail[padding..]);
        BinaryPrimitives.WriteInt32LittleEndian(tail[(padding + payload.Length)..], payload.Length);
        _magic.CopyTo(tail[^_magic.Length..]);

        GrowCertificateTable(result, file.Length, length);
        return result;
    }

    public Result<EmbeddedServerData> TryGetEmbeddedData(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            if (fs.Length > TrailerLength)
            {
                var trailer = new byte[TrailerLength];
                fs.Seek(-TrailerLength, SeekOrigin.End);
                fs.ReadExactly(trailer);
                if (trailer.AsSpan(4).SequenceEqual(_magic))
                {
                    var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(trailer);
                    if (payloadLength <= 0 || payloadLength > 4096 || payloadLength > fs.Length - TrailerLength)
                    {
                        return Result.Fail<EmbeddedServerData>("Données intégrées invalides.");
                    }
                    var payload = new byte[payloadLength];
                    fs.Seek(-TrailerLength - payloadLength, SeekOrigin.End);
                    fs.ReadExactly(payload);
                    return Result.Ok(MessagePackSerializer.Deserialize<EmbeddedServerData>(payload));
                }
            }
        }
        catch (Exception ex)
        {
            return Result.Fail<EmbeddedServerData>(ex);
        }

        return TryGetFromFileName(filePath);
    }

    // ponytail: ancien format (« Remotely_Desktop[base64].exe ») pour les clients déjà téléchargés, à retirer d'ici quelques versions.
    private static Result<EmbeddedServerData> TryGetFromFileName(string filePath)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var start = fileName.LastIndexOf('[') + 1;
            var end = fileName.LastIndexOf(']');
            var base64 = fileName[start..end]
                .Replace("_", "/")
                .Replace("-", "+");

            var embeddedData = MessagePackSerializer.Deserialize<EmbeddedServerData>(Convert.FromBase64String(base64));
            return Result.Ok(embeddedData);
        }
        catch (Exception ex)
        {
            return Result.Fail<EmbeddedServerData>(ex);
        }
    }

    /// <summary>
    /// PE signé dont la table de certificats finit le fichier : l'allonger de <paramref name="added"/> octets.
    /// Tout autre fichier (exe non signé, ELF…) : rien à faire, le chargeur ignore les octets ajoutés.
    /// </summary>
    private static void GrowCertificateTable(byte[] pe, int originalLength, int added)
    {
        var span = pe.AsSpan(0, originalLength);
        if (originalLength < 0x40 || span[0] != 'M' || span[1] != 'Z')
        {
            return;
        }

        var peOffset = BinaryPrimitives.ReadInt32LittleEndian(span[0x3C..]);
        if (peOffset <= 0 || peOffset > originalLength - 26 || !span.Slice(peOffset, 4).SequenceEqual("PE\0\0"u8))
        {
            return;
        }

        var optionalHeader = peOffset + 24;
        int dataDirectories;
        switch (BinaryPrimitives.ReadUInt16LittleEndian(span[optionalHeader..]))
        {
            case 0x10B: dataDirectories = optionalHeader + 96; break;  // PE32
            case 0x20B: dataDirectories = optionalHeader + 112; break; // PE32+
            default: return;
        }

        var securityEntry = dataDirectories + 4 * 8;
        if (securityEntry + 8 > originalLength)
        {
            return;
        }

        var tableOffset = BinaryPrimitives.ReadInt32LittleEndian(span[securityEntry..]);
        var tableSize = BinaryPrimitives.ReadInt32LittleEndian(span[(securityEntry + 4)..]);
        if (tableSize <= 0 || tableOffset <= 0 || (long)tableOffset + tableSize != originalLength)
        {
            // Non signé, ou table ailleurs qu'en fin de fichier : données simplement ajoutées à la fin.
            return;
        }

        // Dernière entrée WIN_CERTIFICATE : son dwLength est allongé pour couvrir les données.
        var entry = tableOffset;
        while (true)
        {
            var entryLength = BinaryPrimitives.ReadInt32LittleEndian(span[entry..]);
            if (entryLength < 8)
            {
                return;
            }
            var next = entry + ((entryLength + 7) & ~7);
            if (next >= originalLength)
            {
                BinaryPrimitives.WriteInt32LittleEndian(pe.AsSpan(entry), originalLength - entry + added);
                break;
            }
            entry = next;
        }

        BinaryPrimitives.WriteInt32LittleEndian(pe.AsSpan(securityEntry + 4), tableSize + added);
    }
}
