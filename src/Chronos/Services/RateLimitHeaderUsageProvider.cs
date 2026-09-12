using System.IO;
using System.Net.Http;
using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// HDR-01 / HDR-02 / HDR-06 — LA SONDE. Provider EXACT qui n'obtient pas ses chiffres d'un corps de
/// réponse mais des EN-TÊTES d'une requête JETABLE : <c>POST /v1/messages</c>, <c>max_tokens</c> à 1,
/// sur le modèle le moins cher de la gamme. Deux fenêtres exactes, sans pont statusLine, sans
/// <c>usage.json</c>.
///
/// RAISON D'ÊTRE : répondre MÊME QUAND L'API REFUSE. <c>GET /api/oauth/usage</c> ne rend rien
/// d'utile en erreur (vérifié : son 401 est rendu en bordure, <c>request_id</c> nul, aucun en-tête de
/// limite). Une requête de messages, elle, porte ses compteurs dans ses en-têtes — et c'est
/// précisément à l'instant où l'API sature que l'overlay sert le plus.
///
/// FORME IMPOSÉE PAR TROIS FAITS VÉRIFIÉS EMPIRIQUEMENT LE 2026-09-12 :
/// <list type="number">
///   <item>l'envoi ne LÈVE pas sur un 429 : la réponse arrive intacte, en-têtes compris ;</item>
///   <item>l'exception de transport ne porte AUCUNE collection d'en-têtes — contrôler le succès par
///         exception détruirait donc l'information qui fait tout l'intérêt de cette source. Le
///         contrôle du code de statut n'est JAMAIS le chemin de contrôle : les en-têtes sont lus
///         AVANT toute décision, et l'on n'aiguille ensuite que sur <c>(int)resp.StatusCode</c> ;</item>
///   <item>les en-têtes non standard vivent sur la RÉPONSE, pas sur son contenu, et la recherche est
///         insensible à la casse — aucune normalisation de casse à écrire.</item>
/// </list>
///
/// ELLE N'EST PAS UN RAFRAÎCHISSEUR DE JETON, et ne peut pas le devenir : elle ne connaît ni le
/// coffre chiffré, ni le jeton de renouvellement, ni le client du protocole. Elle demande un jeton
/// d'accès à <see cref="ChronosTokenAuthority"/> et à personne d'autre. POURQUOI ce n'est pas
/// négociable : le flux OAuth fait TOURNER le jeton de renouvellement, et deux détenteurs concurrents
/// présentant le même produisent un <c>invalid_grant</c> sur le second — donc une FAUSSE déconnexion
/// sur un compte parfaitement sain (bug ouvert <c>anthropics/claude-code#25609</c>).
///
/// LE CORPS DE LA RÉPONSE N'EST JAMAIS LU, donc jamais journalisé ni transporté : aucune désérialisation,
/// aucune lecture de flux, aucune remontée de texte venu du réseau. <c>using (resp)</c> suffit. Corollaire
/// assumé : on ne peut pas distinguer un 429 de quota d'un 429 de plafond de dépense — et l'on n'en a
/// pas besoin, l'absence d'en-tête unifié suffit à décider de ne rien afficher.
///
/// ELLE EST AUSSI LE CANAL LATÉRAL <see cref="IEtatServeur"/> (HDR-03 / HDR-04), et non un service
/// parallèle qui devrait se resynchroniser : la même instance sera réexposée sous les deux contrats en DI
/// (motif <see cref="ChronosTokenAuthority"/> / <c>IAuthStatus</c>, plan 18-05). POURQUOI un canal en plus
/// des champs de <c>WindowState</c> : quand la forme « dépassement » est SEULE, les deux fenêtres de la
/// sonde sont légitimement indisponibles, et <c>Best()</c> retient alors l'instance d'une AUTRE source
/// <c>Exact</c> — le dépassement porté par la fenêtre écartée disparaîtrait sans bruit. Le statut serveur,
/// lui, n'a pas besoin de canal : il DÉCRIT une fenêtre, donc il doit mourir avec elle.
///
/// Type NEUTRE : aucun type WPF. Aucune horloge système : tout passe par <see cref="IClock"/>, sans quoi
/// aucun test de cadence ne serait déterministe.
/// </summary>
public sealed class RateLimitHeaderUsageProvider : IUsageProvider, IEtatServeur
{
    // --- Requête. Constantes GROUPÉES et DATÉES : chaque nom d'en-tête est une HYPOTHÈSE, pas un fait. ---

    private const string Url = "https://api.anthropic.com/v1/messages";

    /// <summary>PROUVÉ obligatoire le 2026-09-12 : sans cet en-tête, le serveur interprète le jeton
    /// porteur comme une CLÉ D'API et répond 401 « invalid x-api-key » — un message qui ne parle même pas
    /// du protocole OAuth. On en conclurait à un jeton mort et l'on allumerait une FAUSSE pastille de
    /// déconnexion.</summary>
    private const string Beta = "oauth-2025-04-20";

    private const string VersionApi = "2023-06-01";

    /// <summary>Même valeur que le provider d'usage OAuth (ChronosOAuthUsageProvider:41) : sans
    /// identification cliente crédible, le serveur devient agressif en limitation.</summary>
    private const string UserAgent = "claude-code/2.1.30";

    /// <summary>ALIAS documenté, et non un instantané daté : <c>claude-haiku-4-5-20251001</c> est Active
    /// mais son plancher de retrait (2026-10-15) est le plus proche de toute la gamme — l'alias survit au
    /// remplacement du snapshot. Modèle le MOINS CHER : 1 $/MTok en entrée, 5 $/MTok en sortie. Vérifié le
    /// 2026-09-12 sur la documentation officielle.</summary>
    private const string Modele = "claude-haiku-4-5";

    // Corps MINIMAL, et rien de plus : aucun prompt système, aucun outil (400 documenté avec un jeton
    // OAuth), aucun réglage d'échantillonnage (400 sur les modèles >= 4.7), aucun effort (non supporté par
    // Haiku 4.5), aucune métadonnée. Un prompt système volumineux ferait basculer la requête sur la voie
    // « crédits d'API » et rendrait un 429 de plafond de DÉPENSE, qui ne parle pas du quota d'abonnement.
    // Le modèle est INTERPOLÉ depuis la constante ci-dessus (interpolation constante, résolue à la
    // compilation) : le corps ne peut pas diverger de l'identifiant annoncé.
    private const string Corps =
        $$"""{"model":"{{Modele}}","max_tokens":1,"messages":[{"role":"user","content":"hi"}]}""";

    // --- En-têtes de RÉPONSE. Noms et niveaux de confiance : cf. tests/EnTetesDeReference.cs. ---

    // Chaque nom est une HYPOTHÈSE, pas un fait : la famille anthropic-ratelimit-unified-* est ABSENTE de
    // la documentation publique Anthropic (qui ne documente que anthropic-ratelimit-{requests,tokens,
    // input-tokens,output-tokens}-*, avec un reset en RFC 3339 et non en epoch). Deux écarts ont déjà été
    // relevés entre 18-CONTEXT.md et le code d'origine. D'où : plusieurs noms candidats par information,
    // absence -> null, et un test par en-tête absent.

    private const string H5hUtil = "anthropic-ratelimit-unified-5h-utilization";    // fraction 0..1, TEXTE
    private const string H5hReset = "anthropic-ratelimit-unified-5h-reset";         // epoch SECONDES, TEXTE
    private const string H7dUtil = "anthropic-ratelimit-unified-7d-utilization";    // fraction 0..1, TEXTE
    private const string H7dReset = "anthropic-ratelimit-unified-7d-reset";         // epoch SECONDES, TEXTE

    /// <summary>HDR-03 — statut de la fenêtre de 5 h. CONFIANCE HAUTE : lu par le code d'origine ET observé
    /// dans un dump indépendant (valeur vue : <c>allowed</c>).</summary>
    private const string H5hStatut = "anthropic-ratelimit-unified-5h-status";

    /// <summary>HDR-03 — statut de la fenêtre hebdomadaire. CONFIANCE NULLE : aucune observation, aucune
    /// lecture dans le code d'origine. Lecture strictement OPTIONNELLE — son absence n'est PAS une
    /// anomalie, et ne doit jamais être comblée par une valeur devinée.</summary>
    private const string H7dStatut = "anthropic-ratelimit-unified-7d-status";

    /// <summary>HDR-04 — fraction d'usage en DÉPASSEMENT, en 0..1. CONFIANCE MOYENNE : lu par le code
    /// d'origine, jamais observé sur cette machine (autre type d'abonnement).</summary>
    private const string HOverUtil = "anthropic-ratelimit-unified-overage-utilization";

    /// <summary>HDR-04 — instant de reset du dépassement, epoch SECONDES. CONFIANCE MOYENNE : lu par le code
    /// d'origine, jamais observé ici.</summary>
    private const string HOverReset = "anthropic-ratelimit-unified-overage-reset";

    /// <summary>HDR-04 — statut du dépassement. CONFIANCE NULLE : annoncé par 18-CONTEXT.md, mais le code
    /// d'origine lit le statut global SANS segment. Lu par précaution, jamais exigé.</summary>
    private const string HOverStatut = "anthropic-ratelimit-unified-overage-status";

    /// <summary>HDR-03 — statut GLOBAL, SANS segment de fenêtre. CONFIANCE MOYENNE : c'est le nom que lit
    /// réellement le code d'origine dans sa branche de dépassement — correction d'un écart de
    /// 18-CONTEXT.md, établie par lecture du code. Comme c'est un statut de COMPTE et non de fenêtre, il ne
    /// sert QUE de repli : un statut nommé par fenêtre l'emporte toujours sur lui.</summary>
    private const string HStatutGlobal = "anthropic-ratelimit-unified-status";

    /// <summary>Optionnel et purement informatif (ex. <c>five_hour</c>) : lu UNIQUEMENT pour alimenter
    /// <see cref="NomsEnTetesRecus"/>. Sa valeur n'est exploitée par aucun calcul de la phase 18.</summary>
    private const string HClaim = "anthropic-ratelimit-unified-representative-claim";

    // --- Cadence et reculs. ---

    /// <summary>Aligné sur le délai du provider d'usage OAuth (8 s) : au-delà, l'overlay attendrait
    /// visiblement plus longtemps qu'un tick de rafraîchissement.</summary>
    private static readonly TimeSpan Delai = TimeSpan.FromSeconds(8);

    /// <summary>HDR-06 — au plus UNE sonde par cadence. 300 s est la valeur de <c>UsageWatcher.INTERVAL</c>
    /// chez l'auteur d'origine ; son <c>POLL_INTERVAL</c> de 60 s gouverne l'AFFICHAGE LOCAL, pas la sonde.
    /// Sonder plus souvent n'apprend RIEN, puisque l'instant de reset est DONNÉ par l'en-tête : on
    /// dépenserait du quota pour réapprendre un chiffre déjà connu. Publique parce qu'elle est le chiffre
    /// annoncé à l'utilisateur dans les réglages — un test la verrouille.</summary>
    public static readonly TimeSpan CadenceNominale = TimeSpan.FromSeconds(300);

    /// <summary>Plancher du recul après un 429. Une sonde REJETÉE ne consomme pas de quota : on peut donc
    /// rester réactif précisément pendant la saturation — l'instant où l'overlay sert le plus.</summary>
    private static readonly TimeSpan Recul429Plancher = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan ReculReseauInitial = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ReculReseauMax = TimeSpan.FromMinutes(15);

    /// <summary>Un identifiant de modèle périmé est une panne de CONFIGURATION, pas de quota : réessayer
    /// toutes les 5 minutes un modèle qui n'existe plus ne répare rien et consomme.</summary>
    private static readonly TimeSpan ReculModeleRefuse = TimeSpan.FromMinutes(60);

    /// <summary>Volontairement PLUS COURT que les 15 min de cache du provider d'usage OAuth, parce que
    /// <c>CompositeUsageProvider.Best()</c> privilégie le <c>primary</c> à fiabilité égale : un cache long
    /// de la sonde battrait une lecture FRAÎCHE de <c>/api/oauth/usage</c>. Le classement par fraîcheur est
    /// la phase 19 ; en attendant, la parade est de ne pas servir de cache vieux.</summary>
    private static readonly TimeSpan CacheUtilisable = TimeSpan.FromSeconds(300);

    private readonly ChronosTokenAuthority _autorite;
    private readonly HttpClient _http;
    private readonly IClock _clock;
    private readonly SettingsService _settings;

    private UsageSnapshot? _cache;
    private DateTimeOffset _cacheAt;
    private DateTimeOffset _prochainAppelAutorise;
    private TimeSpan _reculReseau;

    public RateLimitHeaderUsageProvider(
        ChronosTokenAuthority autorite, HttpClient http, IClock clock, SettingsService settings)
    {
        _autorite = autorite;
        _http = http;
        _clock = clock;
        _settings = settings;
    }

    /// <summary>Issue du DERNIER passage. Observabilité HDR-01/02/06 : un vocabulaire fermé, jamais une
    /// chaîne venue du réseau. Satisfera <see cref="IEtatServeur"/> au plan 18-04.</summary>
    public ResultatSonde DernierResultat { get; private set; } = ResultatSonde.JamaisSondee;

    /// <summary>Noms des en-têtes unifiés RÉELLEMENT reçus au dernier passage. Ce sont les CONSTANTES
    /// locales, jamais l'orthographe du serveur : aucune chaîne venant du réseau ne remonte au
    /// diagnostic. Vide = la famille a été renommée, ou ce plan n'en a pas.</summary>
    public IReadOnlyList<string> NomsEnTetesRecus { get; private set; } = Array.Empty<string>();

    /// <summary>HDR-04 — dernier dépassement rapporté. null = rien rapporté. Mis à jour UNIQUEMENT quand
    /// des en-têtes ont effectivement été lus (2xx ou 429) : une panne de transport ne doit pas effacer un
    /// fait de compte, et un frein actif n'apprend rien de neuf.</summary>
    public EtatDepassement? Depassement { get; private set; }

    /// <summary>Émis sur un thread du POOL, sur TRANSITION uniquement. L'abonné marshalle lui-même
    /// (frontière RAF-04). Motif ChronosTokenAuthority.EtatChange / RefreshOrchestrator.SnapshotChanged.</summary>
    public event EventHandler<EtatDepassement?>? DepassementChange;

    public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        // INTERRUPTEUR, relu FRAIS à chaque appel (motif GatedOAuthUsageProvider) : une bascule dans les
        // réglages prend effet au prochain passage, sans redémarrage. Court-circuit AVANT toute demande de
        // jeton et avant tout accès réseau — couper la seule source qui DÉPENSE doit réellement tout
        // couper. Et l'on ne sert PAS le cache ici : couper la sonde doit la faire taire, pas la faire
        // ressasser un chiffre périmé.
        if (!_settings.Load().SondeEnTetesActivee)
        {
            DernierResultat = ResultatSonde.Desactivee;
            return UsageSnapshot.Empty;
        }

        // FREIN INCONDITIONNEL (HDR-06). « Ai-je le droit d'appeler ? » est une question DISTINCTE de
        // « ai-je un cache à servir ? » : les confondre — le défaut n° 3 corrigé en phase 17 — faisait
        // disparaître le frein à CHAQUE redémarrage de l'exe, puisque le cache vit en RAM. Donc
        // exactement quand le martèlement se produit.
        if (now < _prochainAppelAutorise)
        {
            DernierResultat = ResultatSonde.FreinActif;
            return ServirCacheOu(UsageSnapshot.Empty, now);
        }

        // SOURCE UNIQUE DE JETON. Coffre vide = l'utilisateur ne s'est jamais connecté : ce n'est PAS une
        // panne, et l'état est déjà publié par l'autorité — ne rien republier ici.
        var jeton = await _autorite.GetAccessTokenAsync(ct);
        if (jeton is null)
        {
            DernierResultat = ResultatSonde.PasDeJeton;
            return ServirCacheOu(UsageSnapshot.Empty, now);
        }

        try
        {
            var resp = await EnvoyerAsync(jeton, ct);
            var rejoue = false;

            // Le serveur peut révoquer un jeton AVANT son expiration locale (cas documenté
            // claude-code#54443) : le renouvellement préventif ne suffit donc pas. UN SEUL rejeu — sans
            // cette garde, on boucle jusqu'à déclencher une limitation sur le point de terminaison de jeton.
            if ((int)resp.StatusCode == 401 && !rejoue)
            {
                resp.Dispose();
                rejoue = true;
                _autorite.InvaliderAccessToken();
                var frais = await _autorite.GetAccessTokenAsync(ct);
                if (frais is null)
                {
                    _prochainAppelAutorise = now + CadenceNominale;
                    DernierResultat = ResultatSonde.PasDeJeton;
                    return ServirCacheOu(UsageSnapshot.Empty, now);
                }
                resp = await EnvoyerAsync(frais, ct);
            }

            using (resp)
            {
                // HDR-02, LITTÉRALEMENT : les en-têtes sont lus ICI, AVANT toute décision sur le code de
                // statut. C'est CET ORDRE qui est la raison d'être de la phase — un refus PORTE les
                // chiffres, et tout contrôle de succès placé en amont les jetterait.
                var (snap, noms) = LireEnTetes(resp, now);
                NomsEnTetesRecus = noms;
                var exploitables = EnTetesExploitables(snap);

                var code = (int)resp.StatusCode;

                if (code is 401 or 403)
                {
                    // Jeton FRAIS et pourtant refusé (ou portée insuffisante) : ce n'est pas un problème
                    // de fraîcheur, c'est un refus de compte. Seule une reconnexion répare. Et un 401 ne
                    // porte AUCUN en-tête de limite (confirmé sur 6 sondes réelles) : rien à espérer.
                    _autorite.SignalerRefusServeur();
                    _prochainAppelAutorise = now + CadenceNominale;
                    DernierResultat = ResultatSonde.RefusServeur;
                    return ServirCacheOu(UsageSnapshot.Empty, now);
                }

                if (code == 429)
                {
                    _prochainAppelAutorise = now + Recul429(resp, now);

                    // Branche SaturationEnTetesLus / SaturationSansEnTetes : les en-têtes ONT été lus — le
                    // serveur a répondu. Qu'il ne rapporte aucun dépassement est une information, pas un
                    // silence de transport : publier null est donc correct ici, alors que ce serait un
                    // mensonge sur une panne réseau ou derrière le frein.
                    PublierDepassement(snap.FiveHour.Depassement);

                    if (exploitables)
                    {
                        // Un 429 PROUVE que le jeton est valide : ne pas le signaler laisserait la
                        // pastille de déconnexion MENTIR pendant toute la saturation. Différence ASSUMÉE
                        // avec le provider /api/oauth/usage, qui sur 429 se contente de reculer parce
                        // qu'il n'apprend rien.
                        _autorite.SignalerSucces();
                        _reculReseau = TimeSpan.Zero;
                        _cache = snap;
                        _cacheAt = now;
                        DernierResultat = ResultatSonde.SaturationEnTetesLus;
                        return snap;
                    }

                    // 429 MUET. Sans lire le corps, impossible de distinguer un 429 de quota d'un 429 de
                    // plafond de dépense — et l'on n'en a PAS besoin : l'absence d'en-tête unifié suffit à
                    // décider de ne rien afficher. Ni 0 %, ni 100 %, et surtout aucune déconnexion.
                    DernierResultat = ResultatSonde.SaturationSansEnTetes;
                    return ServirCacheOu(UsageSnapshot.Empty, now);
                }

                if (code is 400 or 404)
                {
                    // Panne de CONFIGURATION nommée (identifiant de modèle périmé), jamais confondue avec
                    // « pas de données ». L'authentification étant évaluée AVANT le corps, un modèle
                    // périmé ne peut pas se déguiser en problème d'authentification.
                    _prochainAppelAutorise = now + ReculModeleRefuse;
                    DernierResultat = ResultatSonde.ModeleRefuse;
                    return ServirCacheOu(UsageSnapshot.Empty, now);
                }

                if (resp.IsSuccessStatusCode)
                {
                    _autorite.SignalerSucces();   // un 2xx déverrouille l'état, même sans chiffres
                    _reculReseau = TimeSpan.Zero;
                    _prochainAppelAutorise = now + CadenceNominale;

                    // Branche SuccesEnTetesLus / SuccesSansEnTetes : même raison que sur le 429 — le serveur
                    // a parlé, donc son silence sur le dépassement est une réponse et non une absence.
                    PublierDepassement(snap.FiveHour.Depassement);

                    if (exploitables)
                    {
                        _cache = snap;
                        _cacheAt = now;
                        DernierResultat = ResultatSonde.SuccesEnTetesLus;
                        return snap;
                    }

                    // 2xx sans la famille unifiée : signal que les noms ont changé côté serveur, ou que ce
                    // plan n'en a pas. À DIRE, jamais à confondre avec « pas de données ».
                    DernierResultat = ResultatSonde.SuccesSansEnTetes;
                    return ServirCacheOu(UsageSnapshot.Empty, now);
                }

                // 5xx et tout le reste : indisponibilité transitoire du serveur.
                ReculerReseau(now);
                DernierResultat = ResultatSonde.PanneReseau;
                return ServirCacheOu(UsageSnapshot.Empty, now);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                     or OperationCanceledException or IOException)
        {
            // Réseau / délai dépassé : surtout PAS de « déconnecté » — c'est peut-être seulement le wifi.
            ReculerReseau(now);
            DernierResultat = ResultatSonde.PanneReseau;
            return ServirCacheOu(UsageSnapshot.Empty, now);
        }
    }

    // En-tête de RÉPONSE : il vit sur la réponse. Le repli sur les en-têtes de CONTENU ne devrait JAMAIS
    // servir (vérifié) — il est là pour qu'un jour où le serveur classerait l'en-tête autrement, on ne
    // perde pas la donnée en silence. Le nom AJOUTÉ à la liste est la CONSTANTE locale, jamais
    // l'orthographe du serveur : aucune chaîne venant du réseau ne remonte au diagnostic.
    private static string? Lire(HttpResponseMessage resp, string nom, List<string> noms)
    {
        string? v = null;
        if (resp.Headers.TryGetValues(nom, out var a)) v = a.FirstOrDefault();
        else if (resp.Content?.Headers.TryGetValues(nom, out var b) == true) v = b.FirstOrDefault();
        // SANS DOUBLON : le statut GLOBAL est SONDÉ plusieurs fois (repli des deux fenêtres, puis du
        // dépassement). Un nom répété dans le rapport de diagnostic ne dirait rien de plus sur le serveur,
        // seulement quelque chose sur notre ordre de lecture. L'ordre de première apparition est conservé.
        if (v is not null && !noms.Contains(nom)) noms.Add(nom);
        return v;
    }

    // Une seule et même lecture, quel que soit le code de statut.
    private static (UsageSnapshot Snap, IReadOnlyList<string> Noms) LireEnTetes(
        HttpResponseMessage resp, DateTimeOffset now)
    {
        var noms = new List<string>();

        // UNE SEULE lecture de la famille de dépassement, AVANT les fenêtres : c'est un fait de COMPTE, pas
        // une propriété de fenêtre. La même instance est ensuite posée à l'identique sur les deux — ce qui
        // permet aussi à EnTetesExploitables de n'en tester qu'une.
        var depassement = LireDepassement(resp, noms);

        var cinqHeures = LireFenetre(resp, noms, WindowKind.FiveHour, H5hUtil, H5hReset, H5hStatut,
                                     TimeSpan.FromHours(5), now, depassement);
        var hebdo = LireFenetre(resp, noms, WindowKind.SevenDay, H7dUtil, H7dReset, H7dStatut,
                                TimeSpan.FromDays(7), now, depassement);

        Lire(resp, HClaim, noms);   // informatif : alimente l'observabilité, aucun calcul ne l'utilise

        var snap = new UsageSnapshot { FiveHour = cinqHeures, SevenDay = hebdo, SourceCapturedAt = now };
        return (snap, noms);
    }

    // Lecture TOLÉRANTE, en-tête par en-tête : une valeur illisible n'invalide pas l'autre fenêtre, ni
    // même l'autre moitié de la sienne. Toute conversion passe par le point unique (piège de culture
    // fr-FR : une lecture « naturelle » rendrait false sur « 0.63 », donc une perte SILENCIEUSE).
    private static WindowState LireFenetre(HttpResponseMessage resp, List<string> noms, WindowKind kind,
                                           string nomUtil, string nomReset, string nomStatut,
                                           TimeSpan longueurFenetre, DateTimeOffset now,
                                           EtatDepassement? depassement)
    {
        var util = UsageNormalization.FractionDepuisTexteFraction(Lire(resp, nomUtil, noms));
        var reset = UsageNormalization.InstantDepuisTexteEpoch(Lire(resp, nomReset, noms));

        // Un statut sans chiffre n'a rien à décrire : Unavailable doit rester neutre (et son test
        // WindowStateTests.Unavailable_neutralise_les_champs_et_garde_la_fenetre l'exige).
        // On lit quand même l'en-tête de statut pour le déclarer dans NomsEnTetesRecus — le diagnostic doit
        // pouvoir dire « le serveur a envoyé un statut mais aucun chiffre ».
        var statut = LireStatut(resp, nomStatut, noms);

        // En-tête ABSENT n'est PAS « 0 % ». Le code d'origine rend 0 dans sa branche d'erreur ; transposé
        // tel quel, un en-tête manquant afficherait « 0 % de quota consommé » — le mensonge exactement
        // inverse de celui que v1.5 corrige.
        // WindowState.Unavailable(kind) reste la FABRIQUE NEUTRE (son test de neutralité l'exige, et la
        // phase 19 s'appuie dessus) : on ne l'élargit pas. Ici on construit explicitement parce qu'une
        // fenêtre indisponible peut néanmoins TRANSPORTER un fait de compte — c'est exactement la forme
        // « dépassement seul », où les deux fenêtres sont légitimement inconnues.
        if (util is null && reset is null)
            return new WindowState { Kind = kind, Reliability = SourceReliability.Unavailable,
                                     Depassement = depassement };

        return new WindowState
        {
            Kind = kind,
            Utilization = util,
            ResetsAt = reset,
            Reliability = SourceReliability.Exact,
            // PAR FENÊTRE, et non par snapshot : la doctrine de la phase 19 (limite d'âge, correction par
            // delta) en dépend. Le provider /api/oauth/usage ne le fait PAS, d'où son captured_at nul en
            // persistance — la sonde corrige ce défaut pour elle-même.
            CapturedAt = now,
            FractionTimeRemaining = WindowState.FractionRemaining(reset, now, longueurFenetre),
            StatutServeur = statut,       // HDR-03 — voyage par référence à travers Best()
            Depassement = depassement,    // HDR-04 — canal n° 1 ; le canal latéral est le n° 2
        };
    }

    // HDR-04. Le code d'origine lit les deux familles en branches ALTERNATIVES, donc comme mutuellement
    // exclusives. C'est un choix d'AFFICHAGE de sa part, pas une contrainte de protocole : on lit les deux.
    // Le dépassement est un fait de COMPTE (la branche de dépassement correspond à « acct: ent », qui n'a ni
    // 5 h ni 7 j), d'où le double canal — champ de fenêtre ET canal latéral.
    private static EtatDepassement? LireDepassement(HttpResponseMessage resp, List<string> noms)
    {
        var d = new EtatDepassement
        {
            Utilization = UsageNormalization.FractionDepuisTexteFraction(Lire(resp, HOverUtil, noms)),
            ResetsAt    = UsageNormalization.InstantDepuisTexteEpoch(Lire(resp, HOverReset, noms)),
            Statut      = StatutServeurTexte.DepuisEnTete(Lire(resp, HOverStatut, noms))
                          ?? StatutServeurTexte.DepuisEnTete(Lire(resp, HStatutGlobal, noms)),
        };
        return d.EstRenseigne ? d : null;   // rien rapporté != un dépassement de 0 %
    }

    // Égalité STRUCTURELLE de record : la comparaison est exacte et gratuite, et c'est elle qui garantit
    // « sur transition uniquement ». Sans cette garde, l'abonné recevrait un événement par tick de sonde —
    // donc 288 par jour disant tous la même chose.
    private void PublierDepassement(EtatDepassement? nouveau)
    {
        if (nouveau == Depassement) return;
        Depassement = nouveau;
        DepassementChange?.Invoke(this, nouveau);
    }

    // Trois noms candidats, du plus spécifique au plus global. Le nom de fenêtre l'emporte sur le global :
    // « anthropic-ratelimit-unified-status » (SANS segment) est ce que lit la branche de dépassement du code
    // d'origine — c'est un statut de COMPTE, donc un repli, jamais une vérité par fenêtre.
    // Le mapping du vocabulaire vit dans StatutServeurTexte et NULLE PART ailleurs : aucun aiguillage de
    // valeurs textuelles n'est dupliqué ici, sans quoi les deux copies divergeraient au premier nouveau
    // statut inventé par le serveur.
    private static StatutServeur? LireStatut(HttpResponseMessage resp, string nomFenetre, List<string> noms)
        => StatutServeurTexte.DepuisEnTete(Lire(resp, nomFenetre, noms))
           ?? StatutServeurTexte.DepuisEnTete(Lire(resp, HStatutGlobal, noms));

    // Exploitable = au moins une des deux fenêtres porte une utilisation OU un reset, OU un dépassement est
    // rapporté. Sinon : RIEN.
    private static bool EnTetesExploitables(UsageSnapshot s)
        => s.FiveHour.Utilization is not null || s.FiveHour.ResetsAt is not null
        || s.SevenDay.Utilization is not null || s.SevenDay.ResetsAt is not null
        // HDR-04 — la forme « dépassement seul » n'a NI 5 h NI 7 j. Sans cette ligne elle serait classée
        // SuccesSansEnTetes et JETÉE, alors que c'est une réponse parfaitement exploitable : elle porte le
        // seul fait que ce compte-là rapporte. Le dépassement est posé à l'identique sur les deux fenêtres,
        // en tester une suffit donc.
        || s.FiveHour.Depassement is not null;

    // Recul après un 429 : la valeur typée du serveur si elle est présente et plus longue que le
    // plancher, sinon le plancher. Une sonde rejetée ne consommant pas de quota, rester réactif pendant
    // la saturation est gratuit.
    private static TimeSpan Recul429(HttpResponseMessage resp, DateTimeOffset now)
    {
        var ra = resp.Headers.RetryAfter;
        var attente = ra?.Delta ?? (ra?.Date is { } d ? d - now : (TimeSpan?)null);
        return attente is { } a && a > Recul429Plancher ? a : Recul429Plancher;
    }

    // Recul exponentiel PLAFONNÉ (motif ChronosTokenAuthority) : hors ligne, un tick de 60 s tenterait
    // 1 440 requêtes par jour et transformerait une panne bénigne en limitation. Remis à zéro à chaque
    // succès — 2xx comme 429 authentifié.
    private void ReculerReseau(DateTimeOffset now)
    {
        _reculReseau = _reculReseau <= TimeSpan.Zero
            ? ReculReseauInitial
            : TimeSpan.FromTicks(Math.Min(_reculReseau.Ticks * 2, ReculReseauMax.Ticks));
        _prochainAppelAutorise = now + _reculReseau;
    }

    private UsageSnapshot ServirCacheOu(UsageSnapshot repli, DateTimeOffset now)
        => _cache is not null && (now - _cacheAt) < CacheUtilisable ? _cache : repli;

    // Envoi d'UNE sonde avec le jeton fourni. SÉCURITÉ : le jeton ne vit qu'en variable locale, le temps
    // de construire l'en-tête d'autorisation — jamais journalisé, jamais en URL.
    private async Task<HttpResponseMessage> EnvoyerAsync(string jeton, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Delai);

        using var req = new HttpRequestMessage(HttpMethod.Post, Url);
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + jeton);
        req.Headers.TryAddWithoutValidation("anthropic-beta", Beta);
        req.Headers.TryAddWithoutValidation("anthropic-version", VersionApi);
        req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        req.Content = new StringContent(Corps, System.Text.Encoding.UTF8, "application/json");

        return await _http.SendAsync(req, cts.Token);
    }
}
