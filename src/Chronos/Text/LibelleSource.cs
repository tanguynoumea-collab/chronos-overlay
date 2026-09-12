namespace Chronos.Text;

/// <summary>
/// EXA-06 — vocabulaire FR UNIQUE du relevé : QUI l'a produit, DEPUIS QUAND, et ce qu'on a vérifié.
/// Un seul point, consommé par le service de diagnostic ET par l'infobulle du ViewModel. La règle est
/// déjà posée par <c>WindowGaugeViewModel.LibelleStatut</c> : deux mappings divergeraient le jour où le
/// vocabulaire bouge, et l'utilisateur lirait deux noms différents pour la même source selon l'endroit
/// où il regarde.
///
/// Pur : aucune dépendance WPF, aucun I/O, aucune CultureInfo (motif du dossier Text/). <c>now</c> est
/// toujours un PARAMÈTRE — cette classe n'a pas d'horloge propre, donc chacun de ses paliers est
/// déterministe en test.
///
/// Ne réutilise PAS <see cref="CountdownFormatter"/> : celui-ci formate une durée RESTANTE avant un
/// reset (« 3 h 12 » veut y dire « encore trois heures »), là où l'ancienneté regarde vers le passé
/// (« il y a 3 h 12 »). Même arithmétique, sens inverse : les confondre produirait un contresens.
/// </summary>
public static class LibelleSource
{
    /// <summary>
    /// Nom FR du producteur d'un chiffre. <c>null</c> — la fenêtre ne nomme personne — rend le seul
    /// libellé de repli de cette classe, et JAMAIS un nom de source par défaut : affirmer qu'une source
    /// alimente l'affichage alors qu'on l'ignore est exactement la panne silencieuse que ce milestone
    /// traque.
    /// </summary>
    public static string Format(Chronos.Models.SourceUsage? source) => source switch
    {
        Chronos.Models.SourceUsage.SondeEnTetes         => "sonde d'en-têtes de rate-limit",
        Chronos.Models.SourceUsage.EndpointOAuthChronos => "endpoint OAuth (login Chronos)",
        Chronos.Models.SourceUsage.EndpointOAuthClaude  => "endpoint OAuth (jeton app bureau / CLI)",
        Chronos.Models.SourceUsage.PontStatusLine       => "pont statusLine Claude Code",
        Chronos.Models.SourceUsage.MagasinDernierExact  => "dernier exact persisté",
        _                                               => "non renseignée",
    };

    /// <summary>
    /// Ancienneté d'un relevé, rédigée pour suivre le mot « relevé » : « relevé il y a 12 min ».
    ///
    /// Un instant POSTÉRIEUR à <paramref name="now"/> (horloge qui recule, source qui horodate en
    /// avance) rend « à l'instant » et jamais une durée négative : un chiffre daté du futur n'est pas
    /// une information, et l'afficher tel quel ferait passer un défaut d'horloge pour une précision.
    ///
    /// Paliers : &lt; 1 min → « à l'instant » ; &lt; 1 h → minutes ; &lt; 24 h → heures et minutes sur
    /// deux chiffres ; au-delà → jours. Chaque palier tronque vers le bas, donc n'exagère jamais la
    /// fraîcheur du relevé.
    /// </summary>
    public static string Anciennete(System.DateTimeOffset? capturedAt, System.DateTimeOffset now)
    {
        if (capturedAt is not { } t) return "de date inconnue";

        var age = now - t;

        if (age < System.TimeSpan.FromMinutes(1)) return "à l'instant";   // couvre aussi l'âge négatif
        if (age < System.TimeSpan.FromHours(1)) return $"il y a {(int)age.TotalMinutes} min";
        if (age < System.TimeSpan.FromDays(1)) return $"il y a {(int)age.TotalHours} h {age.Minutes:D2}";
        return $"il y a {(int)age.TotalDays} j";
    }

    /// <summary>
    /// Ce qu'on a VÉRIFIÉ sur le chiffre — axe orthogonal au nom de la source. <c>null</c> (la doctrine
    /// ne s'est pas prononcée) rend la chaîne VIDE : mieux vaut ne rien dire que qualifier un chiffre
    /// sur lequel personne n'a statué.
    /// </summary>
    public static string Provenance(Chronos.Models.ProvenanceReleve? p) => p switch
    {
        Chronos.Models.ProvenanceReleve.Frais                => "frais",
        Chronos.Models.ProvenanceReleve.EncoreValide         => "encore valide (aucune activité depuis)",
        Chronos.Models.ProvenanceReleve.PlancherAvecActivite => "borne inférieure (activité depuis)",
        _                                                    => "",
    };
}
