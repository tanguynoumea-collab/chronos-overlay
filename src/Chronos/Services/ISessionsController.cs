using Chronos.Theming;

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

    /// <summary>Applique le style visuel du widget (refonte) à la fenêtre live si elle existe. La
    /// persistance est faite par l'appelant (MainViewModel) ; ici on ne fait que rafraîchir l'affichage.</summary>
    void SetStyle(SessionStyle style);

    /// <summary>Bascule la disposition VERTICALE des styles en rangée (Sonar/Jetons/Veilleurs) sur la
    /// fenêtre live (persistance = appelant).</summary>
    void SetVerticalLayout(bool vertical);

    /// <summary>Applique le thème de couleur (celui du cadran) au widget de sessions : couleurs d'état + fonds/texte.</summary>
    void SetTheme(ChronosTheme theme);
}
