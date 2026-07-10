using System;
using System.Collections.Generic;
using System.Linq;

namespace Chronos.Services;

/// <summary>Santé du dernier poll de la source bureau — le repli est TRACÉ (jamais un zéro silencieux).</summary>
public enum DesktopHealth
{
    Unknown,        // aucun poll encore effectué
    Ok,             // fenêtre Claude + ancre RootWebArea trouvées
    WindowMissing,  // arbre null : app fermée / fenêtre absente
    AnchorMissing,  // fenêtre présente MAIS ancre RootWebArea absente (ex. MAJ de l'app) — distinct de « fermée »
}

/// <summary>
/// Source de sessions lisant l'arbre d'accessibilité de l'app bureau Claude (via un
/// <see cref="IUiaTreeProvider"/> injecté). Toute la logique vit dans <see cref="MapTree"/>, PURE et
/// testable par un FAUX arbre — aucune dépendance à System.Windows.Automation ici.
///
/// Threading (ROB-07) : <see cref="Poll"/> (appelé HORS thread UI par le poll de fond, Plan 02) remplit
/// un cache ; <see cref="Read"/> rend ce cache de façon SYNCHRONE et NON bloquante (aucune I/O UIA dans
/// le chemin appelé par le timer 2 s de SessionsViewModel).
/// </summary>
public sealed class DesktopUiaSessionSource : ISessionSource
{
    /// <summary>Littéral d'ancre du foreground : rôle a11y Chromium/IA2 STABLE porté par l'AutomationId
    /// (ControlType "Document"). SEULE exception au principe « ne pas matcher par AutomationId » — ce
    /// n'est PAS un identifiant volatil « base-ui-_r_XXX_ ».</summary>
    private const string AnchorAutomationId = "RootWebArea";

    /// <summary>Libellé lisible de la conversation au premier plan (titre fenêtre = « Claude », non fiable).</summary>
    private const string ForegroundProject = "Claude (bureau)";

    private readonly IUiaTreeProvider _provider;
    private volatile IReadOnlyList<SessionSnapshot> _cache = Array.Empty<SessionSnapshot>();

    /// <summary>Dernier état de santé du poll (diag/futur). WindowMissing ≠ AnchorMissing ≠ Ok.</summary>
    public DesktopHealth Health { get; private set; } = DesktopHealth.Unknown;

    public DesktopUiaSessionSource(IUiaTreeProvider provider)
        => _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>
    /// Acquiert l'arbre, évalue la santé et remplit le cache. Appelé HORS thread UI (Plan 02).
    /// Toute erreur est une DÉGRADATION : on garde l'ancien cache, on ne jette jamais.
    /// </summary>
    public void Poll(DateTimeOffset now)
    {
        try
        {
            var tree = _provider.TryGetTree();
            Health = EvaluateHealth(tree);
            _cache = MapTree(tree, now);
        }
        catch
        {
            // Dégradation silencieuse côté crash : on conserve le dernier cache connu (pas d'exception qui remonte).
        }
    }

    /// <summary>Rend le dernier cache, NON bloquant (ROB-07) — aucune I/O UIA dans ce chemin.</summary>
    public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => _cache;

    /// <summary>
    /// Diagnostic de santé : le repli AnchorMissing est TRACÉ (pas un zéro silencieux) et reste
    /// DISTINCT de WindowMissing (app fermée) — un « fenêtre présente / ancre absente » n'est jamais
    /// confondu avec « app fermée ».
    /// </summary>
    private static DesktopHealth EvaluateHealth(UiaNode? root)
    {
        if (root is null) return DesktopHealth.WindowMissing;
        return HasAnchor(root) ? DesktopHealth.Ok : DesktopHealth.AnchorMissing;
    }

    /// <summary>
    /// Traduit un arbre NEUTRE en snapshots honnêtes. PURE (aucun état, aucune I/O). Ne lève JAMAIS :
    /// racine null ou ancre absente → liste VIDE.
    /// </summary>
    public static IReadOnlyList<SessionSnapshot> MapTree(UiaNode? root, DateTimeOffset now)
    {
        if (root is null) return Array.Empty<SessionSnapshot>();

        var all = Descendants(root).ToList();

        // a. ANCRE : le foreground n'est exploitable que si l'ancre RootWebArea (AutomationId) est présente.
        //    Ancre absente → liste vide (le repli tracé Health=AnchorMissing est posé par Poll/EvaluateHealth).
        if (!all.Any(n => string.Equals(n.AutomationId, AnchorAutomationId, StringComparison.Ordinal)))
            return Array.Empty<SessionSnapshot>();

        var result = new List<SessionSnapshot>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // c. TYPE foreground : Chat prioritaire, puis Code (panneaux), puis Cowork (pont VM).
        var kind = InferKind(all);

        // b. ÉTAT foreground : Responding/Stop → Working ; Mode chat + placeholder → WaitingTurn ;
        //    sinon Unknown (jamais inventé). Pas de WaitingAttention : permission non détectable (cf. InferActivity).
        var activity = InferActivity(all);

        // d. COWORK VM (BUR-05) : sa présence est signalée, son exécution distante N'est PAS connue → Unknown forcé.
        if (kind == SessionKind.Cowork) activity = SessionActivity.Unknown;

        // e. Snapshot foreground, clé synthétique stable desktop:foreground:<kind>.
        var fgKey = $"desktop:foreground:{KindSlug(kind)}";
        if (seen.Add(fgKey))
            result.Add(new SessionSnapshot(fgKey, ForegroundProject, activity, null, now, kind, SessionOrigin.Desktop));

        // f. SIDEBAR (BUR-04) : chaque bouton « En cours d'exécution <nom> » = 1 session active nommée.
        //    Les boutons SANS ce préfixe ne produisent RIEN. Kind hérité du foreground (ou Code par défaut agentique).
        var sidebarKind = kind == SessionKind.Unknown ? SessionKind.Code : kind;
        foreach (var n in all)
        {
            if (!string.Equals(n.ControlType, "Button", StringComparison.OrdinalIgnoreCase)) continue;
            if (!UiaLabels.StartsWithAny(n.Name, UiaLabels.RunningPrefix, out var nom)) continue;
            if (string.IsNullOrWhiteSpace(nom)) continue;

            var key = $"desktop:{KindSlug(sidebarKind)}:{nom}";
            if (seen.Add(key))
                result.Add(new SessionSnapshot(key, nom, SessionActivity.Working, null, now, sidebarKind, SessionOrigin.Desktop));
        }

        return result;
    }

    // --- Inférences pures sur l'ensemble aplati des nœuds ---

    private static SessionActivity InferActivity(IReadOnlyCollection<UiaNode> all)
    {
        if (all.Any(n => UiaLabels.Matches(n.Name, UiaLabels.Responding))
            || all.Any(n => UiaLabels.Matches(n.Name, UiaLabels.StopButton)))
            return SessionActivity.Working;

        // Pas de branche WaitingAttention : « attend une permission » n'est PAS détectable de façon fiable
        // dans l'arbre bureau (le seul libellé candidat, « Ignorer les permissions », est un toggle
        // persistant → faux positif ; validé en app réelle). Voir UiaLabels. On sous-claim honnêtement.
        var chatMode = all.Any(n => UiaLabels.Matches(n.Name, UiaLabels.ChatMode));
        var placeholder = all.Any(n => UiaLabels.Matches(n.Name, UiaLabels.ChatPlaceholder));
        if (chatMode && placeholder)
            return SessionActivity.WaitingTurn;

        return SessionActivity.Unknown; // rien d'exploitable → indéterminé, jamais deviné
    }

    private static SessionKind InferKind(IReadOnlyCollection<UiaNode> all)
    {
        if (all.Any(n => UiaLabels.Matches(n.Name, UiaLabels.ChatMode)))
            return SessionKind.Chat;
        if (all.Any(n => UiaLabels.Matches(n.Name, UiaLabels.CodePanels)))
            return SessionKind.Code;
        if (all.Any(n => UiaLabels.Matches(n.Name, UiaLabels.RemoteControl)))
            return SessionKind.Cowork;
        return SessionKind.Unknown;
    }

    private static bool HasAnchor(UiaNode root)
        => Descendants(root).Any(n => string.Equals(n.AutomationId, AnchorAutomationId, StringComparison.Ordinal));

    private static string KindSlug(SessionKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>Parcours en profondeur, racine incluse. Tolérant : enfants null ignorés.</summary>
    private static IEnumerable<UiaNode> Descendants(UiaNode root)
    {
        var stack = new Stack<UiaNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            yield return n;
            if (n.Children is null) continue;
            foreach (var c in n.Children)
                if (c is not null) stack.Push(c);
        }
    }
}
