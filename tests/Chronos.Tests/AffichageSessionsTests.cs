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
    public void Chaque_etat_a_son_libelle(SessionActivity a, string attendu)
        => Assert.Equal(attendu, AffichageSessions.Etat(a));

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
            new FakeClock(Maintenant), new ArchiveStore(TempFichier()));
        vm.Refresh(Maintenant);

        Assert.Equal(new[] { "à toi", "en cours" }, vm.Items.Select(i => i.StateText).ToArray());
        Assert.Equal(new[] { "il y a 5 min", "à l'instant" }, vm.Items.Select(i => i.Detail).ToArray());
        Assert.Equal(1, vm.WaitingCount);
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
