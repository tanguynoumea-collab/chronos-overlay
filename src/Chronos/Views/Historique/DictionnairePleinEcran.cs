using System.Windows;
using System.Windows.Controls;

namespace Chronos.Views.Historique;

/// <summary>
/// HIS-10 (phase 38, DESIGN_PLAN_CYCLE2 § 4.2) — construit le dictionnaire « PleinEcran » de l'Historique. Ses clés sont les clés
/// NORMALES des tokens ; ses valeurs viennent des tokens <c>clé + "PleinEcran"</c> de <c>DesignTokens.xaml</c>. Fusionné en dernier dans
/// les ressources d'un propriétaire, il remplace en bloc toutes les <c>DynamicResource</c> concernées : le plein écran n'est pas une
/// autre mise en page, c'est la même avec d'autres valeurs.
/// <para>Pourquoi les rangées sont dérivées des hauteurs normales : l'étoile d'une rangée reprend la hauteur normale de sa piste
/// (150* / 72* / 72*…), donc les proportions du plan sont garanties PAR CONSTRUCTION, sans second chiffre à maintenir.</para>
/// <para>Pourquoi une instance par propriétaire (fenêtre + trois vues) : chaque vue fusionne ses propres tokens, qui masqueraient un
/// dictionnaire posé sur la fenêtre seule ; la construction est triviale, on n'en partage aucune.</para>
/// WPF autorisé ici (couche vue, pas <c>Services/</c>).
/// </summary>
internal static class DictionnairePleinEcran
{
    /// <summary>Clés dont la valeur plein écran est le token « clé + PleinEcran » de DesignTokens.xaml (contrat § 4.2, plus
    /// l'en-tête 124 — écart consigné).</summary>
    internal static readonly string[] ClesEchelonnees =
    {
        "HistoCorpsInfime", "HistoCorpsLegende", "HistoCorpsMini", "HistoCorpsPetit", "HistoCorpsMoyen", "HistoCorpsNormal",
        "HistoCorpsGrand", "HistoCorpsTitre", "HistoLargeurLibelles", "HistoLargeurLegendeDroite",
        "HistoEpaisseurEscalier", "HistoEpaisseurPremierPlan", "HistoLongueurTiretReset", "HistoHauteurEnTete",
        "HistoPlafondNiveau", "HistoPlafondPiste",
    };

    /// <summary>(clé de rangée, clé de hauteur normale) : l'étoile reprend la hauteur normale → proportions par construction.</summary>
    internal static readonly (string Rangee, string Hauteur)[] Rangees =
    {
        ("HistoRangeeNiveauSemaine", "HistoHauteurNiveauPistes"),
        ("HistoRangeeRythmeSemaine", "HistoHauteurRythmePistes"),
        ("HistoRangeeTokensSemaine", "HistoHauteurTokensPistes"),
        ("HistoRangeeNiveauJour", "HistoHauteurNiveauJour"),
        ("HistoRangeeRythmeJour", "HistoHauteurRythmeJour"),
        ("HistoRangeeTokensJour", "HistoHauteurTokensJour"),
        ("HistoRangeeNiveauQuatreSemaines", "HistoHauteurNiveauQuatreSemaines"),
    };

    /// <summary>Construit le dictionnaire plein écran depuis les tokens. Une clé absente ou mal typée LÈVE (bug de contrat, attrapé
    /// par les tests) : aucun repli silencieux.</summary>
    internal static ResourceDictionary Construire(ResourceDictionary tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var d = new ResourceDictionary();
        foreach (var cle in ClesEchelonnees)
            d[cle] = Lire(tokens, cle + "PleinEcran");
        foreach (var (rangee, hauteur) in Rangees)
        {
            d[rangee] = new GridLength(Lire(tokens, hauteur), GridUnitType.Star);
            d[hauteur] = double.NaN;   // la piste s'étire dans sa rangée étoilée
        }
        d["HistoDefilementVertical"] = ScrollBarVisibility.Disabled;   // hauteur finie passée au contenu : les étoiles se résolvent
        return d;
    }

    private static double Lire(ResourceDictionary tokens, string cle)
        => tokens[cle] is double valeur
            ? valeur
            : throw new InvalidOperationException($"DesignTokens.xaml : le token « {cle} » (sys:Double) manque au dictionnaire plein écran.");
}
