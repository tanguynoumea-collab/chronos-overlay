using System;
using System.Threading;
using System.Threading.Tasks;

namespace Chronos.Services;

/// <summary>
/// AUTORITÉ UNIQUE DE JETON (TOK-01 / TOK-02). Un seul objet du processus a le droit d'appeler
/// <see cref="ChronosOAuthClient.RefreshAsync"/> et d'écrire dans <see cref="ChronosOAuthStore"/> ;
/// tous les autres (provider d'usage, service de fond, sonde d'en-têtes de la phase 18) deviennent de
/// simples clients de <see cref="GetAccessTokenAsync"/>, sérialisés par construction.
///
/// POURQUOI CE N'EST PAS NÉGOCIABLE : le flux OAuth fait TOURNER le refresh token. Deux
/// rafraîchisseurs présentant le MÊME refresh token produisent un <c>invalid_grant</c> sur le second —
/// donc, puisqu'un <c>invalid_grant</c> signifie « déconnecté », une FAUSSE déconnexion sur un compte
/// parfaitement sain. Ce n'est pas théorique : c'est le bug ouvert <c>anthropics/claude-code#25609</c>
/// (« no coordination (file lock, mutex, or leader election) during token refresh … a classic refresh
/// token rotation race condition »). Claude Code lui-même s'y est fait prendre. La réponse est de
/// retirer le droit de rafraîchir à tout le monde sauf un.
///
/// Type NEUTRE : aucun type WPF. L'événement <see cref="EtatChange"/> est émis sur un thread du POOL,
/// l'abonné marshalle lui-même (motif <see cref="RefreshOrchestrator.SnapshotChanged"/>).
/// </summary>
public sealed class ChronosTokenAuthority : IAuthStatus, IDisposable
{
    /// <summary>Marge de rafraîchissement préventif. STRICTEMENT supérieure à la période de tick du
    /// service de fond (60 s) : c'est ce qui garantit que le préventif gagne toujours la course contre
    /// un chemin paresseux — définition même de « préventif ». Volontairement large : la durée de vie
    /// réelle de l'access token Claude Code n'est pas documentée et ne doit être codée en dur nulle part.</summary>
    private static readonly TimeSpan Marge = TimeSpan.FromMinutes(12);

    /// <summary>Recul initial après un échec TEMPORAIRE. Hors ligne, un tick de 60 s tenterait 1 440
    /// rafraîchissements par jour et transformerait une panne bénigne en 429 (constaté en direct).</summary>
    private static readonly TimeSpan ReculInitial = TimeSpan.FromMinutes(2);

    /// <summary>Plafond du recul : au-delà, l'overlay resterait muet des heures après une panne courte.</summary>
    private static readonly TimeSpan ReculMax = TimeSpan.FromMinutes(30);

    private readonly SemaphoreSlim _verrou = new SemaphoreSlim(1, 1);
    private readonly ChronosOAuthStore _coffre;
    private readonly ChronosOAuthClient _client;
    private readonly IClock _horloge;

    private OAuthTokens? _jetons;          // copie mémoire : survit à un Save en échec
    private bool _forcerRafraichissement;  // posé par InvaliderAccessToken() après un 401 sur l'usage
    private bool _refusDefinitif;          // VERROU : identifiants rejetés => plus AUCUN réessai auto
    private DateTimeOffset _prochainEssai; // fenêtre de recul (échecs temporaires)
    private TimeSpan _recul = ReculInitial;

    /// <summary>État courant. Lisible à tout instant, y compris avant la première transition.</summary>
    public EtatAuthentification Etat { get; private set; } = EtatAuthentification.NonConnecte;

    /// <summary>Émis sur un thread du POOL, uniquement sur TRANSITION (jamais un événement par tick).</summary>
    public event EventHandler<EtatAuthentification>? EtatChange;

    public ChronosTokenAuthority(ChronosOAuthStore coffre, ChronosOAuthClient client, IClock horloge)
    {
        _coffre = coffre ?? throw new ArgumentNullException(nameof(coffre));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _horloge = horloge ?? throw new ArgumentNullException(nameof(horloge));
    }

    /// <summary>
    /// Rend un access token VALIDE, en rafraîchissant si nécessaire ; null si indisponible.
    /// SÉCURITÉ : c'est la SEULE sortie d'un jeton hors de cette classe, et jamais le refresh token.
    /// Ce dernier ne quitte l'autorité que vers <see cref="ChronosOAuthClient.RefreshAsync"/>.
    ///
    /// Toute la séquence (charger → décider → rafraîchir → persister → publier) est sous le sémaphore.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        await _verrou.WaitAsync(ct);
        try
        {
            var now = _horloge.UtcNow;
            _jetons ??= _coffre.Load();

            // Coffre vide : l'utilisateur ne s'est jamais connecté. Rien à rafraîchir — ce n'est PAS
            // une panne, et cela ne doit allumer aucune pastille de déconnexion.
            if (_jetons is null) { Publier(EtatAuthentification.NonConnecte); return null; }

            // VERROU DÉFINITIF : le serveur a refusé les identifiants. Seul un login répare ; réessayer
            // ne ferait que consommer du rate-limit sur le point de terminaison de jeton.
            if (_refusDefinitif) { Publier(EtatAuthentification.Deconnecte); return null; }

            // DOUBLE-VÉRIFICATION : un appelant concurrent vient peut-être de rafraîchir pendant
            // l'attente du sémaphore. Sans elle, N appelants en attente déclenchent N rotations en
            // série, donc N-1 invalid_grant — une fausse déconnexion fabriquée par nous-mêmes.
            if (!_forcerRafraichissement && !DoitRafraichir(_jetons.ExpiresAt, now, Marge))
            {
                Publier(EtatAuthentification.Connecte);
                return _jetons.AccessToken;
            }

            // Fenêtre de recul : ne jamais marteler le point de terminaison. Cette politique vit ICI,
            // dans l'autorité, et ne consulte AUCUN cache d'usage — le garde-fou anti-429 du provider
            // (ChronosOAuthUsageProvider.cs:53) exigeait un cache en RAM, donc vide à chaque démarrage
            // de l'exe : il ne freinait rien du tout au redémarrage, ce qui entretenait le 429.
            if (now < _prochainEssai) return null;

            var res = await _client.RefreshAsync(_jetons.RefreshToken, ct);
            switch (res.Issue)
            {
                case IssueRafraichissement.Succes:
                    _jetons = res.Jetons!;
                    // ROTATION : persister AVANT de rendre le jeton. Un Save en échec ne doit PAS faire
                    // croire à un échec de refresh : les jetons neufs restent en mémoire, la session
                    // courante continue — l'ancien refresh token est de toute façon déjà mort côté serveur.
                    try { _coffre.Save(_jetons); } catch { /* dégradation : session courante préservée */ }
                    _forcerRafraichissement = false;
                    _recul = ReculInitial;
                    _prochainEssai = default;
                    Publier(EtatAuthentification.Connecte);
                    return _jetons.AccessToken;

                case IssueRafraichissement.IdentifiantsRejetes:
                    // AUCUN réessai automatique. Et surtout AUCUN effacement du coffre : un faux positif
                    // serveur (cas documenté claude-code#54443) détruirait définitivement un login
                    // parfaitement récupérable. On attend un login utilisateur.
                    _refusDefinitif = true;
                    Publier(EtatAuthentification.Deconnecte);
                    return null;

                default: // EchecTemporaire : réseau, timeout, 429, 5xx
                    _prochainEssai = now + _recul;
                    _recul = _recul < ReculMax ? Doubler(_recul) : ReculMax;
                    Publier(EtatAuthentification.HorsLigne);
                    return null;
            }
        }
        finally { _verrou.Release(); }
    }

    /// <summary>Après un 401 sur l'endpoint d'usage : le prochain appel forcera un rafraîchissement.
    /// Le serveur peut révoquer un jeton AVANT son ExpiresAt local (cas documenté) — le préventif seul
    /// ne suffit donc pas.</summary>
    public void InvaliderAccessToken() => _forcerRafraichissement = true;

    /// <summary>TOK-03 — après un login réussi : relâche le verrou « Deconnecte » ET le recul, et
    /// oublie les jetons mémorisés pour relire le coffre que le login vient de réécrire. Sans cet
    /// appel, le jeton tout neuf ne serait pas utilisé et la pastille survivrait à sa réparation.</summary>
    public void ReinitialiserApresLogin()
    {
        _jetons = null;
        _forcerRafraichissement = false;
        _refusDefinitif = false;
        _recul = ReculInitial;
        _prochainEssai = default;
        Publier(EtatAuthentification.NonConnecte);   // sera relevé au premier GetAccessTokenAsync
    }

    /// <summary>Un 2xx exploité déverrouille l'état : la pastille disparaît dès qu'un chiffre exact est
    /// de nouveau obtenu (critère de succès 3 de la phase).</summary>
    public void SignalerSucces()
    {
        _refusDefinitif = false;
        _recul = ReculInitial;
        _prochainEssai = default;
        Publier(EtatAuthentification.Connecte);
    }

    /// <summary>Le serveur a refusé un access token FRAÎCHEMENT rafraîchi (401/403 sur l'usage après
    /// rejeu) : ce n'est pas un problème de fraîcheur, c'est un refus de compte.</summary>
    public void SignalerRefusServeur()
    {
        _refusDefinitif = true;
        Publier(EtatAuthentification.Deconnecte);
    }

    /// <summary>PUR, aucun I/O. Comparaison d'HORLOGE MURALE et non de temps écoulé : un portable qui
    /// dort huit heures rattrape naturellement au tick suivant, et un jeton expiré depuis deux mois
    /// est traité au premier tick. Un réveil calculé jusqu'à ExpiresAt casse à la mise en veille.</summary>
    internal static bool DoitRafraichir(DateTimeOffset? expiresAt, DateTimeOffset now, TimeSpan marge)
        => expiresAt is null || expiresAt.Value - marge <= now;

    // Doublement PLAFONNÉ : sans plafond, quelques heures hors ligne suffiraient à repousser le
    // prochain essai au-delà de toute session utile.
    private static TimeSpan Doubler(TimeSpan t) => t + t > ReculMax ? ReculMax : t + t;

    // N'émettre que sur TRANSITION : sinon un événement par tick, soit 1 440 par jour pour rien.
    private void Publier(EtatAuthentification e)
    {
        if (Etat == e) return;
        Etat = e;
        EtatChange?.Invoke(this, e);
    }

    public void Dispose() => _verrou.Dispose();
}
