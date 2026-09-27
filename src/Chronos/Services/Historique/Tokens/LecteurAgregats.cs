using System.IO;
using System.Text;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;

namespace Chronos.Services.Historique.Tokens;

/// <summary>
/// TOK-04 — lecture PAR PLAGE des agrégats de tokens, le jumeau de <see cref="LecteurJournal.Lire"/> (JRN-05).
///
/// <para>Ouvre les SEULS fichiers mensuels (<c>tokens-AAAA-MM.jsonl</c>, mois UTC du slot, D-33-02) qui chevauchent
/// <c>[de, a[</c>, relit chaque ligne par <see cref="LigneAgregat.Parser"/> (tolérant : une ligne refusée est COMPTÉE, jamais
/// tue), filtre sur la plage, trie (slot, modèle ordinal, sub) — l'ordre d'écriture du magasin (D-33-04) — et charge
/// <c>couverture.json</c> à côté des agrégats, découpé en sous-plages par <see cref="RenduLocalTokens.SousPlagesCouverture"/>.</para>
///
/// <para>POURQUOI lire les fichiers plutôt que <c>MagasinAgregats.TranchesDuMois</c> : la lecture ne doit pas muter l'état
/// du magasin (qui appartient à la reconstruction, 33-03) ; et le fichier est tenu le moins longtemps possible, en partage
/// large (<c>FileShare.ReadWrite | Delete</c>), parce qu'un lecteur qui tient le fichier fait échouer le <c>Move</c> de
/// l'écrivain (mesuré en 33-01). Pas d'offset à tenir ici : un <c>StreamReader</c> suffit.</para>
///
/// <para>Ne lève JAMAIS : dossier absent → lecture vide SANS le créer, couverture « hors couverture » de bout en bout ;
/// fichier absent → sauté ; verrou → quelques reprises courtes puis vide ; une E/S qui casse rend ce qui a été lu.
/// Entiers seulement (garde TOK-05). Type NEUTRE.</para>
/// </summary>
public static class LecteurAgregats
{
    // Même discipline que LecteurJournal.LireFichier : céder la main quelques fois, puis rendre vide plutôt que bloquer.
    private const int EssaisOuverture = 20;

    /// <summary>
    /// Lit la plage <c>[de, a[</c> : tranches triées et filtrées, lignes ignorées comptées, couverture en sous-plages,
    /// plus ancienne ligne vue. Dossier absent → lecture vide, jamais créé.
    /// </summary>
    public static LectureAgregats Lire(string dossier, DateTimeOffset de, DateTimeOffset a)
    {
        var plage = new Plage(de, a);
        if (string.IsNullOrEmpty(dossier) || !Directory.Exists(dossier))
        {
            // Rien lu, rien garanti : la couverture vide classe toute la plage « hors couverture ».
            return new LectureAgregats(Array.Empty<TrancheTokens>(), 0, plage,
                RenduLocalTokens.SousPlagesCouverture(plage, new CouvertureTokens()), null);
        }

        var tranches = new List<TrancheTokens>();
        var ignorees = 0;

        try
        {
            foreach (var mois in MoisUtcChevauchant(de, a))
                ignorees += LireFichier(Path.Combine(dossier, MagasinAgregats.NomFichier(mois)), plage, tranches);
        }
        catch (IOException) { /* lecture partielle : les agrégats ne font jamais tomber leur lecteur */ }
        catch (UnauthorizedAccessException) { }

        var couverture = CouvertureTokens.Charger(Path.Combine(dossier, CouvertureTokens.NomFichier));

        return new LectureAgregats(
            tranches.OrderBy(t => t.Slot).ThenBy(t => t.Model, StringComparer.Ordinal).ThenBy(t => t.Sub).ToList(),
            ignorees,
            plage,
            RenduLocalTokens.SousPlagesCouverture(plage, couverture),
            couverture.PlusAncienneLigneVue);
    }

    // Lit un fichier mensuel : ajoute à `cible` les tranches dans la plage, rend le nombre de lignes refusées.
    // Fichier absent ou inaccessible → 0, rien ajouté.
    private static int LireFichier(string chemin, Plage plage, List<TrancheTokens> cible)
    {
        if (!File.Exists(chemin)) return 0;

        FileStream? flux = null;
        for (var essai = 1; flux is null; essai++)
        {
            try
            {
                flux = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (FileNotFoundException) { return 0; }
            catch (DirectoryNotFoundException) { return 0; }
            catch (IOException) when (essai < EssaisOuverture) { if (essai <= 12) Thread.Yield(); else Thread.Sleep(1); }
            catch (IOException) { return 0; }
            catch (UnauthorizedAccessException) { return 0; }
        }

        var ignorees = 0;
        using (flux)
        using (var lecteur = new StreamReader(flux, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true))
        {
            string? ligne;
            while ((ligne = LireLigne(lecteur)) is not null)
            {
                ligne = ligne.TrimEnd('\r');   // un fichier repassé par un éditeur Windows ne devient pas illisible
                if (ligne.Length == 0) continue;   // ni lue, ni comptée

                if (LigneAgregat.Parser(ligne, out var tranche))
                {
                    if (tranche is not null && plage.Contient(tranche.Slot)) cible.Add(tranche);
                }
                else
                {
                    ignorees++;
                }
            }
        }
        return ignorees;
    }

    // Une E/S qui casse en cours de lecture termine la lecture sur ce qui a été lu.
    private static string? LireLigne(StreamReader lecteur)
    {
        try { return lecteur.ReadLine(); }
        catch (IOException) { return null; }
    }

    // Le 1er du mois UTC de `de`, puis chaque mois jusqu'à celui de `a − 1 tick` inclus : la borne `a` étant EXCLUE, une
    // plage qui finit pile au 1er du mois n'ouvre pas ce mois. Plage vide ou inversée → rien. (Motif LecteurJournal.)
    private static IEnumerable<DateTimeOffset> MoisUtcChevauchant(DateTimeOffset de, DateTimeOffset a)
    {
        if (a <= de) yield break;
        var dernierMois = TrancheTokens.MoisDe(a - TimeSpan.FromTicks(1));
        for (var mois = TrancheTokens.MoisDe(de); mois <= dernierMois; mois = mois.AddMonths(1))
            yield return mois;
    }
}
