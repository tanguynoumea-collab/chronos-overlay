using Chronos.Models.Historique;

namespace Chronos.Services.Historique;

/// <summary>
/// HIS-01 — la façade de lecture NEUTRE de la fenêtre Historique (Pattern 2 de la recherche 34). Elle reçoit une plage DÉJÀ
/// calculée (les bornes sont l'affaire du ViewModel, via <see cref="BornesPlage"/>) et rend des records immuables prêts à
/// dessiner. Appelée HORS du thread UI (<c>Task.Run</c>) ; le résultat est appliqué par <c>IUiDispatcher.Post</c>.
/// Deux implémentations : <see cref="SourceHistoriqueDisque"/> (le vrai dossier <c>%APPDATA%\Chronos\historique</c>) et
/// <c>SourceHistoriqueDemonstration</c> (la semaine de référence en mémoire : galerie et tests). Contrat NEUTRE (aucun WPF).
/// </summary>
public interface ISourceHistorique
{
    /// <summary>Le repère de la semaine de forfait (D-34-13) : le <c>R7</c> du DERNIER relevé de <c>[now − 7 j, now]</c>
    /// qui en porte un ; <c>null</c> si aucun (le VM se replie sur <c>ChronosSettings.WeeklyAnchor</c>, puis le calendrier).</summary>
    DateTimeOffset? RepereHebdo(DateTimeOffset now);

    /// <summary>La semaine de forfait <paramref name="semaine"/> et sa précédente <paramref name="precedente"/>, analysées à
    /// <paramref name="now"/>. Ne lève jamais : en cas de panne inattendue, des données VIDES (« aucun relevé »).</summary>
    DonneesSemaine LireSemaine(Plage semaine, Plage precedente, DateTimeOffset now);

    /// <summary>Le jour local <paramref name="jour"/>, analysé à <paramref name="now"/>. Ne lève jamais.</summary>
    DonneesJour LireJour(Plage jour, DateTimeOffset now);
}

/// <summary>
/// Règle partagée par les façades : un trou n'est « encore ouvert » qu'à l'INSTANT de l'analyse, et cet instant ne dépasse jamais
/// la fin de la plage. Analysée au vrai <c>now</c>, toute plage RÉVOLUE (semaine précédente, jour d'hier) finirait par un faux trou
/// ouvert « cause inconnue » après son dernier relevé — <c>AnalyseReleves.Analyser</c> ouvre un trou dès que
/// <c>now − Serie[^1].T</c> dépasse deux cadences. <c>min(now, Plage.Fin)</c> corrige cela sans toucher l'analyse pure.
/// </summary>
public static class InstantsHistorique
{
    /// <summary>L'instant auquel analyser <paramref name="plage"/> : <paramref name="now"/>, borné à <c>plage.Fin</c>.</summary>
    public static DateTimeOffset InstantDAnalyse(DateTimeOffset now, Plage plage)
    {
        ArgumentNullException.ThrowIfNull(plage);
        return now < plage.Fin ? now : plage.Fin;
    }
}
