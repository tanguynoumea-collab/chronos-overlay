namespace Chronos.Models;

/// <summary>Provenance d'une donnée d'usage : exacte (sonde d'en-têtes, secours OAuth, dernier exact frais ou encore
/// valide) ; <c>Estimated</c> = plancher « ≥ X % » : dernier relevé exact vieilli alors que Claude Code a travaillé
/// (seul chiffre non exact) ; ou indisponible.</summary>
public enum SourceReliability { Exact, Estimated, Unavailable }
