using System.Diagnostics;
using System.Windows;
using Chronos.Services;

namespace Chronos.Views;

/// <summary>
/// Implémentation WPF de <see cref="IStatusLineSetup"/> : active/désactive la source EXACTE via le pont
/// statusLine de Claude Code. Vit dans <c>Chronos.Views</c> (hors pureté Services) car elle affiche des
/// <see cref="MessageBox"/> de confirmation/erreur. Persiste l'état côté Chronos avec le pattern GAP-1
/// (relire le disque avant d'écrire, pour ne pas écraser un réglage posé par l'OverlayController).
/// </summary>
public sealed class StatusLineSetup : IStatusLineSetup
{
    private readonly StatusLineInstaller _installer;
    private readonly SettingsService _settings;

    public StatusLineSetup(StatusLineInstaller installer, SettingsService settings)
    {
        _installer = installer;
        _settings = settings;
    }

    // Chemin de CE Chronos.exe (mono-fichier : ProcessPath est fiable, jamais Assembly.Location).
    private static string ExePath =>
        Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "Chronos.exe";

    public bool IsEnabled() => _installer.IsInstalled(ExePath);

    public void Enable()
    {
        try
        {
            var inner = _installer.Install(ExePath); // chaîne toute barre préexistante (non destructif)
            Persist(s => s with
            {
                InnerStatusLineCommand = inner ?? s.InnerStatusLineCommand,
                StatusLinePromptDismissed = true,
            });
            MessageBox.Show(Owner(),
                "Source exacte activée.\n\nLes chiffres apparaîtront dès que Claude Code aura traité un\n" +
                "message (la barre de statut se met à jour à ce moment-là).",
                "Chronos", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Owner(), "Impossible d'écrire la configuration de Claude Code :\n" + ex.Message,
                "Chronos", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void Disable()
    {
        try
        {
            var s = _settings.Load();
            _installer.Uninstall(ExePath, s.InnerStatusLineCommand);
            Persist(x => x with { StatusLinePromptDismissed = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(Owner(), "Impossible de retirer l'intégration :\n" + ex.Message,
                "Chronos", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void OfferOnFirstRun()
    {
        var s = _settings.Load();
        if (s.StatusLinePromptDismissed || IsEnabled()) return;

        var r = MessageBox.Show(Owner(),
            "Chronos peut afficher tes chiffres d'usage EXACTS (fenêtres 5 h et hebdo) en\n" +
            "s'intégrant à Claude Code : il ajoute une petite ligne de statut qui lui transmet\n" +
            "les pourcentages officiels. Aucune donnée ne quitte ta machine, et c'est réversible.\n\n" +
            "Activer la source exacte maintenant ?",
            "Chronos — source exacte", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (r == MessageBoxResult.Yes) Enable();
        else Persist(x => x with { StatusLinePromptDismissed = true }); // ne plus reproposer
    }

    // GAP-1 : relire le disque le plus récent avant d'écrire.
    private void Persist(Func<ChronosSettings, ChronosSettings> mutate)
        => _settings.Save(mutate(_settings.Load()));

    private static Window? Owner() => Application.Current?.MainWindow;
}
