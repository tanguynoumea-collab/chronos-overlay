namespace Chronos.Services;

/// <summary>
/// Quick 260927-reglages-v2 — le presse-papiers vu par un ViewModel (bouton « ⧉ Copier » du diagnostic). Contrat NEUTRE : le
/// ViewModel reste testable sans WPF, l'implémentation (<c>Clipboard.SetText</c>) vit sous <c>Views</c>.
/// </summary>
public interface IPressePapiers
{
    /// <summary>Place <paramref name="texte"/> dans le presse-papiers. Rend <c>false</c> si le presse-papiers est indisponible
    /// (tenu par une autre application) ; ne lève jamais.</summary>
    bool Copier(string texte);
}
