namespace Chronos.Models.Historique.Tokens;

/// <summary>
/// TOK-02 — où en est la reconstruction des agrégats de tokens. Un ÉTAT nommé, jamais un nombre : le ViewModel (bandeau F2,
/// phase 34) et le diagnostic (33-05) en tirent un mot, pas une barre. La progression chiffrée vit à côté, en entiers
/// (<c>FichiersTraites</c> / <c>FichiersTotal</c>), jamais en rapport (garde TOK-05).
/// </summary>
public enum PhaseReconstruction
{
    /// <summary>Jamais lancée : le service n'a pas démarré (ou pas encore atteint sa première passe).</summary>
    JamaisLancee,

    /// <summary>Reconstruction : la première passe complète est en cours — l'historique se remplit du plus récent au plus ancien.</summary>
    Reconstruction,

    /// <summary>Incrémental : à jour ; seuls les fichiers qui bougent sont relus, à chaque cadence (60 s).</summary>
    Incremental,

    /// <summary>Arrêtée : l'annulation a été reçue, le dernier flush est fait, le thread de fond est sorti.</summary>
    Arretee,

    /// <summary>En échec : la dernière passe a échoué (<c>DerniereErreur</c> dit pourquoi) ; nouvelle tentative au cycle suivant.</summary>
    EnEchec,
}
