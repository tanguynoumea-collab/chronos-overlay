using System.IO;
using Chronos.Services;
using Chronos.ViewModels;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// TRT-03 — le GESTE EXPLICITE. L'acquittement par focus est tombé en phase 21 (il exigeait une origine
/// « app bureau » qu'une session Claude Code n'a jamais) : il ne reste que le menu contextuel, et ces cas
/// éprouvent les deux commandes qu'il porte, sans jamais monter la moindre fenêtre.
///
/// <para>Deux exigences pèsent sur leur conception. L'EFFET DE MASSE d'abord : sur un corpus vivant de
/// 66 états, vingt sessions sur cinquante-quatre basculent ensemble en attente déduite — un geste par
/// session sur vingt n'est pas un geste, d'où la seconde commande et le nombre porté par son libellé. La
/// PRUDENCE ensuite : le geste de masse écrit dans le magasin RÉVERSIBLE, jamais dans celui des archives ;
/// il ne peut donc pas devenir destructif.</para>
///
/// <para>Aucun de ces cas n'est un test ROUGE-AVANT : ils portent sur du code qui n'existait pas et sont
/// écrits dans la même tâche que lui — un commit qui ne compile pas est interdit, et un test qui ne
/// compile pas n'est pas un test rouge. Le dernier a un statut à part, dit chez lui.</para>
///
/// <para>Aucun accès au vrai magasin de l'utilisateur : sources substituées, chemins temporaires.</para>
/// </summary>
public class GesteTraiteTests
{
    private static readonly DateTimeOffset T = new(2026, 9, 12, 21, 0, 0, TimeSpan.Zero);

    // Source de base substituée, MUTABLE : le cas de la réversibilité doit pouvoir faire redire à la même
    // session, plus tard, qu'elle attend toujours.
    private sealed class SourceMutable : ISessionSource
    {
        public List<SessionSnapshot> Snaps { get; } = new();
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => Snaps;
    }

    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-geste-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // aucun chemin ne peut viser le magasin de l'utilisateur
        return d;
    }

    private sealed record Montage(SessionsViewModel Vm, TreatedStore Treated, SourceMutable Source, FakeClock Horloge);

    /// <summary>
    /// Montage commun. Le <see cref="TreatedStore"/> est PARTAGÉ entre le moniteur et le ViewModel : sans
    /// ce partage, le filtre du moniteur ne verrait pas ce que le geste écrit, et la session ne
    /// disparaîtrait pas. Le détecteur n'est branché que là où c'est LUI qu'on éprouve.
    /// </summary>
    private static Montage Monter(bool avecDetecteur, params SessionSnapshot[] snaps)
    {
        var horloge = new FakeClock(T);
        var treated = new TreatedStore(Path.Combine(TempDir(), "t.json"), horloge);
        var tracker = avecDetecteur ? new SessionTreatmentTracker(treated) : null;

        var source = new SourceMutable();
        source.Snaps.AddRange(snaps);

        var monitor = new SessionMonitor(TempDir(), source,
            new ArchiveStore(Path.Combine(TempDir(), "a.json"), horloge), treated, tracker);
        var vm = new SessionsViewModel(monitor, horloge,
            new ArchiveStore(Path.Combine(TempDir(), "b.json"), horloge), treated);
        vm.Refresh(T);
        return new Montage(vm, treated, source, horloge);
    }

    private static SessionSnapshot Att(string id, DateTimeOffset quand)
        => new(id, "p", SessionActivity.WaitingAttention, null, quand);

    private static SessionSnapshot[] Cinq()
        => new[] { Att("s1", T), Att("s2", T.AddMinutes(-1)), Att("s3", T.AddMinutes(-2)),
                   Att("s4", T.AddMinutes(-3)), Att("s5", T.AddMinutes(-4)) };

    [Fact]
    public void Marquer_traitee_fait_disparaitre_la_session_immediatement()
    {
        var m = Monter(avecDetecteur: false, Cinq());
        Assert.Equal(5, m.Vm.Items.Count);   // une liste vide ne prouverait rien

        var cible = m.Vm.Items[0].SessionId;
        m.Vm.Items[0].MarquerTraiteeCommand.Execute(null);

        Assert.Equal(4, m.Vm.Items.Count);
        Assert.DoesNotContain(m.Vm.Items, i => i.SessionId == cible);
        Assert.Contains(cible, m.Treated.Load().Keys);
    }

    /// <summary>
    /// L'effet de masse, mesuré le 2026-09-12 : vingt sessions sur cinquante-quatre basculent ensemble en
    /// attente déduite. Vider le widget une session à la fois ne serait pas un geste.
    /// </summary>
    [Fact]
    public void Marquer_tout_traite_vide_le_widget_en_un_geste()
    {
        var m = Monter(avecDetecteur: false, Cinq());
        Assert.Equal(5, m.Vm.Items.Count);

        m.Vm.Items[0].MarquerToutTraiteCommand.Execute(null);

        Assert.Empty(m.Vm.Items);
        var magasin = m.Treated.Load();
        Assert.Equal(5, magasin.Count);
        foreach (var id in new[] { "s1", "s2", "s3", "s4", "s5" })
            Assert.Contains(id, magasin.Keys);
    }

    /// <summary>
    /// Un geste de masse qui ne dit pas sur combien il porte est exactement l'ambiguïté que ce projet
    /// s'interdit — et, encadré par un libellé réversible et un libellé définitif, un troisième libellé
    /// muet sur le retour de la session serait la même faute.
    /// </summary>
    [Fact]
    public void Le_libelle_du_geste_de_masse_porte_le_nombre_de_sessions()
    {
        var m = Monter(avecDetecteur: false, Cinq());
        Assert.Equal(5, m.Vm.Items.Count);

        Assert.Contains("5", m.Vm.Items[0].ToutTraiterLibelle);
        Assert.Contains("reviennent", m.Vm.Items[0].ToutTraiterLibelle);
    }

    /// <summary>
    /// La preuve LITTÉRALE du libellé « revient si elle me redemande » : ce n'est pas une promesse, c'est
    /// la description de NET-03. Le geste écrit l'instant de l'ÉPISODE que l'utilisateur vient de voir ;
    /// un signal plus récent le dépasse, et le détecteur retire l'entrée.
    /// </summary>
    [Fact]
    public void Le_geste_est_reversible_et_le_libelle_ne_ment_pas()
    {
        var t0 = T.AddMinutes(-10);
        var m = Monter(avecDetecteur: true, Att("s-rev", t0));
        Assert.Single(m.Vm.Items);

        m.Vm.Items[0].MarquerTraiteeCommand.Execute(null);
        Assert.Empty(m.Vm.Items);
        Assert.Contains("s-rev", m.Treated.Load().Keys);

        // La même session REDEMANDE quelque chose : son signal porte un instant plus récent.
        m.Source.Snaps[0] = Att("s-rev", T.AddMinutes(-1));
        m.Vm.Refresh(T);

        Assert.Contains(m.Vm.Items, i => i.SessionId == "s-rev");
        Assert.DoesNotContain("s-rev", m.Treated.Load().Keys);
    }

    /// <summary>
    /// STATUT À PART. Ce cas est ROUGE sans le point (b bis) du plan 26-01 (la borne du magasin des
    /// traitées passée de six à vingt-quatre heures) et VERT avec — il n'est donc pas mesurable rouge dans
    /// l'ordre des vagues, (b bis) le précède de deux plans.
    ///
    /// <para>Le signal date de sept heures : le moniteur le lit encore (il va jusqu'à huit), mais l'ancienne
    /// borne de six heures aurait écarté à la lecture l'entrée que le geste vient d'écrire —
    /// <c>Set(id, UpdatedAtMs)</c> aurait inscrit un instant vieux de sept heures. Le clic n'aurait rien
    /// fait, EN SILENCE, sur exactement le genre de session dont ce milestone est parti. Les quatre cas
    /// précédents, tous horodatés au frais, ne l'auraient jamais vu.</para>
    /// </summary>
    [Fact]
    public void Marquer_traitee_une_session_qui_attend_depuis_sept_heures_la_fait_disparaitre()
    {
        var m = Monter(avecDetecteur: false,
            Att("vieille", T.AddHours(-7)),                                           // lue, mais hors de l'ancienne borne
            new SessionSnapshot("fraiche", "p", SessionActivity.Working, null, T));

        Assert.Equal(2, m.Vm.Items.Count);   // les deux sont bien affichées avant le geste

        m.Vm.Items.Single(i => i.SessionId == "vieille").MarquerTraiteeCommand.Execute(null);

        Assert.DoesNotContain(m.Vm.Items, i => i.SessionId == "vieille");
        Assert.Contains(m.Vm.Items, i => i.SessionId == "fraiche");
        Assert.Contains("vieille", m.Treated.Load().Keys);
    }
}
