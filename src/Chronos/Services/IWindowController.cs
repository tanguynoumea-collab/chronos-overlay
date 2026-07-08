namespace Chronos.Services;

/// <summary>
/// Contrat NEUTRE de pilotage de la fenêtre overlay, consommé par le ViewModel (menu contextuel
/// en 06-04) sans jamais dépendre d'un type WPF. L'implémentation concrète est l'adaptateur WPF
/// <see cref="OverlayController"/> (ajouté à l'allow-list de pureté Services). Garder cette interface
/// exempte de tout type WPF garantit que le VM reste testable et neutre.
/// </summary>
public interface IWindowController
{
    /// <summary>Envoie la fenêtre à l'arrière-plan (Topmost off + HWND_BOTTOM + suspend le guard) — FEN-05.</summary>
    void SendToBackground();

    /// <summary>Ramène la fenêtre au premier plan (Topmost on + reprise du guard) — FEN-05.</summary>
    void BringToForeground();

    /// <summary>Ferme proprement l'application (item « Quitter » du menu contextuel — FEN-06).</summary>
    void Quit();
}
