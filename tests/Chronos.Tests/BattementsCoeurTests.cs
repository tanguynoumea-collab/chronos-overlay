using System.Text.Json;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// EVT-03 — « en cours » cesse d'être deviné par un seuil d'expiration : il est RÉAFFIRMÉ par des
/// battements de cœur, et le veto sous-agent est ce qui rend ces battements honnêtes.
///
/// <para><b>Le piège que ce fichier garde.</b> Les hooks d'un sous-agent portent le MÊME
/// <c>session_id</c> que la session parente et ne s'en distinguent QUE par la présence de
/// <c>agent_id</c> / <c>agent_type</c> (relevé du 2026-09-12). Sur cette machine, quatre-vingt-quatorze
/// pour cent des transcripts sont des sous-agents : sans veto, une vague d'agents parallèles
/// réaffirmerait « en cours » sur un parent qui n'y est plus, et écraserait un « à toi » en attente.</para>
///
/// <para>Le cœur testé ici est PUR : <c>Process</c> ne prend qu'une chaîne. Aucun test de cette section
/// n'ouvre de fichier, ne lit le vrai <c>~/.claude/</c> ni le vrai <c>%APPDATA%\Chronos\</c>.</para>
/// </summary>
public class BattementsCoeurTests
{
    /// <summary>Stdin d'un hook : les deux champs COMMUNS confirmés (<c>session_id</c>, <c>cwd</c>), plus
    /// un fragment JSON inséré TEL QUEL — il doit pouvoir porter une valeur non textuelle.</summary>
    private static string Stdin(string? fragment = null)
        => "{\"session_id\":\"s\",\"cwd\":\"C:/dev/MonProjet\""
           + (string.IsNullOrEmpty(fragment) ? "" : "," + fragment) + "}";

    private const string MarqueurId = "\"agent_id\":\"a-1\"";
    private const string MarqueurType = "\"agent_type\":\"code-reviewer\"";
    private const string VraieDemande = "\"notification_type\":\"agent_needs_input\"";

    /// <summary>L'activité RÉELLEMENT écrite dans le fichier d'état — et la preuve, au passage, qu'un
    /// état a bien été produit.</summary>
    private static string Activite(SessionHookResult r)
    {
        Assert.False(r.Ignore);
        Assert.NotNull(r.StateJson);
        using var doc = JsonDocument.Parse(r.StateJson!);
        return doc.RootElement.GetProperty("activity").GetString()!;
    }

    // --- Le VETO sous-agent ---

    /// <summary>
    /// Un sous-agent ne parle jamais de l'ACTIVITÉ ni du CYCLE DE VIE de son parent : ni pour dire qu'il
    /// travaille, ni pour dire que son tour est fini. Les deux marqueurs sont éprouvés, car un seul
    /// des deux suffit à trahir un sous-agent.
    /// </summary>
    [Theory]
    [InlineData("PostToolUse", MarqueurId)]
    [InlineData("PostToolUse", MarqueurType)]
    [InlineData("UserPromptSubmit", MarqueurId)]
    [InlineData("Stop", MarqueurId)]
    public void Un_evenement_d_activite_ou_de_fin_de_tour_emis_par_un_sous_agent_est_ignore(string ev, string marqueur)
    {
        var r = SessionHookProcessor.Process(ev, Stdin(marqueur), 0);

        Assert.True(r.Ignore);
        Assert.Null(r.StateJson);
        Assert.False(r.Delete);
    }

    /// <summary>
    /// LE CAS LE PLUS GRAVE. Un sous-agent qui termine ne doit jamais faire disparaître le fichier d'état
    /// de la session parente : ce n'est pas une observation fausse, c'est une DESTRUCTION. D'où les deux
    /// assertions — « ignoré » ne suffirait pas si <c>Delete</c> restait vrai.
    /// </summary>
    [Fact]
    public void Un_SessionEnd_de_sous_agent_ne_supprime_pas_le_fichier_d_etat_du_parent()
    {
        var r = SessionHookProcessor.Process("SessionEnd", Stdin(MarqueurId), 0);

        Assert.True(r.Ignore);
        Assert.False(r.Delete);
    }

    /// <summary>
    /// Les DEUX seules exceptions au veto, et elles ne s'élargissent pas : un sous-agent peut
    /// parfaitement réclamer MON intervention, et c'est la seule chose qu'il dise de vrai pour la session
    /// parente. Vetoer ces deux-là ferait perdre de VRAIES demandes.
    /// </summary>
    [Theory]
    [InlineData("PermissionRequest", MarqueurId)]
    [InlineData("Notification", MarqueurId + "," + VraieDemande)]
    public void Un_sous_agent_peut_en_revanche_reclamer_mon_intervention(string ev, string fragment)
    {
        var r = SessionHookProcessor.Process(ev, Stdin(fragment), 0);

        Assert.Equal("WaitingAttention", Activite(r));
    }

    /// <summary>
    /// NON-RÉGRESSION EXPLICITE : sans marqueur de sous-agent, les SEPT routages posés au plan 25-01 sont
    /// rigoureusement inchangés. Un veto qui aurait élargi sa prise se verrait ici.
    /// </summary>
    [Fact]
    public void Sans_marqueur_de_sous_agent_les_sept_routages_restent_ceux_de_la_phase()
    {
        Assert.Equal("Working", Activite(SessionHookProcessor.Process("SessionStart", Stdin(), 0)));
        Assert.Equal("Working", Activite(SessionHookProcessor.Process("UserPromptSubmit", Stdin(), 0)));
        Assert.Equal("WaitingTurn", Activite(SessionHookProcessor.Process("Stop", Stdin(), 0)));
        Assert.Equal("WaitingAttention", Activite(SessionHookProcessor.Process("PermissionRequest", Stdin(), 0)));
        Assert.Equal("WaitingAttention", Activite(SessionHookProcessor.Process("Notification", Stdin(VraieDemande), 0)));

        var fin = SessionHookProcessor.Process("SessionEnd", Stdin(), 0);
        Assert.False(fin.Ignore);
        Assert.True(fin.Delete);

        Assert.True(SessionHookProcessor.Process("SubagentStop", Stdin(), 0).Ignore);
    }

    /// <summary>
    /// La lecture TOLÉRANTE ne fabrique jamais un veto à partir d'un champ illisible. Un marqueur vide ou
    /// non textuel n'est PAS un sous-agent : conclure l'inverse ferait disparaître en silence l'activité
    /// d'une vraie session parente, et c'est exactement le mode de défaillance que le milestone bannit.
    /// </summary>
    [Fact]
    public void Un_marqueur_de_sous_agent_vide_ou_non_textuel_n_est_pas_un_sous_agent()
    {
        Assert.Equal("Working",
            Activite(SessionHookProcessor.Process("UserPromptSubmit", Stdin("\"agent_id\":\"\""), 0)));
        Assert.Equal("Working",
            Activite(SessionHookProcessor.Process("UserPromptSubmit", Stdin("\"agent_id\":42"), 0)));
    }
}
