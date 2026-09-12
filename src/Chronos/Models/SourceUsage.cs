namespace Chronos.Models;

/// <summary>
/// EXA-06 — QUI a produit un chiffre d'usage. Les cinq membres sont calqués sur la chaîne DI réelle
/// (App.xaml.cs), de l'externe vers l'interne : rien n'est prévu « au cas où », chaque membre correspond
/// à un producteur réellement inscrit.
///
/// Orthogonal à <see cref="ProvenanceReleve"/>, qui dit ce qu'on a VÉRIFIÉ : un plancher a une SOURCE
/// (celle du relevé mémorisé) ET un ÉTAT (« borne inférieure »). Les fusionner produirait un enum
/// produit cartésien de quinze membres — et rendrait le diagnostic incapable de dire lequel des deux
/// points de terminaison OAuth répond, c'est-à-dire de faire son travail.
///
/// AUCUN membre fourre-tout, et c'est une règle de non-retour : l'absence de source se dit par
/// <c>null</c>, jamais par une valeur d'enum. Le projet proscrit les affirmations nées d'une absence —
/// une fenêtre que personne n'alimente ne doit nommer personne. Le critère de garde est un comptage par
/// grep : ce commentaire ne reproduit donc pas le nom du membre interdit, sinon il se déclencherait
/// lui-même (doctrine établie en phase 19).
/// </summary>
public enum SourceUsage
{
    /// <summary>RateLimitHeaderUsageProvider — en-têtes anthropic-ratelimit-unified-*.</summary>
    SondeEnTetes,

    /// <summary>ChronosOAuthUsageProvider — /api/oauth/usage, jeton du login Chronos.</summary>
    EndpointOAuthChronos,

    /// <summary>ClaudeOAuthUsageProvider — /api/oauth/usage, coffre de l'app bureau / CLI.</summary>
    EndpointOAuthClaude,

    /// <summary>ClaudeUsageObjectProvider — usage.json écrit par le pont statusLine.</summary>
    PontStatusLine,

    /// <summary>LastExactStore.Reconstruire — dernier relevé exact persisté sur disque.</summary>
    MagasinDernierExact,
}
