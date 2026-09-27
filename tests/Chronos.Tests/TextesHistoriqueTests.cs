using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Text;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-06 / HIS-08 — <see cref="TextesHistorique"/> est le producteur UNIQUE des mots de la fenêtre Historique (DESIGN_PLAN §4 :
/// mêmes mots partout), en fr-FR EXPLICITE (jamais la culture de la machine). Chaque chaîne est testée AU CARACTÈRE PRÈS, sans
/// une ligne de XAML : libellés de période, ligne de fraîcheur, alerte « journal muet », saut non localisé, épuisée, pied de
/// page, bandeau F2 — et aucun mot qui annonce l'avenir. Tests en <c>[Fact]</c> pur, fuseau de Paris injecté.
/// </summary>
public class TextesHistoriqueTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();
    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static readonly Plage CetteSemaine = BornesPlage.SemaineDeForfait(Utc("2026-09-24T15:12:00Z"), Utc("2026-09-25T22:00:00Z"), null, Tz);

    private static ReleveJournal Releve(DateTimeOffset t, double? u5 = 0.43, double? u7 = 0.61, DateTimeOffset? r5 = null)
        => new(t, SourceUsage.SondeEnTetes, u5, r5 ?? Utc("2026-09-24T15:00:00Z"), StatutServeur.Autorise, u7, Utc("2026-09-25T22:00:00Z"), StatutServeur.Autorise, null, null);

    private static AnalyseJournal Analyse(IReadOnlyList<ReleveJournal> serie, IReadOnlyList<Trou> trous, DateTimeOffset? journalOuvertLe, Plage? plage = null)
        => new(serie.Count > 0 ? SourceUsage.SondeEnTetes : null, serie, trous, Array.Empty<ResetObserve>(), Array.Empty<ResetObserve>(),
               Array.Empty<DeltaConsommation>(), Array.Empty<DeltaConsommation>(), Array.Empty<SautNonLocalise>(), journalOuvertLe, plage ?? CetteSemaine);

    private static IReadOnlyList<ReleveJournal> Serie(int n, DateTimeOffset dernier)
        => Enumerable.Range(0, n).Select(i => Releve(dernier - TimeSpan.FromMinutes(5 * (n - 1 - i)))).ToList();

    private static readonly Trou TrouArrete = new(Utc("2026-09-22T21:00:00Z"), Utc("2026-09-23T05:00:00Z"), CauseTrou.ChronosArrete);
    private static readonly Trou TrouJeton = new(Utc("2026-09-24T12:00:00Z"), Utc("2026-09-24T14:00:00Z"), CauseTrou.JetonInvalide);

    [Fact]
    public void Les_constantes_du_vocabulaire_sont_celles_du_plan()
    {
        Assert.Equal("Historique", TextesHistorique.Titre);
        Assert.Equal("Jour", TextesHistorique.SegmentJour);
        Assert.Equal("Semaine", TextesHistorique.SegmentSemaine);
        Assert.Equal("4 semaines", TextesHistorique.SegmentQuatreSemaines);
        Assert.Equal("bientôt (phase 35)", TextesHistorique.InfobulleBientot);
        Assert.Equal("Cette semaine", TextesHistorique.CetteSemaine);
        Assert.Equal("Aujourd'hui", TextesHistorique.Aujourdhui);
        Assert.Equal("NIVEAU", TextesHistorique.PisteNiveau);
        Assert.Equal("RYTHME", TextesHistorique.PisteRythme);
        Assert.Equal("TOKENS CLAUDE CODE", TextesHistorique.PisteTokens);
        Assert.Equal("COUVERTURE", TextesHistorique.PisteCouverture);
        Assert.Equal("FENÊTRES 5 H", TextesHistorique.PisteFenetres5h);
        Assert.Equal("reset hebdo →", TextesHistorique.ResetHebdo);
        Assert.Equal("0 – 25 %", TextesHistorique.EchelleRythme);
        Assert.Equal("▮ principal ▮ sous-agents", TextesHistorique.LegendeTokens);
        Assert.Equal("épuisée à 100 % — le serveur refuse (statut rejected)", TextesHistorique.Epuisee);
        Assert.Equal("Aucun trou n'est interpolé. Les pourcentages sont des relevés exacts du serveur ; les tokens sont un comptage local partiel, sur leur propre axe.", TextesHistorique.PiedDePage);
        Assert.Equal("cadre violet : marches de % sans tokens Code = consommé ailleurs (Cowork, claude.ai)", TextesHistorique.PiedDivergence);
        Assert.Equal("aucun relevé sur cette semaine de forfait", TextesHistorique.AucunReleveSemaine);
        Assert.Equal("aucun relevé sur ce jour", TextesHistorique.AucunReleveJour);
    }

    [Fact]
    public void Le_libelle_permanent_des_tokens_porte_le_grain()
    {
        const string suffixe = ", comptés localement — hors Cowork et claude.ai · bruts, non pondérés · ce n'est PAS un % du forfait";

        Assert.Equal("par heure" + suffixe, TextesHistorique.LibellePermanentTokens(TextesHistorique.GrainHeure));
        var jour = TextesHistorique.LibellePermanentTokens(TextesHistorique.GrainQuartDHeure);
        Assert.StartsWith("par quart d'heure, ", jour, StringComparison.Ordinal);
        Assert.EndsWith(suffixe, jour, StringComparison.Ordinal);
    }

    [Fact]
    public void Les_libelles_de_periode_sont_en_francais_explicite()
    {
        static (string Semaine, string Jour, string JourCourt, string Heure, string Date) Produire()
            => (TextesHistorique.LibellePeriodeSemaine(CetteSemaine, Tz),
                TextesHistorique.LibellePeriodeJour(BornesPlage.Jour(Utc("2026-09-24T15:12:00Z"), Tz), CetteSemaine, Tz),
                TextesHistorique.LibelleJour(Utc("2026-09-18T22:00:00Z"), Tz),
                TextesHistorique.LibelleHeure(Utc("2026-09-24T01:00:00Z"), Tz),
                TextesHistorique.DateLongue(Utc("2026-09-14T10:00:00Z"), Tz));

        var attendu = ("Semaine de forfait · sam. 19 sept. 00:00 → sam. 26 sept. 00:00",
                       "Jour · jeudi 24 sept. 2026 · semaine de forfait du 19 sept.",
                       "sam. 19", "3 h", "14 sept. 2026");
        Assert.Equal(attendu, Produire());
        Assert.Equal("0 h", TextesHistorique.LibelleHeure(Utc("2026-09-23T22:00:00Z"), Tz));
        Assert.Equal("21 h", TextesHistorique.LibelleHeure(Utc("2026-09-24T19:00:00Z"), Tz));
        Assert.Equal("14:00", TextesHistorique.HeureMinute(Utc("2026-09-24T12:00:00Z"), Tz));

        // Preuve du fr-FR explicite : sous la culture invariante, le résultat est IDENTIQUE (sinon « Sat 19 Sep »).
        var culture = CultureInfo.CurrentCulture;
        var cultureUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Assert.Equal(attendu, Produire());
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = cultureUi;
        }
    }

    [Fact]
    public void La_ligne_de_fraicheur_semaine_dit_tout_ou_dit_aucun_releve()
    {
        var now = Utc("2026-09-24T15:12:00Z");
        var ouverture = Utc("2026-09-14T10:00:00Z");

        Assert.Equal("Dernier relevé il y a 2 min · sonde d'en-têtes de rate-limit · 3 relevés · 2 interruptions · journal ouvert le 14 sept. 2026",
            TextesHistorique.LigneFraicheurSemaine(Analyse(Serie(3, now - TimeSpan.FromMinutes(2)), new[] { TrouArrete, TrouJeton }, ouverture), now, Tz));
        Assert.Equal("Dernier relevé il y a 2 min · sonde d'en-têtes de rate-limit · 1 relevé · 1 interruption · journal ouvert le 14 sept. 2026",
            TextesHistorique.LigneFraicheurSemaine(Analyse(Serie(1, now - TimeSpan.FromMinutes(2)), new[] { TrouJeton }, ouverture), now, Tz));
        Assert.Equal("Dernier relevé il y a 2 min · sonde d'en-têtes de rate-limit · 3 relevés · 0 interruption · journal ouvert le 14 sept. 2026",
            TextesHistorique.LigneFraicheurSemaine(Analyse(Serie(3, now - TimeSpan.FromMinutes(2)), Array.Empty<Trou>(), ouverture), now, Tz));
        Assert.Equal("Dernier relevé il y a 2 min · sonde d'en-têtes de rate-limit · 3 relevés · 0 interruption",
            TextesHistorique.LigneFraicheurSemaine(Analyse(Serie(3, now - TimeSpan.FromMinutes(2)), Array.Empty<Trou>(), null), now, Tz));
        Assert.Equal(TextesHistorique.AucunReleveSemaine,
            TextesHistorique.LigneFraicheurSemaine(Analyse(Array.Empty<ReleveJournal>(), Array.Empty<Trou>(), ouverture), now, Tz));
    }

    [Fact]
    public void La_ligne_de_fraicheur_jour_compte_les_attendus_depuis_la_plage()
    {
        var cadence = RateLimitHeaderUsageProvider.CadenceNominale;
        var jour = BornesPlage.Jour(Utc("2026-09-24T15:12:00Z"), Tz);
        var serie = Serie(262, Utc("2026-09-24T15:10:00Z"));

        Assert.Equal("288 relevés attendus · 262 présents · 1 interruption (jeton invalide, 14:00 → 16:00)",
            TextesHistorique.LigneFraicheurJour(jour, Analyse(serie, new[] { TrouJeton }, null, jour), cadence, Tz));
        Assert.StartsWith("300 relevés attendus", TextesHistorique.LigneFraicheurJour(BornesPlage.Jour(Utc("2026-10-25T12:00:00Z"), Tz), Analyse(serie, Array.Empty<Trou>(), null), cadence, Tz), StringComparison.Ordinal);
        Assert.StartsWith("276 relevés attendus", TextesHistorique.LigneFraicheurJour(BornesPlage.Jour(Utc("2027-03-28T12:00:00Z"), Tz), Analyse(serie, Array.Empty<Trou>(), null), cadence, Tz), StringComparison.Ordinal);
        Assert.Equal("288 relevés attendus · 262 présents · 2 interruptions (Chronos arrêté, 23:00 → 07:00 ; jeton invalide, 14:00 → 16:00)",
            TextesHistorique.LigneFraicheurJour(jour, Analyse(serie, new[] { TrouArrete, TrouJeton }, null, jour), cadence, Tz));
        Assert.Equal("288 relevés attendus · 262 présents · 0 interruption",
            TextesHistorique.LigneFraicheurJour(jour, Analyse(serie, Array.Empty<Trou>(), null, jour), cadence, Tz));
        Assert.Equal("288 relevés attendus · 262 présents · 1 interruption (Chronos arrêté, 23:00 → en cours)",
            TextesHistorique.LigneFraicheurJour(jour, Analyse(serie, new[] { TrouArrete with { Fin = null } }, null, jour), cadence, Tz));
        Assert.Equal(TextesHistorique.AucunReleveJour,
            TextesHistorique.LigneFraicheurJour(jour, Analyse(Array.Empty<ReleveJournal>(), Array.Empty<Trou>(), null, jour), cadence, Tz));
    }

    [Fact]
    public void L_alerte_le_saut_le_reset_et_l_ouverture_ont_leurs_mots()
    {
        Assert.Equal("journal muet depuis 16 min", TextesHistorique.AlerteJournalMuet(TimeSpan.FromSeconds(16 * 60 + 30)));
        Assert.Equal("+2 % pendant l'absence (répartition inconnue)", TextesHistorique.Saut(new SautNonLocalise(WindowKind.SevenDay, TrouArrete, 0.41, 0.43, 0.02)));
        Assert.Equal("au moins un reset pendant l'absence (répartition inconnue)", TextesHistorique.Saut(new SautNonLocalise(WindowKind.FiveHour, TrouArrete, 0.14, 0.02, null)));
        Assert.Equal("−1 % pendant l'absence (répartition inconnue)", TextesHistorique.Saut(new SautNonLocalise(WindowKind.SevenDay, TrouArrete, 0.42, 0.41, -0.01)));
        Assert.Equal("reset 5 h 17:00", TextesHistorique.Reset5h(Utc("2026-09-24T15:00:00Z"), Tz));
        Assert.Equal("journal ouvert le 14 sept. 2026", TextesHistorique.JournalOuvertLe(Utc("2026-09-14T10:00:00Z"), Tz));
        Assert.Equal("jeton invalide", TextesHistorique.Trou(TrouJeton));
        Assert.Equal("Chronos arrêté", TextesHistorique.Trou(TrouArrete));
    }

    [Fact]
    public void L_echelle_des_tokens_et_la_legende_des_modeles()
    {
        Assert.Equal("0 – 1,2 M (sortie)", TextesHistorique.EchelleTokens(1_200_000));
        Assert.Equal("0 – 250 k (sortie)", TextesHistorique.EchelleTokens(250_000));
        Assert.Equal("0 – 800 (sortie)", TextesHistorique.EchelleTokens(800));
        Assert.Equal("opus · sonnet · haiku · sous-agents inclus", TextesHistorique.LegendeModeles(new[] { "claude-opus-5", "claude-sonnet-4-5", "claude-haiku-4-5" }));
        Assert.Equal("opus · sonnet · haiku · sous-agents inclus", TextesHistorique.LegendeModeles(new[] { "claude-opus-5", "claude-sonnet-4-5", "claude-haiku-4-5", "claude-quatrieme-1" }));
        Assert.Equal("aucun modèle · sous-agents inclus", TextesHistorique.LegendeModeles(Array.Empty<string>()));
    }

    [Fact]
    public void L_infobulle_a_quatre_lignes()
    {
        var t = Utc("2026-09-24T12:35:00Z");
        var now = t + TimeSpan.FromMinutes(12);

        Assert.Equal("14:35 · relevé exact\n5 h : 43 % — reset à 17:00\nhebdo : 61 %\nsonde d'en-têtes de rate-limit · il y a 12 min",
            TextesHistorique.Infobulle(Releve(t), now, Tz));
        Assert.Equal("14:35 · relevé exact\n5 h : —\nhebdo : —\nsonde d'en-têtes de rate-limit · il y a 12 min",
            TextesHistorique.Infobulle(Releve(t, u5: null, u7: null), now, Tz));
    }

    [Fact]
    public void Le_bandeau_F2_a_le_texte_exact()
    {
        Assert.Equal("Reconstruction des tokens depuis vos transcripts Claude Code — 886 / 1603 fichiers · la semaine courante est déjà complète", TextesHistorique.BandeauF2(886, 1603, true));
        Assert.Equal("Reconstruction des tokens depuis vos transcripts Claude Code — 12 / 1603 fichiers", TextesHistorique.BandeauF2(12, 1603, false));
        Assert.Equal("en arrière-plan, priorité basse · du plus récent au plus ancien · les pourcentages du forfait ne se reconstruisent pas : ils commencent au 14 sept. 2026",
            TextesHistorique.SousTexteF2(Utc("2026-09-14T10:00:00Z"), Tz));
        Assert.EndsWith("ils commencent au premier relevé du journal", TextesHistorique.SousTexteF2(null, Tz), StringComparison.Ordinal);
    }

    [Fact]
    public void Pourcent_arrondit_a_l_entier()
    {
        Assert.Equal("43 %", TextesHistorique.Pourcent(0.434));
        Assert.Equal("100 %", TextesHistorique.Pourcent(1.0));
        Assert.Equal("—", TextesHistorique.Pourcent(null));
    }

    [Fact]
    public void Aucune_projection_dans_les_textes()
    {
        var interdits = new Regex(@"épuisé vers|à ce rythme|projection|prévision|estim|tendance|dans \d+ h", RegexOptions.IgnoreCase);
        var now = Utc("2026-09-24T15:12:00Z");

        var constantes = typeof(TextesHistorique)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
        Assert.True(constantes.Count >= 20, $"Seulement {constantes.Count} constantes vues : la garde ne voit pas le vocabulaire.");

        var sorties = new List<string>
        {
            TextesHistorique.LibellePermanentTokens(TextesHistorique.GrainHeure),
            TextesHistorique.LibellePeriodeSemaine(CetteSemaine, Tz),
            TextesHistorique.LibellePeriodeJour(BornesPlage.Jour(now, Tz), CetteSemaine, Tz),
            TextesHistorique.LigneFraicheurSemaine(Analyse(Serie(3, now), new[] { TrouArrete }, Utc("2026-09-14T10:00:00Z")), now, Tz),
            TextesHistorique.LigneFraicheurJour(BornesPlage.Jour(now, Tz), Analyse(Serie(3, now), new[] { TrouArrete with { Fin = null } }, null), RateLimitHeaderUsageProvider.CadenceNominale, Tz),
            TextesHistorique.AlerteJournalMuet(TimeSpan.FromMinutes(16)),
            TextesHistorique.Saut(new SautNonLocalise(WindowKind.SevenDay, TrouArrete, 0.41, 0.43, 0.02)),
            TextesHistorique.Saut(new SautNonLocalise(WindowKind.FiveHour, TrouArrete, 0.14, 0.02, null)),
            TextesHistorique.Reset5h(now, Tz),
            TextesHistorique.JournalOuvertLe(now, Tz),
            TextesHistorique.EchelleTokens(1_200_000),
            TextesHistorique.LegendeModeles(new[] { "claude-opus-5" }),
            TextesHistorique.Infobulle(Releve(now), now, Tz),
            TextesHistorique.BandeauF2(886, 1603, true),
            TextesHistorique.SousTexteF2(null, Tz),
            TextesHistorique.LibelleStyle(HistoriqueStyleSemaine.Simplifie),
        };

        foreach (var texte in constantes.Concat(sorties))
            Assert.False(interdits.IsMatch(texte), $"Mot interdit dans « {texte} »");
    }

    [Fact]
    public void Les_styles_ont_leurs_noms()
    {
        Assert.Equal("Pistes", TextesHistorique.LibelleStyle(HistoriqueStyleSemaine.Pistes));
        Assert.Equal("Simplifié", TextesHistorique.LibelleStyle(HistoriqueStyleSemaine.Simplifie));
        Assert.Equal("Tuiles", TextesHistorique.LibelleStyle(HistoriqueStyleSemaine.Tuiles));
        Assert.Equal("Style :", TextesHistorique.StylePrefixe);
        Assert.Equal("Pistes", TextesHistorique.StylePistes);
        Assert.Equal("Simplifié", TextesHistorique.StyleSimplifie);
        Assert.Equal("Tuiles", TextesHistorique.StyleTuiles);
    }
}
