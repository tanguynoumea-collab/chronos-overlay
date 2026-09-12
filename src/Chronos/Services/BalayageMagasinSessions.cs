using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Ce qu'un balayage a RÉELLEMENT retiré, et ce qu'il a laissé. Trois OBSERVATIONS, jamais des intentions :
/// une suppression qui échoue ne figure pas dans les retirés (précédent <see cref="ArchiveStore.PurgerPrefixe"/>,
/// où annoncer ce qu'on a repéré plutôt que ce qu'on a fait aurait menti sur le seul canal disponible).
/// </summary>
public sealed record BilanBalayage(int EtatsRetires, int TemporairesRetires, int EtatsConserves);

/// <summary>
/// CYC-01 — le magasin d'états de session cesse de ne faire que grandir.
///
/// <para>Pourquoi il fallait un balayage : la seule suppression d'un fichier d'état repose sur l'événement
/// de fin de session, dont aucune valeur de motif ne couvre un terminal tué, un plantage ou un redémarrage
/// machine — dans ces cas, le hook ne s'exécute pas du tout. Mesuré le 2026-09-12 : 54 fichiers, dont 48 de
/// plus de sept jours, âge médian 37 jours, 32 se déclarant encore au travail ; plus 12 fichiers temporaires
/// abandonnés, dont cinq pour une même session.</para>
///
/// <para>DEUX GESTES DISTINCTS, comme purger et expirer le sont dans
/// <see cref="ArchiveStore.PurgerPrefixe"/> :
/// retirer des DÉBRIS (des fichiers temporaires qui ne sont l'état de personne — ils n'ont jamais atteint
/// leur destination) n'est pas la même chose que balayer un ÉTAT (qui, lui, a été vrai).</para>
///
/// <para>LA DOCTRINE : expirer, c'est ne plus savoir. Une session balayée disparaît ; elle n'est
/// PAS déclarée terminée, ni traitée. Ce type ne connaît donc aucun magasin de verdict, et une garde par
/// réflexion le vérifie. Conséquence directe : le critère d'ÂGE ne peut pas être le seul, sinon une session
/// vivante depuis plusieurs jours serait effacée. Un état n'est balayé que si, en plus d'être vieux, plus
/// aucune source n'atteste que sa session vit.</para>
///
/// <para>Le seuil de <see cref="ExpirationEtat"/> vaut neuf fois celui au-delà duquel le moniteur cesse
/// d'afficher un fichier : on ne balaie jamais quelque chose que le widget pourrait encore montrer, et le
/// passé récent reste lisible dans le rapport de diagnostic.</para>
///
/// <para>L'horloge est INJECTÉE, et ce n'est pas une coquetterie : deux magasins de ce dépôt comparent un
/// horodatage à l'horloge du système sans pouvoir être pilotés, et trois plans de la phase 22 ont buté sur
/// des tests qui viraient au rouge à une heure donnée du soir sans qu'aucun code ne bouge.</para>
///
/// Aucun type WPF (couche neutre). Ne lève jamais : un balayage best-effort ne doit pas empêcher un démarrage.
/// </summary>
public sealed class BalayageMagasinSessions
{
    /// <summary>Au-delà, un état sans attestation de vie est balayé. Neuf fois le seuil d'affichage du moniteur.</summary>
    public static readonly System.TimeSpan ExpirationEtat = System.TimeSpan.FromHours(72);

    /// <summary>En deçà, un fichier temporaire est épargné : un hook est peut-être en train d'écrire
    /// (latence mesurée d'un hook : 584-611 ms, soit six mille fois moins).</summary>
    public static readonly System.TimeSpan AgeMinimalTemporaire = System.TimeSpan.FromHours(1);

    private const string MarqueurTemporaire = ".json.tmp-";

    private readonly string _dossier;
    private readonly ISessionSource _attestationDeVie;
    private readonly IClock _horloge;

    public BalayageMagasinSessions(string dossier, ISessionSource attestationDeVie, IClock horloge)
    {
        _dossier = dossier;
        _attestationDeVie = attestationDeVie;
        _horloge = horloge;
    }

    /// <summary>Le dossier balayé. Exposé pour qu'un test puisse VÉRIFIER qu'il est temporaire avant
    /// d'appeler <see cref="Balayer"/> : ce code supprime des fichiers.</summary>
    public string Dossier => _dossier;

    public BilanBalayage Balayer()
    {
        var maintenant = _horloge.UtcNow;
        if (!Directory.Exists(_dossier)) return new BilanBalayage(0, 0, 0);

        string[] entrees;
        try { entrees = Directory.GetFiles(_dossier); }
        catch { return new BilanBalayage(0, 0, 0); }

        // 1) DÉBRIS. Un fichier temporaire n'est l'état de personne : il n'a jamais atteint sa destination.
        //    Aucun critère d'attestation ne s'y applique — il n'y a rien à attester.
        var temporairesRetires = 0;
        foreach (var entree in entrees)
        {
            if (!Path.GetFileName(entree).Contains(MarqueurTemporaire, System.StringComparison.Ordinal)) continue;
            if (maintenant - DateEcriture(entree) < AgeMinimalTemporaire) continue;
            try { File.Delete(entree); temporairesRetires++; } catch { }
        }

        // 2) ATTESTATION DE VIE. Lue UNE fois, à l'instant du balayage. Best-effort : une source en panne
        //    n'atteste rien : le balayage retombe alors sur le seul critère d'âge. C'est le cas le moins
        //    favorable, et il est assumé — 72 h sans le moindre événement de hook ET sans transcript lisible.
        var vivantes = new HashSet<string>(System.StringComparer.Ordinal);
        try { foreach (var s in _attestationDeVie.Read(maintenant)) vivantes.Add(s.SessionId); }
        catch { }

        // 3) ÉTATS. Vieux ET sans attestation de vie. Les deux, jamais l'un seul.
        var etatsRetires = 0;
        var etatsConserves = 0;
        foreach (var entree in entrees)
        {
            if (!entree.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase)) continue;

            var (identifiant, date) = Dater(entree);
            if (vivantes.Contains(identifiant)) { etatsConserves++; continue; }
            if (maintenant - date <= ExpirationEtat) { etatsConserves++; continue; }

            try { File.Delete(entree); etatsRetires++; }
            catch { etatsConserves++; }   // pas supprimé = pas retiré : le bilan reste une observation
        }

        return new BilanBalayage(etatsRetires, temporairesRetires, etatsConserves);
    }

    // Identifiant et date d'un fichier d'état. Même règle de repli que le moniteur pour l'identifiant (le
    // nom du fichier), et pour la date : la valeur DÉCLARÉE d'abord ; à défaut, la date d'écriture du
    // fichier — un fait observé sur le fichier, jamais une date inventée.
    private static (string Identifiant, System.DateTimeOffset Date) Dater(string fichier)
    {
        var identifiant = Path.GetFileNameWithoutExtension(fichier);
        System.DateTimeOffset? declaree = null;
        try
        {
            using var flux = new FileStream(fichier, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var doc = JsonDocument.Parse(flux);
            var racine = doc.RootElement;
            if (racine.ValueKind == JsonValueKind.Object)
            {
                if (racine.TryGetProperty("session_id", out var sid) && sid.ValueKind == JsonValueKind.String
                    && !string.IsNullOrEmpty(sid.GetString()))
                    identifiant = sid.GetString()!;
                if (racine.TryGetProperty("updated_at", out var ua) && ua.TryGetInt64(out var ms))
                    declaree = UsageNormalization.InstantDepuisEpochMillisecondes(ms);
            }
        }
        catch { }
        return (identifiant, declaree ?? DateEcriture(fichier));
    }

    // Date d'écriture du fichier. Illisible -> instant maximal, donc âge négatif, donc JAMAIS balayé :
    // ne pas savoir quand un fichier a été écrit n'autorise pas à le supprimer.
    private static System.DateTimeOffset DateEcriture(string fichier)
    {
        try { return new System.DateTimeOffset(File.GetLastWriteTimeUtc(fichier), System.TimeSpan.Zero); }
        catch { return System.DateTimeOffset.MaxValue; }
    }
}
