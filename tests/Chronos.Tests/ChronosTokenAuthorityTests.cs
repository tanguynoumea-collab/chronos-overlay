using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// CŒUR DE PREUVE DE LA PHASE 17 (TOK-01 / TOK-02) — l'autorité UNIQUE de jeton.
///
/// Le flux OAuth fait TOURNER le refresh token. Deux rafraîchisseurs coexistants présentant le même
/// refresh token produisent un <c>invalid_grant</c> sur le second, donc — avec la table de
/// classification du plan 17-02 — une FAUSSE déconnexion sur un compte parfaitement sain. Ce n'est pas
/// théorique : c'est le bug ouvert <c>anthropics/claude-code#25609</c> (« no coordination … a classic
/// refresh token rotation race condition »). Claude Code lui-même s'y est fait prendre.
///
/// Ce fichier prouve les six invariants de l'autorité : section critique unique, double-vérification,
/// rotation persistée AVANT le retour, tolérance à un Save en échec, non-effacement du coffre, et
/// sortie de jeton limitée à un <c>string</c> d'access token.
///
/// SÉCURITÉ — contrainte qui prime sur tout : aucun test n'ouvre le coffre réel de l'utilisateur et
/// aucun ne joint le réseau. Le coffre est toujours construit sur un chemin issu de
/// <c>Path.GetTempPath()</c> (garde <c>Assert.StartsWith</c>) et tout le trafic passe par
/// <c>FakeHttpMessageHandler</c>. Motif : le point de terminaison de rafraîchissement fait TOURNER le
/// refresh token — un appel réel dont le résultat n'est pas re-sauvegardé invaliderait DÉFINITIVEMENT
/// le login de l'utilisateur.
///
/// Pas de [Collection("XAML WPF")] : cette classe ne charge aucun BAML, elle reste parallélisable.
/// </summary>
public class ChronosTokenAuthorityTests
{
    // Horloge figée : tous les instants du fichier se lisent par rapport à elle.
    internal static readonly DateTimeOffset Maintenant = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Réponse nominale du point de terminaison de jeton. Le refresh token change (ROTATION) :
    /// REF-1 devient REF-2, ce qui est exactement ce que la persistance doit capturer.</summary>
    internal const string CorpsRefresh200 =
        """{"access_token":"ACC-2","refresh_token":"REF-2","expires_in":3600}""";

    // 200 à corps TRONQUÉ : le serveur a déjà roulé le refresh token de son côté, l'ancien est mort et
    // le nouveau est perdu. Littéral échappé (et non raw string) : une séquence de guillemets en fin de
    // raw string est ambiguë au regard du comptage du délimiteur de fermeture.
    private const string CorpsRefresh200Tronque = "{\"access_token\":\"ACC-2\"";

    private const string CorpsInvalidGrant = """{"error":"invalid_grant"}""";
    private const string CorpsInvalidClient = """{"error":"invalid_client"}""";
    private const string CorpsRateLimit = """{"error":{"type":"rate_limit_error"}}""";

    // ------------------------------------------------------------------------------------------
    // Helpers — INTERNAL STATIC : partagés avec TokenRefreshServiceTests (plan 17-03, tâche 2).
    // Un seul point de construction, donc une seule garde de sécurité de chemin à maintenir.
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// SÉCURITÉ : chemin de coffre TOUJOURS sous %TEMP%. Le refresh token réel de
    /// <c>%APPDATA%\Chronos\oauth.dat</c> n'est ni lu, ni déchiffré, ni envoyé : l'endpoint le FAIT
    /// TOURNER, un appel dont le résultat n'est pas re-sauvegardé invaliderait définitivement le login.
    /// </summary>
    internal static string CheminCoffreTemp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosAutoriteTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var chemin = Path.Combine(dir, "oauth.dat");
        Assert.StartsWith(Path.GetTempPath(), chemin);   // garde anti-accident
        return chemin;
    }

    /// <summary>
    /// POINT UNIQUE de construction de l'autorité sous test. <paramref name="expiration"/> null =&gt;
    /// coffre VIDE (aucun jeton enregistré).
    /// </summary>
    internal static (ChronosTokenAuthority autorite, FakeHttpMessageHandler handler, FakeClock horloge, string chemin)
        Autorite(DateTimeOffset? expiration, FakeHttpMessageHandler? handler = null)
    {
        var h = handler ?? FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsRefresh200);
        var horloge = new FakeClock(Maintenant);
        var chemin = CheminCoffreTemp();
        var coffre = new ChronosOAuthStore(chemin);
        if (expiration is { } e) coffre.Save(new OAuthTokens("ACC-1", "REF-1", e));
        var autorite = new ChronosTokenAuthority(coffre, new ChronosOAuthClient(new HttpClient(h), horloge), horloge);
        return (autorite, h, horloge, chemin);
    }

    /// <summary>Handler qui renvoie une séquence de réponses (une par appel), puis répète la dernière.</summary>
    internal static FakeHttpMessageHandler Sequence(params (HttpStatusCode statut, string corps)[] etapes)
    {
        var i = 0;
        return new FakeHttpMessageHandler(_ =>
        {
            var e = etapes[Math.Min(i++, etapes.Length - 1)];
            return new HttpResponseMessage(e.statut) { Content = new StringContent(e.corps) };
        });
    }

    // ------------------------------------------------------------------------------------------
    // 1. Le prédicat PUR de décision
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Comparaison d'HORLOGE MURALE, et non de temps écoulé : un portable qui dort huit heures rattrape
    /// naturellement au tick suivant, et le cas RÉEL de l'utilisateur — jeton expiré le 2026-07-12, soit
    /// deux mois dans le passé — est traité au premier tick. Un réveil calculé
    /// (<c>Task.Delay</c> jusqu'à <c>ExpiresAt</c>) casserait à la mise en veille.
    /// </summary>
    [Theory]
    [InlineData(null, true)]        // expiration inconnue => rafraîchir (on ne parie jamais sur un jeton)
    [InlineData(60, false)]         // expire dans 1 h, marge 12 min => pas encore
    [InlineData(12, true)]          // expiration EXACTEMENT à la marge => rafraîchir (borne inclusive)
    [InlineData(-86400, true)]      // expiré depuis 60 jours (cas réel) => rafraîchir
    public void DoitRafraichir_compare_l_horloge_murale_et_jamais_le_temps_ecoule(
        int? minutesAvantExpiration, bool attendu)
    {
        DateTimeOffset? expiration = minutesAvantExpiration is { } m ? Maintenant.AddMinutes(m) : null;

        var doit = ChronosTokenAuthority.DoitRafraichir(expiration, Maintenant, TimeSpan.FromMinutes(12));

        Assert.Equal(attendu, doit);
    }

    // ------------------------------------------------------------------------------------------
    // 2. Chemins nominaux
    // ------------------------------------------------------------------------------------------

    /// <summary>Coffre vide = l'utilisateur ne s'est jamais connecté. Ce n'est PAS une panne : aucun
    /// appel réseau, et surtout pas de pastille de déconnexion.</summary>
    [Fact]
    public async Task Un_coffre_vide_ne_declenche_aucun_appel_et_ne_crie_pas_a_la_panne()
    {
        var (autorite, handler, _, _) = Autorite(expiration: null);

        Assert.Null(await autorite.GetAccessTokenAsync());
        Assert.Equal(EtatAuthentification.NonConnecte, autorite.Etat);
        Assert.Equal(0, handler.SendCount);
    }

    /// <summary>Un jeton encore valide ne coûte RIEN : l'autorité n'appelle pas le réseau pour rien.</summary>
    [Fact]
    public async Task Un_jeton_encore_valide_est_rendu_sans_le_moindre_appel_reseau()
    {
        var (autorite, handler, _, _) = Autorite(Maintenant.AddHours(1));   // hors marge de 12 min

        Assert.Equal("ACC-1", await autorite.GetAccessTokenAsync());
        Assert.Equal(0, handler.SendCount);
        Assert.Equal(EtatAuthentification.Connecte, autorite.Etat);
    }

    [Fact]
    public async Task Un_jeton_expire_est_rafraichi_et_le_NOUVEL_access_token_est_rendu()
    {
        var (autorite, handler, _, _) = Autorite(Maintenant.AddMinutes(-1));

        Assert.Equal("ACC-2", await autorite.GetAccessTokenAsync());
        Assert.Equal(1, handler.SendCount);
        Assert.Equal(EtatAuthentification.Connecte, autorite.Etat);
    }

    /// <summary>
    /// ROTATION PERSISTÉE : le serveur a remplacé REF-1 par REF-2 et a tué REF-1. Si le nouveau couple
    /// n'est pas écrit AVANT le retour du jeton, l'appel suivant (ou le prochain démarrage de l'exe)
    /// présentera un refresh token mort et fabriquera une déconnexion.
    /// </summary>
    [Fact]
    public async Task La_rotation_est_persistee_AVANT_que_le_jeton_ne_soit_rendu()
    {
        var (autorite, _, _, chemin) = Autorite(Maintenant.AddMinutes(-1));

        Assert.Equal("ACC-2", await autorite.GetAccessTokenAsync());

        // Relecture par un coffre NEUF sur le même chemin : la preuve vient du disque, pas de la RAM.
        var surDisque = new ChronosOAuthStore(chemin).Load();
        Assert.NotNull(surDisque);
        Assert.Equal("REF-2", surDisque!.RefreshToken);
        Assert.Equal("ACC-2", surDisque.AccessToken);
    }

    /// <summary>
    /// Invariant 4 : <c>Save</c> peut échouer (disque plein, fichier verrouillé). Un échec d'ÉCRITURE ne
    /// doit pas faire croire à un échec de RAFRAÎCHISSEMENT : les jetons neufs restent en mémoire et la
    /// session courante continue — l'ancien refresh token est de toute façon déjà mort côté serveur.
    /// </summary>
    [Fact]
    public async Task Un_echec_d_ecriture_du_coffre_ne_tue_PAS_la_session_courante()
    {
        var chemin = CheminCoffreTemp();
        var coffre = new ChronosOAuthStore(chemin);
        coffre.Save(new OAuthTokens("ACC-1", "REF-1", Maintenant.AddMinutes(-1)));   // jeton expiré

        var horloge = new FakeClock(Maintenant);
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, CorpsRefresh200);
        var autorite = new ChronosTokenAuthority(
            coffre, new ChronosOAuthClient(new HttpClient(handler), horloge), horloge);

        // Le fichier est maintenu OUVERT en lecture PARTAGÉE : Load() passe encore (File.ReadAllBytes
        // demande FileShare.Read), mais le File.Move de Save ne peut plus remplacer la cible => Save LÈVE.
        using var verrouFichier = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Garde de FALSIFIABILITÉ : sans elle, ce test passerait aussi si Save réussissait.
        Assert.ThrowsAny<Exception>(() => coffre.Save(new OAuthTokens("X", "Y", Maintenant)));

        Assert.Equal("ACC-2", await autorite.GetAccessTokenAsync());
        Assert.Equal(EtatAuthentification.Connecte, autorite.Etat);
    }

    // ------------------------------------------------------------------------------------------
    // 3. LE test de la phase : la concurrence
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// LE test de la phase. Deux rafraîchissements concurrents présentant le MÊME refresh token
    /// produisent un invalid_grant sur le second — donc, avec la table de classification du plan 17-02,
    /// une FAUSSE déconnexion sur un compte parfaitement sain (bug claude-code#25609). Le sémaphore
    /// plus la double-vérification rendent cette situation structurellement impossible dans le processus.
    /// </summary>
    [Fact]
    public async Task Dix_demandes_concurrentes_ne_declenchent_QU_UNE_rotation()
    {
        var (autorite, handler, _, _) = Autorite(Maintenant.AddMinutes(-1));   // jeton expiré

        var resultats = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => autorite.GetAccessTokenAsync()));

        Assert.Equal(1, handler.SendCount);                    // UNE seule rotation
        Assert.All(resultats, r => Assert.Equal("ACC-2", r));  // tous servis par le même jeton frais
    }

    // ------------------------------------------------------------------------------------------
    // 4. Classification des causes (table du plan 17-02, projetée sur l'état d'authentification)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// La distinction qui fonde TOK-02 : un 429 <c>rate_limit_error</c> — mode d'échec RÉEL et vérifié
    /// de ce point de terminaison — n'est PAS une déconnexion. Crier « reconnecte-toi » à un utilisateur
    /// rate-limité serait une fausse alerte sur un compte parfaitement sain.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, CorpsInvalidGrant, EtatAuthentification.Deconnecte)]
    [InlineData(HttpStatusCode.Unauthorized, CorpsInvalidClient, EtatAuthentification.Deconnecte)]
    [InlineData(HttpStatusCode.TooManyRequests, CorpsRateLimit, EtatAuthentification.HorsLigne)]
    [InlineData(HttpStatusCode.InternalServerError, "", EtatAuthentification.HorsLigne)]
    [InlineData(HttpStatusCode.OK, CorpsRefresh200Tronque, EtatAuthentification.Deconnecte)]
    public async Task Chaque_signal_du_serveur_se_traduit_en_un_etat_distinct(
        HttpStatusCode statut, string corps, EtatAuthentification attendu)
    {
        var (autorite, handler, _, _) = Autorite(Maintenant.AddMinutes(-1),
            FakeHttpMessageHandler.Json(statut, corps));

        Assert.Null(await autorite.GetAccessTokenAsync());
        Assert.Equal(attendu, autorite.Etat);
        Assert.Equal(1, handler.SendCount);
    }

    /// <summary>Wifi coupé : le jeton est peut-être parfaitement bon. JAMAIS « déconnecté ».</summary>
    [Fact]
    public async Task Un_cable_debranche_dit_hors_ligne_et_jamais_deconnecte()
    {
        var (autorite, _, _, _) = Autorite(Maintenant.AddMinutes(-1),
            FakeHttpMessageHandler.Throws(new HttpRequestException("DNS introuvable")));

        Assert.Null(await autorite.GetAccessTokenAsync());
        Assert.Equal(EtatAuthentification.HorsLigne, autorite.Etat);
    }

    /// <summary>Timeout de 15 s du client : même raisonnement que le câble débranché.</summary>
    [Fact]
    public async Task Un_timeout_dit_hors_ligne_et_jamais_deconnecte()
    {
        var (autorite, _, _, _) = Autorite(Maintenant.AddMinutes(-1),
            FakeHttpMessageHandler.Throws(new TaskCanceledException("délai dépassé")));

        Assert.Null(await autorite.GetAccessTokenAsync());
        Assert.Equal(EtatAuthentification.HorsLigne, autorite.Etat);
    }

    // ------------------------------------------------------------------------------------------
    // 5. Le coffre est intouchable, le refus est verrouillé
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Pitfall 4 : un faux positif serveur ne doit JAMAIS détruire un login récupérable. Le cas est
    /// documenté (<c>anthropics/claude-code#54443</c> : « OAuth sessions rejected by the server before
    /// the locally stored expiresAt time … refresh returns HTTP 400 »). L'autorité passe en
    /// « Deconnecte » et ATTEND un login ; elle ne supprime rien.
    /// </summary>
    [Fact]
    public async Task Un_refus_d_identifiants_n_efface_JAMAIS_le_coffre()
    {
        var (autorite, _, _, chemin) = Autorite(Maintenant.AddMinutes(-1),
            FakeHttpMessageHandler.Json(HttpStatusCode.BadRequest, CorpsInvalidGrant));

        Assert.Null(await autorite.GetAccessTokenAsync());
        Assert.Equal(EtatAuthentification.Deconnecte, autorite.Etat);
        Assert.True(File.Exists(chemin));
        Assert.Equal("REF-1", new ChronosOAuthStore(chemin).Load()!.RefreshToken);
    }

    /// <summary>
    /// VERROU DÉFINITIF. Sans lui, le tick de 60 s tenterait 1 440 rafraîchissements par jour sur un
    /// refus irréparable — et transformerait une panne de compte en 429 sur le point de terminaison de
    /// jeton (429 vérifié en direct le 2026-09-09). Seul un login relâche le verrou.
    /// </summary>
    [Fact]
    public async Task Apres_un_refus_definitif_cinq_appels_de_plus_n_emettent_AUCUNE_requete()
    {
        var (autorite, handler, _, _) = Autorite(Maintenant.AddMinutes(-1),
            FakeHttpMessageHandler.Json(HttpStatusCode.BadRequest, CorpsInvalidGrant));

        Assert.Null(await autorite.GetAccessTokenAsync());

        for (var i = 0; i < 5; i++) Assert.Null(await autorite.GetAccessTokenAsync());

        Assert.Equal(1, handler.SendCount);   // aucun réessai automatique sur l'irréparable
        Assert.Equal(EtatAuthentification.Deconnecte, autorite.Etat);
    }

    // ------------------------------------------------------------------------------------------
    // 6. Le recul — porté par l'AUTORITÉ, indépendamment de tout cache d'usage
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// DÉFAUT D'AVANT-PHASE ÉRADIQUÉ (découverte du plan 17-01, commit <c>e3e326c</c>). Le garde-fou
    /// anti-429 de <c>ChronosOAuthUsageProvider.cs:53</c> exige DEUX conditions : <c>now &lt;
    /// _nextAllowedCall</c> ET <c>_cached is not null</c>. Or <c>_cached</c> est un champ d'instance en
    /// RAM, donc VIDE à chaque démarrage de l'exe : un exe qui redémarre avec un jeton mort remartèle
    /// l'endpoint à chaque tick sans aucun frein, ce qui ENTRETIENT le 429 qui l'a causé — le cas
    /// nominal de la panne de deux mois de l'utilisateur.
    ///
    /// L'autorité porte SA PROPRE politique de recul, qui ne consulte aucun cache d'usage : le frein
    /// mord dès le PREMIER échec, sur une instance qui n'a jamais rien réussi.
    /// </summary>
    [Fact]
    public async Task Le_recul_freine_des_le_PREMIER_429_sans_aucun_cache_prealable()
    {
        var (autorite, handler, _, _) = Autorite(Maintenant.AddMinutes(-1),
            FakeHttpMessageHandler.Json(HttpStatusCode.TooManyRequests, CorpsRateLimit));

        Assert.Null(await autorite.GetAccessTokenAsync());   // 1er échec : le recul se pose
        Assert.Equal(1, handler.SendCount);
        Assert.Equal(EtatAuthentification.HorsLigne, autorite.Etat);

        // Rappel immédiat (même instant d'horloge) : AUCUNE requête. Aucun succès préalable n'a été
        // nécessaire pour que le frein existe — c'est toute la différence avec le provider d'usage.
        Assert.Null(await autorite.GetAccessTokenAsync());
        Assert.Equal(1, handler.SendCount);
    }

    /// <summary>Le recul initial est de 2 min : 1 min ne suffit pas, 3 min oui.</summary>
    [Fact]
    public async Task Le_recul_initial_de_deux_minutes_expire_et_autorise_un_nouvel_essai()
    {
        var (autorite, handler, horloge, _) = Autorite(Maintenant.AddMinutes(-1),
            FakeHttpMessageHandler.Json(HttpStatusCode.InternalServerError, ""));

        Assert.Null(await autorite.GetAccessTokenAsync());

        horloge.UtcNow = Maintenant.AddMinutes(1);            // dans la fenêtre de recul
        Assert.Null(await autorite.GetAccessTokenAsync());
        Assert.Equal(1, handler.SendCount);

        horloge.UtcNow = Maintenant.AddMinutes(3);            // recul expiré
        Assert.Null(await autorite.GetAccessTokenAsync());
        Assert.Equal(2, handler.SendCount);
    }

    /// <summary>Après un second échec, le recul DOUBLE : 3 min ne suffisent plus, 5 min oui.</summary>
    [Fact]
    public async Task Le_recul_double_apres_chaque_echec_temporaire()
    {
        var (autorite, handler, horloge, _) = Autorite(Maintenant.AddMinutes(-1),
            FakeHttpMessageHandler.Json(HttpStatusCode.InternalServerError, ""));

        await autorite.GetAccessTokenAsync();                 // échec 1 => fenêtre 2 min
        horloge.UtcNow = Maintenant.AddMinutes(3);
        await autorite.GetAccessTokenAsync();                 // échec 2 => fenêtre 4 min
        Assert.Equal(2, handler.SendCount);

        horloge.UtcNow = Maintenant.AddMinutes(3 + 3);        // 3 min : insuffisant (fenêtre 4 min)
        await autorite.GetAccessTokenAsync();
        Assert.Equal(2, handler.SendCount);

        horloge.UtcNow = Maintenant.AddMinutes(3 + 5);        // 5 min : suffisant
        await autorite.GetAccessTokenAsync();
        Assert.Equal(3, handler.SendCount);
    }

    /// <summary>Le recul est PLAFONNÉ à 30 min : un doublement non borné finirait par ne plus jamais
    /// réessayer, et une panne réseau de 40 min laisserait l'overlay muet des heures.</summary>
    [Fact]
    public async Task Le_recul_est_plafonne_a_trente_minutes()
    {
        var (autorite, handler, horloge, _) = Autorite(Maintenant.AddMinutes(-1),
            FakeHttpMessageHandler.Json(HttpStatusCode.InternalServerError, ""));

        // 5 échecs : fenêtres successives 2, 4, 8, 16 puis 30 min (2+2=4, 4+4=8, 8+8=16, 16+16>30 => 30).
        var t = Maintenant;
        for (var i = 0; i < 5; i++)
        {
            horloge.UtcNow = t;
            await autorite.GetAccessTokenAsync();
            t = t.AddMinutes(60);                             // dépasse toute fenêtre
        }
        Assert.Equal(5, handler.SendCount);

        var dernierEchec = t.AddMinutes(-60);
        horloge.UtcNow = dernierEchec.AddMinutes(29);         // sous le plafond
        await autorite.GetAccessTokenAsync();
        Assert.Equal(5, handler.SendCount);

        horloge.UtcNow = dernierEchec.AddMinutes(31);         // au-delà du plafond
        await autorite.GetAccessTokenAsync();
        Assert.Equal(6, handler.SendCount);
    }

    /// <summary>Un succès remet le recul à son plancher de 2 min : une panne passagère ne doit pas
    /// laisser un frein de 30 min derrière elle.</summary>
    [Fact]
    public async Task Un_succes_remet_le_recul_a_son_plancher()
    {
        var (autorite, handler, horloge, _) = Autorite(Maintenant.AddMinutes(-1), Sequence(
            (HttpStatusCode.InternalServerError, ""),
            (HttpStatusCode.OK, CorpsRefresh200),
            (HttpStatusCode.InternalServerError, "")));

        await autorite.GetAccessTokenAsync();                 // échec => fenêtre 2 min, recul suivant 4 min
        horloge.UtcNow = Maintenant.AddMinutes(3);
        Assert.Equal("ACC-2", await autorite.GetAccessTokenAsync());   // succès => recul remis à 2 min

        autorite.InvaliderAccessToken();                      // forcer un nouveau rafraîchissement
        await autorite.GetAccessTokenAsync();                 // échec => fenêtre 2 min (et non 4)
        Assert.Equal(3, handler.SendCount);

        // 3 min suffisent : si le recul n'avait PAS été remis à zéro, la fenêtre vaudrait 4 min.
        horloge.UtcNow = Maintenant.AddMinutes(3 + 3);
        await autorite.GetAccessTokenAsync();
        Assert.Equal(4, handler.SendCount);
    }

    // ------------------------------------------------------------------------------------------
    // 7. L'état ne s'émet que sur TRANSITION
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Une_transition_n_emet_qu_UN_evenement_meme_apres_trois_echecs()
    {
        var (autorite, _, horloge, _) = Autorite(Maintenant.AddMinutes(-1),
            FakeHttpMessageHandler.Json(HttpStatusCode.InternalServerError, ""));
        var emis = new List<EtatAuthentification>();
        autorite.EtatChange += (_, e) => emis.Add(e);

        for (var i = 0; i < 3; i++)
        {
            await autorite.GetAccessTokenAsync();
            horloge.UtcNow = horloge.UtcNow.AddMinutes(40);   // dépasse tout recul
        }

        Assert.Single(emis);
        Assert.Equal(EtatAuthentification.HorsLigne, emis[0]);
    }

    // ------------------------------------------------------------------------------------------
    // 8. Les quatre méthodes de pilotage
    // ------------------------------------------------------------------------------------------

    /// <summary>Le serveur peut révoquer un access token AVANT son ExpiresAt local (cas documenté) :
    /// le rafraîchissement préventif seul ne suffit donc pas, il faut pouvoir forcer.</summary>
    [Fact]
    public async Task InvaliderAccessToken_force_le_rafraichissement_d_un_jeton_pourtant_valide()
    {
        var (autorite, handler, _, _) = Autorite(Maintenant.AddHours(1));   // largement valide

        Assert.Equal("ACC-1", await autorite.GetAccessTokenAsync());
        Assert.Equal(0, handler.SendCount);

        autorite.InvaliderAccessToken();

        Assert.Equal("ACC-2", await autorite.GetAccessTokenAsync());
        Assert.Equal(1, handler.SendCount);
    }

    /// <summary>TOK-03 : sans ce réarmement, le jeton tout neuf écrit par le login ne serait pas utilisé
    /// et la pastille survivrait à sa propre réparation.</summary>
    [Fact]
    public async Task ReinitialiserApresLogin_relache_le_verrou_ET_relit_le_coffre()
    {
        var (autorite, handler, _, chemin) = Autorite(Maintenant.AddMinutes(-1), Sequence(
            (HttpStatusCode.BadRequest, CorpsInvalidGrant),
            (HttpStatusCode.OK, CorpsRefresh200)));

        Assert.Null(await autorite.GetAccessTokenAsync());
        Assert.Equal(EtatAuthentification.Deconnecte, autorite.Etat);
        Assert.Null(await autorite.GetAccessTokenAsync());
        Assert.Equal(1, handler.SendCount);                   // verrou en place

        // Le login réécrit le coffre (motif Views/OAuthLogin.cs : Save, jamais Clear).
        new ChronosOAuthStore(chemin).Save(new OAuthTokens("ACC-N", "REF-N", Maintenant.AddMinutes(-1)));
        autorite.ReinitialiserApresLogin();

        Assert.Equal("ACC-2", await autorite.GetAccessTokenAsync());
        Assert.Equal(2, handler.SendCount);
        Assert.Equal(EtatAuthentification.Connecte, autorite.Etat);
    }

    /// <summary>Critère de succès 3 de la phase : la pastille disparaît dès qu'un chiffre exact est de
    /// nouveau obtenu, même après une période « Deconnecte ».</summary>
    [Fact]
    public async Task SignalerSucces_deverrouille_l_etat_meme_apres_une_deconnexion()
    {
        var (autorite, _, _, _) = Autorite(Maintenant.AddMinutes(-1),
            FakeHttpMessageHandler.Json(HttpStatusCode.BadRequest, CorpsInvalidGrant));
        await autorite.GetAccessTokenAsync();
        Assert.Equal(EtatAuthentification.Deconnecte, autorite.Etat);

        autorite.SignalerSucces();

        Assert.Equal(EtatAuthentification.Connecte, autorite.Etat);
    }

    /// <summary>Le serveur a refusé un access token FRAÎCHEMENT rafraîchi : ce n'est pas un problème de
    /// fraîcheur, c'est un refus de compte.</summary>
    [Fact]
    public void SignalerRefusServeur_publie_une_deconnexion()
    {
        var (autorite, _, _, _) = Autorite(Maintenant.AddHours(1));
        var emis = new List<EtatAuthentification>();
        autorite.EtatChange += (_, e) => emis.Add(e);

        autorite.SignalerRefusServeur();

        Assert.Equal(EtatAuthentification.Deconnecte, autorite.Etat);
        Assert.Equal(new[] { EtatAuthentification.Deconnecte }, emis);
    }

    // ------------------------------------------------------------------------------------------
    // 9. Sécurité structurelle et cycle de vie
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// SÉCURITÉ STRUCTURELLE (invariant 6) : le jeton ne quitte l'autorité que comme <c>string</c>
    /// d'access token. Aucun membre public ne rend un <see cref="OAuthTokens"/> — sinon le refresh
    /// token fuirait vers des appelants qui n'ont aucune raison de le connaître, et la garantie
    /// « une seule rotation dans le processus » tomberait avec lui.
    /// </summary>
    [Fact]
    public void Aucun_membre_public_de_l_autorite_ne_laisse_sortir_le_refresh_token()
    {
        var t = typeof(ChronosTokenAuthority);

        var fuites = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => (nom: m.Name, type: m.ReturnType))
            .Concat(t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Select(p => (nom: p.Name, type: p.PropertyType)))
            .Where(x => x.type == typeof(OAuthTokens) || x.type == typeof(Task<OAuthTokens>))
            .Select(x => x.nom)
            .ToList();

        Assert.Empty(fuites);
    }

    [Fact]
    public void Dispose_est_idempotent_et_ne_leve_jamais()
    {
        var (autorite, _, _, _) = Autorite(Maintenant.AddHours(1));

        var ex = Record.Exception(() => { autorite.Dispose(); autorite.Dispose(); });

        Assert.Null(ex);
    }
}
