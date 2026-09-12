using Chronos.Models;
using Chronos.Text;
using Chronos.ViewModels;
using Xunit;

namespace Chronos.Tests;

/// <summary>Prouve INT-02 : une fenêtre EXACT (issue de l'OAuth) éteint le badge « estimée »
/// (IsEstimated == false) et porte l'utilisation réelle (couleur de rampe) ; une fenêtre ESTIMÉE
/// rallume le badge. L'honnêteté joue dans les deux sens. Tests PURS ([Fact]).</summary>
public class WindowGaugeViewModelTests
{
    [Fact]
    public void Fenetre_exacte_masque_le_badge_estimee_et_porte_utilisation_reelle()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = 0.74,
            ResetsAt = DateTimeOffset.UtcNow + TimeSpan.FromHours(2),
        });

        Assert.False(vm.IsEstimated);              // badge « estimée » masqué (INT-02)
        Assert.Equal(0.74, vm.Utilization);        // arc en vraie couleur (utilization exacte)
        Assert.False(vm.HasTokens);                // pas de surfaçage tokens estimés en Exact
    }

    [Fact]
    public void Fenetre_estimee_rallume_le_badge()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromDays(7));
        vm.Apply(new WindowState { Kind = WindowKind.SevenDay, Reliability = SourceReliability.Estimated });
        Assert.True(vm.IsEstimated);               // honnêteté dans l'autre sens
    }

    // --- VIS-05 : PercentFormatter pur (honnêteté : null → rien, « ~ » si estimé, arrondi entier) ---

    [Fact]
    public void UtilizationText_null_rend_vide()
    {
        Assert.Equal("", PercentFormatter.Format(null, false));
        Assert.Equal("", PercentFormatter.Format(null, true));   // null → rien, MÊME estimé
    }

    [Fact]
    public void UtilizationText_exact_rend_80pourcent()
    {
        Assert.Equal("80 %", PercentFormatter.Format(0.80, false)); // espace normal avant %
    }

    [Fact]
    public void UtilizationText_estime_prefixe_tilde()
    {
        Assert.Equal("~80 %", PercentFormatter.Format(0.80, true)); // « ~ » = estimation, pas exact
    }

    [Fact]
    public void UtilizationText_arrondi_entier()
    {
        Assert.Equal("80 %", PercentFormatter.Format(0.804, false)); // arrondi vers le bas
        Assert.Equal("81 %", PercentFormatter.Format(0.806, false)); // arrondi vers le haut
    }

    [Fact]
    public void UtilizationText_plein_100()
    {
        Assert.Equal("100 %", PercentFormatter.Format(1.0, false));
    }

    // --- DEL-04 : le plancher se marque « ≥ », l'exact ne porte AUCUNE marque ---

    /// <summary>DEL-04 — le plancher porte « ≥ » : incertitude UNILATÉRALE, borne supérieure inconnue.
    /// Un « ~ » dirait « autour de 80 » et autoriserait la lecture « peut-être 75 » : faux, on SAIT
    /// qu'on est à 80 au minimum. Les trois autres provenances (frais, encore valide, non statuée)
    /// rendent un chiffre nu — la marque distingue le plancher, elle ne salit pas l'exact.</summary>
    [Fact]
    public void UtilizationText_plancher_prefixe_superieur_ou_egal()
    {
        Assert.Equal("≥ 80 %", PercentFormatter.Format(0.80, ProvenanceReleve.PlancherAvecActivite));
        Assert.Equal("80 %",   PercentFormatter.Format(0.80, ProvenanceReleve.Frais));
        Assert.Equal("80 %",   PercentFormatter.Format(0.80, ProvenanceReleve.EncoreValide));
        Assert.Equal("80 %",   PercentFormatter.Format(0.80, (ProvenanceReleve?)null));
        Assert.Equal("",       PercentFormatter.Format(null, ProvenanceReleve.PlancherAvecActivite));
    }

    /// <summary>DEL-04, bout en bout du sous-VM : c'est la PROVENANCE qui décide du préfixe, et non la
    /// fiabilité. Un plancher (Estimated + PlancherAvecActivite) s'annonce « ≥ 42 % » ; un exact encore
    /// valide (DEL-03) reste « 42 % » — c'est un chiffre juste, il n'a rien à porter.</summary>
    [Fact]
    public void Apply_derive_le_prefixe_de_la_provenance_et_non_de_la_fiabilite()
    {
        var plancher = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        plancher.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Estimated,
            Utilization = 0.42,
            Provenance = ProvenanceReleve.PlancherAvecActivite,
        });

        Assert.Equal("≥ 42 %", plancher.UtilizationText);
        Assert.True(plancher.IsEstimated);
        Assert.True(plancher.HasUtilizationText);

        var exact = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        exact.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = 0.42,
            Provenance = ProvenanceReleve.EncoreValide,
        });

        Assert.Equal("42 %", exact.UtilizationText);
        Assert.False(exact.IsEstimated);
    }

    // --- VIS-01 : FractionElapsed = clamp(1 − FractionRemaining) recalculée à chaque Interpolate ---

    [Fact]
    public void Elapsed_inverse_le_remplissage()
    {
        var now = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        // reste = 1.25 h sur 5 h → FractionRemaining = 0.25 → FractionElapsed = 0.75
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            ResetsAt = now + TimeSpan.FromHours(1.25),
        });
        vm.Interpolate(now);

        Assert.Equal(0.25, vm.FractionRemaining, 9);
        Assert.Equal(0.75, vm.FractionElapsed, 9);
    }

    [Fact]
    public void Elapsed_clampe_0_1()
    {
        var now = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));

        // reset déjà passé → FractionRemaining = 0 → FractionElapsed = 1 (jamais > 1)
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            ResetsAt = now - TimeSpan.FromHours(1),
        });
        vm.Interpolate(now);
        Assert.InRange(vm.FractionElapsed, 0.0, 1.0);
        Assert.Equal(1.0, vm.FractionElapsed, 9);

        // reset très loin → FractionRemaining = 1 → FractionElapsed = 0 (jamais < 0)
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            ResetsAt = now + TimeSpan.FromHours(50),
        });
        vm.Interpolate(now);
        Assert.InRange(vm.FractionElapsed, 0.0, 1.0);
        Assert.Equal(0.0, vm.FractionElapsed, 9);
    }

    // --- VIS-01 (correctif) : reset INCONNU → arc VIDE (0), pas plein — sinon un « — » trompeur affiche un plein ---
    [Fact]
    public void Elapsed_reset_inconnu_est_vide_pas_plein()
    {
        var now = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));

        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Estimated,
            ResetsAt = null, // reset inconnu (repli JSONL sans inférence) → countdown « — »
        });
        vm.Interpolate(now);

        Assert.Equal("—", vm.CountdownText);
        Assert.Equal(0.0, vm.FractionElapsed, 9); // arc VIDE, jamais plein
    }

    // --- VIS-05 : UtilizationText + HasUtilizationText posés par Apply ---

    [Fact]
    public void UtilizationText_pose_par_Apply_exact()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = 0.8,
        });
        Assert.Equal("80 %", vm.UtilizationText);
        Assert.True(vm.HasUtilizationText);
    }

    [Fact]
    public void UtilizationText_absent_si_null()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Estimated,   // estimé mais utilization null
        });
        Assert.Equal("", vm.UtilizationText);
        Assert.False(vm.HasUtilizationText);
    }

    // --- HDR-03 : le statut DÉCLARÉ par le serveur, exposé en texte FR + drapeau de visibilité ---
    // Posé et testé ici pour que la phase 20 (EXA-03) n'ait plus qu'à binder. AUCUNE géométrie n'en
    // dépend aujourd'hui : FractionRemaining / FractionElapsed / Utilization restent intacts.

    [Fact]
    public void Statut_serveur_rejete_est_rendu_en_FR_et_visible()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = 1.0,
            StatutServeur = StatutServeur.Rejete,
        });

        Assert.Equal("REJETÉ", vm.TexteStatutServeur);
        Assert.True(vm.HasStatutServeur);
    }

    [Fact]
    public void Statut_serveur_autorise_et_avertissement_ont_DEUX_libelles_distincts()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));

        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
            StatutServeur = StatutServeur.Autorise,
        });
        Assert.Equal("AUTORISÉ", vm.TexteStatutServeur);

        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
            StatutServeur = StatutServeur.AutoriseAvertissement,
        });
        Assert.Equal("AUTORISÉ (avertissement)", vm.TexteStatutServeur);
    }

    [Fact]
    public void Statut_NON_RECONNU_le_dit_plutot_que_de_le_ranger_dans_autorise()
    {
        // La famille d'en-têtes « unified » n'est documentée nulle part : une valeur inédite est une
        // INFORMATION (le vocabulaire du serveur a bougé), pas une absence, et encore moins un « oui ».
        var vm = new WindowGaugeViewModel(TimeSpan.FromDays(7));
        vm.Apply(new WindowState
        {
            Kind = WindowKind.SevenDay, Reliability = SourceReliability.Exact,
            StatutServeur = StatutServeur.NonReconnu,
        });

        Assert.Equal("statut non reconnu", vm.TexteStatutServeur);
        Assert.True(vm.HasStatutServeur);
    }

    [Fact]
    public void Statut_ABSENT_rend_le_VIDE_et_JAMAIS_autorise()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact, Utilization = 0.4,
            StatutServeur = null,
        });

        Assert.Equal("", vm.TexteStatutServeur);
        Assert.False(vm.HasStatutServeur);
    }

    [Fact]
    public void Fenetre_indisponible_ne_porte_AUCUN_statut_serveur()
    {
        // Un statut décrit une fenêtre : sans chiffre, il n'a rien à décrire. Et une fenêtre rebouchée
        // depuis le disque (LastExactStore) n'en porte jamais — le statut est volatil, jamais persisté.
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
            StatutServeur = StatutServeur.Rejete,
        });
        Assert.True(vm.HasStatutServeur);

        vm.Apply(WindowState.Unavailable(WindowKind.FiveHour));

        Assert.Equal("", vm.TexteStatutServeur);
        Assert.False(vm.HasStatutServeur);   // le statut précédent ne SURVIT pas à une fenêtre muette
    }
}
