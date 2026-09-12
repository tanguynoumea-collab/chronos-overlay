using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// SRC-03 — la source transcripts cesse de s'aveugler pendant les vagues de sous-agents.
///
/// Fait MESURÉ le 2026-09-12 sur ~/.claude/projects : 870 transcripts, dont 819 « agent-*.jsonl »
/// rangés sous « &lt;uuid-de-session&gt;/subagents/ » — 94 %. La limite de douze fichiers était appliquée
/// AVANT le filtre isSidechain : chaque sous-agent chaud consommait un emplacement puis était jeté.
/// 4 des 5 fichiers chauds à l'instant de la mesure étaient des sous-agents.
///
/// Ces tests montent une racine de projets TEMPORAIRE : aucun ne lit le vrai ~/.claude/projects.
/// </summary>
public class TranscriptSousAgentsTests
{
    private static string TempRoot()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-souagents-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // aucun test n'écrit hors du dossier temporaire
        return d;
    }

    private const string CwdLine = """{"type":"user","cwd":"C:\dev\MonProjet","message":{"role":"user","content":[{"type":"text","text":"salut"}]}}""";
    private const string AssistantEndTurn = """{"type":"assistant","message":{"role":"assistant","stop_reason":"end_turn","content":[{"type":"text","text":"fini"}]}}""";
    private const string SousAgentToolUse = """{"type":"assistant","isSidechain":true,"message":{"role":"assistant","stop_reason":"tool_use","content":[{"type":"tool_use","name":"Bash"}]}}""";
    private const string SousAgentUser = """{"type":"user","isSidechain":true,"cwd":"C:\dev\MonProjet","message":{"role":"user","content":[{"type":"text","text":"va"}]}}""";

    // Écrit un fichier .jsonl à un CHEMIN RELATIF donné sous la racine, avec une date de dernière
    // écriture choisie (c'est elle que la fenêtre d'activité de 15 min regarde).
    private static void Ecrire(string root, string cheminRelatif, string[] lignes, TimeSpan age)
    {
        var f = Path.Combine(root, cheminRelatif);
        Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        File.WriteAllText(f, string.Join("\n", lignes) + "\n");
        File.SetLastWriteTimeUtc(f, DateTime.UtcNow - age);
    }

    [Fact]
    public void Douze_sous_agents_chauds_ne_font_plus_disparaitre_la_vraie_session()
    {
        var root = TempRoot();
        var now = DateTimeOffset.UtcNow;

        // La vraie session est VOLONTAIREMENT la plus ANCIENNE : avec l'ancien Take(12) appliqué au tri
        // par fraîcheur, les douze sous-agents la repoussaient en 13e position et elle disparaissait.
        for (var i = 0; i < 12; i++)
            Ecrire(root, Path.Combine("C--dev-MonProjet", "sess-uuid", "subagents", $"agent-{i:D2}.jsonl"),
                   new[] { SousAgentUser, SousAgentToolUse }, TimeSpan.FromSeconds(10 + i));
        Ecrire(root, Path.Combine("C--dev-MonProjet", "sess-uuid.jsonl"),
               new[] { CwdLine, AssistantEndTurn }, TimeSpan.FromMinutes(2));

        var snaps = new TranscriptSessionSource(root).Read(now);

        Assert.Single(snaps);
        Assert.Equal("sess-uuid", snaps[0].SessionId);
    }

    [Fact]
    public void La_limite_de_douze_porte_sur_les_sessions_retenues()
    {
        var root = TempRoot();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 15; i++)
            Ecrire(root, Path.Combine("C--dev-MonProjet", $"vraie-{i:D2}.jsonl"),
                   new[] { CwdLine, AssistantEndTurn }, TimeSpan.FromSeconds(i));

        var snaps = new TranscriptSessionSource(root).Read(now);

        Assert.Equal(12, snaps.Count);                                   // la limite tient
        Assert.Contains(snaps, s => s.SessionId == "vraie-00");          // la plus récente
        Assert.DoesNotContain(snaps, s => s.SessionId == "vraie-14");    // la plus ancienne, écartée
    }

    [Fact]
    public void Un_fichier_agent_hors_dossier_subagents_est_ecarte_par_son_nom()
    {
        var root = TempRoot();
        var now = DateTimeOffset.UtcNow;

        // Contenu PARFAITEMENT normal (aucun isSidechain) : seul le préfixe « agent- » le trahit.
        Ecrire(root, Path.Combine("C--dev-MonProjet", "agent-zzz.jsonl"),
               new[] { CwdLine, AssistantEndTurn }, TimeSpan.FromMinutes(1));

        Assert.Empty(new TranscriptSessionSource(root).Read(now));
    }

    [Fact]
    public void Le_champ_isSidechain_garde_l_autorite_sur_un_fichier_mal_range()
    {
        var root = TempRoot();
        var now = DateTimeOffset.UtcNow;

        // Nom banal, hors « subagents/ » : le pré-filtre de chemin ne le voit pas. Le filtre de CONTENU,
        // lui, le reconnaît — et il ne doit consommer aucun des douze emplacements.
        Ecrire(root, Path.Combine("C--dev-MonProjet", "range-n-importe-ou.jsonl"),
               new[] { SousAgentUser, SousAgentToolUse }, TimeSpan.FromMinutes(1));
        Ecrire(root, Path.Combine("C--dev-MonProjet", "vraie.jsonl"),
               new[] { CwdLine, AssistantEndTurn }, TimeSpan.FromMinutes(2));

        var snaps = new TranscriptSessionSource(root).Read(now);

        Assert.Single(snaps);
        Assert.Equal("vraie", snaps[0].SessionId);
    }
}
