using System;

namespace Chronos.Services;

/// <summary>
/// Table de libellés fr/en des signaux lus dans l'arbre d'accessibilité de l'app bureau Claude,
/// + helpers de matching TOLÉRANT (insensible à la casse et aux espaces).
///
/// Cœur de ROB-06 : la table est EXTENSIBLE — une MAJ de l'app qui renomme/traduit un libellé se
/// corrige en AJOUTANT une variante ici, SANS toucher au code de logique (DesktopUiaSessionSource).
/// On matche par ControlType + Name (libellés) et JAMAIS par un AutomationId volatil « base-ui-_r_XXX_ ».
/// (Seule exception documentée à la règle AutomationId : l'ancre "RootWebArea", un rôle a11y stable,
/// traitée dans DesktopUiaSessionSource — pas ici.)
/// </summary>
public static class UiaLabels
{
    /// <summary>Texte « génération en cours ».</summary>
    public static readonly string[] Responding = { "Claude répond.", "Claude is responding" };

    /// <summary>Bouton d'arrêt présent = l'assistant bosse.</summary>
    public static readonly string[] StopButton = { "Arrêter", "Stop" };

    /// <summary>Bouton de décision de permission = attend une intervention maintenant.</summary>
    public static readonly string[] PermissionButton =
        { "Ignorer les permissions", "Skip permissions", "Autoriser", "Allow" };

    /// <summary>Étiquette « Mode chat » (repos, conversation non agentique).</summary>
    public static readonly string[] ChatMode = { "Mode chat", "Chat mode" };

    /// <summary>Placeholder du champ de saisie au repos (attend ton message).</summary>
    public static readonly string[] ChatPlaceholder = { "Tapez / pour les commandes", "Type / for commands" };

    /// <summary>Préfixe des boutons sidebar de sessions agentiques ACTIVES : « En cours d'exécution &lt;nom&gt; ».</summary>
    public static readonly string[] RunningPrefix = { "En cours d'exécution", "Running" };

    /// <summary>Pont VM Cowork : sa présence signale une session Cowork (état d'exécution NON connu localement).</summary>
    public static readonly string[] RemoteControl = { "Contrôle à distance", "Remote control" };

    /// <summary>Affordances d'une vue agentique Code.</summary>
    public static readonly string[] CodePanels =
        { "Terminal", "Diff", "Aperçu", "Preview", "Actions de session", "Session actions" };

    /// <summary>
    /// Vrai si <paramref name="name"/> (trimé) est égal à l'une des <paramref name="variants"/>,
    /// casse insensible et culture-invariante. Faux si name null/vide. Ne lève jamais.
    /// </summary>
    public static bool Matches(string? name, string[] variants)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var trimmed = name.Trim();
        foreach (var v in variants)
            if (string.Equals(trimmed, v, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>
    /// Vrai si <paramref name="name"/> (trimé) COMMENCE par l'un des <paramref name="prefixes"/>
    /// (casse insensible). Dans ce cas <paramref name="remainder"/> = reste trimé (ex. le &lt;nom&gt;
    /// de « En cours d'exécution &lt;nom&gt; »). Sinon remainder = "" et retourne false. Ne lève jamais.
    /// </summary>
    public static bool StartsWithAny(string? name, string[] prefixes, out string remainder)
    {
        remainder = "";
        if (string.IsNullOrWhiteSpace(name)) return false;
        var trimmed = name.Trim();
        foreach (var p in prefixes)
        {
            if (trimmed.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            {
                remainder = trimmed.Substring(p.Length).Trim();
                return true;
            }
        }
        return false;
    }
}
