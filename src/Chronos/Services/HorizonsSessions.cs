namespace Chronos.Services;

/// <summary>
/// LA CHAÎNE DES HORIZONS du widget de sessions, en UN seul endroit (SIL-01 ; recommandation n° 2 de l'audit v1.6,
/// §6). Chaque inégalité a une raison, et un test rougit si l'une se défait :
/// <list type="bullet">
///   <item><see cref="Silence"/> &lt; <see cref="Abandon"/> : on cesse de dire « Réflexion » bien avant de cesser de
///   lire — vingt minutes pour ne plus mentir, huit heures pour disparaître ;</item>
///   <item><see cref="Abandon"/> &lt; <see cref="RetentionTraitees"/> : une entrée « traitée » ne peut pas expirer
///   pendant que sa session est encore lisible — sinon elle reviendrait sans avoir rien redemandé ;</item>
///   <item><see cref="RetentionTraitees"/> &lt; <see cref="ExpirationEtat"/> : on ne balaie jamais un fichier d'état
///   que le widget ou le magasin des traitées pourraient encore lire ;</item>
///   <item><see cref="Abandon"/> ≤ <see cref="LectureAppBureau"/> : la fenêtre de LECTURE des métadonnées de l'app
///   bureau couvre tout ce qui peut encore s'afficher — elle économise des lectures, elle ne cache aucun signal ;</item>
///   <item><see cref="GraceLecture"/> &lt; <see cref="Silence"/> : la grâce d'un regard n'a rien à voir avec la durée
///   d'un silence.</item>
/// </list>
/// Avant la phase 28, ces valeurs vivaient en littéraux privés dans quatre fichiers, et une cinquième — quinze
/// minutes, la fenêtre des transcripts — faisait disparaître en silence toute session sans fichier de hook (trou
/// §9.1 de l'audit v1.6). Aucun type WPF.
/// </summary>
public static class HorizonsSessions
{
    /// <summary>Un TRAVAIL sans nouveau signal depuis ce délai devient une attente DÉDUITE (« En attente ? »).
    /// Appliqué par le moniteur, en un seul point, à TOUS les signaux — battement de hook comme dernier message de
    /// transcript.</summary>
    public static readonly System.TimeSpan Silence = System.TimeSpan.FromMinutes(20);

    /// <summary>Au-delà, un signal n'est plus lu du tout — fichier de hook comme transcript.</summary>
    public static readonly System.TimeSpan Abandon = System.TimeSpan.FromHours(8);

    /// <summary>Rétention du FICHIER treated.json : borne de croissance, jamais délai d'affichage.</summary>
    public static readonly System.TimeSpan RetentionTraitees = System.TimeSpan.FromHours(24);

    /// <summary>Au-delà ET sans attestation de vie, un fichier d'état est balayé (phase 23).</summary>
    public static readonly System.TimeSpan ExpirationEtat = System.TimeSpan.FromHours(72);

    /// <summary>Fenêtre de LECTURE des métadonnées de l'app bureau (APP-01) : un fichier écrit il y a plus longtemps
    /// n'est pas ouvert. Économie de lecture, JAMAIS un horizon d'affichage — l'âge d'un signal se juge sur son instant
    /// (<see cref="Abandon"/>). Elle vaut au moins <see cref="Abandon"/> : l'app réécrit le fichier d'une session à
    /// chaque changement, donc un signal de moins de huit heures vient d'un fichier écrit depuis moins de huit heures.</summary>
    public static readonly System.TimeSpan LectureAppBureau = System.TimeSpan.FromHours(24);

    /// <summary>LUE-02 — la GRÂCE de la lecture au premier plan : la session sélectionnée dans l'app, processus claude au
    /// premier plan, n'est « lue » que 2,5 s après le plus tardif de la fin du tour et du retour au premier plan — le
    /// regard a le temps de se poser. Bien en deçà de <see cref="Silence"/> : on lit bien avant de se taire.</summary>
    public static readonly System.TimeSpan GraceLecture = System.TimeSpan.FromMilliseconds(2500);
}
