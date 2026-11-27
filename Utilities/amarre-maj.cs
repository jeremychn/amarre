#:project ../Shared/Shared.csproj
// Signature des mises à jour d'agent (SDK .NET 10, rien d'autre à installer). Depuis la racine du dépôt :
//   dotnet run Utilities/amarre-maj.cs -- cle <nom> [--sans-mot-de-passe]
//       crée <nom>.pem (clé privée, SECRET, chiffrée par mot de passe sauf option) et <nom>.pub (clé publique)
//   dotnet run Utilities/amarre-maj.cs -- signer <dossier Content> [<cle.pem>]
//       écrit <dossier>/Manifest.txt ; sans fichier, clé PEM lue dans la variable AMARRE_UPDATE_KEY (CI)
// Format et vérification par l'agent : Shared/Helpers/UpdateManifest.cs. Procédure : docs/GUIDE.md §5.5.
using Remotely.Shared.Helpers;
using System.Security.Cryptography;
using System.Text;

switch (args)
{
    case ["cle", var name, ..]:
    {
        if (File.Exists(name + ".pem"))
        {
            throw new IOException($"{name}.pem existe déjà");
        }
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string pem;
        if (args.Contains("--sans-mot-de-passe"))
        {
            pem = key.ExportPkcs8PrivateKeyPem();
        }
        else
        {
            var password = ReadPassword("Mot de passe (long, à ranger dans KeePass) : ");
            if (password.Length < 12 || password != ReadPassword("Encore une fois : "))
            {
                throw new ArgumentException("Mots de passe différents ou trop courts (12 caractères au moins).");
            }
            pem = key.ExportEncryptedPkcs8PrivateKeyPem(password,
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 600_000));
        }
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        File.WriteAllText(name + ".pem", pem);
        File.WriteAllText(name + ".pub", publicKey + "\n");
        Console.WriteLine($"Créés : {name}.pem (SECRET) et {name}.pub\nClé publique : {publicKey}");
        return 0;
    }
    case ["signer", var contentDir, ..]:
    {
        var pem = args.Length > 2
            ? File.ReadAllText(args[2])
            : Environment.GetEnvironmentVariable("AMARRE_UPDATE_KEY");
        if (string.IsNullOrWhiteSpace(pem))
        {
            throw new ArgumentException("Clé absente : fichier .pem ou variable AMARRE_UPDATE_KEY.");
        }
        using var key = ECDsa.Create();
        if (pem.Contains("ENCRYPTED PRIVATE KEY"))
        {
            key.ImportFromEncryptedPem(pem, ReadPassword("Mot de passe de la clé : "));
        }
        else
        {
            key.ImportFromPem(pem);
        }
        var manifest = UpdateManifest.Write(contentDir, key);
        Console.Write(manifest);
        Console.WriteLine($"Signé avec la clé publique {Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())}");
        return 0;
    }
    default:
        Console.Error.WriteLine("Usage : cle <nom> [--sans-mot-de-passe] | signer <dossier Content> [<cle.pem>]");
        return 1;
}

static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var password = new StringBuilder();
    for (ConsoleKeyInfo k; (k = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter;)
    {
        if (k.Key == ConsoleKey.Backspace)
        {
            password.Length = Math.Max(0, password.Length - 1);
        }
        else
        {
            password.Append(k.KeyChar);
        }
    }
    Console.WriteLine();
    return password.ToString();
}
