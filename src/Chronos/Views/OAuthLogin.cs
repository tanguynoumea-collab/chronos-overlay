using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Chronos.Services;

namespace Chronos.Views;

/// <summary>
/// Implémentation WPF d'<see cref="IOAuthLogin"/> : réalise le login OAuth PKCE de Chronos.
/// Dialogue construit en code (pas de XAML) : instructions → ouverture du navigateur sur la page de
/// connexion Claude → collage du code affiché → échange → stockage chiffré. Vit dans Views (hors pureté
/// Services) car il manipule des fenêtres. GAP-1 pour la persistance côté réglages n'est pas requis ici
/// (le coffre OAuth est un fichier distinct de settings.json).
/// </summary>
public sealed class OAuthLogin : IOAuthLogin
{
    private readonly ChronosOAuthClient _client;
    private readonly ChronosOAuthStore _store;

    public OAuthLogin(ChronosOAuthClient client, ChronosOAuthStore store)
    {
        _client = client;
        _store = store;
    }

    public bool IsLoggedIn => _store.Exists;

    public void Logout() => _store.Clear();

    public Task<bool> LoginAsync()
    {
        var (verifier, challenge, state) = ChronosOAuthClient.CreatePkce();
        var url = ChronosOAuthClient.BuildAuthorizeUrl(challenge, state);

        var tcs = new TaskCompletionSource<bool>();
        var dlg = BuildDialog(url, verifier, state, tcs);
        dlg.Owner = Application.Current?.MainWindow;
        dlg.ShowDialog();
        // Si l'utilisateur ferme sans valider, tcs n'a pas été résolu → false.
        if (!tcs.Task.IsCompleted) tcs.TrySetResult(false);
        return tcs.Task;
    }

    private Window BuildDialog(string url, string verifier, string state, TaskCompletionSource<bool> tcs)
    {
        var codeBox = new TextBox { MinWidth = 380, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        var status = new TextBlock { Margin = new Thickness(0, 8, 0, 0), Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };

        var openBtn = new Button { Content = "1) Ouvrir la page de connexion Claude", Padding = new Thickness(10, 5, 10, 5) };
        var validateBtn = new Button { Content = "3) Valider", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(8, 0, 0, 0), IsDefault = true };
        var cancelBtn = new Button { Content = "Annuler", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = "Connexion à Claude (source exacte)",
            FontWeight = FontWeights.SemiBold, FontSize = 15, Margin = new Thickness(0, 0, 0, 6),
        });
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, MaxWidth = 420,
            Text = "1) Ouvre la page de connexion, autorise l'accès.\n" +
                   "2) Copie le code affiché à la fin.\n" +
                   "3) Colle-le ci-dessous puis Valider.",
        });
        panel.Children.Add(openBtn);
        panel.Children.Add(new TextBlock { Text = "2) Colle ici le code copié :", Margin = new Thickness(0, 10, 0, 0) });
        panel.Children.Add(codeBox);
        panel.Children.Add(status);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(validateBtn);
        buttons.Children.Add(cancelBtn);
        panel.Children.Add(buttons);

        var window = new Window
        {
            Title = "Chronos — Connexion",
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };

        openBtn.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { status.Text = "Impossible d'ouvrir le navigateur. Copie l'URL manuellement (voir Diagnostic)."; }
        };

        validateBtn.Click += async (_, _) =>
        {
            var code = codeBox.Text?.Trim();
            if (string.IsNullOrEmpty(code)) { status.Text = "Colle d'abord le code."; return; }
            validateBtn.IsEnabled = false; status.Foreground = System.Windows.Media.Brushes.Gray; status.Text = "Connexion…";
            var tokens = await _client.ExchangeCodeAsync(code, verifier, state);
            if (tokens is null)
            {
                status.Foreground = System.Windows.Media.Brushes.Firebrick;
                status.Text = "Échec : code invalide/expiré, ou trop de tentatives. Recommence l'étape 1 " +
                              "(si ça persiste, attends 1 min puis réessaie).";
                validateBtn.IsEnabled = true;
                return;
            }
            _store.Save(tokens);
            tcs.TrySetResult(true);
            window.DialogResult = true;
            window.Close();
        };

        return window;
    }
}
