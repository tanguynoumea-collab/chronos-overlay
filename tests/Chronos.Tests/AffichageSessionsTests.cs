using Chronos.Services;
using Chronos.ViewModels;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// OBS-01 — l'ordre, le libellé d'état et le libellé d'ancienneté du widget ont UN SEUL producteur, neutre,
/// que le rapport de diagnostic consommera en vague 2. Ces tests prouvent deux choses : que la couche
/// neutre dit bien ce que le widget disait, et que le ViewModel n'en fabrique plus une seconde version.
/// Aucun accès disque réel : source de sessions substituée, magasins sur chemins temporaires.
/// </summary>
public class AffichageSessionsTests
{
    private static readonly DateTimeOffset Maintenant = new(2026, 9, 12, 13, 28, 0, TimeSpan.Zero);

    /// <summary>LIB-01 — les trois attentes OBSERVÉES disent le même mot ; l'ordre et la couleur portent la
    /// distinction. L'attente DÉDUITE porte son « ? ». « indéterminé » n'est lu que par le rapport de
    /// diagnostic : l'état indéterminé n'a pas de ligne dans le widget.</summary>
    [Theory]
    [InlineData(SessionActivity.WaitingAttention, "En attente")]
    [InlineData(SessionActivity.WaitingTurn, "En attente")]
    [InlineData(SessionActivity.Working, "Réflexion")]
    [InlineData(SessionActivity.Unknown, "indéterminé")]
    [InlineData(SessionActivity.WaitingDeduced, "En attente ?")]
    public void Chaque_etat_a_son_libelle(SessionActivity a, string attendu)
        => Assert.Equal(attendu, AffichageSessions.Etat(a));

    /// <summary>
    /// LIB-01 — les TROIS mots du widget, verrouillés par l'utilisateur, et rien d'autre. Les constantes sont
    /// comparées à leurs chaînes EXACTES (un accent, une capitale, l'espace avant « ? » comptent) ; puis
    /// l'ensemble de ce que le producteur rend pour une session qui a une ligne doit être EXACTEMENT ces trois
    /// mots — un quatrième libellé qui naîtrait en silence rougirait ici, pas chez l'utilisateur.
    /// </summary>
    [Fact]
    public void Le_producteur_ne_connait_que_trois_mots_visibles()
    {
        Assert.Equal("Réflexion", AffichageSessions.Reflexion);
        Assert.Equal("En attente", AffichageSessions.EnAttente);
        Assert.Equal(AffichageSessions.EnAttente + " ?", AffichageSessions.EnAttenteDeduite);

        var valeurs = Enum.GetValues<SessionActivity>();
        Assert.Equal(5, valeurs.Length);   // garde anti-muette : un parcours vide ne prouverait rien

        var visibles = valeurs.Where(AffichageSessions.AUneLigne)
                              .Select(AffichageSessions.Etat)
                              .ToHashSet(StringComparer.Ordinal);
        var attendus = new[] { "Réflexion", "En attente", "En attente ?" };
        Assert.True(visibles.SetEquals(attendus),
            "Le widget doit parler en trois mots exactement. Mots produits : « "
            + string.Join(" », « ", visibles.OrderBy(m => m, StringComparer.Ordinal)) + " »");

        // L'indéterminé n'a pas de ligne ; son mot n'est lu que par le rapport de diagnostic.
        Assert.Equal("indéterminé", AffichageSessions.Etat(SessionActivity.Unknown));
    }

    /// <summary>EVT-04 — sans ce parcours, un futur état pourrait naître MUET : le `_` du switch de libellés
    /// l'absorberait en silence, et le rapport dirait « indéterminé » pour quelque chose qui ne l'est pas.
    /// C'est exactement ce qui serait arrivé à l'attente déduite si personne ne lui avait écrit de mot.</summary>
    [Fact]
    public void Chaque_valeur_de_l_enumeration_a_un_libelle_non_vide()
    {
        var valeurs = Enum.GetValues<SessionActivity>();
        Assert.Equal(5, valeurs.Length);   // garde anti-muette : un parcours vide ne prouverait rien

        foreach (var a in valeurs)
            Assert.False(string.IsNullOrWhiteSpace(AffichageSessions.Etat(a)), $"libellé vide pour {a}");
    }

    /// <summary>Garde de COMPACITÉ du widget, mécanique et falsifiable : huit gabarits affichent ce libellé
    /// sur une seule ligne, et « En attente ? » (douze caractères) est désormais le plus long des mots à
    /// l'écran.</summary>
    [Fact]
    public void Aucun_libelle_d_etat_ne_depasse_seize_caracteres()
    {
        foreach (var a in Enum.GetValues<SessionActivity>())
        {
            var libelle = AffichageSessions.Etat(a);
            Assert.True(libelle.Length <= 16, $"libellé trop long pour {a} : « {libelle} » ({libelle.Length} car.)");
        }
    }

    /// <summary>Les cinq rangs de l'ORDRE D'ÉCRAN, et de lui seul (LIB-04) : le widget et le rapport de
    /// diagnostic le lisent, l'arbitrage entre sources NE le lit PAS (il a son propre rang, figé — réserve R4
    /// de l'audit v1.6, tenue par <c>ArbitrageSessionsTests</c>). Une attente, même déduite, passe devant un
    /// travail ; l'indéterminé ferme la marche.</summary>
    [Theory]
    [InlineData(SessionActivity.WaitingAttention, 0)]
    [InlineData(SessionActivity.WaitingTurn, 1)]
    [InlineData(SessionActivity.WaitingDeduced, 2)]
    [InlineData(SessionActivity.Working, 3)]
    [InlineData(SessionActivity.Unknown, 4)]
    public void Chaque_etat_a_son_rang_d_urgence(SessionActivity a, int attendu)
        => Assert.Equal(attendu, AffichageSessions.Urgence(a));

    /// <summary>LIB-04, versant ÉCRAN : une attente déduite passe DEVANT la réflexion — c'est ce qui la rend
    /// visible. Le même instant pour les trois, pour que seul le rang d'urgence puisse trancher.
    /// <para>La règle d'origine, « une déduction ne bat jamais une observation », n'a pas disparu : elle vaut
    /// désormais pour l'ARBITRAGE entre sources, et c'est le test R4
    /// (<c>ArbitrageSessionsTests.Remonter_la_deduction_a_l_ecran_ne_change_aucun_arbitrage</c>) qui la porte.</para></summary>
    [Fact]
    public void A_l_ecran_une_attente_deduite_passe_devant_la_reflexion()
    {
        var ordre = AffichageSessions.Ordonner(new[]
        {
            new SessionSnapshot("deduit",    "p", SessionActivity.WaitingDeduced, null, Maintenant),
            new SessionSnapshot("travail",   "p", SessionActivity.Working, null, Maintenant),
            new SessionSnapshot("attention", "p", SessionActivity.WaitingAttention, null, Maintenant),
        }).Select(s => s.SessionId).ToArray();

        Assert.Equal(new[] { "attention", "deduit", "travail" }, ordre);
    }

    /// <summary>LIB-04 en entier : l'urgence d'abord (question, tour fini, attente déduite, réflexion), la
    /// fraîcheur ensuite, à l'intérieur de chaque rang. Les âges sont choisis pour que la fraîcheur SEULE
    /// donne un autre ordre : une question de trois heures reste en tête, un travail frais reste derrière
    /// une déduction de vingt-cinq minutes.</summary>
    [Fact]
    public void L_ordre_d_ecran_dit_l_urgence_puis_la_fraicheur()
    {
        var ordre = AffichageSessions.Ordonner(new[]
        {
            new SessionSnapshot("travail-vieux", "p", SessionActivity.Working, null, Maintenant.AddMinutes(-30)),
            new SessionSnapshot("tour-vieux",    "p", SessionActivity.WaitingTurn, null, Maintenant.AddHours(-2)),
            new SessionSnapshot("deduit",        "p", SessionActivity.WaitingDeduced, null, Maintenant.AddMinutes(-25)),
            new SessionSnapshot("travail-frais", "p", SessionActivity.Working, null, Maintenant),
            new SessionSnapshot("question",      "p", SessionActivity.WaitingAttention, null, Maintenant.AddHours(-3)),
            new SessionSnapshot("tour-frais",    "p", SessionActivity.WaitingTurn, null, Maintenant.AddMinutes(-1)),
        }).Select(s => s.SessionId).ToArray();

        Assert.Equal(new[] { "question", "tour-frais", "tour-vieux", "deduit", "travail-frais", "travail-vieux" }, ordre);
    }

    [Theory]
    [InlineData(30, "à l'instant")]
    [InlineData(90, "il y a 1 min")]
    [InlineData(3600, "il y a 1 h")]
    public void L_anciennete_se_dit_au_grain_du_widget(int secondes, string attendu)
        => Assert.Equal(attendu, AffichageSessions.Age(TimeSpan.FromSeconds(secondes)));

    /// <summary>FUS-02 — un écart d'âge n'est pas une ancienneté, et le zéro (égalité d'âge) doit se voir.</summary>
    [Theory]
    [InlineData(0, "0 s")]
    [InlineData(45, "45 s")]
    [InlineData(420, "7 min")]
    [InlineData(25200, "7 h")]
    public void L_ecart_d_age_se_dit_en_distance_pas_en_instant(int secondes, string attendu)
        => Assert.Equal(attendu, AffichageSessions.Ecart(TimeSpan.FromSeconds(secondes)));

    [Fact]
    public void L_ordre_place_l_attention_d_abord_puis_le_plus_recent()
    {
        var ordre = AffichageSessions.Ordonner(new[]
        {
            new SessionSnapshot("vieux-travail", "p", SessionActivity.Working, null, Maintenant.AddMinutes(-30)),
            new SessionSnapshot("inconnu",       "p", SessionActivity.Unknown, null, Maintenant),
            new SessionSnapshot("tour",          "p", SessionActivity.WaitingTurn, null, Maintenant),
            new SessionSnapshot("a-toi",         "p", SessionActivity.WaitingAttention, null, Maintenant.AddHours(-2)),
            new SessionSnapshot("travail-frais", "p", SessionActivity.Working, null, Maintenant),
        }).Select(s => s.SessionId).ToArray();

        Assert.Equal(new[] { "a-toi", "tour", "travail-frais", "vieux-travail", "inconnu" }, ordre);
    }

    [Fact]
    public void Reordonner_une_liste_deja_ordonnee_ne_la_change_pas()
    {
        var source = new[]
        {
            new SessionSnapshot("a", "p", SessionActivity.WaitingTurn, null, Maintenant),
            new SessionSnapshot("b", "p", SessionActivity.Working, null, Maintenant),
        };
        var une = AffichageSessions.Ordonner(source).Select(s => s.SessionId);
        var deux = AffichageSessions.Ordonner(AffichageSessions.Ordonner(source)).Select(s => s.SessionId);
        Assert.Equal(une, deux);
    }

    [Fact]
    public void Le_widget_affiche_ce_que_la_couche_neutre_produit()
    {
        // Preuve de NON-RÉGRESSION du widget : mêmes libellés, même ordre qu'avant l'extraction.
        var source = new SourceFixe(
            new SessionSnapshot("s-work", "chronos", SessionActivity.Working, null, Maintenant),
            new SessionSnapshot("s-att",  "overlay", SessionActivity.WaitingAttention, "permission_prompt",
                                Maintenant.AddMinutes(-5)));

        var vm = new SessionsViewModel(
            new SessionMonitor(TempDir(), source, new ArchiveStore(TempFichier())),
            new FakeClock(Maintenant), new ArchiveStore(TempFichier()),
            new TreatedStore(System.IO.Path.Combine(TempDir(), "t.json"), new FakeClock(Maintenant)));
        vm.Refresh(Maintenant);

        Assert.Equal(new[] { "En attente", "Réflexion" }, vm.Items.Select(i => i.StateText).ToArray());
        Assert.Equal(new[] { "il y a 5 min", "à l'instant" }, vm.Items.Select(i => i.Detail).ToArray());
        Assert.Equal(1, vm.WaitingCount);
    }

    /// <summary>
    /// EVT-04, versant ÉCRAN. La déduction doit être VISIBLE — le critère n°3 du ROADMAP exige un état
    /// juste ET visible, et deux gabarits estompent les fantômes jusqu'à 0,22 d'opacité : une session qui
    /// m'attend peut-être ne doit pas être la plus effacée de l'écran.
    ///
    /// <para>Elle rejoint donc la famille VISUELLE des attentes (<c>IsTurn</c>), et c'est un choix de
    /// FORME, pas une affirmation : un drapeau de gabarit ne dit rien, l'affirmation est dans
    /// <c>StateText</c>, et c'est lui qui porte l'interrogation.</para>
    /// </summary>
    [Fact]
    public void Une_session_deduite_est_visible_ambre_et_dit_sa_deduction()
    {
        var vm = VmAvec(new SessionSnapshot("s-deduit", "interrompu", SessionActivity.WaitingDeduced, null,
                                            Maintenant.AddMinutes(-25)));

        var item = Assert.Single(vm.Items);

        Assert.Equal("En attente ?", item.StateText);     // le libellé DIT qu'il déduit
        Assert.False(item.IsGhost);                       // …et il n'est pas estompé comme un inconnu
        Assert.False(item.IsWorking);                     // ni présenté comme un travail en cours
        Assert.False(item.IsAttention);                   // ni comme une demande OBSERVÉE
        Assert.True(item.IsTurn);                         // la famille visuelle des attentes
        Assert.True(item.IsWaiting);

        // La rampe AMBRE, celle des attentes — jamais le vert du travail. Comparée au pinceau qu'un
        // WaitingTurn reçoit du même thème : c'est le seul moyen de l'asserter sans recopier une couleur.
        // (La comparaison passait autrefois par le gris d'une session inconnue ; depuis LIB-01 l'état
        // indéterminé n'a plus de ligne dans le widget, donc plus de pinceau à comparer.)
        var ambre = Assert.Single(VmAvec(new SessionSnapshot("s-tour", "p", SessionActivity.WaitingTurn, null,
                                                             Maintenant)).Items).StateBrush;
        var vert = Assert.Single(VmAvec(new SessionSnapshot("s-travail", "p", SessionActivity.Working, null,
                                                            Maintenant)).Items).StateBrush;
        Assert.Equal(ambre.ToString(), item.StateBrush.ToString());
        Assert.NotEqual(vert.ToString(), item.StateBrush.ToString());
    }

    /// <summary>
    /// Le compteur sert à ALERTER. Taire une attente probable serait pire que l'annoncer avec un point
    /// d'interrogation : l'utilisateur verrait un compteur à zéro pendant qu'une session l'attend.
    /// </summary>
    [Fact]
    public void Le_compteur_d_attente_inclut_la_deduction()
    {
        var vm = VmAvec(
            new SessionSnapshot("s-deduit",  "interrompu", SessionActivity.WaitingDeduced, null, Maintenant.AddMinutes(-25)),
            new SessionSnapshot("s-tour",    "p",          SessionActivity.WaitingTurn, null, Maintenant),
            new SessionSnapshot("s-travail", "p",          SessionActivity.Working, null, Maintenant),
            new SessionSnapshot("s-inconnu", "p",          SessionActivity.Unknown, null, Maintenant));

        // LIB-01 : l'indéterminé n'a plus de ligne — le moniteur le masque, donc le compteur total ne le
        // compte pas, et le geste « Tout marquer traité (N) » non plus.
        Assert.Equal(3, vm.TotalCount);
        Assert.DoesNotContain(vm.Items, i => i.SessionId == "s-inconnu");
        Assert.Equal(2, vm.WaitingCount);   // la déduction compte ; l'inconnu et le travail, non
        Assert.True(vm.HasWaiting);
    }

    /// <summary>
    /// LIB-03 — IL N'Y A QU'UN prédicat « est une attente », et il porte un nom : c'est le point d'entrée de
    /// la règle « lue » (phase 30). Le détecteur de traitement garde sa propre copie, parce qu'une garde
    /// documentaire du contrat des hooks en lit le texte ; ce test tient les deux ÉGAUX, état par état, pour
    /// qu'aucune dérive ne puisse s'installer entre ce que l'écran appelle une attente et ce que le détecteur
    /// appelle une attente. C'est exactement la divergence que la phase 26 a dû refermer.
    /// </summary>
    [Fact]
    public void EstUneAttente_dit_exactement_ce_que_dit_le_detecteur()
    {
        var valeurs = Enum.GetValues<SessionActivity>();
        Assert.Equal(5, valeurs.Length);   // garde anti-muette : un parcours vide ne prouverait rien

        foreach (var a in valeurs)
            Assert.True(AffichageSessions.EstUneAttente(a) == SessionTreatmentTracker.EstAttente(a),
                $"le prédicat d'écran et celui du détecteur divergent sur {a}");

        // …et ce qu'ils disent ENSEMBLE est le bon partage : trois attentes, deux non-attentes.
        Assert.Equal(
            new[] { SessionActivity.WaitingAttention, SessionActivity.WaitingDeduced, SessionActivity.WaitingTurn },
            valeurs.Where(AffichageSessions.EstUneAttente).OrderBy(a => a.ToString()).ToArray());
        Assert.Contains(valeurs, a => !AffichageSessions.EstUneAttente(a));
    }

    /// <summary>LIB-01 — seul l'état indéterminé n'a pas de ligne dans le widget. Les quatre autres en ont
    /// une, attente déduite comprise : une session qui m'attend peut-être ne disparaît pas.</summary>
    [Fact]
    public void Seul_l_etat_indetermine_n_a_pas_de_ligne()
    {
        var valeurs = Enum.GetValues<SessionActivity>();
        Assert.Equal(5, valeurs.Length);   // garde anti-muette

        var sansLigne = valeurs.Where(a => !AffichageSessions.AUneLigne(a)).ToArray();
        Assert.Equal(new[] { SessionActivity.Unknown }, sansLigne);
    }

    /// <summary>Un ViewModel dont la source de sessions est substituée et les magasins temporaires.</summary>
    private static SessionsViewModel VmAvec(params SessionSnapshot[] snaps)
    {
        var vm = new SessionsViewModel(
            new SessionMonitor(TempDir(), new SourceFixe(snaps), new ArchiveStore(TempFichier())),
            new FakeClock(Maintenant), new ArchiveStore(TempFichier()),
            new TreatedStore(System.IO.Path.Combine(TempDir(), "t.json"), new FakeClock(Maintenant)));
        vm.Refresh(Maintenant);
        return vm;
    }

    private sealed class SourceFixe : ISessionSource
    {
        private readonly IReadOnlyList<SessionSnapshot> _snaps;
        public SourceFixe(params SessionSnapshot[] snaps) => _snaps = snaps;
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => _snaps;
    }

    private static string TempDir()
    {
        var d = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "chronos-aff-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(d);
        return d;
    }

    private static string TempFichier() => System.IO.Path.Combine(TempDir(), "magasin.json");
}
