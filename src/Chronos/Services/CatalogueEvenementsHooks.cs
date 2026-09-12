namespace Chronos.Services;

/// <summary>Un événement du catalogue officiel des hooks Claude Code : son nom EXACT tel qu'il doit
/// apparaître comme clé dans settings.json, et le fait qu'il accepte — ou non — un « matcher ».</summary>
public sealed record EvenementHook(string Nom, bool AccepteUnMatcher);

/// <summary>
/// LISTE BLANCHE des noms d'événements de hooks, codée en dur (relevé du 2026-09-12, référence
/// officielle des hooks Claude Code).
///
/// <para><b>Pourquoi elle existe.</b> Le sort d'un nom d'événement INCONNU n'est pas documenté : les
/// termes de rejet sont absents de la page, et le guide a rejeté sa propre affirmation d'un rejet
/// silencieux comme fabriquée. Notre mode de défaillance le plus dangereux est donc un hook MORT et
/// MUET : un nom mal orthographié qui ne produit aucune erreur visible. D'où cette liste blanche,
/// consultée AVANT toute écriture dans le fichier de l'utilisateur — on ne compte jamais sur un
/// avertissement de Claude Code.</para>
///
/// <para><b>Pourquoi le support du « matcher » est porté ici aussi.</b> Un « matcher » posé sur un
/// événement qui n'en accepte pas est <i>silencieusement ignoré</i> : le groupe installé ne ferait
/// alors pas ce qu'il annonce. Une configuration morte ne doit pas s'installer.</para>
///
/// <para>Comparaisons ORDINALES et sensibles à la casse : une clé de settings.json est comparée telle
/// quelle par Claude Code. Aucun rognage implicite — une clé JSON n'a pas d'espaces.</para>
///
/// <para>Type PUR : aucune E/S, aucune horloge, aucun type d'interface graphique.</para>
/// </summary>
public static class CatalogueEvenementsHooks
{
    /// <summary>Les trente-trois événements du catalogue, groupés comme dans le relevé.</summary>
    public static readonly EvenementHook[] Tous =
    {
        // Session
        new("SessionStart",        true),
        new("Setup",               true),
        new("SessionEnd",          true),
        // Tour
        new("UserPromptSubmit",    false),
        new("UserPromptExpansion", true),
        new("Stop",                false),
        new("StopFailure",         true),
        // Outils
        new("PreToolUse",          true),
        new("PermissionRequest",   true),
        new("PermissionDenied",    true),
        new("PostToolUse",         true),
        new("PostToolUseFailure",  true),
        new("PostToolBatch",       false),
        // Affichage
        new("Notification",        true),
        new("MessageDisplay",      false),
        // Sous-agents et tâches
        new("SubagentStart",       true),
        new("SubagentStop",        true),
        new("TaskCreated",         false),
        new("TaskCompleted",       false),
        new("TeammateIdle",        false),
        // Contexte et configuration
        new("InstructionsLoaded",  true),
        new("ConfigChange",        true),
        new("PreCompact",          true),
        new("PostCompact",         true),
        new("PreModelSwitch",      true),
        new("PostModelSwitch",     true),
        // Fichiers
        new("CwdChanged",          false),
        new("DirectoryAdded",      true),
        new("FileChanged",         true),
        new("WorktreeCreate",      false),
        new("WorktreeRemove",      false),
        // MCP
        new("Elicitation",         true),
        new("ElicitationResult",   true),
    };

    // Index ordinal : la clé de settings.json est comparée telle quelle, casse comprise.
    private static readonly System.Collections.Generic.Dictionary<string, EvenementHook> ParNom =
        Tous.ToDictionary(e => e.Nom, System.StringComparer.Ordinal);

    /// <summary>Ce nom figure-t-il, EXACTEMENT, au catalogue officiel ? Nul, vide ou entouré
    /// d'espaces ⇒ non : une clé JSON ne porte pas d'espaces, et on ne devine jamais l'intention.</summary>
    public static bool EstConnu(string? nom) => nom is not null && ParNom.ContainsKey(nom);

    /// <summary>Cet événement accepte-t-il un « matcher » ? Un nom inconnu n'accepte rien.</summary>
    public static bool AccepteUnMatcher(string? nom)
        => nom is not null && ParNom.TryGetValue(nom, out var e) && e.AccepteUnMatcher;
}
