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

    // --- FUS-01 : les cinq lignes MESURÉES le 2026-09-12, rejouées contre les classes réelles ---
    //
    // Vérité terrain commune aux cinq : le modèle TRAVAILLE, le transcript a été écrit 10 secondes plus
    // tôt. Ce qui change d'une ligne à l'autre, c'est le fichier de hook qui traîne à côté.
    //
    // Trois de ces cinq tests étaient ROUGES avant le correctif (les trois inversions du ROADMAP). Le
    // premier est un TÉMOIN — il passait déjà —, le dernier un CONTRÔLE de non-régression dont la seule
    // assertion neuve est « 0 désaccord ». Écrire que les cinq seraient rouges serait exactement le genre
    // d'affirmation non observée que ce milestone bannit.

    private const string Mesuree = "session-mesuree";

    /// <summary>Le transcript frais et CORRECT : le modèle travaille, il l'a écrit il y a 10 secondes.</summary>
    private static SourceFixe TranscriptFrais()
        => new(Snap(Mesuree, SessionActivity.Working, Maintenant.AddSeconds(-10)));

    /// <summary>Écrit un fichier d'état de hook au format RÉEL, dans un dossier TEMPORAIRE.</summary>
    private static void EcritEtat(string dir, string id, SessionActivity a, DateTimeOffset maj, string? motif = null)
        => File.WriteAllText(Path.Combine(dir, id + ".json"),
               SessionHookProcessor.BuildStateJson(id, "Proj-" + id, a, motif, maj.ToUnixTimeMilliseconds()));

    private static LectureSessions Lire(string hookDir)
        => new SessionMonitor(hookDir, TranscriptFrais(), new ArchiveStore(TempFichier())).Inspecter(Maintenant);

    [Fact]
    public void Un_transcript_frais_seul_est_annonce_en_cours()
    {
        // TÉMOIN : vert avant comme après. Sans lui, les quatre suivants ne prouveraient rien — on ne
        // saurait pas si le bon état vient de l'arbitrage ou d'une source déjà fautive.
        var lecture = Lire(TempDir());

        Assert.Equal(SessionActivity.Working, Assert.Single(lecture.Visibles).Activity);
        Assert.Empty(lecture.Desaccords);
    }

    [Fact]
    public void Un_hook_Working_de_25_min_ne_rend_plus_un_transcript_frais_inconnu()
    {
        // ROUGE AVANT LE CORRECTIF. Le hook, périmé au-delà du seuil de 20 min, était ramené à « inconnu »
        // — et c'est cet « inconnu » qui écrasait un « en cours » frais ET correct.
        var dir = TempDir();
        EcritEtat(dir, Mesuree, SessionActivity.Working, Maintenant.AddMinutes(-25));

        var lecture = Lire(dir);

        Assert.Equal(SessionActivity.Working, Assert.Single(lecture.Visibles).Activity);
        var desaccord = Assert.Single(lecture.Desaccords);
        Assert.Equal(SessionActivity.Unknown, desaccord.EtatEcarte);
        Assert.Equal(SourceSession.Hook, desaccord.SourceEcartee);
    }

    [Fact]
    public void Un_hook_WaitingTurn_de_7_h_ne_bat_plus_un_transcript_de_10_s()
    {
        // ROUGE AVANT LE CORRECTIF : « tour fini » s'affichait pendant que le modèle travaillait.
        var dir = TempDir();
        EcritEtat(dir, Mesuree, SessionActivity.WaitingTurn, Maintenant.AddHours(-7));

        var lecture = Lire(dir);

        Assert.Equal(SessionActivity.Working, Assert.Single(lecture.Visibles).Activity);
        var desaccord = Assert.Single(lecture.Desaccords);
        Assert.Equal(SessionActivity.WaitingTurn, desaccord.EtatEcarte);
        Assert.Equal(SourceSession.Transcript, desaccord.SourceRetenue);
    }

    [Fact]
    public void Un_hook_WaitingAttention_de_7_h_ne_bat_plus_un_transcript_de_10_s()
    {
        // ROUGE AVANT LE CORRECTIF, et le plus coûteux des trois : « à toi » sans qu'aucune permission
        // n'ait été demandée. Le motif porté par le hook ne change rien — la précision ne bat pas la
        // fraîcheur, elle ne départage que des signaux du MÊME âge.
        var dir = TempDir();
        EcritEtat(dir, Mesuree, SessionActivity.WaitingAttention, Maintenant.AddHours(-7), "permission_prompt");

        var lecture = Lire(dir);

        Assert.Equal(SessionActivity.Working, Assert.Single(lecture.Visibles).Activity);
        var desaccord = Assert.Single(lecture.Desaccords);
        Assert.Equal(SessionActivity.WaitingAttention, desaccord.EtatEcarte);
        Assert.Equal(TimeSpan.FromHours(7) - TimeSpan.FromSeconds(10), desaccord.EcartAge);
    }

    [Fact]
    public void Un_hook_de_9_h_n_est_plus_candidat_du_tout_et_ne_produit_aucun_desaccord()
    {
        // CONTRÔLE DE NON-RÉGRESSION : le résultat était déjà bon, pour une mauvaise raison (le hook
        // s'effaçait de lui-même au-delà du seuil de 8 h). Seule l'assertion « 0 désaccord » est neuve :
        // un fichier écarté pour son âge n'a pas perdu un arbitrage, il n'y a jamais participé.
        var dir = TempDir();
        EcritEtat(dir, Mesuree, SessionActivity.WaitingTurn, Maintenant.AddHours(-9));

        var lecture = Lire(dir);

        Assert.Equal(SessionActivity.Working, Assert.Single(lecture.Visibles).Activity);
        Assert.Empty(lecture.Desaccords);
        Assert.Equal(1, lecture.FichiersEcartesParAnciennete);
    }

    [Fact]
    public void Un_fragment_illisible_est_une_ABSENCE_de_signal_jamais_un_vieux_signal()
    {
        // CONTREPARTIE ASSUMÉE de l'écriture directe livrée en phase 23 : elle tronque la cible avant de
        // la réécrire, donc un lecteur malchanceux lit un fragment. Le compter comme un signal sans date
        // en ferait un « très vieux signal » perdant contre n'importe quoi — une déposition fabriquée là
        // où il n'y a rien à déposer. Il n'est ni écarté pour son âge, ni retenu : il n'existe pas.
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, Mesuree + ".json"),
            "{\"session_id\":\"" + Mesuree + "\",\"project\":\"P\",\"activ");

        var lecture = Lire(dir);

        Assert.Equal(SessionActivity.Working, Assert.Single(lecture.Visibles).Activity);
        Assert.Equal(0, lecture.FichiersEcartesParAnciennete);
        Assert.Empty(lecture.Desaccords);
    }

    [Fact]
    public void Un_hook_sans_updated_at_ne_gagne_jamais()
    {
        // JSON parfaitement VALIDE, mais muet sur son instant. Un signal qui ne sait pas quand il a été
        // observé ne peut pas prétendre l'emporter sur un signal daté : faute de date, il tombe sous le
        // seuil d'ancienneté et se compte comme écarté pour cela.
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, Mesuree + ".json"),
            "{\"session_id\":\"" + Mesuree + "\",\"project\":\"P\",\"activity\":\"WaitingAttention\"}");

        var lecture = Lire(dir);

        Assert.Equal(SessionActivity.Working, Assert.Single(lecture.Visibles).Activity);
        Assert.Equal(1, lecture.FichiersEcartesParAnciennete);
        Assert.Empty(lecture.Desaccords);
    }

    // --- FUS-01, versant « ordre » : le critère n°2 au niveau du MONITEUR ---

    /// <summary>Corpus IMPOSÉ : deux sessions EX AEQUO sur (urgence, horodatage). Un corpus aux couples
    /// tous distincts aurait été vert avant comme après — une garde muette. C'est l'égalité qui prouve.</summary>
    private static SessionSnapshot[] Trois() => new[]
    {
        Snap("b-deux",   SessionActivity.Working,     Maintenant.AddMinutes(-1)),  // ex aequo…
        Snap("a-un",     SessionActivity.Working,     Maintenant.AddMinutes(-1)),  // …avec b-deux
        Snap("c-trois",  SessionActivity.WaitingTurn, Maintenant.AddMinutes(-1)),
    };

    private static IEnumerable<T[]> Permutations<T>(T[] source)
    {
        if (source.Length <= 1) { yield return source; yield break; }
        for (var i = 0; i < source.Length; i++)
        {
            var tete = source[i];
            var reste = source.Take(i).Concat(source.Skip(i + 1)).ToArray();
            foreach (var suite in Permutations(reste))
                yield return new[] { tete }.Concat(suite).ToArray();
        }
    }

    private static string Sequence(IEnumerable<SessionSnapshot> sessions)
        => string.Join(" | ", sessions.Select(s => $"{s.SessionId}={s.Activity}"));

    [Fact]
    public void Permuter_l_ordre_rendu_par_la_source_ne_change_pas_l_affichage()
    {
        // Deux couches sont canonisées : celle que l'arbitrage contrôle (Visibles) ET celle que l'écran
        // montre (Ordonner). Avant le correctif, une entrée indexée par identifiant rendait l'ordre
        // d'insertion, et le tri d'affichage est stable : les deux ex aequo sortaient dans l'ordre où la
        // source les avait rendues. Le test était donc ROUGE.
        var distincts = new HashSet<string>(StringComparer.Ordinal);
        var distinctsAffiches = new HashSet<string>(StringComparer.Ordinal);
        var vues = 0;

        foreach (var permutation in Permutations(Trois()))
        {
            var visibles = new SessionMonitor(TempDir(), new SourceFixe(permutation),
                                              new ArchiveStore(TempFichier())).Inspecter(Maintenant).Visibles;

            distincts.Add(Sequence(visibles));
            distinctsAffiches.Add(Sequence(AffichageSessions.Ordonner(visibles)));
            vues++;
        }

        Assert.Equal(6, vues);                  // 3! — une garde qui n'énumérerait rien serait muette
        Assert.Single(distincts);               // la couche que l'arbitrage contrôle
        Assert.Single(distinctsAffiches);       // …et ce que l'écran montre
    }
}
