namespace Chronos.Services;

/// <summary>
/// Pilote l'activation de la SOURCE EXACTE via le pont statusLine de Claude Code (installe/retire
/// l'intégration dans ~/.claude/settings.json et persiste l'état côté Chronos). Abstraction NEUTRE :
/// l'implémentation (dialogues de confirmation/erreur) vit dans la couche Views, hors pureté Services.
/// </summary>
public interface IStatusLineSetup
{
    /// <summary>Le pont pointe-t-il déjà sur ce Chronos.exe dans settings.json ?</summary>
    bool IsEnabled();

    /// <summary>Installe le pont (chaîne toute barre préexistante) et persiste l'état.</summary>
    void Enable();

    /// <summary>Retire le pont et restaure la barre d'origine (le cas échéant).</summary>
    void Disable();

    /// <summary>Au premier lancement seulement : propose d'activer la source exacte (une fois).</summary>
    void OfferOnFirstRun();
}
