using System;
using System.Collections.Generic;
using System.Windows.Automation;

namespace Chronos.Services;

/// <summary>
/// Implémentation RÉELLE et OS-dépendante de <see cref="IUiaTreeProvider"/> : lit l'arbre
/// d'accessibilité de la fenêtre de l'app bureau Claude via <c>System.Windows.Automation</c>
/// (interop COM MANAGÉ — pas d'admin, pas de dépendance native de RENDU ; cf. CLAUDE.md).
///
/// Ce type est MINCE et SANS logique métier : il projette chaque <see cref="AutomationElement"/>
/// vers le DTO neutre <see cref="UiaNode"/>. Toute l'interprétation (états, type, sidebar, ancre)
/// vit dans <see cref="DesktopUiaSessionSource.MapTree"/> (Plan 01, PURE et testée par faux arbre).
///
/// Il N'EST PAS unit-testé : il dépend d'une vraie fenêtre Claude et de l'OS. Sa correction se
/// juge en exécution. La suite de tests couvre la logique en aval (MapTree) via un faux arbre.
///
/// Surface PUBLIQUE strictement neutre : seul <c>UiaNode? TryGetTree()</c> est public — aucun
/// <see cref="AutomationElement"/> n'apparaît en signature publique (garde ServicesLayerPurityTests :
/// UIAutomationClient/Types ne sont pas dans la liste des assemblies WPF interdits, et de toute
/// façon rien de ces types ne fuit publiquement).
///
/// CONTRAT D'ANCRE (piège « tests verts / prod vide ») : chaque UiaNode émis renseigne
/// <see cref="UiaNode.AutomationId"/> depuis <c>Current.AutomationId</c>. C'est INDISPENSABLE :
/// l'ancre du foreground est le nœud <c>[Document] AID="RootWebArea"</c> (rôle a11y Chromium/IA2
/// stable). Sans propagation de l'AutomationId, MapTree ne trouve jamais l'ancre → widget vide en
/// PROD alors que tous les tests par faux arbre passent. RootWebArea est la SEULE exception au
/// principe « ne pas matcher par AutomationId » (ce n'est pas un « base-ui-_r_XXX_ » volatil).
/// </summary>
public sealed class WindowsUiaTreeProvider : IUiaTreeProvider
{
    /// <summary>Profondeur maximale du walk. VALIDÉ EN APP RÉELLE (2026-07-10) : les signaux utiles de
    /// l'app Claude — barre de composition (« Mode chat », « Arrêter », « Contrôle à distance ») et boutons
    /// sidebar « En cours d'exécution » — sont à profondeur ~13-18. Une borne à 12 les TRONQUAIT : l'ancre
    /// RootWebArea (~prof. 5) était trouvée (santé Ok) mais Kind/Activity ressortaient Unknown et la sidebar
    /// vide. L'arbre complet ne fait que ~1,3 k nœuds → le walk reste léger et non bloquant (poll de fond).</summary>
    private const int MaxDepth = 28;

    /// <summary>Largeur maximale d'enfants explorés par nœud : garde-fou anti-explosion. Borne
    /// naturellement la liste de messages (longue) sans masquer la barre de composition, qui en est un
    /// FRÈRE (atteinte quel que soit le nombre de messages).</summary>
    private const int MaxChildrenPerNode = 400;

    /// <summary>Racine (fenêtre Claude) mise en CACHE entre les polls, réacquise si invalide.</summary>
    private AutomationElement? _root;

    /// <summary>
    /// Arbre neutre de la fenêtre Claude, ou <c>null</c> si absente/indisponible. NE LÈVE JAMAIS :
    /// toute erreur d'accès UIA (fenêtre fermée, élément périmé, COM indisponible) est une
    /// DÉGRADATION silencieuse → retourne null.
    /// </summary>
    public UiaNode? TryGetTree()
    {
        try
        {
            // a. Racine cachée : réacquise si null ou périmée (accès à une propriété lève / fenêtre disparue).
            if (!IsRootAlive(_root))
                _root = FindClaudeWindow();

            if (_root is null)
                return null; // fenêtre Claude introuvable → dégradation (app fermée)

            // b. Walk récursif borné → UiaNode neutre (AutomationId inclus : contrat d'ancre).
            return Convert(_root, 0);
        }
        catch
        {
            // Toute erreur = dégradation. On invalide la racine cachée pour forcer une réacquisition propre.
            _root = null;
            return null;
        }
    }

    /// <summary>La racine cachée est-elle toujours vivante ? Un accès à <c>Current</c> sur un
    /// élément périmé lève → on considère la racine à réacquérir.</summary>
    private static bool IsRootAlive(AutomationElement? element)
    {
        if (element is null) return false;
        try
        {
            _ = element.Current.NativeWindowHandle; // touche l'élément : lève si périmé
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cherche la fenêtre de l'app bureau Claude sous le bureau : ControlType Window dont le Name
    /// contient « Claude » (matching tolérant, insensible à la casse). Borné à <c>TreeScope.Children</c>
    /// (fenêtres de premier niveau) pour rester léger. Retourne null si absente.
    /// </summary>
    private static AutomationElement? FindClaudeWindow()
    {
        var desktop = AutomationElement.RootElement;
        if (desktop is null) return null;

        var windows = desktop.FindAll(
            TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));

        foreach (AutomationElement w in windows)
        {
            try
            {
                var name = w.Current.Name;
                if (!string.IsNullOrEmpty(name)
                    && name.IndexOf("Claude", StringComparison.OrdinalIgnoreCase) >= 0)
                    return w;
            }
            catch
            {
                // Fenêtre disparue pendant l'énumération : ignorer, continuer.
            }
        }

        return null;
    }

    /// <summary>
    /// Projette un <see cref="AutomationElement"/> et son sous-arbre vers un <see cref="UiaNode"/>
    /// neutre, via <c>TreeWalker.ControlViewWalker</c>. Borné en profondeur (<see cref="MaxDepth"/>)
    /// et en largeur (<see cref="MaxChildrenPerNode"/>). RENSEIGNE LES QUATRE champs du UiaNode —
    /// dont <see cref="UiaNode.AutomationId"/> (contrat d'ancre RootWebArea).
    /// </summary>
    private static UiaNode Convert(AutomationElement element, int depth)
    {
        string controlType;
        string name;
        string? automationId;
        bool enabled;

        try
        {
            var info = element.Current;
            controlType = info.ControlType?.ProgrammaticName ?? string.Empty;
            name = info.Name ?? string.Empty;
            // AutomationId : porte le littéral d'ancre « RootWebArea » (Document). Sans lui, MapTree
            // ne reconnaît jamais le foreground → widget vide en prod. Ne JAMAIS l'oublier.
            automationId = info.AutomationId;
            enabled = info.IsEnabled;
        }
        catch
        {
            // Élément périmé pendant la lecture : nœud minimal neutre, on ne descend pas.
            return new UiaNode(string.Empty, string.Empty, null, false, Array.Empty<UiaNode>());
        }

        var children = depth >= MaxDepth
            ? (IReadOnlyList<UiaNode>)Array.Empty<UiaNode>()
            : ReadChildren(element, depth);

        return new UiaNode(controlType, name, automationId, enabled, children);
    }

    /// <summary>Lit les enfants (vue Control) d'un élément, bornés en nombre, tolérant aux périmés.</summary>
    private static IReadOnlyList<UiaNode> ReadChildren(AutomationElement element, int depth)
    {
        var walker = TreeWalker.ControlViewWalker;
        var result = new List<UiaNode>();

        try
        {
            var child = walker.GetFirstChild(element);
            var count = 0;
            while (child is not null && count < MaxChildrenPerNode)
            {
                result.Add(Convert(child, depth + 1));
                count++;

                try
                {
                    child = walker.GetNextSibling(child);
                }
                catch
                {
                    break; // fratrie périmée : on arrête proprement
                }
            }
        }
        catch
        {
            // Sous-arbre inaccessible : on rend ce qui a été collecté (dégradation).
        }

        return result;
    }
}
