using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// EVT-03 — « en cours » cesse d'être deviné par un seuil d'expiration : il est RÉAFFIRMÉ par des
/// battements de cœur.
///
/// <para><b>SUB-01 (phase 30.1) — un sous-agent qui écrit est un travail de sa session.</b> Ses battements
/// réaffirment « en cours » pour la session parente : une session dont le tour est fini mais dont un agent
/// tourne en arrière-plan « réfléchit » (écart E2 du constat de phase 31). Ce qui rend ces battements
/// honnêtes, ce n'est plus le silence du veto : c'est qu'ils ne réaffirment qu'un travail ou un tour fini —
/// jamais une attente d'intervention, jamais un état absent (<c>EcritureEtatSession</c>, sous le verrou).</para>
///
/// <para><b>Le piège que ce fichier garde.</b> Les hooks d'un sous-agent portent le MÊME
/// <c>session_id</c> que la session parente et ne s'en distinguent QUE par la présence de
/// <c>agent_id</c> / <c>agent_type</c> (relevé du 2026-09-12). Sur cette machine, quatre-vingt-quatorze
/// pour cent des transcripts sont des sous-agents : une vague d'agents parallèles ne doit ni parler du
/// CYCLE DE VIE de son parent (naître, recevoir un prompt, finir un tour, mourir), ni écraser un « à toi »
/// en attente — la brèche R3, fermée désormais au bon endroit.</para>
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
    /// D-30.1-01 — le veto se réduit au CYCLE DE VIE. Un sous-agent ne parle jamais du cycle de vie de son
    /// parent : ni pour dire qu'une session naît, ni qu'un prompt arrive, ni que le tour est fini (le
    /// <c>SessionEnd</c> a son propre test, plus bas : c'est le cas le plus grave). Les deux marqueurs sont
    /// éprouvés, car un seul des deux suffit à trahir un sous-agent.
    /// </summary>
    [Theory]
    [InlineData("SessionStart", MarqueurId)]
    [InlineData("SessionStart", MarqueurType)]
    [InlineData("UserPromptSubmit", MarqueurId)]
    [InlineData("UserPromptSubmit", MarqueurType)]
    [InlineData("Stop", MarqueurId)]
    [InlineData("Stop", MarqueurType)]
    public void Un_evenement_de_cycle_de_vie_emis_par_un_sous_agent_est_ignore(string ev, string marqueur)
    {
        var r = SessionHookProcessor.Process(ev, Stdin(marqueur), 0);

        Assert.True(r.Ignore);
        Assert.Null(r.StateJson);
        Assert.False(r.Delete);
        Assert.False(r.BattementSousAgent);
    }

    /// <summary>
    /// SUB-01 (phase 30.1) — LE RENVERSEMENT, sur les battements seulement. Un sous-agent qui appelle un outil
    /// travaille POUR sa session : le battement écrit « en cours » pour la session PARENTE (le
    /// <c>session_id</c> que le hook reçoit), daté de son instant, et son motif le NOMME (« … (sous-agent) »),
    /// pour que le moniteur et le diagnostic sachent d'où vient ce travail. Le drapeau porté par le résultat
    /// est ce que l'écriture lit pour ne réaffirmer qu'un travail ou un tour fini (D-30.1-03).
    /// </summary>
    [Theory]
    [InlineData("PreToolUse", MarqueurId)]
    [InlineData("PreToolUse", MarqueurType)]
    [InlineData("PostToolUse", MarqueurId)]
    [InlineData("PostToolUse", MarqueurType)]
    public void Un_battement_de_sous_agent_ecrit_Reflexion_pour_la_session_parente(string ev, string marqueur)
    {
        var r = SessionHookProcessor.Process(ev, Stdin(marqueur), 4242);

        Assert.False(r.Ignore);
        Assert.False(r.Delete);
        Assert.True(r.BattementSousAgent);
        Assert.Equal("s", r.SessionId);
        using var doc = JsonDocument.Parse(r.StateJson!);
        Assert.Equal("Working", doc.RootElement.GetProperty("activity").GetString());
        Assert.Equal(ev + " (sous-agent)", doc.RootElement.GetProperty("reason").GetString());
        Assert.Equal(4242L, doc.RootElement.GetProperty("updated_at").GetInt64());
        Assert.Equal("MonProjet", doc.RootElement.GetProperty("project").GetString());
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
    /// Les deux DEMANDES échappent au veto depuis la phase 25 : un sous-agent peut parfaitement réclamer MON
    /// intervention. Vetoer ces deux-là ferait perdre de VRAIES demandes. Depuis la phase 30.1 (D-30.1-02),
    /// elles gardent leur motif ORDINAIRE, sans suffixe, et ne portent pas le drapeau de battement : une
    /// demande est une attente, pas un travail — le moniteur ne doit jamais la confondre avec un signal de
    /// sous-agent.
    /// </summary>
    [Theory]
    [InlineData("PermissionRequest", MarqueurId)]
    [InlineData("Notification", MarqueurId + "," + VraieDemande)]
    public void Un_sous_agent_peut_en_revanche_reclamer_mon_intervention(string ev, string fragment)
    {
        var r = SessionHookProcessor.Process(ev, Stdin(fragment), 0);

        Assert.Equal("WaitingAttention", Activite(r));
        Assert.False(r.BattementSousAgent);
        using var doc = JsonDocument.Parse(r.StateJson!);
        Assert.DoesNotContain(" (sous-agent)", doc.RootElement.GetProperty("reason").GetString()!, StringComparison.Ordinal);
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

        // SUB-01 : un marqueur vide ne fabrique pas davantage un battement de sous-agent — le motif reste celui
        // du parent, sans suffixe, et l'écriture garde pour lui la doctrine du parent.
        var battement = SessionHookProcessor.Process("PostToolUse", Stdin("\"agent_id\":\"\""), 0);
        Assert.False(battement.BattementSousAgent);
        using var doc = JsonDocument.Parse(battement.StateJson!);
        Assert.Equal("PostToolUse", doc.RootElement.GetProperty("reason").GetString());
    }

    // --- Les deux battements de cœur, routés ---

    /// <summary>
    /// Le motif inscrit est le NOM DE L'ÉVÉNEMENT — même règle que <c>PermissionRequest</c> au plan
    /// 25-01 : c'est nous qui avons câblé cet événement, donc son nom est un fait OBSERVÉ, là où aucun
    /// champ spécifique à l'événement n'est confirmable (page de référence tronquée).
    ///
    /// <para>Et la doctrine qui va avec, tenue par ce test comme par le contrat : un battement dit
    /// « ça travaillait à cet instant », jamais « ça travaille encore maintenant ».</para>
    /// </summary>
    [Theory]
    [InlineData("PreToolUse", 1000)]
    [InlineData("PostToolUse", 2000)]
    public void Un_battement_de_coeur_dit_en_cours_et_nomme_l_evenement_observe(string ev, long ms)
    {
        var r = SessionHookProcessor.Process(ev, Stdin(), ms);

        Assert.False(r.Ignore);
        Assert.False(r.Delete);
        Assert.False(r.BattementSousAgent);   // le battement de la session ELLE-MÊME : doctrine inchangée
        using var doc = JsonDocument.Parse(r.StateJson!);
        Assert.Equal("Working", doc.RootElement.GetProperty("activity").GetString());
        Assert.Equal(ev, doc.RootElement.GetProperty("reason").GetString());
        Assert.Equal("MonProjet", doc.RootElement.GetProperty("project").GetString());
        Assert.Equal(ms, doc.RootElement.GetProperty("updated_at").GetInt64());
    }

    // --- Le relevé de 14:44 (écart E2 du constat de phase 31), côté hooks ---

    /// <summary>
    /// SUB-01, critère 1 côté hooks — LE RELEVÉ QUI A OUVERT LA PHASE 30.1, rejoué avec ses instants réels (UTC ;
    /// 14:33 locale = 12:33Z). Le tour parent de la session 88677186 s'est terminé (<c>Stop</c> à 12:33:23Z) juste
    /// après avoir lancé un agent en arrière-plan ; l'agent <c>a7df37762e5ce2df4</c> écrivait encore à
    /// 12:45:09.853Z. Chronos affichait « En attente » : le battement du sous-agent était vetoé. Il doit désormais
    /// dire « Réflexion », daté du battement — puis le <c>Stop</c> suivant du parent (12:50:00Z), plus récent,
    /// rend « En attente ».
    ///
    /// <para>Relecture HERMÉTIQUE par le moniteur réel, à ses propres instants figés : le <see cref="Relire"/> de
    /// ce fichier est figé au 2026-09-12 et ne convient pas ici.</para>
    /// </summary>
    [Fact]
    public void Le_releve_de_14h44_cote_hooks_Reflexion_puis_En_attente_au_Stop()
    {
        const string sid = "88677186-8690-44dc-ac77-7a319a0aa5cb";
        var parent = "{\"session_id\":\"" + sid + "\",\"cwd\":\"C:/Projets/Projet-A\"}";
        var sousAgent = "{\"session_id\":\"" + sid + "\",\"cwd\":\"C:/Projets/Projet-A\","
                        + "\"agent_id\":\"a7df37762e5ce2df4\",\"agent_type\":\"gsd-executor\"}";

        var stopParent = new DateTimeOffset(2026, 9, 26, 12, 33, 23, TimeSpan.Zero);
        var battementAgent = new DateTimeOffset(2026, 9, 26, 12, 45, 9, 853, TimeSpan.Zero);
        var lecture1 = new DateTimeOffset(2026, 9, 26, 12, 45, 30, TimeSpan.Zero);
        var stopSuivant = new DateTimeOffset(2026, 9, 26, 12, 50, 0, TimeSpan.Zero);
        var lecture2 = new DateTimeOffset(2026, 9, 26, 12, 50, 5, TimeSpan.Zero);

        var dossier = TempDossier();
        var vide = TempDossier();
        try
        {
            IReadOnlyList<SessionSnapshot> Lire(DateTimeOffset instant)
                => new SessionMonitor(dossier,
                                      new TranscriptSessionSource(vide),
                                      new ArchiveStore(Path.Combine(vide, "a.json")))
                    .Read(instant);

            Assert.True(EcritureEtatSession.Appliquer(dossier,
                SessionHookProcessor.Process("Stop", parent, stopParent.ToUnixTimeMilliseconds())).Reussi);
            Assert.True(EcritureEtatSession.Appliquer(dossier,
                SessionHookProcessor.Process("PostToolUse", sousAgent, battementAgent.ToUnixTimeMilliseconds())).Reussi);

            var s = Assert.Single(Lire(lecture1));
            Assert.Equal(SessionActivity.Working, s.Activity);
            Assert.Equal(battementAgent, s.UpdatedAt);
            Assert.Equal("PostToolUse (sous-agent)", s.Reason);
            Assert.Equal("Réflexion", AffichageSessions.Etat(s.Activity));
            Assert.Equal("Projet-A", s.Project);

            Assert.True(EcritureEtatSession.Appliquer(dossier,
                SessionHookProcessor.Process("Stop", parent, stopSuivant.ToUnixTimeMilliseconds())).Reussi);

            var fin = Assert.Single(Lire(lecture2));
            Assert.Equal(SessionActivity.WaitingTurn, fin.Activity);
            Assert.Equal(stopSuivant, fin.UpdatedAt);
            Assert.Equal("En attente", AffichageSessions.Etat(fin.Activity));
        }
        finally { Directory.Delete(dossier, true); Directory.Delete(vide, true); }
    }

    // --- La CADENCE : ce que les battements imposent au fichier d'état ---

    private const string Sid = "77777777-8888-9999-aaaa-bbbbbbbbbbbb";

    /// <summary>Instant FIGÉ. Rien ici ne se compare à l'horloge du système, et tous les horodatages
    /// écrits en dérivent : un test à retardement serait vert le jour de son écriture puis rouge ensuite.</summary>
    private static readonly DateTimeOffset Maintenant = new(2026, 9, 12, 13, 28, 0, TimeSpan.Zero);

    /// <summary>Le premier battement est daté d'une minute avant l'instant de lecture : la relecture
    /// tombe donc très en deçà du seuil de silence, et le test ne dépend d'aucune durée d'exécution.</summary>
    private static long BaseMs => Maintenant.AddMinutes(-1).ToUnixTimeMilliseconds();

    private static string TempDossier()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-battements-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // garde anti-accident : ce code TRONQUE des fichiers
        return d;
    }

    /// <summary>Un battement complet, tel que le hook le produirait, pour la session <see cref="Sid"/>.</summary>
    private static SessionHookResult Battement(long ms)
        => SessionHookProcessor.Process("PostToolUse",
               "{\"session_id\":\"" + Sid + "\",\"cwd\":\"C:/dev/MonProjet\"}", ms);

    /// <summary>Relecture HERMÉTIQUE : les trois arguments du moniteur sont INJECTÉS et l'instant est
    /// FIGÉ. Les défauts du constructeur liraient le vrai ~/.claude/projects et le vrai archived.json.</summary>
    private static IReadOnlyList<SessionSnapshot> Relire(string dossierEtats, string dossierVide)
        => new SessionMonitor(dossierEtats,
                              new TranscriptSessionSource(dossierVide),
                              new ArchiveStore(Path.Combine(dossierVide, "a.json")))
            .Read(Maintenant);

    /// <summary>
    /// CADENCE SÉQUENTIELLE. Un battement par appel d'outil, c'est un volume d'écritures sans commune
    /// mesure avec les cinq événements de cycle de vie d'avant cette phase — et le widget relit le dossier
    /// toutes les deux secondes pendant ce temps. Motif repris de
    /// <c>EcritureEtatSessionTests.Sous_un_lecteur_concurrent_aucune_ecriture_n_est_perdue</c>.
    /// </summary>
    [Fact]
    public void Cinq_cents_battements_consecutifs_sous_lecteur_concurrent_ne_perdent_rien()
    {
        var dossier = TempDossier();
        var vide = TempDossier();
        try
        {
            var fichier = Path.Combine(dossier, Sid + ".json");

            // La cible doit exister pour qu'un lecteur puisse l'ouvrir : premier battement, sans lecteur.
            Assert.True(EcritureEtatSession.Appliquer(dossier, Battement(BaseMs)).Reussi);

            var echecs = 0;
            var dernier = BaseMs;
            using (var lecteur = new FileStream(fichier, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Assert.True(lecteur.CanRead);   // le lecteur tient RÉELLEMENT le fichier pendant la boucle
                for (var i = 1; i <= 500; i++)
                {
                    dernier = BaseMs + i;
                    if (!EcritureEtatSession.Appliquer(dossier, Battement(dernier)).Reussi) echecs++;
                }
            }

            Assert.Equal(0, echecs);

            // Et ce n'est pas seulement « aucune erreur » : l'état final est RELU par le moniteur réel.
            var snap = Assert.Single(Relire(dossier, vide));
            Assert.Equal(SessionActivity.Working, snap.Activity);
            Assert.Equal("PostToolUse", snap.Reason);
            Assert.Equal(dernier, snap.UpdatedAt.ToUnixTimeMilliseconds());
        }
        finally { Directory.Delete(dossier, true); Directory.Delete(vide, true); }
    }

    /// <summary>
    /// CADENCE CONCURRENTE — le cas NEUF de cette phase, et celui que le test séquentiel ne peut pas voir.
    ///
    /// <para>Avant les battements, l'écrivain unique était garanti : les événements câblés étaient tous des
    /// signaux de cycle de vie, un à la fois. Il ne l'est plus. Claude Code lance jusqu'à cinq processus de
    /// hook en parallèle par événement, et les appels d'outil PARALLÈLES existent — huit écrivains sur la
    /// même session, c'est le motif exact d'un batch. Sans parade, tous sauf un prennent un refus de
    /// partage, l'échec remonte sur la sortie d'erreur, et Claude Code affiche un message à l'utilisateur
    /// À CHAQUE batch d'outils.</para>
    ///
    /// <para>Le fichier final est DÉSÉRIALISÉ, pas mesuré : sa taille ne dirait rien d'un fragment.</para>
    /// </summary>
    [Fact]
    public void Huit_ecrivains_paralleles_sur_la_meme_session_n_echouent_jamais()
    {
        var dossier = TempDossier();
        try
        {
            var echecs = 0;
            var causes = new ConcurrentBag<string>();

            // L'attribution des horodatages est ADVERSE À DESSEIN : le premier écrivain porte le bloc le
            // plus RÉCENT, le dernier le plus ancien. Les quatre cents mêmes horodatages sont émis qu'avant,
            // mais celui qui finit le plus tard n'est plus celui qui gagnerait par simple écrasement — sans
            // quoi l'assertion de monotonie plus bas serait verte par pure chance d'ordonnancement.
            Parallel.For(0, 8, ecrivain =>
            {
                for (var i = 1; i <= 50; i++)
                {
                    var r = EcritureEtatSession.Appliquer(dossier, Battement(BaseMs + (7 - ecrivain) * 1000 + i));
                    if (!r.Reussi) { Interlocked.Increment(ref echecs); causes.Add(r.Cause ?? "(sans cause)"); }
                }
            });

            Assert.True(echecs == 0,
                echecs + " écriture(s) refusée(s) sur 400. Causes distinctes : "
                + string.Join(" | ", causes.Distinct().Take(3)));

            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dossier, Sid + ".json")));
            Assert.Equal(Sid, doc.RootElement.GetProperty("session_id").GetString());
            Assert.Equal("Working", doc.RootElement.GetProperty("activity").GetString());
            Assert.Equal("PostToolUse", doc.RootElement.GetProperty("reason").GetString());

            // R3, cran 1 — CE QUI MANQUAIT. « Aucune n'échoue » et « le fichier n'est pas un fragment » ne
            // disent RIEN de l'état qui survit : le dernier écrivain physique gagnait, quel que soit son
            // âge. L'état survivant doit être le PLUS RÉCENT, et il n'y a qu'un candidat : le plus grand
            // horodatage émis par la vague.
            var lePlusRecent = BaseMs + 7 * 1000 + 50;
            Assert.Equal(lePlusRecent, doc.RootElement.GetProperty("updated_at").GetInt64());
        }
        finally { Directory.Delete(dossier, true); }
    }

    /// <summary>
    /// R3, cran 1 — LE SCÉNARIO REDOUTÉ, en concurrence réelle : une demande de permission au milieu d'une
    /// vague de battements d'appels d'outil PARALLÈLES.
    ///
    /// <para>Le batch d'outils frères continue de battre pendant que le prompt de permission attend une
    /// réponse, et l'instant de chaque hook est capturé à l'ENTRÉE de son processus : rien ne garantit
    /// l'ordre d'arrivée au disque. Sans monotonie, un battement parti avant la demande écrit après elle,
    /// « à toi » redevient « en cours », et le détecteur de traitement MASQUE la session — qui reste alors
    /// bloquée sur un prompt que plus rien n'annonce.</para>
    ///
    /// <para>Ici, la demande porte l'horodatage le plus grand : quel que soit l'ordre physique, elle doit
    /// être sur le disque à la fin.</para>
    /// </summary>
    [Fact]
    public void Une_demande_de_permission_survit_a_une_vague_de_battements_paralleles_plus_anciens()
    {
        var dossier = TempDossier();
        try
        {
            var demandeMs = BaseMs + 10_000;   // la demande est le signal le PLUS RÉCENT de la vague
            var demande = SessionHookProcessor.Process(
                "PermissionRequest", "{\"session_id\":\"" + Sid + "\",\"cwd\":\"C:/dev/MonProjet\"}", demandeMs);

            var echecs = 0;

            // Huit écrivains : le PREMIER pose la demande — donc il a toutes les chances de finir AVANT les
            // sept autres — et les sept suivants battent trois cent cinquante horodatages ANTÉRIEURS à elle.
            // Aucun ordonnancement n'est imposé au-delà : c'est tout l'intérêt.
            Parallel.For(0, 8, ecrivain =>
            {
                if (ecrivain == 0)
                {
                    if (!EcritureEtatSession.Appliquer(dossier, demande).Reussi) Interlocked.Increment(ref echecs);
                    return;
                }
                for (var i = 1; i <= 50; i++)
                    if (!EcritureEtatSession.Appliquer(dossier, Battement(BaseMs + ecrivain * 1000 + i)).Reussi)
                        Interlocked.Increment(ref echecs);
            });

            Assert.Equal(0, echecs);

            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dossier, Sid + ".json")));
            Assert.Equal("WaitingAttention", doc.RootElement.GetProperty("activity").GetString());
            Assert.Equal("PermissionRequest", doc.RootElement.GetProperty("reason").GetString());
            Assert.Equal(demandeMs, doc.RootElement.GetProperty("updated_at").GetInt64());
        }
        finally { Directory.Delete(dossier, true); }
    }

    /// <summary>
    /// SUB-01 (phase 30.1) — LA BRÈCHE R3 FERMÉE AU BON ENDROIT, en concurrence réelle. Le test précédent tient
    /// la demande contre des battements PLUS ANCIENS qu'elle (monotonie). Celui-ci tient le cas que la monotonie
    /// ne peut pas voir : huit écrivains de battements de SOUS-AGENT, tous PLUS RÉCENTS que la demande de
    /// permission — l'agent en arrière-plan continue d'appeler des outils pendant que le prompt attend.
    ///
    /// <para>C'est exactement la vague que le veto de la phase 25 faisait taire. Ces battements passent
    /// désormais le processeur, et c'est l'écriture qui les écarte, sous le verrou, sur le même descripteur
    /// (D-30.1-03). Aucun ne doit échouer (un refus légitime est un succès) et « à toi » doit survivre, à
    /// l'instant de la demande.</para>
    /// </summary>
    [Fact]
    public void Huit_ecrivains_de_battements_de_sous_agent_n_effacent_jamais_une_permission()
    {
        var dossier = TempDossier();
        try
        {
            var demandeMs = BaseMs;
            var demande = SessionHookProcessor.Process(
                "PermissionRequest", "{\"session_id\":\"" + Sid + "\",\"cwd\":\"C:/dev/MonProjet\"}", demandeMs);
            Assert.True(EcritureEtatSession.Appliquer(dossier, demande).Reussi);

            SessionHookResult BattementDeSousAgent(long ms)
                => SessionHookProcessor.Process("PostToolUse",
                       "{\"session_id\":\"" + Sid + "\",\"cwd\":\"C:/dev/MonProjet\"," + MarqueurId + "}", ms);

            // Anti-vacuité : ce sont bien des battements de SOUS-AGENT qui passent le processeur — un veto
            // rétabli rendrait ce test vert sans rien prouver de l'écriture.
            var echantillon = BattementDeSousAgent(BaseMs + 1);
            Assert.False(echantillon.Ignore);
            Assert.True(echantillon.BattementSousAgent);

            var echecs = 0;
            var causes = new ConcurrentBag<string>();
            Parallel.For(0, 8, ecrivain =>
            {
                for (var i = 1; i <= 50; i++)
                {
                    // TOUS postérieurs à la demande : BaseMs + 1 001 au plus tôt.
                    var r = EcritureEtatSession.Appliquer(dossier, BattementDeSousAgent(BaseMs + (ecrivain + 1) * 1000 + i));
                    if (!r.Reussi) { Interlocked.Increment(ref echecs); causes.Add(r.Cause ?? "(sans cause)"); }
                }
            });

            Assert.True(echecs == 0,
                echecs + " écriture(s) refusée(s) sur 400. Causes distinctes : "
                + string.Join(" | ", causes.Distinct().Take(3)));

            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dossier, Sid + ".json")));
            Assert.Equal("WaitingAttention", doc.RootElement.GetProperty("activity").GetString());
            Assert.Equal("PermissionRequest", doc.RootElement.GetProperty("reason").GetString());
            Assert.Equal(demandeMs, doc.RootElement.GetProperty("updated_at").GetInt64());
        }
        finally { Directory.Delete(dossier, true); }
    }
}
