using System.Globalization;
using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// SIL-01 — une session connue par son SEUL transcript vit selon les mêmes horizons qu'une session à fichier de
/// hook : vingt minutes de silence après un travail ⇒ « En attente ? », tour fini ⇒ « En attente » jusqu'à huit
/// heures après son dernier message, absente au-delà.
///
/// <para><b>Le trou refermé (§9.1 de l'audit v1.6).</b> Avant la phase 28, la fenêtre des transcripts valait
/// quinze minutes et la règle de silence n'était appliquée qu'aux fichiers de hook. Une session sans fichier de
/// hook — ouverte avant la réconciliation, widget désactivé, ou fichier supprimé par le <c>SessionEnd</c> que
/// l'app bureau émet à la frontière des tours — disparaissait donc en silence quinze minutes après son dernier
/// message : jamais « En attente ? », et jamais « En attente » au-delà d'un quart d'heure.</para>
///
/// <para>Tout passe par un VRAI <see cref="SessionMonitor"/> sur une VRAIE <see cref="TranscriptSessionSource"/> :
/// racines TEMPORAIRES, lignes horodatées (décision D-28-01 : un transcript est daté par son dernier message),
/// date d'écriture posée sur celle de la dernière ligne, <c>now</c> injecté, et un <see cref="ArchiveStore"/>
/// temporaire TOUJOURS injecté — sans lui, le moniteur lirait le vrai <c>%APPDATA%\Chronos\archived.json</c>
/// (Piège 15). Aucun test ne demande l'heure ni ne lit <c>~/.claude</c>.</para>
/// </summary>
public class HorizonsSessionsTests : IDisposable
{
    /// <summary>L'instant de référence. Fixe : jamais <c>DateTimeOffset.UtcNow</c>.</summary>
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 16, 0, 0, TimeSpan.Zero);

    private readonly List<string> _racines = new();

    public void Dispose()
    {
        foreach (var r in _racines)
            try { Directory.Delete(r, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private string TempRoot()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-horizons-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // aucun test n'écrit hors du dossier temporaire
        _racines.Add(d);
        return d;
    }

    // Un transcript principal, écrit à la date d'écriture donnée (par défaut : celle de sa dernière ligne).
    private static void Ecrire(string racine, string id, DateTimeOffset ecriture, params string[] lignes)
    {
        var dossier = Path.Combine(racine, "C--Projets-Projet-H");
        Directory.CreateDirectory(dossier);
        var f = Path.Combine(dossier, id + ".jsonl");
        File.WriteAllText(f, string.Join("\n", lignes) + "\n");
        File.SetLastWriteTimeUtc(f, ecriture.UtcDateTime);
    }

    private static string Iso(DateTimeOffset t)
        => t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static string Invite(DateTimeOffset t) => $$$"""
        {"type":"user","isSidechain":false,"timestamp":"{{{Iso(t)}}}","cwd":"C:\\Projets\\Projet-H","message":{"role":"user","content":[{"type":"text","text":"une invite"}]}}
        """;

    // Un message assistant dont le dernier bloc est un appel d'outil nommé.
    private static string AssistantOutil(string outil, DateTimeOffset t) => $$$"""
        {"type":"assistant","isSidechain":false,"timestamp":"{{{Iso(t)}}}","cwd":"C:\\Projets\\Projet-H","message":{"role":"assistant","stop_reason":"tool_use","content":[{"type":"tool_use","id":"toolu_x","name":"{{{outil}}}","input":{}}]}}
        """;

    // Une fin de tour (end_turn, bloc texte).
    private static string FinDeTour(DateTimeOffset t) => $$$"""
        {"type":"assistant","isSidechain":false,"timestamp":"{{{Iso(t)}}}","cwd":"C:\\Projets\\Projet-H","message":{"role":"assistant","stop_reason":"end_turn","content":[{"type":"text","text":"fini"}]}}
        """;

    private SessionMonitor Moniteur(string racineTranscripts, string? dossierHooks = null)
        => new(dossierHooks ?? TempRoot(),
               new TranscriptSessionSource(racineTranscripts),
               new ArchiveStore(Path.Combine(TempRoot(), "archived.json")));

    // ------------------------------------------------------------------ SIL-01 : les scénarios

    /// <summary>Le cœur du trou §9.1 : un transcript seul, qui travaillait, muet depuis vingt-cinq minutes. Il
    /// ne disparaît plus : il dit qu'il attend peut-être, avec son point d'interrogation.</summary>
    [Fact]
    public void Transcript_seul_Working_muet_depuis_25_min_dit_En_attente_interrogatif()
    {
        var racine = TempRoot();
        Ecrire(racine, "s-muette", Now.AddMinutes(-25),
               Invite(Now.AddMinutes(-26)), AssistantOutil("Bash", Now.AddMinutes(-25)));

        var s = Assert.Single(Moniteur(racine).Read(Now));

        Assert.Equal("s-muette", s.SessionId);
        Assert.Equal(SessionActivity.WaitingDeduced, s.Activity);
        Assert.Equal("En attente ?", AffichageSessions.Etat(s.Activity));
        Assert.Equal(Now.AddMinutes(-25), s.UpdatedAt);   // l'instant du signal n'est pas réécrit par la règle
    }

    /// <summary>En deçà du seuil de silence, un travail reste un travail.</summary>
    [Fact]
    public void Transcript_seul_Working_depuis_19_min_dit_encore_Reflexion()
    {
        var racine = TempRoot();
        Ecrire(racine, "s-travail", Now.AddMinutes(-19),
               Invite(Now.AddMinutes(-20)), AssistantOutil("Bash", Now.AddMinutes(-19)));

        var s = Assert.Single(Moniteur(racine).Read(Now));

        Assert.Equal(SessionActivity.Working, s.Activity);
        Assert.Equal("Réflexion", AffichageSessions.Etat(s.Activity));
    }

    /// <summary>Un tour fini est une attente OBSERVÉE : elle persiste, comme un fichier de hook en tour fini, et
    /// le silence ne la touche pas.</summary>
    [Fact]
    public void Transcript_seul_en_tour_fini_depuis_3_h_dit_En_attente()
    {
        var racine = TempRoot();
        Ecrire(racine, "s-fini", Now.AddHours(-3),
               Invite(Now.AddHours(-3).AddMinutes(-1)), FinDeTour(Now.AddHours(-3)));

        var s = Assert.Single(Moniteur(racine).Read(Now));

        Assert.Equal(SessionActivity.WaitingTurn, s.Activity);
        Assert.Equal("En attente", AffichageSessions.Etat(s.Activity));
    }

    /// <summary>Une question en suspens (LIB-02) est une attente observée : trois heures de silence ne la
    /// dégradent pas en déduction, elle reste au rang des questions.</summary>
    [Fact]
    public void Transcript_seul_dont_la_question_attend_depuis_3_h_reste_au_rang_des_questions()
    {
        var racine = TempRoot();
        Ecrire(racine, "s-question", Now.AddHours(-3),
               Invite(Now.AddHours(-3).AddMinutes(-1)), AssistantOutil("AskUserQuestion", Now.AddHours(-3)));

        var s = Assert.Single(Moniteur(racine).Read(Now));

        Assert.Equal(SessionActivity.WaitingAttention, s.Activity);
        Assert.Equal("AskUserQuestion", s.Reason);
        Assert.Equal("En attente", AffichageSessions.Etat(s.Activity));
    }

    /// <summary>Borne basse de l'abandon : sept heures cinquante-neuf, la session est encore lue.</summary>
    [Fact]
    public void Transcript_seul_de_7_h_59_est_encore_lu()
    {
        var racine = TempRoot();
        var fin = Now.AddHours(-8).AddMinutes(1);
        Ecrire(racine, "s-limite", fin, Invite(fin.AddMinutes(-1)), FinDeTour(fin));

        var s = Assert.Single(Moniteur(racine).Read(Now));

        Assert.Equal("s-limite", s.SessionId);
        Assert.Equal(SessionActivity.WaitingTurn, s.Activity);
    }

    /// <summary>Borne haute de l'abandon : huit heures et une minute (message ET écriture), la session n'est plus
    /// lue — comme un fichier de hook au-delà de huit heures.</summary>
    [Fact]
    public void Transcript_seul_de_8_h_01_est_absent()
    {
        var racine = TempRoot();
        var fin = Now.AddHours(-8).AddMinutes(-1);
        Ecrire(racine, "s-abandon", fin, Invite(fin.AddMinutes(-1)), FinDeTour(fin));

        Assert.Empty(Moniteur(racine).Read(Now));
    }

    /// <summary>Le Piège 1, tenu à huit heures : un dernier message vieux de deux jours, un fichier touché il y a
    /// une minute par des métadonnées sans horodatage (relevé du 2026-09-25, 16 h 58). Daté par son message
    /// (D-28-01), il reste absent ; daté par l'écriture, il reviendrait « En attente » pendant huit heures.</summary>
    [Fact]
    public void Un_transcript_rajeuni_par_des_metadonnees_reste_absent()
    {
        var racine = TempRoot();
        Ecrire(racine, "s-fermee", Now.AddMinutes(-1),
               Invite(Now.AddDays(-2).AddMinutes(-1)), FinDeTour(Now.AddDays(-2)),
               """{"type":"cost-state","sessionId":"x"}""");

        Assert.Empty(Moniteur(racine).Read(Now));
    }

    /// <summary>Les MÊMES seuils pour les deux sources : un fichier de hook en travail et un transcript en travail,
    /// muets tous deux depuis vingt-cinq minutes, deviennent tous deux une attente déduite.</summary>
    [Fact]
    public void Hook_et_transcript_obeissent_au_meme_seuil_de_silence()
    {
        var racine = TempRoot();
        var hooks = TempRoot();
        File.WriteAllText(Path.Combine(hooks, "h.json"),
            SessionHookProcessor.BuildStateJson("h", "Projet-H", SessionActivity.Working, "PreToolUse",
                                                Now.AddMinutes(-25).ToUnixTimeMilliseconds()));
        Ecrire(racine, "t", Now.AddMinutes(-25),
               Invite(Now.AddMinutes(-26)), AssistantOutil("Bash", Now.AddMinutes(-25)));

        var visibles = Moniteur(racine, hooks).Read(Now).ToDictionary(s => s.SessionId);

        Assert.Equal(2, visibles.Count);
        Assert.Equal(SessionActivity.WaitingDeduced, visibles["h"].Activity);   // battement de hook
        Assert.Equal(SessionActivity.WaitingDeduced, visibles["t"].Activity);   // dernier message de transcript
    }
}
