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

    [Theory]
    [InlineData(SessionActivity.WaitingAttention, "à toi")]
    [InlineData(SessionActivity.WaitingTurn, "tour fini")]
    [InlineData(SessionActivity.Working, "en cours")]
    [InlineData(SessionActivity.Unknown, "inconnu")]
    [InlineData(SessionActivity.WaitingDeduced, "à toi ? déduit")]
    public void Chaque_etat_a_son_libelle(SessionActivity a, string attendu)
        => Assert.Equal(attendu, AffichageSessions.Etat(a));

    /// <summary>EVT-04 — sans ce parcours, un futur état pourrait naître MUET : le `_` du switch de libellés
    /// l'absorberait en silence, et le widget afficherait « inconnu » pour quelque chose qui ne l'est pas.
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
    /// sur une seule ligne, et « à toi ? déduit » est le plus long des cinq (quatorze caractères).</summary>
    [Fact]
    public void Aucun_libelle_d_etat_ne_depasse_seize_caracteres()
    {
        foreach (var a in Enum.GetValues<SessionActivity>())
        {
            var libelle = AffichageSessions.Etat(a);
            Assert.True(libelle.Length <= 16, $"libellé trop long pour {a} : « {libelle} » ({libelle.Length} car.)");
        }
    }

    /// <summary>Les cinq rangs, FIGÉS. Une déduction partage le dernier rang avec l'inconnu ; les quatre
    /// rangs hérités ne bougent pas d'un cran.</summary>
    [Theory]
    [InlineData(SessionActivity.WaitingAttention, 0)]
    [InlineData(SessionActivity.WaitingTurn, 1)]
    [InlineData(SessionActivity.Working, 2)]
    [InlineData(SessionActivity.Unknown, 3)]
    [InlineData(SessionActivity.WaitingDeduced, 3)]
    public void Chaque_etat_a_son_rang_d_urgence(SessionActivity a, int attendu)
        => Assert.Equal(attendu, AffichageSessions.Urgence(a));

    /// <summary>EVT-04, la contrainte de conception : une DÉDUCTION ne devance jamais une OBSERVATION.
    /// Le même instant pour les trois, pour que seul le rang d'urgence puisse trancher.</summary>
    [Fact]
    public void Une_deduction_ne_passe_jamais_devant_une_observation()
    {
        var ordre = AffichageSessions.Ordonner(new[]
        {
            new SessionSnapshot("deduit",    "p", SessionActivity.WaitingDeduced, null, Maintenant),
            new SessionSnapshot("travail",   "p", SessionActivity.Working, null, Maintenant),
            new SessionSnapshot("attention", "p", SessionActivity.WaitingAttention, null, Maintenant),
        }).Select(s => s.SessionId).ToArray();

        Assert.Equal(new[] { "attention", "travail", "deduit" }, ordre);
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

        Assert.Equal(new[] { "à toi", "en cours" }, vm.Items.Select(i => i.StateText).ToArray());
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

        Assert.Equal("à toi ? déduit", item.StateText);   // le libellé DIT qu'il déduit
        Assert.False(item.IsGhost);                       // …et il n'est pas estompé comme un inconnu
        Assert.False(item.IsWorking);                     // ni présenté comme un travail en cours
        Assert.False(item.IsAttention);                   // ni comme une demande OBSERVÉE
        Assert.True(item.IsTurn);                         // la famille visuelle des attentes
        Assert.True(item.IsWaiting);

        // La rampe AMBRE, celle des attentes — jamais le gris de l'inconnu. Comparée au pinceau qu'un
        // WaitingTurn reçoit du même thème : c'est le seul moyen de l'asserter sans recopier une couleur.
        var ambre = Assert.Single(VmAvec(new SessionSnapshot("s-tour", "p", SessionActivity.WaitingTurn, null,
                                                             Maintenant)).Items).StateBrush;
        var gris = Assert.Single(VmAvec(new SessionSnapshot("s-inconnu", "p", SessionActivity.Unknown, null,
                                                            Maintenant)).Items).StateBrush;
        Assert.Equal(ambre.ToString(), item.StateBrush.ToString());
        Assert.NotEqual(gris.ToString(), item.StateBrush.ToString());
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

        Assert.Equal(4, vm.TotalCount);
        Assert.Equal(2, vm.WaitingCount);   // la déduction compte ; l'inconnu et le travail, non
        Assert.True(vm.HasWaiting);
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
