using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// OBS-01 — le moniteur du widget sait dire ce qu'il MASQUE et par quel filtre, et <c>Read</c> n'est plus
/// qu'une projection de cette lecture complète.
///
/// Le cas qui justifie tout : le 2026-09-12, e465420e (PROJET ADVANCED SHEET) était une session VIVANTE en
/// attente de permission depuis 10 h. Le widget ne la montrait nulle part, le diagnostic non plus. Elle
/// n'était pas absente : son fichier de hook avait franchi le seuil de 8 h, le transcript avait repris la
/// main, et treated.json la masquait pour 6 h. Trois faits, aucun visible.
///
/// Tous les tests écrivent dans des dossiers TEMPORAIRES : aucun ne touche le vrai %APPDATA%\Chronos.
/// </summary>
public class InspectionSessionsTests
{
    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-insp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // garde anti-accident
        return d;
    }

    private static string TempFichier() => Path.Combine(TempDir(), "magasin.json");

    private sealed class SourceFixe : ISessionSource
    {
        private readonly IReadOnlyList<SessionSnapshot> _snaps;
        public SourceFixe(params SessionSnapshot[] snaps) => _snaps = snaps;
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => _snaps;
    }

    private static readonly DateTimeOffset Maintenant = new(2026, 9, 12, 13, 28, 0, TimeSpan.Zero);

    // TreatedStore et ArchiveStore purgent leurs entrées au-delà d'un TTL de 6 h mesuré sur l'HORLOGE
    // RÉELLE (aucune horloge injectable). Un horodatage figé dans le passé rendrait ces tests verts le
    // jour de leur écriture puis rouges six heures plus tard : ce qui est écrit dans le magasin porte donc
    // l'heure du système. Le filtre ne regarde que la présence de la clé — la valeur n'est jamais assertée.
    private static long EcritMaintenant() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static SessionSnapshot Snap(string id, SessionActivity a, DateTimeOffset maj)
        => new(id, "Proj-" + id, a, null, maj);

    [Fact]
    public void Une_session_archivee_est_masquee_et_le_motif_le_dit()
    {
        var archive = new ArchiveStore(TempFichier());
        archive.Add("archivee");
        var monitor = new SessionMonitor(TempDir(),
            new SourceFixe(Snap("archivee", SessionActivity.WaitingTurn, Maintenant),
                           Snap("visible",  SessionActivity.Working,     Maintenant)),
            archive);

        var lecture = monitor.Inspecter(Maintenant);

        Assert.Equal("visible", Assert.Single(lecture.Visibles).SessionId);
        var masquee = Assert.Single(lecture.Masquees);
        Assert.Equal("archivee", masquee.Session.SessionId);
        Assert.Equal(MotifMasquage.Archivee, masquee.Motif);
    }

    [Fact]
    public void Une_session_traitee_est_masquee_et_le_motif_le_dit()
    {
        var treated = new TreatedStore(TempFichier());
        treated.Set("traitee", EcritMaintenant());
        var monitor = new SessionMonitor(TempDir(),
            new SourceFixe(Snap("traitee", SessionActivity.WaitingAttention, Maintenant),
                           Snap("visible", SessionActivity.Working,          Maintenant)),
            new ArchiveStore(TempFichier()), treated);

        var lecture = monitor.Inspecter(Maintenant);

        Assert.Equal("visible", Assert.Single(lecture.Visibles).SessionId);
        var masquee = Assert.Single(lecture.Masquees);
        Assert.Equal("traitee", masquee.Session.SessionId);
        Assert.Equal(MotifMasquage.Traitee, masquee.Motif);
    }

    [Fact]
    public void Archivee_ET_traitee_ne_produit_qu_une_entree_et_l_archivage_prime()
    {
        // Le geste explicite de l'utilisateur prime sur l'hystérésis automatique — et surtout, la session
        // ne doit pas être comptée deux fois : le rapport annoncerait deux masquages pour une session.
        var archive = new ArchiveStore(TempFichier());
        archive.Add("double");
        var treated = new TreatedStore(TempFichier());
        treated.Set("double", EcritMaintenant());
        var monitor = new SessionMonitor(TempDir(),
            new SourceFixe(Snap("double", SessionActivity.WaitingTurn, Maintenant)), archive, treated);

        var lecture = monitor.Inspecter(Maintenant);

        Assert.Empty(lecture.Visibles);
        Assert.Equal(MotifMasquage.Archivee, Assert.Single(lecture.Masquees).Motif);
    }

    [Fact]
    public void Le_cas_e465420e_devient_lisible_en_une_lecture()
    {
        // Reconstitution du relevé du 2026-09-12T13:28Z, aux chiffres près :
        //   • transcript frais (10 min) → Working ;
        //   • fichier de hook du MÊME identifiant, figé depuis 625 min → au-delà du seuil de 480 min ;
        //   • treated.json portant l'identifiant → la session disparaît de l'écran.
        const string Id = "e465420e-83e0-428f-97f1-f0174c0848fc";
        var hookDir = TempDir();
        File.WriteAllText(Path.Combine(hookDir, Id + ".json"),
            SessionHookProcessor.BuildStateJson(Id, "PROJET ADVANCED SHEET", SessionActivity.WaitingAttention,
                "permission_prompt", Maintenant.AddMinutes(-625).ToUnixTimeMilliseconds()));

        var treated = new TreatedStore(TempFichier());
        treated.Set(Id, EcritMaintenant());

        var monitor = new SessionMonitor(hookDir,
            new SourceFixe(Snap(Id, SessionActivity.Working, Maintenant.AddMinutes(-10))),
            new ArchiveStore(TempFichier()), treated);

        var lecture = monitor.Inspecter(Maintenant);

        Assert.Empty(lecture.Visibles);                                  // le widget ne montre RIEN
        Assert.Equal(1, lecture.FichiersEcartesParAnciennete);           // le hook de 625 min est tombé
        var masquee = Assert.Single(lecture.Masquees);
        Assert.Equal(Id, masquee.Session.SessionId);
        Assert.Equal(MotifMasquage.Traitee, masquee.Motif);              // …et on sait enfin POURQUOI
    }

    [Fact]
    public void Un_fichier_illisible_n_est_pas_compte_comme_ecarte_pour_anciennete()
    {
        var hookDir = TempDir();
        File.WriteAllText(Path.Combine(hookDir, "corrompu.json"), "ceci n'est pas du JSON {{{");

        var lecture = new SessionMonitor(hookDir, new SourceFixe(), new ArchiveStore(TempFichier()))
            .Inspecter(Maintenant);

        Assert.Empty(lecture.Visibles);
        Assert.Empty(lecture.Masquees);
        Assert.Equal(0, lecture.FichiersEcartesParAnciennete);   // illisible ≠ périmé
    }

    [Fact]
    public void Read_rend_exactement_les_sessions_visibles_d_Inspecter()
    {
        var treated = new TreatedStore(TempFichier());
        treated.Set("cachee", EcritMaintenant());
        var monitor = new SessionMonitor(TempDir(),
            new SourceFixe(Snap("a", SessionActivity.Working, Maintenant),
                           Snap("b", SessionActivity.WaitingTurn, Maintenant.AddMinutes(-5)),
                           Snap("cachee", SessionActivity.WaitingTurn, Maintenant)),
            new ArchiveStore(TempFichier()), treated);

        Assert.Equal(monitor.Inspecter(Maintenant).Visibles.Select(s => s.SessionId),
                     monitor.Read(Maintenant).Select(s => s.SessionId));
    }
}
