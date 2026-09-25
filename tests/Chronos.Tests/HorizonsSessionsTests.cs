using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
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

    // ------------------------------------------------------------------ Les gardes : les horizons ne divergent plus en silence

    // Les quatre fichiers qui CONSOMMENT un horizon, et les noms privés qu'ils portaient avant la phase 28.
    private static readonly string[] Consommateurs =
        { "SessionMonitor.cs", "TranscriptSessionSource.cs", "TreatedStore.cs", "BalayageMagasinSessions.cs" };

    private static readonly Regex AnciensNoms = new(@"\b(ActiveWindow|DropAfter|SilenceDesBattements|RetentionMax)\b");

    private static string DossierServices()
    {
        var racine = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : sans lui, cette garde ne lit aucun "
            + "fichier et ne garde rien.");
        var dossier = Path.Combine(racine, "Services");
        Assert.True(Directory.Exists(dossier), $"Dossier introuvable : {dossier}");
        return dossier;
    }

    /// <summary>
    /// LA CHAÎNE (recommandation n° 2 de l'audit v1.6, §6). Chaque inégalité a une raison : se taire avant de
    /// disparaître (20 min &lt; 8 h) ; ne jamais oublier un « traité » dont la session est encore lisible
    /// (8 h &lt; 24 h) ; ne jamais balayer ce que le widget pourrait montrer (24 h &lt; 72 h). Les inégalités sont
    /// assertées AVANT les valeurs exactes : si l'une se défait, le rouge dit laquelle.
    /// </summary>
    [Fact]
    public void La_chaine_des_horizons_tient()
    {
        Assert.True(HorizonsSessions.Silence < HorizonsSessions.Abandon,
            $"Silence ({HorizonsSessions.Silence}) < Abandon ({HorizonsSessions.Abandon}) défait : on cesserait de lire "
            + "une session avant d'avoir cessé de dire « Réflexion ».");
        Assert.True(HorizonsSessions.Abandon < HorizonsSessions.RetentionTraitees,
            $"Abandon ({HorizonsSessions.Abandon}) < RetentionTraitees ({HorizonsSessions.RetentionTraitees}) défait : "
            + "une entrée « traitée » expirerait pendant que sa session est encore lisible, et la session reviendrait "
            + "sans avoir rien redemandé.");
        Assert.True(HorizonsSessions.RetentionTraitees < HorizonsSessions.ExpirationEtat,
            $"RetentionTraitees ({HorizonsSessions.RetentionTraitees}) < ExpirationEtat ({HorizonsSessions.ExpirationEtat}) "
            + "défait : le balayage supprimerait un fichier d'état que le widget ou le magasin pourraient encore lire.");
        Assert.True(HorizonsSessions.ExpirationEtat >= HorizonsSessions.Abandon * 9,
            $"ExpirationEtat ({HorizonsSessions.ExpirationEtat}) n'est plus au moins neuf fois l'abandon "
            + $"({HorizonsSessions.Abandon}) : le « neuf fois » de l'audit v1.6 (§6) est rompu.");

        Assert.Equal(TimeSpan.FromMinutes(20), HorizonsSessions.Silence);
        Assert.Equal(TimeSpan.FromHours(8), HorizonsSessions.Abandon);
        Assert.Equal(TimeSpan.FromHours(24), HorizonsSessions.RetentionTraitees);
        Assert.Equal(TimeSpan.FromHours(72), HorizonsSessions.ExpirationEtat);

        // L'alias public du balayage ne peut pas diverger du type unique.
        Assert.Equal(HorizonsSessions.ExpirationEtat, BalayageMagasinSessions.ExpirationEtat);
    }

    /// <summary>
    /// LE CÂBLAGE. Sans lui, la chaîne serait vraie pendant qu'un fichier réintroduit un littéral privé : on
    /// testerait un type que plus personne ne lit. Chacun des quatre consommateurs lit <c>HorizonsSessions.</c>,
    /// ne porte plus aucun des anciens noms, et les trois premiers ne déclarent plus aucune durée.
    /// </summary>
    [Fact]
    public void Les_quatre_fichiers_lisent_le_type_unique()
    {
        var services = DossierServices();
        var infractions = new List<string>();

        foreach (var nom in Consommateurs)
        {
            var chemin = Path.Combine(services, nom);
            Assert.True(File.Exists(chemin), $"Fichier introuvable : {chemin}");
            var texte = File.ReadAllText(chemin);
            Assert.True(texte.Length >= 500, $"{nom} — fichier vide ou tronqué : la garde serait muette.");

            if (!texte.Contains("HorizonsSessions.", StringComparison.Ordinal))
                infractions.Add($"{nom} — ne lit pas HorizonsSessions");
            foreach (Match m in AnciensNoms.Matches(texte))
                infractions.Add($"{nom} — ancien nom « {m.Value} »");
            if (nom != "BalayageMagasinSessions.cs" && texte.Contains("TimeSpan.From", StringComparison.Ordinal))
                infractions.Add($"{nom} — littéral « TimeSpan.From »");
        }

        var balayage = File.ReadAllText(Path.Combine(services, "BalayageMagasinSessions.cs"));
        if (!balayage.Contains("ExpirationEtat = HorizonsSessions.ExpirationEtat", StringComparison.Ordinal))
            infractions.Add("BalayageMagasinSessions.cs — l'alias « ExpirationEtat = HorizonsSessions.ExpirationEtat » a disparu");

        Assert.True(infractions.Count == 0,
            "Un horizon vit de nouveau hors de HorizonsSessions :\n" + string.Join("\n", infractions));
    }

    /// <summary>
    /// LE SILENCE EN UN POINT. La règle « un travail muet depuis vingt minutes devient une attente déduite » a
    /// UN siège, le moniteur, qui l'applique à toutes les sources avant l'arbitrage. Une source qui la
    /// reproduirait chez elle ferait diverger les seuils au premier changement — exactement ce que SIL-01 ferme.
    /// </summary>
    [Fact]
    public void La_regle_de_silence_vit_en_un_seul_point()
    {
        var services = DossierServices();
        var fichiers = Directory.GetFiles(services, "*.cs", SearchOption.TopDirectoryOnly);
        Assert.True(fichiers.Length >= 40,
            $"Seulement {fichiers.Length} fichiers dans {services} : la garde lirait le mauvais dossier et serait muette.");

        var lecteurs = fichiers
            .Where(f => !string.Equals(Path.GetFileName(f), "HorizonsSessions.cs", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("HorizonsSessions.Silence", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f)!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "SessionMonitor.cs" }, lecteurs);

        var moniteur = File.ReadAllText(Path.Combine(services, "SessionMonitor.cs"));
        Assert.Contains("private static SessionSnapshot AppliquerSilence(", moniteur, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(moniteur, Regex.Escape("Activity = SessionActivity.WaitingDeduced")));

        var source = File.ReadAllText(Path.Combine(services, "TranscriptSessionSource.cs"));
        Assert.True(source.Length >= 500, "TranscriptSessionSource.cs vide ou tronqué : la garde serait muette.");
        Assert.DoesNotContain("WaitingDeduced", source, StringComparison.Ordinal);
    }
}
