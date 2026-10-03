namespace Chronos.Services;

/// <summary>Mode de démarrage décidé AVANT toute initialisation (verrou, Host, hooks) — SOC-02.</summary>
public enum ModeDemarrage { Overlay, Hook, GalerieCadrans, GalerieSessions, GalerieHistorique, ArgumentInconnu }

/// <summary>Résultat du tri : le mode, et pour --hook l'événement qui le suit (null s'il manque).</summary>
public sealed record InvocationDemarrage(ModeDemarrage Mode, string? EvenementHook = null);

/// <summary>Tri PUR des arguments de la ligne de commande, en LISTE BLANCHE : un « --xxx » qui n'est pas un mode connu
/// (inconnu ou retiré) donne ArgumentInconnu → sortie silencieuse code 0 avant le verrou. Retirer un mode = supprimer sa
/// constante, sa ligne dans ModesConnus et sa ligne dans Trier : il tombe alors de lui-même dans ArgumentInconnu.</summary>
public static class ArgumentsDemarrage
{
    public const string Hook = "--hook";
    public const string Cadrans = "--cadrans";
    public const string Sessions = "--sessions";
    public const string Historique = "--historique";

    /// <summary>La liste blanche (insensible à la casse à l'usage).</summary>
    public static IReadOnlyList<string> ModesConnus { get; } = new[] { Hook, Cadrans, Sessions, Historique };

    public static InvocationDemarrage Trier(IReadOnlyList<string> args)
    {
        // Préséance IDENTIQUE à la 3.4.0 (mode cherché parmi TOUS les arguments, insensible à la casse) :
        // --hook > --cadrans > --sessions > --historique. L'ancien mode de barre de statut est retiré (phase 37) :
        // il tombe dans ArgumentInconnu, sauf s'il accompagne un mode connu.
        int i = Index(args, Hook);
        if (i >= 0) return new(ModeDemarrage.Hook, i + 1 < args.Count ? args[i + 1] : null);
        if (Index(args, Cadrans) >= 0) return new(ModeDemarrage.GalerieCadrans);
        if (Index(args, Sessions) >= 0) return new(ModeDemarrage.GalerieSessions);
        if (Index(args, Historique) >= 0) return new(ModeDemarrage.GalerieHistorique);
        // LISTE BLANCHE : tout autre « --xxx » → sortie silencieuse. Les arguments sans « -- » gardent le comportement actuel (overlay).
        for (int k = 0; k < args.Count; k++)
            if (args[k].StartsWith("--", StringComparison.Ordinal)) return new(ModeDemarrage.ArgumentInconnu);
        return new(ModeDemarrage.Overlay);
    }

    private static int Index(IReadOnlyList<string> args, string mode)
    {
        for (int k = 0; k < args.Count; k++)
            if (string.Equals(args[k], mode, StringComparison.OrdinalIgnoreCase)) return k;
        return -1;
    }
}
