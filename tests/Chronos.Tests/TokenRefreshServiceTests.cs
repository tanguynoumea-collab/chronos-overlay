using System.Net;
using System.Threading;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// PREUVE DE TOK-01 — le rafraîchissement cesse d'être PARESSEUX.
///
/// Avant cette phase, le jeton n'était renouvelé qu'au moment d'un <c>GetAsync</c>, c'est-à-dire
/// uniquement quand quelqu'un regardait le cadran (défaut gravé par le plan 17-01, commit
/// <c>e3e326c</c>, test <c>Le_rafraichissement_est_aujourd_hui_paresseux_declenche_par_le_GetAsync</c>).
/// <see cref="TokenRefreshService"/> ajoute l'horloge de fond qui manquait : elle sollicite l'autorité
/// à cadence fixe, et c'est l'autorité — et elle seule — qui décide s'il faut réellement rafraîchir.
///
/// Les deux garanties testées ici sont celles dont dépend tout le reste de la phase :
/// le premier tick est IMMÉDIAT (le cas réel de l'utilisateur est un jeton mort depuis deux mois, et
/// attendre 60 s afficherait une pastille de déconnexion transitoire au lancement), et un tick ne LÈVE
/// JAMAIS (une exception sur un thread du pool tuerait la boucle, donc le préventif, donc TOK-01).
///
/// SÉCURITÉ : les helpers de construction sont partagés avec <see cref="ChronosTokenAuthorityTests"/>
/// (coffre TOUJOURS sous %TEMP%, tout le trafic par <c>FakeHttpMessageHandler</c>). Le coffre réel de
/// l'utilisateur n'est ni lu, ni déchiffré, ni envoyé : l'endpoint fait TOURNER le refresh token.
///
/// Pas de [Collection("XAML WPF")] : aucun BAML chargé, la classe reste parallélisable.
/// </summary>
public class TokenRefreshServiceTests
{
    private static readonly DateTimeOffset Maintenant = ChronosTokenAuthorityTests.Maintenant;

    /// <summary>Sondage BORNÉ (motif WaitUntilAsync de RefreshOrchestratorTests:19) : jamais de
    /// Thread.Sleep nu, et une borne dure pour qu'un échec se manifeste en échec, pas en blocage.</summary>
    private static async Task<bool> AttendreAsync(Func<bool> condition, int msMax = 2000)
    {
        var fin = DateTime.UtcNow.AddMilliseconds(msMax);
        while (DateTime.UtcNow < fin)
        {
            if (condition()) return true;
            await Task.Delay(20);
        }
        return condition();
    }

    // ------------------------------------------------------------------------------------------
    // 1. Le tick déterministe : l'autorité décide, le service ne fait que solliciter
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Un_tick_sur_un_jeton_expire_declenche_exactement_un_rafraichissement()
    {
        var (autorite, handler, _, _) = ChronosTokenAuthorityTests.Autorite(Maintenant.AddMinutes(-1));
        using var service = new TokenRefreshService(autorite);

        await service.TickAsync();

        Assert.Equal(1, handler.SendCount);
        Assert.Equal(EtatAuthentification.Connecte, autorite.Etat);
    }

    /// <summary>Le service n'a AUCUNE politique propre : sur un jeton encore valide, il ne provoque
    /// aucune requête. Toute la décision vit dans l'autorité (une seule source de vérité).</summary>
    [Fact]
    public async Task Un_tick_sur_un_jeton_encore_valide_n_emet_aucune_requete()
    {
        var (autorite, handler, _, _) = ChronosTokenAuthorityTests.Autorite(Maintenant.AddHours(1));
        using var service = new TokenRefreshService(autorite);

        await service.TickAsync();

        Assert.Equal(0, handler.SendCount);
        Assert.Equal(EtatAuthentification.Connecte, autorite.Etat);
    }

    // ------------------------------------------------------------------------------------------
    // 2. Le premier tick est IMMÉDIAT
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// TOK-01 — cas RÉEL de l'utilisateur : jeton expiré depuis deux mois (2026-07-12) au démarrage de
    /// l'exe. Le premier tick doit partir IMMÉDIATEMENT (dueTime zéro, motif
    /// <c>DesktopUiaPollService.StartAsync:44</c>), pas au bout de 60 s.
    /// </summary>
    [Fact]
    public async Task Un_jeton_deja_expire_est_rafraichi_des_le_demarrage_sans_attendre_le_tick()
    {
        var (autorite, handler, _, _) = ChronosTokenAuthorityTests.Autorite(Maintenant.AddMonths(-2));
        using var service = new TokenRefreshService(autorite);

        await service.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await AttendreAsync(() => handler.SendCount == 1),
                "le premier tick doit être immédiat (dueTime zéro)");
            Assert.True(await AttendreAsync(() => autorite.Etat == EtatAuthentification.Connecte));
        }
        finally { await service.StopAsync(CancellationToken.None); }
    }

    // ------------------------------------------------------------------------------------------
    // 3. Un tick ne peut pas tuer la boucle de fond
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>ChronosOAuthClient.RefreshAsync</c> filtre ses exceptions NOMINATIVEMENT ;
    /// <see cref="InvalidOperationException"/> n'en fait pas partie et peut donc remonter jusqu'au
    /// service. Une exception non gérée sur un thread du pool tuerait la boucle de fond, donc le
    /// rafraîchissement préventif, donc TOK-01 : le service doit l'absorber.
    /// </summary>
    [Fact]
    public async Task Un_tick_ne_leve_JAMAIS_meme_si_le_transport_explose()
    {
        var (autorite, _, _, _) = ChronosTokenAuthorityTests.Autorite(Maintenant.AddMinutes(-1),
            FakeHttpMessageHandler.Throws(new InvalidOperationException("transport en vrac")));
        using var service = new TokenRefreshService(autorite);

        var ex = await Record.ExceptionAsync(() => service.TickAsync());

        Assert.Null(ex);
    }

    /// <summary>Un tick raté ne condamne pas les suivants : la boucle survit à l'échec.</summary>
    [Fact]
    public async Task Un_tick_en_echec_n_empeche_pas_le_tick_suivant()
    {
        var handler = ChronosTokenAuthorityTests.Sequence(
            (HttpStatusCode.InternalServerError, ""),
            (HttpStatusCode.OK, ChronosTokenAuthorityTests.CorpsRefresh200));
        var (autorite, h, horloge, _) = ChronosTokenAuthorityTests.Autorite(Maintenant.AddMinutes(-1), handler);
        using var service = new TokenRefreshService(autorite);

        await service.TickAsync();                            // échec temporaire => recul de 2 min
        Assert.Equal(EtatAuthentification.HorsLigne, autorite.Etat);

        horloge.UtcNow = Maintenant.AddMinutes(3);            // le recul a expiré
        await service.TickAsync();

        Assert.Equal(2, h.SendCount);
        Assert.Equal(EtatAuthentification.Connecte, autorite.Etat);
    }

    // ------------------------------------------------------------------------------------------
    // 4. Cycle de vie (le host appelle Start/Stop à l'ouverture et à la fermeture)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Le_cycle_Start_Stop_Dispose_est_sur_et_idempotent()
    {
        var (autorite, _, _, _) = ChronosTokenAuthorityTests.Autorite(Maintenant.AddHours(1));
        var service = new TokenRefreshService(autorite);

        var ex = await Record.ExceptionAsync(async () =>
        {
            await service.StopAsync(CancellationToken.None);   // Stop avant Start
            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);   // idempotent
            service.Dispose();
            service.Dispose();                                 // idempotent
        });

        Assert.Null(ex);
    }

    /// <summary>Après Dispose, StartAsync ne doit PAS ressusciter de minuteur : un service jeté par le
    /// host à l'arrêt de l'application ne doit plus toucher au réseau.</summary>
    [Fact]
    public async Task Apres_Dispose_StartAsync_ne_recree_aucun_minuteur()
    {
        var (autorite, handler, _, _) = ChronosTokenAuthorityTests.Autorite(Maintenant.AddMinutes(-1));
        var service = new TokenRefreshService(autorite);
        service.Dispose();

        await service.StartAsync(CancellationToken.None);

        // Le jeton est expiré : si un minuteur avait démarré, une requête serait partie immédiatement.
        Assert.False(await AttendreAsync(() => handler.SendCount > 0, msMax: 400));
        Assert.Equal(0, handler.SendCount);
    }

    [Fact]
    public void Le_service_refuse_une_autorite_nulle()
        => Assert.Throws<ArgumentNullException>(() => new TokenRefreshService(null!));
}
