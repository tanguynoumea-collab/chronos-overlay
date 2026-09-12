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
    public void Un_hook_Working_de_25_min_ne_rend_plus_un_transcript_frais_deduit()
    {
        // ROUGE AVANT LE CORRECTIF DE LA PHASE 24. Le hook, silencieux au-delà du seuil de 20 min, était
        // ramené à « inconnu » — et c'est cet « inconnu » qui écrasait un « en cours » frais ET correct.
        //
        // EVT-04 change ce que devient le hook silencieux — « à toi ? déduit » au lieu d'« inconnu » — et
        // RIEN D'AUTRE : le verdict retenu (Working, transcript de 10 s) et la source écartée sont
        // inchangés. C'est la preuve que l'arbitrage par fraîcheur n'a pas bougé d'un cran.
        var dir = TempDir();
        EcritEtat(dir, Mesuree, SessionActivity.Working, Maintenant.AddMinutes(-25));

        var lecture = Lire(dir);

        Assert.Equal(SessionActivity.Working, Assert.Single(lecture.Visibles).Activity);
        var desaccord = Assert.Single(lecture.Desaccords);
        Assert.Equal(SessionActivity.WaitingDeduced, desaccord.EtatEcarte);
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

    // --- EVT-03 : le seuil de vingt minutes change de SENS, jamais de valeur ---
    //
    // Avant cette phase, il devinait combien de temps un travail PEUT durer — une devinette, et elle
    // éteignait « en cours » toute seule au bout de vingt minutes. Depuis les battements de cœur, une
    // session qui travaille est RÉAFFIRMÉE à chaque appel d'outil : le même délai mesure désormais le
    // SILENCE des battements. Sa valeur ne bouge pas, et ce n'est pas un oubli — entre le signal d'entrée
    // et le signal de sortie d'un outil long, il ne se passe rien, donc le seuil doit rester large.

    /// <summary>Le moniteur SEUL face aux fichiers d'état : aucune autre source ne vient arbitrer, donc
    /// ce qui est annoncé vient du seuil de silence et de lui seul.</summary>
    private static LectureSessions LireSansTranscript(string hookDir)
        => new SessionMonitor(hookDir, new SourceFixe(), new ArchiveStore(TempFichier())).Inspecter(Maintenant);

    /// <summary>Un ordre de hook produit par le cœur RÉEL, daté d'un instant figé.</summary>
    private static SessionHookResult Ordre(string evenement, DateTimeOffset instant)
        => SessionHookProcessor.Process(evenement,
               "{\"session_id\":\"" + Mesuree + "\",\"cwd\":\"C:/dev/MonProjet\"}",
               instant.ToUnixTimeMilliseconds());

    /// <summary>
    /// LE critère n°2 du ROADMAP — « réfléchit » ne s'éteint plus tout seul —, prouvé par le PIPELINE
    /// RÉEL : le cœur du hook traduit, l'écriture directe applique au disque, le moniteur relit.
    ///
    /// <para>Le premier signal est un TÉMOIN, et il est indispensable : sans lui, on ne saurait pas si
    /// « en cours » vient du battement ou d'une indulgence du seuil. Seul, un démarrage de session
    /// vieux de deux heures et dix minutes ne dit plus rien.</para>
    /// </summary>
    [Fact]
    public void Un_battement_frais_maintient_en_cours_bien_au_dela_d_une_heure()
    {
        var dir = TempDir();

        // TÉMOIN : la session a démarré il y a plus de deux heures, et ce signal-là s'est tu depuis. Ce que
        // le témoin doit prouver, c'est que « en cours » NE TIENT PAS tout seul — depuis EVT-04, ce silence
        // se dit « à toi ? déduit » et non plus « inconnu ». Ce qui compte ici est inchangé : ce n'est PAS
        // Working, donc le « en cours » de la ligne suivante ne peut venir que du battement.
        Assert.True(EcritureEtatSession.Appliquer(dir, Ordre("SessionStart", Maintenant.AddHours(-2).AddMinutes(-10))).Reussi);
        Assert.Equal(SessionActivity.WaitingDeduced, Assert.Single(LireSansTranscript(dir).Visibles).Activity);

        // Un battement d'il y a UNE MINUTE réaffirme l'activité — et il écrase le signal de démarrage.
        Assert.True(EcritureEtatSession.Appliquer(dir, Ordre("PostToolUse", Maintenant.AddMinutes(-1))).Reussi);

        var visible = Assert.Single(LireSansTranscript(dir).Visibles);
        Assert.Equal(SessionActivity.Working, visible.Activity);
        Assert.Equal("PostToolUse", visible.Reason);   // le motif NOMME le battement qui l'a réaffirmée
    }

    /// <summary>
    /// Ce que le seuil mesure désormais : un SILENCE, plus une durée de travail supposée. Passé ce délai
    /// sans le moindre battement, on ne prétend plus « en cours ».
    /// <para>EVT-04 : depuis ce plan, ce silence ne rend plus un « inconnu » muet mais l'attente que
    /// l'inférence AUTORISE — et le libellé porte l'interrogation.</para>
    /// </summary>
    [Fact]
    public void Vingt_et_une_minutes_de_SILENCE_ne_se_disent_plus_en_cours()
    {
        var dir = TempDir();
        EcritEtat(dir, Mesuree, SessionActivity.Working, Maintenant.AddMinutes(-21));

        Assert.Equal(SessionActivity.WaitingDeduced, Assert.Single(LireSansTranscript(dir).Visibles).Activity);
    }

    /// <summary>
    /// LA FRONTIÈRE, et elle porte tout le sens de cette tâche : la VALEUR du seuil n'a pas bougé, c'est
    /// son sens qui change. Une garde qui ne tiendrait que le côté « périmé » resterait verte si le délai
    /// tombait à une minute — donc elle ne garderait rien.
    /// </summary>
    [Fact]
    public void Dix_neuf_minutes_de_silence_laissent_encore_l_etat_en_cours()
    {
        var dir = TempDir();
        EcritEtat(dir, Mesuree, SessionActivity.Working, Maintenant.AddMinutes(-19));

        Assert.Equal(SessionActivity.Working, Assert.Single(LireSansTranscript(dir).Visibles).Activity);
    }

    /// <summary>
    /// NON-RÉGRESSION EXPLICITE : le seuil de silence ne concerne que le TRAVAIL. Une attente ne bouge
    /// pas tant que je n'ai pas agi — c'est justement ça, le signal.
    /// </summary>
    [Fact]
    public void Le_seuil_de_silence_ne_touche_pas_aux_attentes_qui_persistent()
    {
        var dir = TempDir();
        EcritEtat(dir, Mesuree, SessionActivity.WaitingAttention, Maintenant.AddHours(-2));

        Assert.Equal(SessionActivity.WaitingAttention, Assert.Single(LireSansTranscript(dir).Visibles).Activity);
    }

    // --- EVT-04 : l'interruption au clavier, qu'AUCUNE source ne sait observer ---
    //
    // Le relevé du 2026-09-12 est formel : aucun des trente-trois événements du catalogue ne couvre
    // l'interruption utilisateur. StopFailure ne concerne que les erreurs d'API. Ce qui est OBSERVABLE,
    // c'est qu'une session travaillait et que plus aucun battement n'arrive — et la même signature vaut
    // pour un terminal tué ou une mise en veille. On ne nomme donc jamais la cause : on annonce une
    // attente DÉDUITE, et le libellé le dit.

    /// <summary>
    /// L'INTERDIT ABSOLU du milestone, tenu par une assertion explicite : « tour fini » est une
    /// OBSERVATION (le hook Stop l'a vue), et personne ne l'a faite ici. Le silence ne peut pas
    /// l'emprunter.
    /// </summary>
    [Fact]
    public void Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini()
    {
        var dir = TempDir();
        EcritEtat(dir, Mesuree, SessionActivity.Working, Maintenant.AddMinutes(-21));

        var visible = Assert.Single(LireSansTranscript(dir).Visibles);

        Assert.Equal(SessionActivity.WaitingDeduced, visible.Activity);
        Assert.NotEqual(SessionActivity.WaitingTurn, visible.Activity);   // l'interdit, écrit
        Assert.Equal("à toi ? déduit", AffichageSessions.Etat(visible.Activity));
    }

    /// <summary>
    /// Le silence des battements ne concerne que le TRAVAIL. Une attente a été OBSERVÉE — une permission
    /// demandée, un tour terminé — et elle ne bouge pas tant que je n'ai pas agi : la convertir en
    /// déduction dégraderait un fait en supposition, exactement à l'envers de ce que la phase construit.
    /// </summary>
    [Fact]
    public void Une_attente_observee_ne_se_convertit_jamais_en_deduction()
    {
        var dir = TempDir();
        EcritEtat(dir, "tour-observe",     SessionActivity.WaitingTurn,      Maintenant.AddHours(-2));
        EcritEtat(dir, "demande-observee", SessionActivity.WaitingAttention, Maintenant.AddHours(-2), "permission_prompt");

        var parId = LireSansTranscript(dir).Visibles.ToDictionary(s => s.SessionId, s => s.Activity);

        Assert.Equal(2, parId.Count);   // garde anti-muette : deux fichiers écrits, deux sessions lues
        Assert.Equal(SessionActivity.WaitingTurn, parId["tour-observe"]);
        Assert.Equal(SessionActivity.WaitingAttention, parId["demande-observee"]);
    }

    /// <summary>
    /// Ce qu'on n'a pas pu LIRE n'est pas une déduction, c'est une ABSENCE. L'état porté par ce fichier est
    /// illisible ET silencieux depuis vingt-cinq minutes : les deux conditions du piège sont réunies, et
    /// il reste « inconnu ». Une déduction se tire d'un fait observé (la session travaillait) ; ici il n'y
    /// en a aucun.
    /// </summary>
    [Fact]
    public void Une_activite_illisible_reste_inconnue_et_non_deduite()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, Mesuree + ".json"),
            "{\"session_id\":\"" + Mesuree + "\",\"project\":\"P\",\"activity\":\"NawakQuiNExistePas\","
            + "\"updated_at\":" + Maintenant.AddMinutes(-25).ToUnixTimeMilliseconds() + "}");

        var visible = Assert.Single(LireSansTranscript(dir).Visibles);

        Assert.Equal(SessionActivity.Unknown, visible.Activity);
        Assert.NotEqual(SessionActivity.WaitingDeduced, visible.Activity);
    }

    /// <summary>
    /// NON-RÉGRESSION de l'arbitrage par fraîcheur (phase 24) : une déduction n'écrase jamais un signal
    /// plus récent. Le hook s'est tu depuis vingt-cinq minutes, le transcript parle depuis dix secondes —
    /// c'est le transcript qui a le dernier mot, et la déduction devient le désaccord écarté.
    /// </summary>
    [Fact]
    public void Une_deduction_ne_bat_pas_un_transcript_plus_recent()
    {
        var dir = TempDir();
        EcritEtat(dir, Mesuree, SessionActivity.Working, Maintenant.AddMinutes(-25));

        var lecture = Lire(dir);   // transcript Working de 10 s

        Assert.Equal(SessionActivity.Working, Assert.Single(lecture.Visibles).Activity);
        var desaccord = Assert.Single(lecture.Desaccords);
        Assert.Equal(SessionActivity.WaitingDeduced, desaccord.EtatEcarte);
        Assert.Equal(SourceSession.Transcript, desaccord.SourceRetenue);
    }

    /// <summary>
    /// LE CAS D'ÉGALITÉ À LA MILLISECONDE, figé tel qu'il est — pas corrigé.
    ///
    /// <para>Dans <c>ArbitrageSessions.Departager</c>, le rang <b>source</b> (2) précède le rang
    /// <b>urgence</b> (3). À horodatage rigoureusement identique, un hook l'emporte donc sur un transcript
    /// AVANT que le rang d'urgence n'ait la parole — et une DÉDUCTION issue du hook bat une OBSERVATION
    /// issue du transcript. C'est une conséquence héritée de la phase 24, pas un défaut ouvert par
    /// celle-ci.</para>
    ///
    /// <para>Ce test ÉCRIT le comportement pour que la prochaine personne le trouve écrit, et il ne le
    /// modifie pas : le <c>25-CONTEXT.md</c> exige qu'aucune retouche de l'arbitrage ne se fasse sans
    /// qu'un test la motive. Le motiver est le travail d'une autre phase ; le constater est celui-ci.</para>
    /// </summary>
    [Fact]
    public void Un_hook_deduit_et_un_transcript_travaillant_du_MEME_instant_sont_departages_par_la_source()
    {
        var instant = Maintenant.AddMinutes(-25);   // au-delà du silence des battements : le hook sera déduit
        var dir = TempDir();
        EcritEtat(dir, Mesuree, SessionActivity.Working, instant);

        var lecture = new SessionMonitor(dir,
                new SourceFixe(Snap(Mesuree, SessionActivity.Working, instant)),   // EXACTEMENT le même instant
                new ArchiveStore(TempFichier()))
            .Inspecter(Maintenant);

        var visible = Assert.Single(lecture.Visibles);
        Assert.Equal(SessionActivity.WaitingDeduced, visible.Activity);   // le HOOK gagne, par son rang de source

        var desaccord = Assert.Single(lecture.Desaccords);
        Assert.Equal(SourceSession.Hook, desaccord.SourceRetenue);
        Assert.Equal(SourceSession.Transcript, desaccord.SourceEcartee);
        Assert.Equal(TimeSpan.Zero, desaccord.EcartAge);                  // zéro : c'est bien l'égalité stricte
    }

    /// <summary>
    /// LA PROMESSE, TENUE PAR UNE GARDE et non plus par une XML-doc. <c>WaitingDeduced</c> est DÉRIVÉ à la
    /// lecture, par le moniteur, et nulle part ailleurs : aucun des événements câblés ne doit pouvoir
    /// l'inscrire dans un fichier d'état. Si un jour quelqu'un routait un événement vers cette valeur, une
    /// déduction deviendrait persistante — et indiscernable d'une observation pour toute source qui relit
    /// le magasin.
    /// </summary>
    [Fact]
    public void WaitingDeduced_n_est_jamais_ecrit_dans_un_fichier_d_etat()
    {
        var parcourus = 0;
        var avecEtat = 0;

        foreach (var cable in SessionHookInstaller.Cablage)
        {
            parcourus++;
            var resultat = SessionHookProcessor.Process(cable.Evenement,
                "{\"session_id\":\"" + Mesuree + "\",\"cwd\":\"C:/dev/MonProjet\"}",
                Maintenant.ToUnixTimeMilliseconds());

            if (resultat.StateJson is null) continue;
            avecEtat++;
            Assert.DoesNotContain("WaitingDeduced", resultat.StateJson, StringComparison.Ordinal);
        }

        // Gardes anti-muettes : un câblage vide, ou qui n'écrirait plus aucun état, passerait sans rien dire.
        Assert.Equal(8, parcourus);
        Assert.Equal(7, avecEtat);   // les huit câblés moins SessionEnd, qui SUPPRIME au lieu d'écrire
    }

    /// <summary>
    /// L'EFFET DE MASSE, CHIFFRÉ — contrepartie obligatoire de la décision de l'utilisateur du 2026-09-12
    /// (point A11 de <c>25-VALIDATION.md</c>). Une déduction affichée partout serait visible mais
    /// assourdissante : ce qui départage « honnête mais inutile » de « utile mais bruyant », c'est un
    /// NOMBRE, et il doit être connu AVANT la republication, pas découvert après.
    ///
    /// <para>Corpus DÉTERMINISTE de soixante-six états, dans la FORME du magasin réel relevée le
    /// 2026-09-12 (66 entrées, dont 32 <c>Working</c>, 20 <c>WaitingTurn</c>, 2 <c>WaitingAttention</c> et
    /// 12 reliquats que le moniteur ne retient plus). Les âges, eux, ne sont PAS copiés du magasin réel :
    /// ses 54 états y sont tous au-delà du seuil de huit heures — c'est un cimetière, il ne mesurerait
    /// rien. Le corpus place donc les <c>Working</c> DE PART ET D'AUTRE du seuil de silence, et les y place
    /// serrés : si le seuil bougeait d'une minute dans un sens ou dans l'autre, le nombre changerait.</para>
    /// </summary>
    [Fact]
    public void Combien_de_sessions_d_un_corpus_realiste_basculent_ensemble_en_deduction()
    {
        var dir = TempDir();
        var ecrits = 0;

        // 12 Working FRAIS : de 1 à 12 minutes de silence, tous EN DEÇÀ du seuil de vingt minutes.
        for (var i = 1; i <= 12; i++)
        { EcritEtat(dir, $"frais-{i:00}", SessionActivity.Working, Maintenant.AddMinutes(-i)); ecrits++; }

        // 20 Working SILENCIEUX : de 21 à 40 minutes, tous AU-DELÀ du seuil et bien en deçà des huit heures.
        for (var i = 21; i <= 40; i++)
        { EcritEtat(dir, $"silence-{i:00}", SessionActivity.Working, Maintenant.AddMinutes(-i)); ecrits++; }

        // 20 attentes OBSERVÉES (tour fini), étalées de 20 min à 6 h 40 : elles persistent quel que soit
        // leur âge tant qu'il reste sous les huit heures — c'est justement ça, le signal.
        for (var i = 1; i <= 20; i++)
        { EcritEtat(dir, $"tour-{i:00}", SessionActivity.WaitingTurn, Maintenant.AddMinutes(-20 * i)); ecrits++; }

        // 2 demandes OBSERVÉES.
        for (var i = 1; i <= 2; i++)
        { EcritEtat(dir, $"demande-{i:00}", SessionActivity.WaitingAttention, Maintenant.AddHours(-2), "permission_prompt"); ecrits++; }

        // 12 états TROP VIEUX : au-delà de huit heures, le moniteur ne les lit plus du tout (phase 23).
        for (var i = 1; i <= 12; i++)
        { EcritEtat(dir, $"mort-{i:00}", SessionActivity.Working, Maintenant.AddHours(-9).AddMinutes(-i)); ecrits++; }

        Assert.Equal(66, ecrits);   // garde anti-muette : le corpus est bien celui qu'on annonce

        var lecture = LireSansTranscript(dir);
        var deduites = lecture.Visibles.Count(s => s.Activity == SessionActivity.WaitingDeduced);

        // LE NOMBRE. Vingt sessions basculent ENSEMBLE en « à toi ? déduit » — sur cinquante-quatre lignes
        // visibles, soit un peu plus d'une sur trois. Reporté au SUMMARY et au point A11 de 25-VALIDATION.
        Assert.Equal(20, deduites);

        // Le reste du corpus, pour que le nombre ci-dessus ne puisse pas être juste par accident.
        Assert.Equal(54, lecture.Visibles.Count);
        Assert.Equal(12, lecture.FichiersEcartesParAnciennete);
        Assert.Equal(12, lecture.Visibles.Count(s => s.Activity == SessionActivity.Working));
        Assert.Equal(20, lecture.Visibles.Count(s => s.Activity == SessionActivity.WaitingTurn));
        Assert.Equal(2,  lecture.Visibles.Count(s => s.Activity == SessionActivity.WaitingAttention));
        Assert.Equal(0,  lecture.Visibles.Count(s => s.Activity == SessionActivity.Unknown));
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
