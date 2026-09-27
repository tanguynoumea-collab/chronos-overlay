using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;

namespace Chronos.ViewModels.Historique;

/// <summary>
/// D-34-16 — la semaine de RÉFÉRENCE des maquettes (DESIGN_PLAN §8), fabriquée EN MÉMOIRE : aucun fichier, aucun <c>Random</c>,
/// aucune horloge système. La galerie <c>--historique</c> (34-05) et les tests d'honnêteté (34-08) consomment les MÊMES objets :
/// ce que l'utilisateur voit est ce que les tests prouvent.
///
/// <para><b>Les faits du scénario</b> (heure de Paris ; les constantes UTC sont figées, le fuseau injecté ne sert qu'au calendrier
/// du profil d'activité) : <c>now</c> = jeu. 24 sept. 2026 17:12 ; journal ouvert le lun. 14 sept. 12:00 ; relevés toutes les
/// 5 min (sonde d'en-têtes) ; trou « Chronos arrêté » mar. 22 sept. 23:00 → mer. 23 sept. 07:00 (<c>arret</c> puis <c>demarrage</c>) ;
/// trou « jeton invalide » jeu. 24 sept. 14:00 → 16:00 (<c>jeton_invalide</c> puis <c>reprise</c>) ; grille 5 h ancrée sur le début
/// de la semaine (sam. 19 sept. 00:00) ; fenêtre 5 h ÉPUISÉE mer. 23 sept. 20:00 → jeu. 00:00 (<c>u5 = 1,0</c>, statut
/// <c>rejected</c> jusqu'au reset) ; marche hebdo +0,04 mer. 21:00 → 23:00 SANS un token Code (divergence « consommé ailleurs ») ;
/// saut hebdo +0,02 pendant l'absence de la nuit ; tokens Claude Code (opus / sonnet / haiku + sous-agents opus) par quart d'heure
/// aux heures actives, y compris pendant les deux trous (Claude Code tourne même quand Chronos se tait), sauf pendant le plateau
/// épuisé ; couverture : plus ancienne ligne vue le 23 juin, passe complète garantie jusqu'à <c>now</c>.</para>
/// </summary>
public static class ScenariosHistorique
{
    /// <summary>Le prochain reset hebdo tel que le serveur l'annonce toute la semaine courante : sam. 26 sept. 00:00 Paris.</summary>
    public static readonly DateTimeOffset RepereHebdo = new(2026, 9, 25, 22, 0, 0, TimeSpan.Zero);

    /// <summary>Le reset hebdo annoncé pendant la semaine précédente : sam. 19 sept. 00:00 Paris (= début de la semaine courante).</summary>
    public static readonly DateTimeOffset RepereHebdoPrecedent = new(2026, 9, 18, 22, 0, 0, TimeSpan.Zero);

    /// <summary>La première ligne du journal : lun. 14 sept. 2026 12:00 Paris.</summary>
    public static readonly DateTimeOffset JournalOuvertLe = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    // « Maintenant » en UTC (17:12 Paris) : le point fixe des faits UTC ci-dessous.
    private static readonly DateTimeOffset MaintenantUtc = new(2026, 9, 24, 15, 12, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PlusAncienneLigneVue = new(2026, 6, 23, 12, 44, 22, TimeSpan.Zero);

    // Trou A « Chronos arrêté » : mar. 23:00 → mer. 07:00 local. Trou B « jeton invalide » : jeu. 14:00 → 16:00 local.
    private static readonly DateTimeOffset TrouADebut = new(2026, 9, 22, 21, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TrouAFin = new(2026, 9, 23, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TrouBDebut = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TrouBFin = new(2026, 9, 24, 14, 0, 0, TimeSpan.Zero);

    // Plateau épuisé : mer. 20:00 → jeu. 00:00 local (le reset 5 h de la grille). Divergence : mer. 21:00 → 23:00 local.
    private static readonly DateTimeOffset PlateauDebut = new(2026, 9, 23, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PlateauFin = new(2026, 9, 23, 22, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DivergenceDebut = new(2026, 9, 23, 19, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DivergenceFin = new(2026, 9, 23, 21, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Pas = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CinqHeures = TimeSpan.FromHours(5);

    // Les séries complètes (indépendantes de la plage), calculées une fois par fuseau.
    private static readonly Dictionary<string, Series> Cache = new();
    private static readonly object Verrou = new();

    private sealed record Series(IReadOnlyList<ReleveJournal> Releves, IReadOnlyList<EvenementJournal> Evenements, IReadOnlyList<TrancheTokens> Tranches);

    /// <summary>jeu. 24 sept. 2026 17:12:00 local dans <paramref name="tz"/> (15:12Z à Paris).</summary>
    public static DateTimeOffset Maintenant(TimeZoneInfo tz)
    {
        ArgumentNullException.ThrowIfNull(tz);
        var local = new DateTime(2026, 9, 24, 17, 12, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, tz.GetUtcOffset(local));
    }

    /// <summary>La semaine de forfait de référence : sam. 19 sept. 00:00 → sam. 26 sept. 00:00 local.</summary>
    public static Plage Semaine(TimeZoneInfo tz) => BornesPlage.SemaineDeForfait(Maintenant(tz), RepereHebdo, null, tz);

    /// <summary>La lecture du journal sur <c>[Debut, Fin[</c> : relevés et événements filtrés, <see cref="JournalOuvertLe"/>, la plage.</summary>
    public static LectureJournal Journal(Plage plage, TimeZoneInfo tz)
    {
        ArgumentNullException.ThrowIfNull(plage);
        var s = Obtenir(tz);
        return new LectureJournal(
            s.Releves.Where(r => plage.Contient(r.T)).ToList(),
            s.Evenements.Where(e => plage.Contient(e.T)).ToList(),
            0, JournalOuvertLe, plage);
    }

    /// <summary>La lecture des agrégats sur <c>[Debut, Fin[</c> : tranches filtrées, couverture découpée par <see cref="RenduLocalTokens.SousPlagesCouverture"/>.</summary>
    public static LectureAgregats Agregats(Plage plage, TimeZoneInfo tz)
    {
        ArgumentNullException.ThrowIfNull(plage);
        var s = Obtenir(tz);
        return new LectureAgregats(
            s.Tranches.Where(t => plage.Contient(t.Slot)).ToList(),
            0, plage,
            RenduLocalTokens.SousPlagesCouverture(plage, Couverture()),
            PlusAncienneLigneVue);
    }

    /// <summary>La couverture que la vraie reconstruction produirait : plus ancienne ligne vue le 23 juin 2026, passe complète garantie
    /// jusqu'à <c>now</c> (exclu) — donc « transcripts absents » pour les heures à venir, jamais « hors couverture ».</summary>
    public static CouvertureTokens Couverture()
    {
        var c = new CouvertureTokens();
        c.VoirLigne(PlusAncienneLigneVue);
        c.GarantirPasse(MaintenantUtc, MaintenantUtc);
        return c;
    }

    /// <summary>Une horloge figée à <see cref="Maintenant"/>.</summary>
    public static IClock HorlogeFigee(TimeZoneInfo tz) => new HorlogeFigeeInterne(Maintenant(tz));

    /// <summary>La reconstruction des tokens EN COURS : 886 / 1603 fichiers, semaine courante déjà complète ; <c>Changement</c> jamais levé.</summary>
    public static IEtatReconstruction ReconstructionEnCours() => new ReconstructionFigee();

    // --- Génération déterministe ---

    // Profil d'activité par heure LOCALE : nuit 0 ; 7–8 h 0,5 ; 9–11 h 1 ; 12–13 h 0,5 ; 14–18 h 1 ; 19–22 h 0,5 ; 23 h 0,2.
    private static double Activite(int heureLocale) => heureLocale switch
    {
        >= 0 and <= 6 => 0,
        7 or 8 => 0.5,
        >= 9 and <= 11 => 1,
        12 or 13 => 0.5,
        >= 14 and <= 18 => 1,
        >= 19 and <= 22 => 0.5,
        _ => 0.2,
    };

    private static bool DansUnTrou(DateTimeOffset t)
        => (TrouADebut < t && t < TrouAFin) || (TrouBDebut < t && t < TrouBFin);

    private static Series Obtenir(TimeZoneInfo tz)
    {
        ArgumentNullException.ThrowIfNull(tz);
        lock (Verrou)
        {
            if (!Cache.TryGetValue(tz.Id, out var s)) Cache[tz.Id] = s = Generer(tz);
            return s;
        }
    }

    private static Series Generer(TimeZoneInfo tz)
    {
        var releves = new List<ReleveJournal>();
        var cumul = 0.0;                    // consommation hebdo cumulée depuis le dernier reset hebdo
        var u5 = 0.0;
        DateTimeOffset? fenetreCourante = null;
        var absentAvant = false;

        for (var t = JournalOuvertLe; t <= MaintenantUtc; t += Pas)
        {
            var a = Activite(TimeZoneInfo.ConvertTime(t, tz).Hour);

            // Grille 5 h ancrée sur le début de la semaine courante, prolongée vers l'arrière : u5 repart de 0 à chaque fenêtre et
            // croît même quand Chronos se tait (le serveur compte, lui).
            var debutFenetre = DebutFenetre5h(t);
            if (fenetreCourante != debutFenetre) { fenetreCourante = debutFenetre; u5 = 0; }
            u5 = Math.Min(0.99, u5 + 0.01 * a);

            if (DansUnTrou(t)) { absentAvant = true; continue; }

            var plateau = PlateauDebut <= t && t < PlateauFin;
            var semaineCourante = t >= RepereHebdoPrecedent;
            if (t == RepereHebdoPrecedent) cumul = 0;   // reset hebdo observé à la borne

            // Le relevé qui FERME un trou porte le même cumul que celui qui l'ouvre (le saut est dit à part) ; pendant le plateau
            // épuisé rien ne se consomme côté Code ; sinon le cumul avance avec l'activité.
            var reprise = absentAvant;
            absentAvant = false;
            if (!reprise && !plateau) cumul += 0.0008 * a;
            if (reprise && t == TrouAFin) cumul += 0.02;                                         // « +2 % pendant l'absence »
            if (DivergenceDebut <= t && t < DivergenceFin && t.Minute % 30 == 0) cumul += 0.01;   // marche sans tokens Code

            var baseHebdo = semaineCourante ? 0.02 : 0.0;
            var u7 = Math.Round(baseHebdo + cumul, 2, MidpointRounding.AwayFromZero);

            releves.Add(new ReleveJournal(
                t, SourceUsage.SondeEnTetes,
                plateau ? 1.0 : Math.Round(u5, 4),
                debutFenetre + CinqHeures,
                plateau ? StatutServeur.Rejete : StatutServeur.Autorise,
                u7,
                semaineCourante ? RepereHebdo : RepereHebdoPrecedent,
                StatutServeur.Autorise,
                null, null));
        }

        var evenements = new List<EvenementJournal>
        {
            new(JournalOuvertLe, TypeEvenement.Demarrage, Version: "3.2.2"),
            new(TrouADebut.AddSeconds(30), TypeEvenement.Arret),
            new(TrouAFin.AddSeconds(-10), TypeEvenement.Demarrage, Version: "3.2.2"),
            new(TrouBDebut.AddSeconds(30), TypeEvenement.JetonInvalide),
            new(TrouBFin, TypeEvenement.Reprise, Cause: "trou de 2 h"),
        };

        // Tokens Claude Code par quart d'heure aux heures actives — y compris pendant les deux trous —, sauf pendant le plateau épuisé.
        var tranches = new List<TrancheTokens>();
        for (var slot = JournalOuvertLe; slot < MaintenantUtc; slot += TrancheTokens.Tranche)
        {
            var a = Activite(TimeZoneInfo.ConvertTime(slot, tz).Hour);
            if (a <= 0 || (PlateauDebut <= slot && slot < PlateauFin)) continue;
            tranches.Add(new TrancheTokens(slot, "claude-opus-5", false, 1200, (long)(900 * a), 300, 4000, 6));
            tranches.Add(new TrancheTokens(slot, "claude-sonnet-4-5", false, 600, (long)(450 * a), 100, 1500, 4));
            tranches.Add(new TrancheTokens(slot, "claude-haiku-4-5", false, 200, (long)(150 * a), 0, 300, 2));
            tranches.Add(new TrancheTokens(slot, "claude-opus-5", true, 400, (long)(300 * a), 0, 800, 3));
        }

        return new Series(releves, evenements, tranches);
    }

    // Début de la fenêtre 5 h qui contient t : Ancre + 5k h, k entier (négatif avant l'ancre).
    private static DateTimeOffset DebutFenetre5h(DateTimeOffset t)
    {
        var ticks = (t - RepereHebdoPrecedent).Ticks;
        var f = CinqHeures.Ticks;
        var k = ticks >= 0 ? ticks / f : -((-ticks + f - 1) / f);
        return RepereHebdoPrecedent + TimeSpan.FromTicks(k * f);
    }

    private sealed class HorlogeFigeeInterne(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class ReconstructionFigee : IEtatReconstruction
    {
        public PhaseReconstruction Phase => PhaseReconstruction.Reconstruction;
        public int FichiersTraites => 886;
        public int FichiersTotal => 1603;
        public int FichiersOuvertsDernierePasse => 886;
        public bool SemaineCouranteDisponible => true;
        public string? DernierFichier => "PROJET OVERLAY/2026-09-19-refonte.jsonl";
        public string? DerniereErreur => null;
        public int FichiersDisparus => 0;
        public int LignesIgnorees => 0;
        public int IdsConnus => 41_200;
        public TimeSpan? DureeMurDernierePasse => null;
        public TimeSpan? DureeCpuProcessusDernierePasse => null;
        public DateTimeOffset? DerniereReconstructionTerminee => null;

        // Jamais levé : la reconstruction figée ne bouge pas (accesseurs explicites pour ne pas déclencher CS0067).
        public event EventHandler? Changement { add { } remove { } }
    }
}
