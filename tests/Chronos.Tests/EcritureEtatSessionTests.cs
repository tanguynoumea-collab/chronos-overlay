using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// CYC-02 — une écriture d'état de hook ne peut plus disparaître sans que personne ne le sache.
///
/// Le scénario central est celui MESURÉ sur la machine le 2026-09-12 : le widget relit le dossier toutes
/// les deux secondes en <c>FileShare.ReadWrite</c> (<c>SessionMonitor.TryRead</c>), et l'écriture par
/// fichier temporaire déplacé par-dessus la cible échouait alors 290 fois sur 500, en silence, en laissant
/// un débris derrière elle. Les 12 orphelins relevés dans le magasin en sont la trace.
///
/// Tous les tests écrivent dans des dossiers TEMPORAIRES : aucun ne touche le vrai %APPDATA%\Chronos.
/// </summary>
public class EcritureEtatSessionTests
{
    private static string TempDossier()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-ecriture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // ce code SUPPRIME et TRONQUE des fichiers
        return d;
    }

    private const string Sid = "11111111-2222-3333-4444-555555555555";

    // Instant LITTÉRAL : rien ici ne se compare à l'horloge du système, et les horodatages écrits en
    // dérivent tous (piège de test à retardement des plans de la phase 22).
    private static readonly DateTimeOffset Maintenant = new(2026, 9, 12, 13, 28, 0, TimeSpan.Zero);

    private static string StdinDe(string sid)
        => $$"""{"session_id":"{{sid}}","cwd":"C:/Users/x/PROJET OVERLAY"}""";

    private static SessionHookResult Ordre(string evenement, string sid, long ms)
        => SessionHookProcessor.Process(evenement, StdinDe(sid), ms);

    private sealed class SourceVide : ISessionSource
    {
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => System.Array.Empty<SessionSnapshot>();
    }

    /// <summary>
    /// LE test de ce plan : 200 écritures pendant qu'un lecteur tient la cible ouverte dans le mode EXACT
    /// de <c>SessionMonitor.TryRead</c>. Mesuré à 290 pertes sur 500 avec l'ancienne mécanique.
    /// </summary>
    [Fact]
    public void Sous_un_lecteur_concurrent_aucune_ecriture_n_est_perdue()
    {
        var dossier = TempDossier();
        var fic = Path.Combine(dossier, Sid + ".json");
        var baseMs = Maintenant.ToUnixTimeMilliseconds();

        // La cible doit exister pour qu'un lecteur puisse l'ouvrir : première écriture, sans lecteur.
        Assert.True(EcritureEtatSession.Appliquer(dossier, Ordre("Notification", Sid, baseMs)).Reussi);

        var echecs = 0;
        long dernier = 0;
        using (var verrou = new FileStream(fic, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            Assert.True(verrou.CanRead);   // le lecteur tient RÉELLEMENT le fichier pendant la boucle
            for (var i = 1; i <= 200; i++)
            {
                dernier = baseMs + i;
                if (!EcritureEtatSession.Appliquer(dossier, Ordre("Notification", Sid, dernier)).Reussi) echecs++;
            }
        }

        Assert.Equal(0, echecs);

        // Et ce n'est pas seulement « aucune erreur » : c'est bien la DERNIÈRE écriture qui est sur disque.
        using var doc = JsonDocument.Parse(File.ReadAllText(fic));
        Assert.Equal(dernier, doc.RootElement.GetProperty("updated_at").GetInt64());
    }

    /// <summary>
    /// Le second défaut corrigé : le silence. Une écriture impossible rend sa CAUSE — et ne lève pas, car
    /// un hook ne doit jamais casser la session Claude Code.
    /// </summary>
    [Fact]
    public void Un_echec_d_ecriture_est_rendu_avec_sa_cause_et_ne_leve_jamais()
    {
        var dossier = TempDossier();
        var fic = Path.Combine(dossier, Sid + ".json");
        File.WriteAllText(fic, "{}");

        ResultatEcritureEtat res;
        using (var exclusif = new FileStream(fic, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.True(exclusif.CanWrite);
            // Que le test atteigne l'assertion suivante prouve à lui seul qu'aucune exception n'a traversé.
            res = EcritureEtatSession.Appliquer(dossier, Ordre("Notification", Sid, Maintenant.ToUnixTimeMilliseconds()));
        }

        Assert.False(res.Reussi);
        Assert.False(string.IsNullOrWhiteSpace(res.Cause));
        // La cause n'est pas fabriquée : c'est le TYPE réellement levé qui la porte.
        Assert.Contains("Exception", res.Cause!, StringComparison.Ordinal);
        // Un vrai refus d'écriture n'est PAS une écriture écartée pour antériorité (MON-01) : les deux
        // portent une cause, et un seul des deux doit remonter à l'utilisateur.
        Assert.False(res.Ignoree);
    }

    [Fact]
    public void SessionEnd_supprime_le_fichier_et_un_fichier_absent_n_est_pas_un_echec()
    {
        var dossier = TempDossier();
        var fic = Path.Combine(dossier, Sid + ".json");
        var baseMs = Maintenant.ToUnixTimeMilliseconds();

        Assert.True(EcritureEtatSession.Appliquer(dossier, Ordre("Notification", Sid, baseMs)).Reussi);
        Assert.True(File.Exists(fic));

        var suppression = EcritureEtatSession.Appliquer(dossier, Ordre("SessionEnd", Sid, baseMs));
        Assert.True(suppression.Reussi);
        Assert.False(File.Exists(fic));

        // Rien à faire n'est pas un échec (précédent ArchiveStore.PurgerPrefixe).
        var deuxieme = EcritureEtatSession.Appliquer(dossier, Ordre("SessionEnd", Sid, baseMs));
        Assert.True(deuxieme.Reussi);
        Assert.Null(deuxieme.Cause);
    }

    [Fact]
    public void Un_evenement_ignore_n_ecrit_rien_et_n_est_pas_un_echec()
    {
        var dossier = TempDossier();

        var res = EcritureEtatSession.Appliquer(dossier, SessionHookResult.Ignored);

        Assert.True(res.Reussi);
        Assert.Null(res.Cause);
        Assert.Empty(Directory.GetFiles(dossier));
    }

    /// <summary>La garde des 12 orphelins mesurés : plus aucun débris n'accompagne une écriture.</summary>
    [Fact]
    public void Une_ecriture_ne_laisse_aucun_debris()
    {
        var dossier = TempDossier();

        Assert.True(EcritureEtatSession.Appliquer(
            dossier, Ordre("Notification", Sid, Maintenant.ToUnixTimeMilliseconds())).Reussi);

        var noms = Directory.GetFiles(dossier).Select(f => Path.GetFileName(f)!).ToArray();
        Assert.Equal(new[] { Sid + ".json" }, noms);
    }

    /// <summary>
    /// Écrire sans être relu ne vaudrait rien : le consommateur RÉEL relit ce que le service vient d'écrire.
    /// </summary>
    [Fact]
    public void Le_contenu_ecrit_est_relisible_par_le_moniteur()
    {
        var dossier = TempDossier();

        Assert.True(EcritureEtatSession.Appliquer(
            dossier, Ordre("Notification", Sid, Maintenant.ToUnixTimeMilliseconds())).Reussi);

        var moniteur = new SessionMonitor(dossier, new SourceVide(),
                                          new ArchiveStore(Path.Combine(TempDossier(), "a.json")));

        var vue = Assert.Single(moniteur.Read(Maintenant));
        Assert.Equal(Sid, vue.SessionId);
        Assert.Equal(SessionActivity.WaitingAttention, vue.Activity);
    }

    // --- MONOTONIE (R3, cran 1) : l'horodatage du fichier d'état ne RECULE jamais ---

    /// <summary>Le contenu brut du fichier d'état de <see cref="Sid"/>, sans passer par le moniteur —
    /// on veut voir ce qui est SUR LE DISQUE, pas ce qu'une relecture en déduirait.</summary>
    private static (string Activite, long Horodatage) SurDisque(string dossier)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dossier, Sid + ".json")));
        return (doc.RootElement.GetProperty("activity").GetString() ?? "",
                doc.RootElement.GetProperty("updated_at").GetInt64());
    }

    /// <summary>
    /// LE test de l'inversion d'horodatage (R3, variante aggravée). Aucun test du dépôt ne mêlait jusqu'ici
    /// une demande de permission et un battement.
    ///
    /// <para>Le mécanisme réel : l'instant est capturé à l'ENTRÉE du processus de hook, et la reprise bornée
    /// peut différer l'écriture de jusqu'à soixante essais. Un <c>PreToolUse</c> parti AVANT un
    /// <c>PermissionRequest</c> peut donc écrire APRÈS lui, avec un horodatage PLUS ANCIEN — et l'horodatage
    /// du fichier d'état recule. Le détecteur de traitement lit alors cet effacement comme une réponse, et
    /// la session qui attend réellement une permission devient INVISIBLE : c'est la seule voie de rechute
    /// connue vers le symptôme fondateur du milestone.</para>
    /// </summary>
    [Fact]
    public void Un_battement_ANTERIEUR_n_efface_pas_une_attente_deja_ecrite()
    {
        var dossier = TempDossier();
        var t = Maintenant.ToUnixTimeMilliseconds();

        Assert.True(EcritureEtatSession.Appliquer(dossier, Ordre("PermissionRequest", Sid, t)).Reussi);

        // Le battement parti AVANT, arrivé APRÈS : cinq secondes en arrière.
        var enRetard = EcritureEtatSession.Appliquer(
            dossier, Ordre("PreToolUse", Sid, Maintenant.AddSeconds(-5).ToUnixTimeMilliseconds()));

        // Refuser une écriture périmée n'est pas un échec pour l'appelant : l'état le plus récent est déjà
        // en place. Un message d'erreur ici s'afficherait à l'utilisateur à chaque batch d'outils —
        // App.xaml.cs ne signale sur la sortie d'erreur QUE ce qui n'est pas réussi.
        Assert.True(enRetard.Reussi);
        // …et ce n'est pas non plus un silence : le sort est NOMMÉ, et distinct du refus d'écriture réel.
        Assert.True(enRetard.Ignoree);
        Assert.False(string.IsNullOrWhiteSpace(enRetard.Cause));

        var (activite, horodatage) = SurDisque(dossier);
        Assert.Equal("WaitingAttention", activite);
        Assert.Equal(t, horodatage);
    }

    /// <summary>
    /// L'ANTI-BLOCAGE, sans lequel le test précédent serait vert en refusant TOUTE écriture. Une écriture
    /// plus récente passe — et une écriture du MÊME instant aussi : deux hooks du même millième de seconde
    /// sont un cas ordinaire, et les bloquer figerait l'état au premier arrivé.
    /// </summary>
    [Fact]
    public void Une_ecriture_plus_recente_passe_et_une_ecriture_du_meme_instant_aussi()
    {
        var dossier = TempDossier();
        var t = Maintenant.ToUnixTimeMilliseconds();

        Assert.True(EcritureEtatSession.Appliquer(dossier, Ordre("PermissionRequest", Sid, t)).Reussi);

        // Même instant : admis (comparaison STRICTE).
        var memeInstant = EcritureEtatSession.Appliquer(dossier, Ordre("Stop", Sid, t));
        Assert.True(memeInstant.Reussi);
        Assert.False(memeInstant.Ignoree);
        Assert.Equal(("WaitingTurn", t), SurDisque(dossier));

        // Plus récent : admis, évidemment — c'est le cas nominal.
        var plusTard = Maintenant.AddSeconds(1).ToUnixTimeMilliseconds();
        var nominal = EcritureEtatSession.Appliquer(dossier, Ordre("PreToolUse", Sid, plusTard));
        Assert.True(nominal.Reussi);
        Assert.False(nominal.Ignoree);
        Assert.Equal(("Working", plusTard), SurDisque(dossier));
    }

    /// <summary>
    /// DOCTRINE DE LA PHASE 23, tenue à l'écriture comme elle l'est à la lecture : un fichier illisible ou
    /// un FRAGMENT est une ABSENCE de signal. On écrit donc, on ne refuse pas.
    ///
    /// <para>L'erreur que ce test interdit : traiter une relecture ratée comme « un signal très récent »,
    /// ce qui bloquerait toute écriture ultérieure et gèlerait un fichier corrompu pour de bon.</para>
    /// </summary>
    [Theory]
    [InlineData("{\"activity\":\"WaitingAttention\",\"updated_at\":99999")]   // fragment tronqué
    [InlineData("{\"activity\":\"WaitingAttention\"}")]                       // valide, mais sans horodatage
    [InlineData("")]                                                          // vide
    public void Un_fichier_illisible_ou_sans_horodatage_n_empeche_pas_l_ecriture(string debris)
    {
        var dossier = TempDossier();
        File.WriteAllText(Path.Combine(dossier, Sid + ".json"), debris);

        var t = Maintenant.ToUnixTimeMilliseconds();
        var res = EcritureEtatSession.Appliquer(dossier, Ordre("PreToolUse", Sid, t));

        Assert.True(res.Reussi);
        Assert.False(res.Ignoree);   // l'absence de signal n'est JAMAIS lue comme « un signal très récent »
        Assert.Equal(("Working", t), SurDisque(dossier));
    }

    // --- SUB-01 (phase 30.1) : un battement de SOUS-AGENT réaffirme, il n'efface ni ne crée ---

    /// <summary>Le stdin d'un hook émis par un SOUS-AGENT de la session <paramref name="sid"/> : le même que
    /// celui du parent, plus les deux marqueurs — valeurs du relevé de 14:44 (écart E2 du constat).</summary>
    private static string StdinSousAgent(string sid)
        => $$"""{"session_id":"{{sid}}","cwd":"C:/Users/x/PROJET OVERLAY","agent_id":"a7df37762e5ce2df4","agent_type":"gsd-executor"}""";

    private static SessionHookResult OrdreSousAgent(string evenement, string sid, long ms)
        => SessionHookProcessor.Process(evenement, StdinSousAgent(sid), ms);

    /// <summary>L'empreinte des OCTETS du fichier d'état : « rien n'a été écrit » se prouve octet pour octet,
    /// pas par une relecture qui pourrait retomber sur les mêmes valeurs.</summary>
    private static string Md5(string dossier)
        => Convert.ToHexString(MD5.HashData(File.ReadAllBytes(Path.Combine(dossier, Sid + ".json"))));

    /// <summary>Le motif (<c>reason</c>) lu dans le fichier d'état, ou <c>null</c> s'il n'y en a pas.</summary>
    private static string? Motif(string dossier)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dossier, Sid + ".json")));
        return doc.RootElement.TryGetProperty("reason", out var r) ? r.GetString() : null;
    }

    /// <summary>
    /// LA BRÈCHE R3, FERMÉE AU BON ENDROIT (30.1-CONTEXT, « Specific Ideas »). Une permission est demandée ; un
    /// agent en arrière-plan continue d'appeler des outils, et son battement est PLUS RÉCENT que la demande — la
    /// monotonie ne peut donc rien pour elle. Le veto de la phase 25 faisait taire ce battement ; il passe
    /// désormais le processeur, et c'est l'écriture qui le refuse : « à toi » ne s'efface pas par un sous-agent.
    /// La preuve est octet pour octet.
    /// </summary>
    [Fact]
    public void Un_battement_de_sous_agent_n_efface_pas_une_permission_fichier_identique_octet_pour_octet()
    {
        var dossier = TempDossier();
        try
        {
            var t = Maintenant.ToUnixTimeMilliseconds();
            Assert.True(EcritureEtatSession.Appliquer(dossier, Ordre("PermissionRequest", Sid, t)).Reussi);
            var avant = Md5(dossier);

            var res = EcritureEtatSession.Appliquer(
                dossier, OrdreSousAgent("PreToolUse", Sid, Maintenant.AddSeconds(60).ToUnixTimeMilliseconds()));

            // Un refus légitime n'est pas un échec : App.xaml.cs rend 0 et n'écrit rien sur la sortie d'erreur.
            Assert.True(res.Reussi);
            Assert.True(res.Ignoree);
            Assert.Contains("sous-agent", res.Cause!, StringComparison.Ordinal);
            Assert.Equal(avant, Md5(dossier));
            Assert.Equal(("WaitingAttention", t), SurDisque(dossier));
        }
        finally { Directory.Delete(dossier, true); }
    }

    /// <summary>
    /// L'ANTI-BLOCAGE de la règle précédente : sans lui, elle serait verte en refusant TOUT battement de
    /// sous-agent. Sur un tour fini (le cas du relevé de 14:44 : le parent a rendu la main, l'agent tourne
    /// encore) comme sur un travail, le battement RÉAFFIRME le travail, daté de son instant, et le motif le
    /// nomme.
    /// </summary>
    [Theory]
    [InlineData("Stop", "WaitingTurn")]
    [InlineData("UserPromptSubmit", "Working")]
    public void Un_battement_de_sous_agent_reaffirme_le_travail_sur_un_travail_ou_un_tour_fini(string evenementParent, string etatParent)
    {
        var dossier = TempDossier();
        try
        {
            var t = Maintenant.ToUnixTimeMilliseconds();
            Assert.True(EcritureEtatSession.Appliquer(dossier, Ordre(evenementParent, Sid, t)).Reussi);
            Assert.Equal((etatParent, t), SurDisque(dossier));

            var plusTard = Maintenant.AddSeconds(1).ToUnixTimeMilliseconds();
            var res = EcritureEtatSession.Appliquer(dossier, OrdreSousAgent("PostToolUse", Sid, plusTard));

            Assert.True(res.Reussi);
            Assert.False(res.Ignoree);
            Assert.Equal(("Working", plusTard), SurDisque(dossier));
            Assert.Equal("PostToolUse (sous-agent)", Motif(dossier));
        }
        finally { Directory.Delete(dossier, true); }
    }

    /// <summary>
    /// D-30.1-03 — RÉAFFIRMER, JAMAIS CRÉER. Naître est un fait du cycle de vie du PARENT : un battement de
    /// sous-agent arrivé après le <c>SessionEnd</c> du parent ressusciterait sinon une session morte. Le dossier
    /// doit rester VIDE — ni fichier d'état, ni fichier vide qu'une ouverture en création aurait laissé.
    /// </summary>
    [Fact]
    public void Un_battement_de_sous_agent_ne_cree_jamais_le_fichier_d_etat()
    {
        var dossier = TempDossier();
        try
        {
            var res = EcritureEtatSession.Appliquer(
                dossier, OrdreSousAgent("PreToolUse", Sid, Maintenant.ToUnixTimeMilliseconds()));

            Assert.True(res.Reussi);
            Assert.True(res.Ignoree);
            Assert.Empty(Directory.GetFiles(dossier));
        }
        finally { Directory.Delete(dossier, true); }
    }

    /// <summary>
    /// D-30.1-03 — un état ILLISIBLE n'est pas un travail à réaffirmer. Les TROIS débris de
    /// <see cref="Un_fichier_illisible_ou_sans_horodatage_n_empeche_pas_l_ecriture"/>, et le contraste est voulu :
    /// pour un battement du PARENT, la doctrine de la phase 23 reste entière (« un fichier illisible est une
    /// absence : on écrit ») ; pour un battement de SOUS-AGENT, l'absence de travail lisible ne reçoit rien.
    /// Le fragment tronqué et l'objet sans horodatage disent même « WaitingAttention » : les réécrire effacerait
    /// peut-être une attente.
    /// </summary>
    [Theory]
    [InlineData("{\"activity\":\"WaitingAttention\",\"updated_at\":99999")]   // fragment tronqué
    [InlineData("{\"activity\":\"WaitingAttention\"}")]                       // valide, mais sans horodatage
    [InlineData("")]                                                          // vide
    public void Un_battement_de_sous_agent_ne_reecrit_pas_un_etat_illisible(string debris)
    {
        var dossier = TempDossier();
        try
        {
            File.WriteAllText(Path.Combine(dossier, Sid + ".json"), debris);
            var avant = Md5(dossier);

            var res = EcritureEtatSession.Appliquer(
                dossier, OrdreSousAgent("PreToolUse", Sid, Maintenant.ToUnixTimeMilliseconds()));

            Assert.True(res.Reussi);
            Assert.True(res.Ignoree);
            Assert.Equal(avant, Md5(dossier));
        }
        finally { Directory.Delete(dossier, true); }
    }

    /// <summary>
    /// MON-01 vaut aussi pour un sous-agent : sur un état qu'il PEUT réaffirmer, un battement STRICTEMENT
    /// antérieur reste écarté par la monotonie, avec la cause de la monotonie — les deux règles se composent,
    /// aucune ne remplace l'autre.
    /// </summary>
    [Fact]
    public void Un_battement_de_sous_agent_ANTERIEUR_reste_ecarte_par_la_monotonie()
    {
        var dossier = TempDossier();
        try
        {
            var t = Maintenant.ToUnixTimeMilliseconds();
            Assert.True(EcritureEtatSession.Appliquer(dossier, Ordre("Stop", Sid, t)).Reussi);

            var res = EcritureEtatSession.Appliquer(
                dossier, OrdreSousAgent("PreToolUse", Sid, Maintenant.AddSeconds(-5).ToUnixTimeMilliseconds()));

            Assert.True(res.Reussi);
            Assert.True(res.Ignoree);
            Assert.Contains("antérieur", res.Cause!, StringComparison.Ordinal);
            Assert.Equal(("WaitingTurn", t), SurDisque(dossier));
        }
        finally { Directory.Delete(dossier, true); }
    }

    /// <summary>
    /// Une DEMANDE de sous-agent passe (phase 25) et s'écrit avec son motif ordinaire (D-30.1-02) ; ses
    /// battements suivants ne l'effacent pas. C'est la LIMITE ÉCRITE de la phase, contrepartie verrouillée de
    /// « n'efface jamais » : une permission demandée par un sous-agent puis accordée reste « En attente » (ou
    /// masquée si elle a été lue) jusqu'au prochain signal du parent.
    /// </summary>
    [Fact]
    public void La_permission_d_un_sous_agent_est_ecrite_et_ses_battements_ne_l_effacent_pas()
    {
        var dossier = TempDossier();
        try
        {
            var t = Maintenant.ToUnixTimeMilliseconds();
            Assert.True(EcritureEtatSession.Appliquer(dossier, Ordre("Stop", Sid, t)).Reussi);

            var t1 = Maintenant.AddSeconds(1).ToUnixTimeMilliseconds();
            var demande = EcritureEtatSession.Appliquer(dossier, OrdreSousAgent("PermissionRequest", Sid, t1));
            Assert.True(demande.Reussi);
            Assert.False(demande.Ignoree);
            Assert.Equal(("WaitingAttention", t1), SurDisque(dossier));
            Assert.Equal("PermissionRequest", Motif(dossier));
            var avant = Md5(dossier);

            var res = EcritureEtatSession.Appliquer(
                dossier, OrdreSousAgent("PostToolUse", Sid, Maintenant.AddSeconds(2).ToUnixTimeMilliseconds()));

            Assert.True(res.Reussi);
            Assert.True(res.Ignoree);
            Assert.Equal(avant, Md5(dossier));
        }
        finally { Directory.Delete(dossier, true); }
    }
}
