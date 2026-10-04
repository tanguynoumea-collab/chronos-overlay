using Chronos.Models.Historique.Tokens;

namespace Chronos.Models.Historique;

/// <summary>
/// HIS-01 / HIS-06 — ce que la fenêtre Historique REÇOIT, prêt à dessiner, pour une plage déjà calculée par
/// <c>BornesPlage</c>. Records IMMUABLES : les pistes ne voient une nouvelle référence que lorsqu'une lecture a été
/// appliquée (jamais au tick), c'est la garantie « jamais de redessin sur le tick » de HIS-07 côté données.
///
/// <para><b>Doctrine</b> : deux séries de NATURE différente, jamais fusionnées. <see cref="DonneesSemaine.Analyse"/> porte des
/// fractions EXACTES du serveur (relevés, trous, resets observés, Δ, sauts non localisés) ; <see cref="DonneesSemaine.Barres"/> /
/// <see cref="DonneesJour.Colonnes"/> portent des ENTIERS comptés localement dans les transcripts de Claude Code (partiels : hors
/// Cowork, hors claude.ai ; bruts, non pondérés). Aucun rapport entre les deux n'est calculé ici ni ailleurs : les seuls
/// croisements permis sont la <see cref="Divergence"/> (une marche de % SANS tokens Code, couverture prouvée — D-34-07) et
/// l'alignement sur le même axe du temps. Types NEUTRES (aucun WPF), hors du namespace <c>.Tokens</c> parce qu'ils portent des
/// <c>double</c> de l'analyse (Pitfall 7 de la recherche 34), sous <c>ServicesLayerPurityTests</c>.</para>
/// </summary>
public enum VueHistorique
{
    /// <summary>Un jour local, minuit → minuit (23 h / 24 h / 25 h), grain 5 min.</summary>
    Jour,

    /// <summary>La semaine de forfait, samedi 00:00 local → samedi 00:00 local (167 h / 168 h / 169 h).</summary>
    Semaine,

    /// <summary>Quatre semaines de forfait S-3 … S (phase 35 : vue active — données et mots en 35-01, XAML en 35-04).</summary>
    QuatreSemaines,
}

/// <summary>
/// D-34-07 — une marche du % hebdo sans tranche de tokens Claude Code sur des heures COUVERTES : « consommé ailleurs
/// (Cowork, claude.ai) ». Heures adjacentes fusionnées. Produite par <c>Divergences.Detecter</c>, jamais par une piste.
/// </summary>
/// <param name="Debut">Début (UTC) de la première heure retenue.</param>
/// <param name="Fin">Fin (UTC, exclue) de la dernière heure retenue.</param>
/// <param name="Delta">Somme des Δ hebdo POSITIFS des heures fusionnées (fraction 0..1).</param>
public sealed record Divergence(DateTimeOffset Debut, DateTimeOffset Fin, double Delta);

/// <summary>Une graduation d'axe : un instant du calendrier LOCAL (minuit, heure ronde) et son libellé fr-FR déjà rédigé.</summary>
/// <param name="Instant">L'instant UTC de la graduation.</param>
/// <param name="Texte">Le libellé produit par <c>TextesHistorique</c> (« sam. 19 », « 3 h »).</param>
public sealed record GraduationLibellee(DateTimeOffset Instant, string Texte);

/// <summary>Nature d'une annotation d'honnêteté posée au-dessus des pistes (DESIGN_PLAN §2.2 / §2.3).</summary>
public enum TypeAnnotation
{
    /// <summary>Absence de relevés de plus de deux cadences, avec sa cause.</summary>
    Trou,

    /// <summary>Saut non localisé de part et d'autre d'un trou fermé : « +N % pendant l'absence (répartition inconnue) ».</summary>
    Saut,

    /// <summary>Marche de % sans tokens Code sur des heures couvertes : cadre « consommé ailleurs ».</summary>
    Divergence,

    /// <summary>« journal ouvert le … » : avant, rien — jamais une courbe reconstituée.</summary>
    JournalOuvert,

    /// <summary>Reset 5 h OBSERVÉ (vue Jour) : « reset 5 h HH:MM ».</summary>
    Reset5h,

    /// <summary>Plateau « épuisée à 100 % — le serveur refuse (statut rejected) » (vue Jour).</summary>
    Epuisee,
}

/// <summary>
/// Une annotation d'honnêteté, texte DÉJÀ rédigé par <c>TextesHistorique</c> : la vue la pose, elle n'invente aucun mot.
/// </summary>
/// <param name="Type">Nature de l'annotation.</param>
/// <param name="Debut">Instant de début (UTC).</param>
/// <param name="Fin">Instant de fin (UTC) ; <c>null</c> = ponctuelle (reset, ouverture du journal).</param>
/// <param name="Texte">Le libellé visible, mot pour mot.</param>
/// <param name="Cause">Pour un trou : sa cause (choix de la couleur de bordure par la vue).</param>
public sealed record AnnotationHistorique(TypeAnnotation Type, DateTimeOffset Debut, DateTimeOffset? Fin, string Texte, CauseTrou? Cause = null);

/// <summary>
/// La vue Semaine, prête à dessiner : la semaine de forfait et la précédente (fantôme gris), les barres de tokens par heure,
/// la couverture des transcripts, les divergences détectées, l'ouverture du journal et l'instant de lecture.
/// </summary>
/// <param name="Plage">La semaine de forfait affichée.</param>
/// <param name="Analyse">Ce que les relevés de la semaine disent (fractions exactes).</param>
/// <param name="PlagePrecedente">La semaine de forfait précédente (peut durer 168 h quand la courante en dure 169).</param>
/// <param name="Precedente">L'analyse de la semaine précédente (série vide si antérieure au journal).</param>
/// <param name="Barres">Une barre par début d'heure UTC de la plage (entiers comptés localement, avec leur état de couverture).</param>
/// <param name="CouvertureTokens">Sous-plages contiguës de couverture des transcripts.</param>
/// <param name="Divergences">Marches de % sans tokens Code sur des heures couvertes.</param>
/// <param name="JournalOuvertLe">Première ligne valide du journal ; <c>null</c> = journal vide ou absent.</param>
/// <param name="LueA">L'instant (horloge injectée) de la lecture : « maintenant » de l'analyse.</param>
/// <param name="LectureIncomplete">42.2-05 — une partie du journal n'a pas pu être lue (fichier verrouillé, dossier inaccessible) :
/// la vue le DIT au lieu de présenter des relevés manquants comme une absence de relevés.</param>
public sealed record DonneesSemaine(
    Plage Plage,
    AnalyseJournal Analyse,
    Plage PlagePrecedente,
    AnalyseJournal Precedente,
    IReadOnlyList<BarreHeure> Barres,
    IReadOnlyList<SousPlageCouverture> CouvertureTokens,
    IReadOnlyList<Divergence> Divergences,
    DateTimeOffset? JournalOuvertLe,
    DateTimeOffset LueA,
    bool LectureIncomplete = false);

/// <summary>
/// La vue Jour, prête à dessiner : le jour local, ses relevés au grain de 5 min, les colonnes de tokens par quart d'heure
/// empilées par modèle, la couverture, l'ouverture du journal et l'instant de lecture.
/// </summary>
/// <param name="Plage">Le jour local affiché (23 h / 24 h / 25 h).</param>
/// <param name="Analyse">Ce que les relevés du jour disent.</param>
/// <param name="Colonnes">Une colonne par tranche de 15 min UTC de la plage (96 ; 100 le 25/10 ; 92 le 28/03).</param>
/// <param name="CouvertureTokens">Sous-plages contiguës de couverture des transcripts.</param>
/// <param name="JournalOuvertLe">Première ligne valide du journal ; <c>null</c> = journal vide ou absent.</param>
/// <param name="LueA">L'instant (horloge injectée) de la lecture.</param>
/// <param name="LectureIncomplete">42.2-05 — une partie du journal n'a pas pu être lue (fichier verrouillé, dossier inaccessible) :
/// la vue le DIT au lieu de présenter des relevés manquants comme une absence de relevés.</param>
public sealed record DonneesJour(
    Plage Plage,
    AnalyseJournal Analyse,
    IReadOnlyList<ColonneQuartDHeure> Colonnes,
    IReadOnlyList<SousPlageCouverture> CouvertureTokens,
    DateTimeOffset? JournalOuvertLe,
    DateTimeOffset LueA,
    bool LectureIncomplete = false);

/// <summary>
/// HIS-05 — la vue 4 semaines, prête à dessiner (D-35-01 : UNE lecture du journal, quatre analyses). Aucune piste tokens en v1.8 :
/// seuls les relevés du serveur sont superposés, semaine par semaine, sur l'axe de la semaine courante.
/// </summary>
/// <param name="Semaines">Les quatre analyses en ordre CHRONOLOGIQUE : <c>[0]</c> = S-3, <c>[1]</c> = S-2, <c>[2]</c> = S-1,
/// <c>[3]</c> = S (celui de <c>BornesPlage.QuatreSemaines</c>). Chaque analyse porte SA plage et a été faite à
/// <c>InstantDAnalyse(now, sa plage)</c> : une semaine révolue ne finit jamais par un faux trou ouvert.</param>
/// <param name="JournalOuvertLe">Première ligne valide du journal ; <c>null</c> = journal vide ou absent (tout est « avant le journal »).</param>
/// <param name="LueA">L'instant (horloge injectée) de la lecture.</param>
/// <param name="LectureIncomplete">42.2-05 — une partie du journal n'a pas pu être lue (fichier verrouillé, dossier inaccessible) :
/// la vue le DIT au lieu de présenter des relevés manquants comme une absence de relevés.</param>
public sealed record DonneesQuatreSemaines(IReadOnlyList<AnalyseJournal> Semaines, DateTimeOffset? JournalOuvertLe, DateTimeOffset LueA, bool LectureIncomplete = false)
{
    /// <summary>La semaine courante S (la dernière de l'ordre chronologique).</summary>
    public AnalyseJournal Courante => Semaines[^1];
}

/// <summary>
/// L'étiquette d'une semaine, à droite des courbes (DESIGN_PLAN §2.4) : « S · 19 sept. · 43 % », « S-2 · 5 sept. · pas de relevés
/// (avant le journal) » ou « S-1 · 12 sept. · pas de relevés » — texte DÉJÀ rédigé par <c>TextesHistorique</c>.
/// </summary>
/// <param name="Rang">0 = S, 1 = S-1, 2 = S-2, 3 = S-3.</param>
/// <param name="Texte">Le libellé visible, mot pour mot.</param>
/// <param name="AvantJournal">La semaine se termine avant (ou à) l'ouverture du journal, ou le journal est absent (D-35-03).</param>
public sealed record EtiquetteSemaine(int Rang, string Texte, bool AvantJournal);

/// <summary>
/// Une rangée de la bande COUVERTURE PAR SEMAINE (DESIGN_PLAN §2.4) : relevés présents, trous avec leur cause, partie « avant le
/// journal » (hachurée) et marqueur « journal ouvert le … » sur la semaine d'ouverture. Tout est déjà calculé : la piste pose.
/// </summary>
/// <param name="Rang">0 = S, 1 = S-1, 2 = S-2, 3 = S-3.</param>
/// <param name="Libelle">« S », « S-1 », … (mot de <c>TextesHistorique.RangSemaine</c>).</param>
/// <param name="Plage">La semaine de forfait de la rangée (son PROPRE axe : 167 / 168 / 169 h).</param>
/// <param name="Serie">Les relevés de la semaine (source retenue par l'analyse).</param>
/// <param name="Trous">Les trous de la semaine, avec leur cause.</param>
/// <param name="InstantLecture">L'instant d'analyse de la semaine : <c>min(LueA, Plage.Fin)</c> (ferme un trou encore ouvert).</param>
/// <param name="ZoneAvantJournal">La partie de la plage ANTÉRIEURE à l'ouverture du journal (toute la plage si la semaine est
/// avant le journal) ; <c>null</c> si la semaine commence après l'ouverture.</param>
/// <param name="Texte">« avant le journal — aucun relevé », « aucun relevé » ou vide.</param>
/// <param name="JournalOuvert">« journal ouvert le … » si l'ouverture tombe dans la plage ; sinon <c>null</c>.</param>
public sealed record RangeeCouverture(int Rang, string Libelle, Plage Plage, IReadOnlyList<ReleveJournal> Serie, IReadOnlyList<Trou> Trous,
    DateTimeOffset InstantLecture, Plage? ZoneAvantJournal, string Texte, AnnotationHistorique? JournalOuvert);
