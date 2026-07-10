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

    /// <summary>Nom de repli du foreground quand aucun repo/workspace n'est lisible (titre fenêtre = « Claude »,
    /// non fiable ; le titre de conversation n'est pas exposé de façon stable — repli honnête).</summary>
    private const string ForegroundFallbackName = "(sans titre)";

    private readonly IUiaTreeProvider _provider;
    private volatile IReadOnlyList<SessionSnapshot> _cache = Array.Empty<SessionSnapshot>();

    /// <summary>Sessions vues RÉCEMMENT (clé → snapshot + horodatage de dernière vue). Touché UNIQUEMENT
    /// par <see cref="Poll"/> (un seul thread de fond) → pas de verrou. Sert à garder une liste STABLE quand
    /// l'utilisateur navigue entre les modes Claude (Home/Code) : l'arbre UIA n'expose QUE le mode courant,
    /// donc sans accumulation la liste changerait à chaque changement d'interface.</summary>
    private readonly Dictionary<string, (SessionSnapshot Snap, DateTimeOffset Seen)> _seen = new(StringComparer.Ordinal);

    /// <summary>Durée de rétention d'une session non re-vue (au-delà → retirée). Assez long pour survivre à
    /// une navigation entre modes, assez court pour ne pas garder indéfiniment une session terminée.</summary>
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromMinutes(3);

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

            // Vue courante (mode Claude actuellement affiché) → mise à jour des sessions vues.
            foreach (var s in MapTree(tree, now)) _seen[s.SessionId] = (s, now);

            // Purge des sessions plus vues depuis > RetentionWindow.
            foreach (var k in _seen.Where(kv => now - kv.Value.Seen > RetentionWindow).Select(kv => kv.Key).ToList())
                _seen.Remove(k);

            // Cache STABLE = sessions vues récemment, avec leur DERNIER état connu conservé (on ne bascule
            // PAS vers Unknown quand un autre mode est affiché : ce serait un clignotement d'état à chaque
            // navigation). La fraîcheur est portée par UpdatedAt (« il y a X min ») ; au-delà de
            // RetentionWindow la session est purgée. Cohérent avec la péremption des sessions par hooks.
            _cache = _seen.Values
                .OrderByDescending(v => v.Seen)
                .Select(v => v.Snap)
                .ToList();
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

        // a. ANCRE (sur l'arbre COMPLET — contrôle structurel) : le foreground n'est exploitable que si
        //    l'ancre RootWebArea (AutomationId) est présente. Absente → liste vide (Health=AnchorMissing).
        if (!Descendants(root).Any(n => string.Equals(n.AutomationId, AnchorAutomationId, StringComparison.Ordinal)))
            return Array.Empty<SessionSnapshot>();

        // Matching des libellés UNIQUEMENT sur les nœuds de CONTRÔLE : le sous-arbre « Messages de la
        // conversation » est EXCLU (son texte pollue type/état/nom — une conversation peut mentionner
        // « Mode chat », « Contrôle à distance »…). Les vrais contrôles (composition, en-tête, sidebar) restent.
        var all = ControlNodes(root).ToList();

        var result = new List<SessionSnapshot>();
        // Dédoublonnage par NOM (insensible à la casse) : une session déjà listée n'est jamais ré-ajoutée —
        // ni entre sidebar et foreground, ni d'un poll à l'autre (l'accumulation clé par SessionId, lui-même
        // stable puisque indépendant du type ci-dessous).
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // c. TYPE foreground : Cowork (pont VM) prioritaire, puis Code (panneaux), puis Chat (cf. InferKind).
        var kind = InferKind(all);

        // b. ÉTAT foreground : Responding/Stop → Working ; Mode chat + placeholder → WaitingTurn ;
        //    sinon Unknown (jamais inventé). Pas de WaitingAttention : permission non détectable (cf. InferActivity).
        var activity = InferActivity(all);

        // d. COWORK VM (BUR-05) : sa présence est signalée, son exécution distante N'est PAS connue → Unknown forcé.
        if (kind == SessionKind.Cowork) activity = SessionActivity.Unknown;

        // e. SIDEBAR (BUR-04) D'ABORD : chaque bouton « En cours d'exécution <nom> » = 1 session active NOMMÉE.
        //    CLÉ INDÉPENDANTE DU TYPE (desktop:session:<nom>). Le type par entrée n'est pas exposé et notre
        //    heuristique (Cowork si app bridgée, sinon Code) dépend de la VUE AFFICHÉE : mettre le type dans la
        //    clé re-cléerait la même session à chaque changement de vue → DOUBLON via l'accumulation. Le NOM est
        //    stable. Dédoublonnage par nom.
        var bridged = all.Any(n => UiaLabels.Matches(n.Name, UiaLabels.RemoteControl));
        var sidebarKind = bridged ? SessionKind.Cowork : SessionKind.Code;
        foreach (var n in all)
        {
            if (!string.Equals(n.ControlType, "Button", StringComparison.OrdinalIgnoreCase)) continue;
            if (!UiaLabels.StartsWithAny(n.Name, UiaLabels.RunningPrefix, out var nom)) continue;
            if (string.IsNullOrWhiteSpace(nom)) continue;
            if (!names.Add(nom)) continue; // déjà listée

            result.Add(new SessionSnapshot($"desktop:session:{nom}", nom, SessionActivity.Working, null, now, sidebarKind, SessionOrigin.Desktop));
        }

        // f. Snapshot FOREGROUND (la conversation affichée). NOM : 1er bouton de l'en-tête « Volet principal »
        //    (titre) → sinon « (sans titre) ». On ne l'émet PAS si :
        //    • kind == Cowork : état VM distant non observable → « Untitled » gris permanent inutile (le Cowork
        //      qui tourne est déjà dans la sidebar) ;
        //    • son nom est DÉJÀ listé par la sidebar : ouvrir une session déjà identifiée ne doit pas créer un
        //      DOUBLON foreground↔sidebar.
        if (kind != SessionKind.Cowork)
        {
            var fgName = ForegroundName(root);
            if (names.Add(fgName))
                result.Add(new SessionSnapshot($"desktop:foreground:{KindSlug(kind)}", fgName, activity, null, now, kind, SessionOrigin.Desktop));
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

    // Ordre du PLUS SPÉCIFIQUE au moins spécifique (validé app réelle le 2026-07-10) :
    //   1) Contrôle à distance = pont VM → Cowork (le signal le plus discriminant) ;
    //   2) panneaux Terminal/Diff/Actions de session → Code (agentique local) ;
    //   3) « Mode chat » → Chat (libellé le plus ambigu : co-présent avec les affordances agentiques
    //      dans l'app unifiée — le tester en DERNIER, sinon toute session Cowork/Code est étiquetée Chat).
    private static SessionKind InferKind(IReadOnlyCollection<UiaNode> all)
    {
        if (all.Any(n => UiaLabels.Matches(n.Name, UiaLabels.RemoteControl)))
            return SessionKind.Cowork;
        if (all.Any(n => UiaLabels.Matches(n.Name, UiaLabels.CodePanels)))
            return SessionKind.Code;
        if (all.Any(n => UiaLabels.Matches(n.Name, UiaLabels.ChatMode)))
            return SessionKind.Chat;
        return SessionKind.Unknown;
    }

    private static bool HasAnchor(UiaNode root)
        => Descendants(root).Any(n => string.Equals(n.AutomationId, AnchorAutomationId, StringComparison.Ordinal));

    /// <summary>Nom du foreground : PREMIER bouton sous l'en-tête de session « Volet principal » (= titre
    /// de la session, « Untitled » si non titrée, sinon le repo/workspace) → sinon
    /// <see cref="ForegroundFallbackName"/>. On ne prend que ce qui est réellement exposé, sans inventer.</summary>
    private static string ForegroundName(UiaNode root)
    {
        var header = FirstDescendant(root, n =>
            string.Equals(n.ControlType, "Group", StringComparison.OrdinalIgnoreCase)
            && UiaLabels.Matches(n.Name, UiaLabels.SessionHeaderGroup));

        if (header is not null)
        {
            var titre = FirstDescendant(header, n =>
                string.Equals(n.ControlType, "Button", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(n.Name));
            if (titre is not null) return titre.Name.Trim();
        }

        return ForegroundFallbackName;
    }

    /// <summary>Premier nœud (racine incluse) satisfaisant <paramref name="pred"/>, en ORDRE DOCUMENT
    /// (contrairement à <see cref="Descendants"/> qui, via une pile, inverse l'ordre des frères — le nom du
    /// repo est le PREMIER bouton du groupe, l'ordre compte). Tolérant : enfants null ignorés.</summary>
    private static UiaNode? FirstDescendant(UiaNode node, Func<UiaNode, bool> pred)
    {
        if (UiaLabels.Matches(node.Name, UiaLabels.MessagesContainer)) return null; // exclure les messages
        if (pred(node)) return node;
        if (node.Children is null) return null;
        foreach (var c in node.Children)
        {
            if (c is null) continue;
            var found = FirstDescendant(c, pred);
            if (found is not null) return found;
        }
        return null;
    }

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

    /// <summary>Nœuds de CONTRÔLE : tout l'arbre SAUF le sous-arbre du conteneur de messages
    /// (<see cref="UiaLabels.MessagesContainer"/>), dont le texte pollue le matching des libellés. On ne
    /// descend JAMAIS dans ce conteneur. Le reste (barre de composition, en-tête « Volet principal »,
    /// sidebar) est conservé.</summary>
    private static IEnumerable<UiaNode> ControlNodes(UiaNode node)
    {
        if (UiaLabels.Matches(node.Name, UiaLabels.MessagesContainer))
            yield break; // pruner : ne pas descendre dans la liste de messages
        yield return node;
        if (node.Children is null) yield break;
        foreach (var c in node.Children)
            if (c is not null)
                foreach (var d in ControlNodes(c))
                    yield return d;
    }
}
