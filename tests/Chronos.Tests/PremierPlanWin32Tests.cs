using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// LUE-02 / LUE-04 — la sonde de premier plan : À QUI appartient la fenêtre au premier plan, et DEPUIS QUAND c'est
/// l'app bureau (<c>claude</c>).
///
/// <para><b>Sonde injectée.</b> Tous les cas d'état (cache d'une seconde, « depuis », trou d'échantillonnage, horloge
/// qui recule, sonde en panne) passent par le constructeur <c>internal</c> : aucune fenêtre, aucun appel Win32, instant
/// injecté. Seul P08 touche l'OS — en lecture, sans asserter AUCUN nom de processus (le premier plan de la machine de
/// test est quelconque).</para>
///
/// <para><b>Garde au texte (P09).</b> Le nom du processus, jamais le titre de la fenêtre ; aucune UI Automation ; aucune
/// comparaison par préfixe ; aucun chemin du paquet de l'app.</para>
/// </summary>
public class PremierPlanWin32Tests
{
    /// <summary>L'instant du geste B (phase 27) où le premier plan a été vu sur <c>claude</c> : 20:33:27 à Paris.</summary>
    private static readonly DateTimeOffset T0 = new(2026, 9, 25, 18, 33, 27, TimeSpan.Zero);

    /// <summary>Une sonde RÉGLABLE entre deux lectures, qui compte ses appels et peut lever.</summary>
    private sealed class Sonde
    {
        public StatutPremierPlan Statut { get; set; } = StatutPremierPlan.Claude;
        public string? Processus { get; set; } = "claude";
        public bool Leve { get; set; }
        public int Appels { get; private set; }

        public (StatutPremierPlan Statut, string? Processus, string? Raison) Sonder()
        {
            Appels++;
            if (Leve) throw new InvalidOperationException("sonde de test en panne");
            return (Statut, Processus, null);
        }

        public void Claude() { Statut = StatutPremierPlan.Claude; Processus = "claude"; }
        public void Autre(string nom) { Statut = StatutPremierPlan.AutreProcessus; Processus = nom; }
        public void AucuneFenetre() { Statut = StatutPremierPlan.AucuneFenetre; Processus = null; }
    }

    private static (PremierPlanWin32 Premier, Sonde Sonde) Nouveau()
    {
        var sonde = new Sonde();
        return (new PremierPlanWin32(sonde.Sonder), sonde);
    }

    // ------------------------------------------------------------------ L'état : cache, depuis, trous (sonde injectée)

    /// <summary>P01 — le résultat est gardé une seconde : 999 ms ⇒ une sonde ; 1000 ms ⇒ deux.</summary>
    [Fact]
    public void Le_premier_plan_est_mis_en_cache_une_seconde()
    {
        var (premier, sonde) = Nouveau();

        premier.Lire(T0);
        premier.Lire(T0.AddMilliseconds(999));
        Assert.Equal(1, sonde.Appels);

        premier.Lire(T0.AddMilliseconds(1000));
        Assert.Equal(2, sonde.Appels);
    }

    /// <summary>P02 — « depuis » tient tant que les échantillons disent claude, retombe à nul sur un autre processus
    /// (dont le nom est dit), et repart de l'instant courant au retour.</summary>
    [Fact]
    public void Depuis_tient_tant_que_claude_et_se_repose_au_retour()
    {
        var (premier, sonde) = Nouveau();

        premier.Lire(T0);
        premier.Lire(T0.AddSeconds(2));
        var e = premier.Lire(T0.AddSeconds(4));
        Assert.Equal(StatutPremierPlan.Claude, e.Statut);
        Assert.Equal(T0, e.Depuis);
        Assert.Equal(T0, e.ClaudeDepuis);

        sonde.Autre("explorer");
        var autre = premier.Lire(T0.AddSeconds(6));
        Assert.Equal(StatutPremierPlan.AutreProcessus, autre.Statut);
        Assert.Null(autre.Depuis);
        Assert.Null(autre.ClaudeDepuis);
        Assert.Equal("explorer", autre.Processus);

        sonde.Claude();
        var retour = premier.Lire(T0.AddSeconds(8));
        Assert.Equal(T0.AddSeconds(8), retour.Depuis);
        Assert.Equal(T0.AddSeconds(8), retour.ClaudeDepuis);
    }

    /// <summary>P03 — le NOM du processus se compare par ÉGALITÉ ordinale insensible à la casse, jamais par préfixe ; un
    /// nom vide n'est pas un processus.</summary>
    [Theory]
    [InlineData("claude", StatutPremierPlan.Claude)]
    [InlineData("CLAUDE", StatutPremierPlan.Claude)]
    [InlineData("Claude", StatutPremierPlan.Claude)]
    [InlineData("claudette", StatutPremierPlan.AutreProcessus)]
    [InlineData("explorer", StatutPremierPlan.AutreProcessus)]
    [InlineData("", StatutPremierPlan.Indisponible)]
    public void Le_nom_du_processus_se_compare_par_egalite_sans_casse(string nom, StatutPremierPlan attendu)
    {
        Assert.Equal(attendu, PremierPlanWin32.Classer(nom));
    }

    /// <summary>P04 — une sonde qui lève ne propage rien : « indisponible », avec le type de l'exception pour raison, et
    /// aucun « depuis » (LUE-02 s'éteint d'elle-même). Au retour d'une sonde saine, le « depuis » part de cet instant.</summary>
    [Fact]
    public void Une_sonde_qui_leve_rend_indisponible_sans_exception()
    {
        var (premier, sonde) = Nouveau();
        sonde.Leve = true;

        var e = premier.Lire(T0);

        Assert.Equal(StatutPremierPlan.Indisponible, e.Statut);
        Assert.Equal("InvalidOperationException", e.Raison);
        Assert.Null(e.Depuis);
        Assert.Null(e.ClaudeDepuis);
        Assert.Null(e.Processus);

        sonde.Leve = false;
        var retour = premier.Lire(T0.AddSeconds(2));
        Assert.Equal(StatutPremierPlan.Claude, retour.Statut);
        Assert.Equal(T0.AddSeconds(2), retour.Depuis);
        Assert.Null(retour.Raison);
    }

    /// <summary>P05 — aucune fenêtre au premier plan (bascule en cours) : ni une panne, ni claude ; le « depuis » est
    /// rompu et repart au retour.</summary>
    [Fact]
    public void Une_fenetre_nulle_est_aucune_fenetre_pas_une_panne()
    {
        var (premier, sonde) = Nouveau();

        premier.Lire(T0);
        sonde.AucuneFenetre();
        var e = premier.Lire(T0.AddSeconds(2));
        Assert.Equal(StatutPremierPlan.AucuneFenetre, e.Statut);
        Assert.Null(e.Depuis);
        Assert.Null(e.Raison);

        sonde.Claude();
        Assert.Equal(T0.AddSeconds(4), premier.Lire(T0.AddSeconds(4)).Depuis);
    }

    /// <summary>P06 — une horloge qui recule ne sert pas le cache et ne prolonge pas le « depuis » : on ne sait pas ce
    /// qui s'est passé.</summary>
    [Fact]
    public void Une_horloge_qui_recule_resonde_et_repose_depuis()
    {
        var (premier, sonde) = Nouveau();

        premier.Lire(T0);
        var e = premier.Lire(T0.AddSeconds(-10));

        Assert.Equal(2, sonde.Appels);
        Assert.Equal(StatutPremierPlan.Claude, e.Statut);
        Assert.Equal(T0.AddSeconds(-10), e.Depuis);
    }

    /// <summary>P07 — un trou d'échantillonnage de plus de cinq secondes (veille, minuteur suspendu) fait repartir le
    /// « depuis » ; cinq secondes pile le laissent tenir (borne INCLUSE).</summary>
    [Fact]
    public void Un_trou_d_echantillonnage_repose_depuis()
    {
        var (premier, _) = Nouveau();

        premier.Lire(T0);
        premier.Lire(T0.AddSeconds(2));
        var pile = premier.Lire(T0.AddSeconds(7));   // écart 5 s : borne incluse
        Assert.Equal(T0, pile.Depuis);

        var apres = premier.Lire(T0.AddMilliseconds(12_001));   // écart 5,001 s
        Assert.Equal(T0.AddMilliseconds(12_001), apres.Depuis);
    }

    // ------------------------------------------------------------------ La sonde réelle (fumée)

    /// <summary>P08 — la sonde RÉELLE ne lève pas et rend un statut défini (jamais « non branché »). Aucun nom de
    /// processus n'est asserté : le premier plan de la machine de test est quelconque.</summary>
    [Fact]
    public void La_sonde_reelle_ne_leve_pas_et_rend_un_statut_defini()
    {
        var e = new PremierPlanWin32().Lire(new DateTimeOffset(2026, 9, 26, 8, 0, 0, TimeSpan.Zero));

        Assert.Contains(e.Statut, new[]
        {
            StatutPremierPlan.Claude, StatutPremierPlan.AutreProcessus,
            StatutPremierPlan.AucuneFenetre, StatutPremierPlan.Indisponible,
        });
        if (e.Statut is StatutPremierPlan.Claude or StatutPremierPlan.AutreProcessus)
            Assert.False(string.IsNullOrWhiteSpace(e.Processus), "un processus classé doit être nommé");
        if (e.Statut == StatutPremierPlan.Indisponible)
            Assert.False(string.IsNullOrWhiteSpace(e.Raison), "« indisponible » doit dire pourquoi");
    }

    // ------------------------------------------------------------------ Garde au texte

    /// <summary>P09 — le TEXTE de la sonde : le nom du processus au premier plan, comparé sans casse ; ni UI
    /// Automation (retirée en phase 21), ni titre de fenêtre (un onglet « Claude » passerait), ni préfixe (« claudette »
    /// passerait), ni chemin du paquet de l'app (garde n° 2 d'APP-05), ni le seuil de silence du moniteur. Anti-muette :
    /// le fichier existe et a du corps.</summary>
    [Fact]
    public void La_sonde_ne_lit_ni_UI_Automation_ni_titre_de_fenetre()
    {
        var sources = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(sources),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        var fichier = Path.Combine(sources, "Services", "PremierPlanWin32.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);
        Assert.True(texte.Length >= 2000, $"PremierPlanWin32.cs ne fait que {texte.Length} caractères : la garde serait muette.");

        foreach (var attendu in new[] { "GetForegroundWindow", "GetWindowThreadProcessId", ".ProcessName", "StringComparison.OrdinalIgnoreCase" })
            Assert.Contains(attendu, texte, StringComparison.Ordinal);

        foreach (var interdit in new[] { "Automation", "GetWindowText", "Claude_", "claude-code-sessions", "HorizonsSessions.Silence", "StartsWith" })
            Assert.DoesNotContain(interdit, texte, StringComparison.Ordinal);
    }
}
