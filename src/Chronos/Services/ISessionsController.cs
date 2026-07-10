namespace Chronos.Services;

/// <summary>
/// Pilote le widget de sessions Claude Code : installe/retire les hooks de suivi, affiche/masque le
/// panneau flottant, persiste l'état. Abstraction NEUTRE ; l'implémentation (fenêtre WPF) vit dans Views.
/// </summary>
public interface ISessionsController
{
    /// <summary>Le widget est-il activé (hooks installés + panneau affiché) ?</summary>
    bool IsEnabled { get; }

    /// <summary>Active : installe les hooks (sessions futures) et affiche le panneau.</summary>
    void Enable();

    /// <summary>Désactive : masque le panneau et retire les hooks.</summary>
    void Disable();

    /// <summary>Au démarrage : affiche le panneau si le widget était activé.</summary>
    void ShowIfEnabled();
}
