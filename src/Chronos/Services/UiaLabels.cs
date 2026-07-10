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

    // NOTE (honnêteté — validé en app réelle le 2026-07-10) : « Ignorer les permissions » / « Skip
    // permissions » sont un TOGGLE PERSISTANT de la barre de composition (mode agentique), présent en
    // PERMANENCE dans les sessions Code — PAS un dialogue transitoire de permission. S'en servir comme
    // signal « attend une permission » produisait un FAUX POSITIF (toute session Code affichée « attend
    // permission »). Faute d'un échantillon UIA vérifié d'une VRAIE demande de permission, la source bureau
    // NE DÉTECTE PAS WaitingAttention (comme TranscriptSessionSource : « non détectable côté bureau ») :
    // elle n'émet que Working / WaitingTurn / Unknown. À rétablir si un signal transitoire fiable est capturé.

    /// <summary>Étiquette « Mode chat » (repos, conversation non agentique).</summary>
    public static readonly string[] ChatMode = { "Mode chat", "Chat mode" };

    /// <summary>Placeholder du champ de saisie au repos (attend ton message).</summary>
    public static readonly string[] ChatPlaceholder = { "Tapez / pour les commandes", "Type / for commands" };

    /// <summary>Préfixe des boutons sidebar de sessions agentiques ACTIVES : « En cours d'exécution &lt;nom&gt; ».</summary>
    public static readonly string[] RunningPrefix = { "En cours d'exécution", "Running" };

    /// <summary>Pont VM Cowork : sa présence signale une session Cowork (état d'exécution NON connu localement).</summary>
    public static readonly string[] RemoteControl = { "Contrôle à distance", "Remote control" };

    /// <summary>Conteneur de la LISTE DE MESSAGES (« Messages de la conversation »). Son sous-arbre est
    /// EXCLU du matching : le TEXTE des messages peut mentionner n'importe quel libellé (« Mode chat »,
    /// « Contrôle à distance »…) et fausserait type/état/nom. On ne cherche les signaux que dans les
    /// zones de CONTRÔLE (barre de composition, en-tête, sidebar) — validé app réelle le 2026-07-10.</summary>
    public static readonly string[] MessagesContainer = { "Messages de la conversation", "Conversation messages" };

    /// <summary>En-tête de session au premier plan (« Volet principal ») : son PREMIER bouton porte le
    /// TITRE/nom de la session (ex. « Untitled » si non titrée, sinon le titre, ou le repo/workspace).
    /// Ancre STABLE validée app réelle le 2026-07-10 (prof. ~13) — contrairement au groupe « Contrôles du
    /// dépôt » qui alterne Group/Text selon l'état de l'UI (fragile).</summary>
    public static readonly string[] SessionHeaderGroup = { "Volet principal", "Main panel" };

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
