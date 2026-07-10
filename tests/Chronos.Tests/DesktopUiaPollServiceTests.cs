using System;
using System.Linq;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la MÉCANIQUE de poll du service de fond (ROB-07) sans dépendre du timer réel ni d'une
/// fenêtre Claude : via le chemin déterministe <see cref="DesktopUiaPollService.PollOnce"/>, on
/// vérifie que le cache de <see cref="DesktopUiaSessionSource"/> passe de VIDE (avant tout poll) à
/// PEUPLÉ (après un tick), et que la dégradation (provider null) ne lève jamais.
///
/// Le walk UIA réel (WindowsUiaTreeProvider) n'est PAS testé ici : il dépend de l'OS. On injecte un
/// faux <see cref="IUiaTreeProvider"/> renvoyant un arbre neutre.
/// </summary>
public class DesktopUiaPollServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 10, 12, 0, 0, TimeSpan.Zero);

    // --- Helpers de faux arbre (mêmes conventions que DesktopUiaSessionSourceTests) ---

    private static UiaNode FakeUiaNode(string type, string name, params UiaNode[] children)
        => new(type, name, null, true, children);

    private static UiaNode FakeUiaNode(string type, string name, string? aid, params UiaNode[] children)
        => new(type, name, aid, true, children);

    /// <summary>Fenêtre Claude minimale au REPOS : ancre RootWebArea + « Mode chat » + placeholder → WaitingTurn.</summary>
    private static UiaNode FenetreRepos()
        => FakeUiaNode("Window", "Claude",
            FakeUiaNode("Document", "", "RootWebArea",
                FakeUiaNode("Text", "Mode chat"),
                FakeUiaNode("Edit", "Tapez / pour les commandes")));

    private sealed class FakeTreeProvider : IUiaTreeProvider
    {
        private readonly UiaNode? _tree;
        public FakeTreeProvider(UiaNode? tree) => _tree = tree;
        public UiaNode? TryGetTree() => _tree;
    }

    [Fact]
    public void PollOnce_remplit_le_cache_a_partir_du_vide()
    {
        var source = new DesktopUiaSessionSource(new FakeTreeProvider(FenetreRepos()));
        var service = new DesktopUiaPollService(source, new FakeClock(Now));

        // Avant tout poll : cache vide (aucune I/O UIA dans Read).
        Assert.Empty(source.Read(Now));

        service.PollOnce();

        // Après un tick : le snapshot foreground mappé est présent (WaitingTurn / Chat).
        var snaps = source.Read(Now);
        var fg = Assert.Single(snaps);
        Assert.Equal(SessionActivity.WaitingTurn, fg.Activity);
        Assert.Equal(SessionKind.Chat, fg.Kind);
        Assert.Equal(SessionOrigin.Desktop, fg.Origin);
    }

    [Fact]
    public void PollOnce_provider_null_ne_leve_pas_et_laisse_le_cache_vide()
    {
        // Fenêtre Claude absente / UIA indisponible → TryGetTree null (dégradation).
        var source = new DesktopUiaSessionSource(new FakeTreeProvider(null));
        var service = new DesktopUiaPollService(source, new FakeClock(Now));

        var ex = Record.Exception(() => service.PollOnce());

        Assert.Null(ex); // ne LÈVE JAMAIS
        Assert.Empty(source.Read(Now));
        Assert.Equal(DesktopHealth.WindowMissing, source.Health);
    }

    [Fact]
    public void PollOnce_utilise_l_heure_de_l_horloge_injectee()
    {
        var source = new DesktopUiaSessionSource(new FakeTreeProvider(FenetreRepos()));
        var service = new DesktopUiaPollService(source, new FakeClock(Now));

        service.PollOnce();

        // Le snapshot porte l'horodatage de l'IClock injecté (déterminisme).
        Assert.All(source.Read(Now), s => Assert.Equal(Now, s.UpdatedAt));
    }

    [Fact]
    public void StartAsync_puis_StopAsync_sont_surs_et_idempotents()
    {
        var source = new DesktopUiaSessionSource(new FakeTreeProvider(FenetreRepos()));
        var service = new DesktopUiaPollService(source, new FakeClock(Now));

        // Le cycle Start/Stop/Dispose ne doit jamais lever (le host l'appelle à l'ouverture/fermeture).
        var ex = Record.Exception(() =>
        {
            service.StartAsync(default).GetAwaiter().GetResult();
            service.StopAsync(default).GetAwaiter().GetResult();
            service.StopAsync(default).GetAwaiter().GetResult(); // idempotent
            service.Dispose();
            service.Dispose(); // idempotent
        });

        Assert.Null(ex);
    }
}
