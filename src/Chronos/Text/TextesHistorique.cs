using System.Globalization;
using Chronos.Models.Historique;
using Chronos.Services;

namespace Chronos.Text;

/// <summary>
/// HIS-06 / HIS-08 — le producteur UNIQUE des mots de la fenêtre Historique (DESIGN_PLAN §4 : mêmes mots partout — fenêtre,
/// réglages, diagnostic, docs). Culture <b>fr-FR EXPLICITE</b> (<see cref="Fr"/>) pour tout libellé visible : la machine peut être
/// réglée en anglais, « sam. 19 sept. » reste « sam. 19 sept. » ; les clés d'heure (« HH:mm ») sont invariantes.
///
/// <para>Doctrine des mots : « relevé » (jamais « mesure ») ; aucun mot qui annonce l'avenir — la fenêtre montre ce qui a été
/// relevé, rien de ce qui pourrait l'être. Les causes des trous viennent de <see cref="CauseTrouTexte"/>, l'ancienneté et le nom
/// des sources de <see cref="LibelleSource"/> : rien n'est recopié. Pur, sans E/S, sans horloge : <c>now</c> et le fuseau sont
/// toujours des paramètres, donc chaque chaîne est déterministe en test.</para>
/// </summary>
public static class TextesHistorique
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    // --- Vocabulaire fixe (§4, §2.1, §2.2, §2.3) ---
    public const string Titre = "Historique";
    public const string SegmentJour = "Jour";
    public const string SegmentSemaine = "Semaine";
    public const string SegmentQuatreSemaines = "4 semaines";
    public const string CetteSemaine = "Cette semaine";
    public const string Aujourdhui = "Aujourd'hui";
    public const string SemaineDeForfait = "Semaine de forfait";

    // Bouton plein écran de l'en-tête (DESIGN_PLAN_CYCLE2 § 4.2) : le libellé change selon l'état.
    public const string BoutonPleinEcran = "⛶ Plein écran";
    public const string BoutonQuitterPleinEcran = "⤢ Quitter le plein écran · Échap";

    public const string PisteNiveau = "NIVEAU";
    public const string PisteRythme = "RYTHME";
    public const string PisteTokens = "TOKENS CLAUDE CODE";
    public const string PisteCouverture = "COUVERTURE";

    public const string ResetHebdo = "reset hebdo →";
    /// <summary>Repères de l'axe NIVEAU (grille 0 / 50 / 100 %, DESIGN_PLAN §2.2 ; D-34-29) : haut et bas de la piste.</summary>
    public const string RepereCent = "100 %";
    public const string RepereZero = "0";
    public const string EchelleRythme = "0 – 25 %";
    public const string LegendeTokens = "▮ principal ▮ sous-agents";
    public const string Epuisee = "épuisée à 100 % — le serveur refuse (statut rejected)";
    public const string PiedDePage = "Aucun trou n'est interpolé. Les pourcentages sont des relevés exacts du serveur ; les tokens sont un comptage local partiel, sur leur propre axe.";
    public const string PiedDivergence = "cadre violet : marches de % sans tokens Code = consommé ailleurs (Cowork, claude.ai)";
    public const string AucunReleveSemaine = "aucun relevé sur cette semaine de forfait";
    public const string AucunReleveJour = "aucun relevé sur ce jour";
    /// <summary>42.2-05 — une partie du journal n'a pas pu être lue : préfixe d'une ligne de fraîcheur PARTIELLE.</summary>
    public const string LectureIncomplete = "lecture incomplète : un fichier du journal était inaccessible";
    /// <summary>42.2-05 — rien n'a pu être lu : remplace un faux « aucun relevé » (verrouillé n'est pas vide).</summary>
    public const string LectureImpossible = "journal momentanément inaccessible — relevés non lus, ce n'est pas une absence de relevés";
    public const string GrainHeure = "heure";
    public const string GrainQuartDHeure = "quart d'heure";
    public const string HistoriqueIndisponible = "historique indisponible";
    public const string Tiret = "—";

    // --- Vue 4 semaines (§2.4, 35-01) ---
    public const string PiedQuatreSemaines = "Rien n'est inventé avant l'ouverture du journal.";
    public const string AvantJournalAucunReleve = "avant le journal — aucun relevé";
    public const string AucunReleve = "aucun relevé";
    public const string CouvertureParSemaine = "COUVERTURE PAR SEMAINE";

    private const string SuffixePermanentTokens = ", comptés localement — hors Cowork et claude.ai · bruts, non pondérés · ce n'est PAS un % du forfait";
    private const string RepartitionInconnue = " pendant l'absence (répartition inconnue)";
    private const string SousAgentsInclus = "sous-agents inclus";

    /// <summary>« par heure, … » en Semaine, « par quart d'heure, … » en Jour (D-34-14) : le libellé porte le GRAIN de la piste.</summary>
    public static string LibellePermanentTokens(string grain) => "par " + grain + SuffixePermanentTokens;

    // --- Périodes et calendrier (fr-FR explicite) ---

    /// <summary>« Semaine de forfait · sam. 19 sept. 00:00 → sam. 26 sept. 00:00 » (bornes locales).</summary>
    public static string LibellePeriodeSemaine(Plage semaine, TimeZoneInfo tz)
        => SemaineDeForfait + " · " + Borne(semaine.Debut, tz) + " → " + Borne(semaine.Fin, tz);

    /// <summary>« Jour · jeudi 24 sept. 2026 · semaine de forfait du 19 sept. ».</summary>
    public static string LibellePeriodeJour(Plage jour, Plage semaine, TimeZoneInfo tz)
        => SegmentJour + " · " + Local(jour.Debut, tz).ToString("dddd d MMM yyyy", Fr)
           + " · semaine de forfait du " + Local(semaine.Debut, tz).ToString("d MMM", Fr);

    /// <summary>« sam. 19 » — libellé d'un minuit local sur l'axe de la semaine.</summary>
    public static string LibelleJour(DateTimeOffset minuitLocal, TimeZoneInfo tz) => Local(minuitLocal, tz).ToString("ddd d", Fr);

    /// <summary>« sam. » — le jour de la semaine seul (axe de la vue 4 semaines : il sert quatre semaines, pas de quantième).</summary>
    public static string LibelleJourCourt(DateTimeOffset t, TimeZoneInfo tz) => Local(t, tz).ToString("ddd", Fr);

    /// <summary>« 4 semaines de forfait · du sam. 29 août au sam. 26 sept. 2026 » (bornes locales du bloc S-3 … S).</summary>
    public static string LibellePeriodeQuatreSemaines(Plage bloc, TimeZoneInfo tz)
        => "4 semaines de forfait · du " + Local(bloc.Debut, tz).ToString("ddd d MMM", Fr)
           + " au " + Local(bloc.Fin, tz).ToString("ddd d MMM yyyy", Fr);

    /// <summary>« S » (rang 0), « S-1 », « S-2 », « S-3 ».</summary>
    public static string RangSemaine(int rang) => rang == 0 ? "S" : "S-" + rang.ToString(CultureInfo.InvariantCulture);

    /// <summary>L'étiquette d'une semaine (D-35-03) : « S · 19 sept. · 43 % » ; « S-2 · 5 sept. · pas de relevés (avant le journal) » ;
    /// « S-1 · 12 sept. · pas de relevés » — une semaine POSTÉRIEURE à l'ouverture du journal sans relevé n'est jamais « avant le journal ».</summary>
    public static string EtiquetteSemaine(int rang, Plage semaine, double? valeurFinale, bool avantJournal, bool sansReleve, TimeZoneInfo tz)
        => RangSemaine(rang) + " · " + Local(semaine.Debut, tz).ToString("d MMM", Fr) + " · "
           + (avantJournal ? "pas de relevés (avant le journal)" : sansReleve ? "pas de relevés" : Pourcent(valeurFinale));

    /// <summary>« épuisée jeu. 20:00 → bloquée jusqu'au reset » — le PREMIER relevé à 100 % (ou refusé) d'une semaine, daté.</summary>
    public static string EpuiseeSemaine(DateTimeOffset t, TimeZoneInfo tz)
        => "épuisée " + LibelleJourCourt(t, tz) + " " + HeureMinute(t, tz) + " → bloquée jusqu'au reset";

    /// <summary>« 3 h », « 0 h », « 21 h » — libellé d'une heure locale ronde sur l'axe du jour.</summary>
    public static string LibelleHeure(DateTimeOffset t, TimeZoneInfo tz) => Local(t, tz).Hour.ToString(CultureInfo.InvariantCulture) + " h";

    /// <summary>« 14:00 » — heure locale, clé invariante.</summary>
    public static string HeureMinute(DateTimeOffset t, TimeZoneInfo tz) => Local(t, tz).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>« 14 sept. 2026 ».</summary>
    public static string DateLongue(DateTimeOffset t, TimeZoneInfo tz) => Local(t, tz).ToString("d MMM yyyy", Fr);

    /// <summary>« 43 % » (arrondi à l'entier, demi vers le haut) ; <c>null</c> → « — ».</summary>
    public static string Pourcent(double? u)
        => u is { } v ? Math.Round(v * 100, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + " %" : Tiret;

    // --- Ligne de fraîcheur (§2.1, §2.3) ---

    /// <summary>« Dernier relevé il y a 2 min · sonde d'en-têtes de rate-limit · 1502 relevés · 2 interruptions · journal ouvert le 14 sept. 2026 » ;
    /// série vide → <see cref="AucunReleveSemaine"/>.</summary>
    public static string LigneFraicheurSemaine(AnalyseJournal a, DateTimeOffset now, TimeZoneInfo tz)
    {
        if (a.Serie.Count == 0) return AucunReleveSemaine;
        var texte = "Dernier relevé " + LibelleSource.Anciennete(a.Serie[^1].T, now)
                    + " · " + LibelleSource.Format(a.Source)
                    + " · " + Pluriel(a.Serie.Count, "relevé")
                    + " · " + Pluriel(a.Trous.Count, "interruption");
        if (a.JournalOuvertLe is { } j) texte += " · " + JournalOuvertLe(j, tz);
        return texte;
    }

    /// <summary>
    /// 42.2-05 (TEST-4, DATA-13) — habille une ligne de fraîcheur de l'état de la lecture. Lecture complète → la ligne, intacte.
    /// Lecture incomplète : un « aucun relevé » (semaine ou jour) devient <see cref="LectureImpossible"/> — on ne présente pas une
    /// ignorance comme un fait — et toute autre ligne est préfixée par <see cref="LectureIncomplete"/> (ses comptes sont partiels).
    /// </summary>
    public static string AvecEtatLecture(string ligne, bool lectureIncomplete)
    {
        if (!lectureIncomplete) return ligne;
        if (ligne is AucunReleveSemaine or AucunReleveJour || string.IsNullOrEmpty(ligne)) return LectureImpossible;
        return LectureIncomplete + " · " + ligne;
    }

    /// <summary>« 288 relevés attendus · 262 présents · 1 interruption (jeton invalide, 14:00 → 16:00) » — les attendus viennent de la
    /// DURÉE de la plage (300 le 25/10, 276 le 28/03 : Pitfall 9) ; « présents » compte les seuls relevés DU JOUR (35-01 : un relevé de la
    /// veille peut accompagner l'analyse) ; une borne de trou antérieure au jour porte son jour (« mar. 23:00 → 07:00 ») ; aucun relevé
    /// du jour → <see cref="AucunReleveJour"/>.</summary>
    public static string LigneFraicheurJour(Plage jour, AnalyseJournal a, TimeSpan cadence, TimeZoneInfo tz)
    {
        var presents = a.Serie.Count(r => jour.Contient(r.T));
        if (presents == 0) return AucunReleveJour;
        var attendus = (int)(jour.Duree / cadence);
        var texte = attendus.ToString(CultureInfo.InvariantCulture) + " relevés attendus · "
                    + presents.ToString(CultureInfo.InvariantCulture) + " présents · "
                    + Pluriel(a.Trous.Count, "interruption");
        if (a.Trous.Count > 0)
            texte += " (" + string.Join(" ; ", a.Trous.Select(t => Trou(t) + ", " + BorneDuJour(t.Debut, jour, tz) + " → " + (t.Fin is { } f ? BorneDuJour(f, jour, tz) : "en cours"))) + ")";
        return texte;
    }

    // « 07:00 » dans le jour affiché ; « mar. 23:00 » pour une borne antérieure (la veille de minuit).
    private static string BorneDuJour(DateTimeOffset t, Plage jour, TimeZoneInfo tz)
        => (t < jour.Debut ? LibelleJourCourt(t, tz) + " " : "") + HeureMinute(t, tz);

    /// <summary>« journal muet depuis 16 min » (règle D-32-21, même mot que le diagnostic).</summary>
    public static string AlerteJournalMuet(TimeSpan age) => "journal muet depuis " + ((int)age.TotalMinutes).ToString(CultureInfo.InvariantCulture) + " min";

    // --- Annotations d'honnêteté (§2.2, §2.3) ---

    /// <summary>« +2 % pendant l'absence (répartition inconnue) » ; Δ indéterminable → « au moins un reset pendant l'absence (répartition
    /// inconnue) » ; négatif → signe moins typographique (U+2212).</summary>
    public static string Saut(SautNonLocalise s)
    {
        if (s.Delta is not { } delta) return "au moins un reset" + RepartitionInconnue;
        var n = (int)Math.Round(Math.Abs(delta) * 100, MidpointRounding.AwayFromZero);
        var signe = delta < 0 ? "−" : "+";
        return signe + n.ToString(CultureInfo.InvariantCulture) + " %" + RepartitionInconnue;
    }

    /// <summary>La cause d'un trou, mot pour mot : « Chronos arrêté », « jeton invalide », « sonde refusée », « cause inconnue ».</summary>
    public static string Trou(Chronos.Models.Historique.Trou t) => CauseTrouTexte.Libelle(t.Cause);

    /// <summary>« journal ouvert le 14 sept. 2026 ».</summary>
    public static string JournalOuvertLe(DateTimeOffset t, TimeZoneInfo tz) => "journal ouvert le " + DateLongue(t, tz);

    /// <summary>« reset 5 h 17:00 » — un reset OBSERVÉ, daté de l'ancienne borne.</summary>
    public static string Reset5h(DateTimeOffset instant, TimeZoneInfo tz) => "reset 5 h " + HeureMinute(instant, tz);

    // --- Piste tokens (§2.2, §2.3) ---

    /// <summary>« 0 – 1,2 M (sortie) », « 0 – 250 k (sortie) », « 0 – 800 (sortie) » — l'axe propre des tokens de SORTIE (D-34-06).</summary>
    public static string EchelleTokens(long maxArrondi)
    {
        if (maxArrondi < 0) maxArrondi = 0;
        string valeur;
        if (maxArrondi >= 1_000_000) valeur = Abrege(maxArrondi / 1_000_000d) + " M";
        else if (maxArrondi >= 1_000) valeur = Abrege(maxArrondi / 1_000d) + " k";
        else valeur = maxArrondi.ToString(CultureInfo.InvariantCulture);
        return "0 – " + valeur + " (sortie)";
    }

    /// <summary>« opus · sonnet · haiku · sous-agents inclus » — trois modèles au plus, dans l'ordre donné (sortie décroissante) ;
    /// aucun → « aucun modèle · sous-agents inclus ».</summary>
    public static string LegendeModeles(IEnumerable<string> modelesParOrdreDeSortie)
    {
        var noms = modelesParOrdreDeSortie.Select(NomCourt).Where(n => n.Length > 0).Take(3).ToList();
        return noms.Count == 0 ? "aucun modèle · " + SousAgentsInclus : string.Join(" · ", noms) + " · " + SousAgentsInclus;
    }

    // --- Réticule (§2.3) ---

    /// <summary>Quatre lignes : « 14:35 · relevé exact » / « 5 h : 43 % — reset à 17:00 » / « hebdo : 61 % » / « source · il y a 12 min ».
    /// Une valeur absente est « — » ; sans valeur 5 h, la ligne se réduit à « 5 h : — ».</summary>
    public static string Infobulle(ReleveJournal r, DateTimeOffset now, TimeZoneInfo tz)
    {
        var cinq = "5 h : " + Pourcent(r.U5);
        if (r.U5 is not null && r.R5 is { } r5) cinq += " — reset à " + HeureMinute(r5, tz);
        return HeureMinute(r.T, tz) + " · relevé exact\n"
               + cinq + "\n"
               + "hebdo : " + Pourcent(r.U7) + "\n"
               + LibelleSource.Format(r.Source) + " · " + LibelleSource.Anciennete(r.T, now);
    }

    // --- Bandeau F2 (§3) ---

    /// <summary>Le texte du bandeau F2 (DESIGN_PLAN §3) : « … — N / M fichiers », suivi de « · la semaine courante est déjà complète »
    /// dès que tous les transcripts de la semaine courante ont été lus.</summary>
    public static string BandeauF2(int traites, int total, bool semaineCouranteDisponible)
        => "Reconstruction des tokens depuis vos transcripts Claude Code — "
           + traites.ToString(CultureInfo.InvariantCulture) + " / " + total.ToString(CultureInfo.InvariantCulture) + " fichiers"
           + (semaineCouranteDisponible ? " · la semaine courante est déjà complète" : "");

    /// <summary>« en arrière-plan, priorité basse · du plus récent au plus ancien · les pourcentages du forfait ne se reconstruisent pas :
    /// ils commencent au 14 sept. 2026 » ; journal vide → « … au premier relevé du journal ».</summary>
    public static string SousTexteF2(DateTimeOffset? journalOuvertLe, TimeZoneInfo tz)
        => "en arrière-plan, priorité basse · du plus récent au plus ancien · les pourcentages du forfait ne se reconstruisent pas : ils commencent au "
           + (journalOuvertLe is { } j ? DateLongue(j, tz) : "premier relevé du journal");

    // --- Internes ---

    private static DateTimeOffset Local(DateTimeOffset t, TimeZoneInfo tz) => TimeZoneInfo.ConvertTime(t, tz);

    // « sam. 19 sept. 00:00 » : date fr-FR + heure invariante.
    private static string Borne(DateTimeOffset t, TimeZoneInfo tz)
        => Local(t, tz).ToString("ddd d MMM", Fr) + " " + HeureMinute(t, tz);

    // « 1 relevé », « 3 relevés », « 0 interruption ».
    private static string Pluriel(int n, string mot) => n.ToString(CultureInfo.InvariantCulture) + " " + mot + (n > 1 ? "s" : "");

    // 1 décimale max, virgule française, « ,0 » supprimé (même règle que TokenFormatter).
    private static string Abrege(double v) => v.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');

    // « claude-opus-5 » → « opus » : sans le préfixe « claude- », coupé au premier tiret.
    private static string NomCourt(string modele)
    {
        var m = modele.StartsWith("claude-", StringComparison.OrdinalIgnoreCase) ? modele["claude-".Length..] : modele;
        var tiret = m.IndexOf('-');
        return tiret >= 0 ? m[..tiret] : m;
    }
}
