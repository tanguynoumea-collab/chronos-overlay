using Chronos.Theming;

namespace Chronos.ViewModels;

/// <summary>
/// Groupe titré de la section Thème (§5.1) ; contient les MÊMES instances <see cref="ThemeChoice"/> que
/// <see cref="MainViewModel.Themes"/>, pour que la surbrillance posée par la sélection reste visible.
/// </summary>
public sealed record GroupeThemes(string Titre, IReadOnlyList<ThemeChoice> Themes)
{
    /// <summary>Titre affiché d'une catégorie : source unique, en capitales comme les autres étiquettes des réglages.</summary>
    public static string TitreDe(CategorieTheme c) => c switch
    {
        CategorieTheme.Pale => "PÂLE",
        CategorieTheme.Classique => "CLASSIQUE",
        _ => "VIVE",
    };
}
