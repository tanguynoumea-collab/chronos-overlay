using System.Runtime.InteropServices;

namespace Chronos.Services;

/// <summary>Ce que la sonde de premier plan a vu (LUE-02). Neutre : aucun HWND, aucun type WPF.</summary>
public enum StatutPremierPlan
{
    /// <summary>Personne n'a encore regardé : aucune sonde branchée, ou aucun échantillon pris.</summary>
    NonBranche,

    /// <summary>La fenêtre au premier plan appartient au processus <c>claude</c> (l'app bureau).</summary>
    Claude,

    /// <summary>La fenêtre au premier plan appartient à un autre processus, dont le nom est dit (explorer, LockApp,
    /// Chronos…).</summary>
    AutreProcessus,

    /// <summary>Aucune fenêtre n'a le premier plan : une bascule de fenêtre est en cours. Pas une panne.</summary>
    AucuneFenetre,

    /// <summary>La sonde n'a pas pu dire à qui appartient le premier plan ; la raison est donnée.</summary>
    Indisponible,
}

/// <summary>Un échantillon du premier plan, tel que la sonde l'a vu, et depuis quand <c>claude</c> le tient.</summary>
/// <param name="Statut">Ce que la sonde a vu.</param>
/// <param name="Processus">Le nom du processus propriétaire de la fenêtre au premier plan (sans extension), s'il est connu.</param>
/// <param name="Depuis">Premier échantillon d'une suite ININTERROMPUE où le premier plan était <c>claude</c> ; nul sinon.</param>
/// <param name="Raison">Pourquoi la sonde a échoué (type de l'exception, ou « GetWindowThreadProcessId a échoué ») ; nul sinon.</param>
public sealed record EtatPremierPlan(StatutPremierPlan Statut, string? Processus, System.DateTimeOffset? Depuis, string? Raison = null)
{
    /// <summary>L'état de départ : personne n'a regardé.</summary>
    public static readonly EtatPremierPlan NonBranche = new(StatutPremierPlan.NonBranche, null, null);

    /// <summary>L'instant depuis lequel claude est au premier plan, ou nul : la SEULE chose que le détecteur en reçoit.</summary>
    public System.DateTimeOffset? ClaudeDepuis => Statut == StatutPremierPlan.Claude ? Depuis : null;
}

/// <summary>Ce que l'OS a au premier plan (LUE-02), derrière une interface neutre et testable sans fenêtre.</summary>
public interface IPremierPlan
{
    /// <summary>Best-effort, JAMAIS d'exception. <paramref name="now"/> est l'instant du moniteur (horloge injectée).</summary>
    EtatPremierPlan Lire(System.DateTimeOffset now);
}

/// <summary>
/// La sonde de premier plan de LUE-02 : À QUI appartient la fenêtre que l'utilisateur a devant lui, et DEPUIS QUAND
/// c'est l'app bureau.
///
/// <para><b>Le processus, jamais le titre.</b> La fenêtre au premier plan (<c>GetForegroundWindow</c>) donne son
/// processus (<c>GetWindowThreadProcessId</c>), dont on lit le NOM (<c>Process.ProcessName</c>, sans extension),
/// comparé par ÉGALITÉ ordinale insensible à la casse avec <c>claude</c>. Un titre de fenêtre ne prouverait rien : un
/// onglet de navigateur titré « Claude » passerait. Mesuré sur la machine de l'utilisateur (30-RESEARCH, Q2.a) : le
/// processus de l'app bureau (<c>…\WindowsApps\…\app\claude.exe</c>) se lit <c>claude</c> depuis l'arbre de l'app
/// comme hors de l'arbre (la situation de l'overlay), pour 0,021 ms médian par lecture.</para>
///
/// <para><b>Machine verrouillée.</b> Le premier plan est alors <c>LockApp</c> : un autre processus, et LUE-02 s'éteint
/// d'elle-même.</para>
///
/// <para><b>Limite écrite, non parée.</b> La CLI Claude Code s'appelle aussi <c>claude</c>. Elle n'a pas de fenêtre à
/// elle dans l'usage de l'utilisateur (l'app bureau seule) ; une console classique pourrait lui attribuer la sienne
/// (non vérifié). Constat en phase 31.</para>
///
/// <para><b>Trois issues, pas deux.</b> Claude, un autre processus (nommé), aucune fenêtre (bascule en cours), ou
/// indisponible (avec la raison) : un booléen ne dirait ni « depuis quand » ni « je ne sais pas », que LUE-04 exige
/// de dire. Une sonde en échec ne lève jamais.</para>
///
/// <para><b>Depuis.</b> Un échantillon <c>claude</c> qui suit un échantillon <c>claude</c> garde son « depuis » ; tout
/// autre statut le remet à nul ; il repart de l'instant courant au retour, après un trou d'échantillonnage de plus de
/// cinq secondes (veille, minuteur suspendu : on ne sait pas ce qui s'est passé entre les deux), ou si l'horloge
/// recule. Le résultat est gardé une seconde : le widget et le rapport voient le même premier plan.</para>
///
/// <para><b>Concurrence.</b> Le minuteur de l'interface et le rapport de diagnostic l'appellent : un verrou protège
/// l'état (coût nul).</para>
///
/// <para><b>Pas d'UIA.</b> La source par arbre d'accessibilité a été retirée en phase 21 et des gardes de non-retour
/// l'interdisent : deux appels user32 (DLL du système, aucune dépendance native de rendu) suffisent.</para>
/// </summary>
public sealed class PremierPlanWin32 : IPremierPlan
{
    /// <summary>Le nom du processus de l'app bureau, sans extension.</summary>
    private const string ProcessusClaude = "claude";

    /// <summary>Cohérence entre le widget et le rapport, pas une performance (0,02 ms mesuré).</summary>
    private static readonly System.TimeSpan DureeCache = System.TimeSpan.FromSeconds(1);

    /// <summary>Au-delà, on ne sait pas ce qui s'est passé entre deux échantillons : le « depuis » repart.</summary>
    private static readonly System.TimeSpan TrouMaximal = System.TimeSpan.FromSeconds(5);

    private readonly System.Func<(StatutPremierPlan Statut, string? Processus, string? Raison)> _sonde;
    private readonly object _verrou = new();

    /// <summary>L'instant du dernier échantillon ; nul avant le premier.</summary>
    private System.DateTimeOffset? _instant;

    private EtatPremierPlan _dernier = EtatPremierPlan.NonBranche;

    /// <summary>La sonde réelle (production).</summary>
    public PremierPlanWin32() : this(SonderWin32) { }

    /// <summary>Une sonde injectée (tests) : l'état — depuis, cache, erreurs — se prouve sans fenêtre.</summary>
    internal PremierPlanWin32(System.Func<(StatutPremierPlan Statut, string? Processus, string? Raison)> sonde)
        => _sonde = sonde ?? throw new System.ArgumentNullException(nameof(sonde));

    /// <inheritdoc/>
    public EtatPremierPlan Lire(System.DateTimeOffset now)
    {
        // SQUELETTE (RED) : personne ne regarde encore.
        lock (_verrou)
        {
            _ = _sonde;
            _ = _instant;
            _ = DureeCache;
            _ = TrouMaximal;
            return _dernier;
        }
    }

    /// <summary>ÉGALITÉ ordinale insensible à la casse, jamais un préfixe : « claudette » n'est pas claude. Un nom
    /// vide n'est pas un processus : indisponible.</summary>
    internal static StatutPremierPlan Classer(string? nomProcessus)
    {
        // SQUELETTE (RED) : tout est un autre processus.
        _ = string.Equals(nomProcessus, ProcessusClaude, System.StringComparison.OrdinalIgnoreCase);
        return StatutPremierPlan.AutreProcessus;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    /// <summary>La sonde réelle. Fenêtre nulle ⇒ AucuneFenetre (bascule en cours) ; identifiant introuvable ⇒
    /// Indisponible ; processus terminé entre-temps ⇒ Indisponible avec le type de l'exception. Jamais d'exception.</summary>
    internal static (StatutPremierPlan Statut, string? Processus, string? Raison) SonderWin32()
    {
        try
        {
            var fenetre = GetForegroundWindow();
            if (fenetre == 0) return (StatutPremierPlan.AucuneFenetre, null, null);
            if (GetWindowThreadProcessId(fenetre, out var pid) == 0 || pid == 0)
                return (StatutPremierPlan.Indisponible, null, "GetWindowThreadProcessId a échoué");
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            var nom = p.ProcessName;   // sans extension
            return (Classer(nom), nom, null);
        }
        catch (System.Exception e) { return (StatutPremierPlan.Indisponible, null, e.GetType().Name); }
    }
}
