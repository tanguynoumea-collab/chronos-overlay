using System.Globalization;
using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Décision D-28-01 — un transcript est daté par le <c>timestamp</c> de sa dernière ligne significative
/// (user / assistant), borné par la date d'écriture de son fichier, avec repli sur celle-ci si le champ manque
/// ou est illisible. JAMAIS par la seule date d'écriture.
///
/// <para>Le fait mesuré (28-RESEARCH.md Q4.b ; 27-RELEVE.md, geste A, point 2) : le 2026-09-25 à 16:58:20, la
/// fermeture de l'app bureau a ajouté des lignes SANS horodatage (<c>bridge-session</c>, <c>last-prompt</c>,
/// <c>cost-state</c>) à douze transcripts dont le dernier vrai message datait de deux heures à deux jours. Datés
/// par l'écriture, ils revenaient « En attente » d'un bloc, faisaient avancer l'épisode d'attente du détecteur
/// (une session marquée traitée ressortait sans avoir rien redemandé — NET-03 dévoyé) et faisaient perdre une
/// permission contre un transcript (FUS-01 faussé). 100 % des lignes user et assistant portent un
/// <c>timestamp</c> ; la date d'écriture ne sert plus qu'au pré-filtre d'énumération.</para>
///
/// <para>Racines TEMPORAIRES, <c>now</c> injecté, dates d'écriture posées : aucun test ne lit le vrai
/// <c>~/.claude/projects</c> ni ne demande l'heure.</para>
/// </summary>
public class TranscriptInstantSignalTests : IDisposable
{
    /// <summary>L'instant de référence. Fixe : jamais <c>DateTimeOffset.UtcNow</c>.</summary>
    private static readonly DateTimeOffset T = new(2026, 9, 25, 16, 0, 0, TimeSpan.Zero);

    private readonly List<string> _racines = new();

    public void Dispose()
    {
        foreach (var r in _racines)
            try { Directory.Delete(r, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private string TempRoot()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-instant-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // aucun test n'écrit hors du dossier temporaire
        _racines.Add(d);
        return d;
    }

    private static void Ecrire(string racine, string id, DateTimeOffset ecriture, params string[] lignes)
    {
        var dossier = Path.Combine(racine, "C--Projets-Projet-S");
        Directory.CreateDirectory(dossier);
        var f = Path.Combine(dossier, id + ".jsonl");
        File.WriteAllText(f, string.Join("\n", lignes) + "\n");
        File.SetLastWriteTimeUtc(f, ecriture.UtcDateTime);
    }

    private static string Iso(DateTimeOffset t)
        => t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    // Une invite utilisateur horodatée.
    private static string Invite(DateTimeOffset t) => Invite(Iso(t));

    private static string Invite(string horodatage) => $$$"""
        {"type":"user","isSidechain":false,"timestamp":"{{{horodatage}}}","cwd":"C:\\Projets\\Projet-S","message":{"role":"user","content":[{"type":"text","text":"une invite"}]}}
        """;

    // Une fin de tour (end_turn, bloc texte) portant l'horodatage donné, tel quel.
    private static string FinDeTour(DateTimeOffset t) => FinDeTour(Iso(t));

    private static string FinDeTour(string horodatage) => $$$"""
        {"type":"assistant","isSidechain":false,"timestamp":"{{{horodatage}}}","cwd":"C:\\Projets\\Projet-S","message":{"role":"assistant","stop_reason":"end_turn","content":[{"type":"text","text":"fini"}]}}
        """;

    [Fact]
    public void Un_transcript_est_date_par_le_timestamp_de_son_dernier_message()
    {
        var racine = TempRoot();
        Ecrire(racine, "s-date", T.AddMinutes(-1), Invite(T.AddMinutes(-6)), FinDeTour(T.AddMinutes(-5)));

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(T));

        Assert.Equal(SessionActivity.WaitingTurn, s.Activity);
        Assert.Equal(T.AddMinutes(-5), s.UpdatedAt);   // l'instant du MESSAGE, pas celui de l'écriture
    }

    /// <summary>Le cas mesuré du 2026-09-25, 16:58 : une session muette depuis deux jours dont le fichier vient
    /// d'être touché par des métadonnées de fermeture. Deux jours, c'est au-delà de la fenêtre actuelle ET de
    /// celle du plan 28-04 : ce test reste vrai après l'élargissement.</summary>
    [Fact]
    public void Un_transcript_rajeuni_par_des_metadonnees_n_est_pas_lu()
    {
        var racine = TempRoot();
        Ecrire(racine, "s-fermee", T.AddMinutes(-1),
               Invite(T.AddDays(-2).AddMinutes(-1)),
               FinDeTour(T.AddDays(-2)),
               """{"type":"cost-state","sessionId":"s-fermee"}""",
               """{"type":"bridge-session","sessionId":"s-fermee","bridgeSessionId":"cse_x","lastSequenceNum":0}""");

        Assert.Empty(new TranscriptSessionSource(racine).Read(T));
    }

    /// <summary>Repli : des lignes sans horodatage (forme des fixtures d'avant la phase 28) sont datées par
    /// l'écriture du fichier, comme en v1.6.</summary>
    [Fact]
    public void Sans_timestamp_le_repli_est_la_date_d_ecriture()
    {
        var racine = TempRoot();
        Ecrire(racine, "s-sans", T.AddMinutes(-2),
               """{"type":"user","cwd":"C:\\Projets\\Projet-S","message":{"role":"user","content":[{"type":"text","text":"salut"}]}}""",
               """{"type":"assistant","message":{"role":"assistant","stop_reason":"end_turn","content":[{"type":"text","text":"fini"}]}}""");

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(T));

        Assert.Equal(T.AddMinutes(-2), s.UpdatedAt);
    }

    /// <summary>Une source dont l'horloge avance ne date pas un signal dans l'avenir : l'horodatage d'un message
    /// ne dépasse jamais l'écriture du fichier qui le contient, il est donc borné par elle.</summary>
    [Fact]
    public void Un_timestamp_posterieur_a_l_ecriture_est_borne_par_elle()
    {
        var racine = TempRoot();
        Ecrire(racine, "s-avance", T.AddMinutes(-1), Invite(T.AddMinutes(-3)), FinDeTour(T.AddMinutes(10)));

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(T));

        Assert.Equal(T.AddMinutes(-1), s.UpdatedAt);
    }

    [Fact]
    public void Un_timestamp_illisible_retombe_sur_la_date_d_ecriture()
    {
        var racine = TempRoot();
        Ecrire(racine, "s-illisible", T.AddMinutes(-3), Invite(T.AddMinutes(-5)), FinDeTour("pas-une-date"));

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(T));

        Assert.Equal(T.AddMinutes(-3), s.UpdatedAt);   // la DERNIÈRE ligne décide : pas de repli sur l'invite
    }

    /// <summary>SRC-03 + D-28-01 : la limite de douze retient les plus récentes PAR LEUR DERNIER MESSAGE. Les
    /// quinze fichiers ont la MÊME date d'écriture ; l'ordre alphabétique est l'INVERSE de la fraîcheur, pour
    /// qu'un ordre d'énumération favorable ne puisse pas faire passer le test par hasard.</summary>
    [Fact]
    public void La_limite_de_douze_garde_les_plus_recents_par_leur_dernier_message()
    {
        var racine = TempRoot();
        var horodatages = new Dictionary<string, DateTimeOffset>();
        for (var i = 0; i < 15; i++)
        {
            var id = $"s-{i:D2}";
            var fin = T.AddMinutes(-1).AddSeconds(-(15 - i) * 10);
            horodatages[id] = fin;
            Ecrire(racine, id, T.AddMinutes(-1), Invite(fin.AddSeconds(-5)), FinDeTour(fin));
        }

        var snaps = new TranscriptSessionSource(racine).Read(T);

        // L'appartenance d'abord : c'est elle que l'ordre d'énumération pourrait fausser.
        Assert.Equal(
            Enumerable.Range(3, 12).Select(i => $"s-{i:D2}").OrderBy(x => x, StringComparer.Ordinal),
            snaps.Select(s => s.SessionId).OrderBy(x => x, StringComparer.Ordinal));
        Assert.All(snaps, s => Assert.Equal(horodatages[s.SessionId], s.UpdatedAt));
    }
}
