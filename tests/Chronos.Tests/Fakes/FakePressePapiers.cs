using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Faux d'<see cref="IPressePapiers"/> (réglages v2) : retient le dernier texte copié, ne touche jamais le vrai presse-papiers.</summary>
public sealed class FakePressePapiers : IPressePapiers
{
    public string? Dernier { get; private set; }
    public int Copies { get; private set; }

    /// <summary>Simule un presse-papiers tenu par une autre application.</summary>
    public bool Indisponible { get; set; }

    public bool Copier(string texte)
    {
        if (Indisponible) return false;
        Dernier = texte;
        Copies++;
        return true;
    }
}
