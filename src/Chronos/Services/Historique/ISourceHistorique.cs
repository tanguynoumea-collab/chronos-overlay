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
