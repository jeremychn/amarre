using Microsoft.Extensions.Caching.Memory;

namespace Remotely.Server.Services;

/// <summary>
/// Compteur d'essais par clé (compte, IP, adresse e-mail) sur une fenêtre fixe, en mémoire.
/// ponytail: un compteur par instance de serveur, perdu au redémarrage ; suffisant pour une
/// instance unique, cache distribué si le serveur passe à plusieurs instances.
/// </summary>
public sealed class AttemptLimiter(int maxAttempts, TimeSpan window)
{
    private sealed class Counter
    {
        public int Count;
    }

    private readonly MemoryCache _counters = new(new MemoryCacheOptions());

    /// <summary>Vrai si <paramref name="key"/> a déjà atteint le plafond dans la fenêtre en cours.</summary>
    public bool IsBlocked(string key) =>
        _counters.TryGetValue(key, out Counter? counter) && Volatile.Read(ref counter!.Count) >= maxAttempts;

    /// <summary>
    /// Compte un essai. Vrai s'il reste sous le plafond (l'essai est permis), faux sinon.
    /// La fenêtre part du premier essai.
    /// </summary>
    public bool TryRecord(string key)
    {
        var counter = _counters.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = window;
            return new Counter();
        })!;
        return Interlocked.Increment(ref counter.Count) <= maxAttempts;
    }
}
