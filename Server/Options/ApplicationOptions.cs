namespace Remotely.Server.Options;

public class ApplicationOptions
{
    public const string SectionKey = "ApplicationOptions";
    public string DbProvider { get; set; } = "SQLite";
    public string? DockerGatewayIp { get; set; }
    // Dossier où persister les clés DataProtection. Fixé par l'image Docker sur un volume, pour que les clés
    // survivent au passage en utilisateur non-root (le dossier par défaut dépend du HOME). Vide = défaut ASP.NET.
    public string? DataProtectionKeyDir { get; set; }
}
