using Remotely.Server.Services;
using System.ComponentModel.DataAnnotations;

namespace Remotely.Server.Models;

/// <summary>
/// Vide, ou adresse http(s) absolue sans identifiants (voir <see cref="PublicUrl"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PublicUrlAttribute : ValidationAttribute
{
    public PublicUrlAttribute()
        : base("Adresse http:// ou https:// complète attendue, par exemple https://amarre.example.") { }

    public override bool IsValid(object? value) =>
        value is null ||
        value is string text && (string.IsNullOrWhiteSpace(text) || PublicUrl.TryParse(text, out _));
}