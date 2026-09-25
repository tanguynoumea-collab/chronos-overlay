using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// APP-01 — la lecture TOLÉRANTE des métadonnées par session de l'app bureau Claude, prouvée sur les six fixtures
/// RÉELLES de la phase 27 (jamais régénérées par sérialisation : <see cref="RacineAppBureau"/>).
///
/// <para><b>Instants en UTC exact.</b> « dernier focus 20:30:44 » est une heure de Paris : chaque instant est asserté
/// en <c>new DateTimeOffset(…, TimeSpan.Zero)</c>, jamais en chaîne locale (29-RESEARCH, Piège 10).</para>
///
/// <para><b>Aucune lecture de la vraie machine.</b> Tout lecteur construit ici reçoit des racines TEMPORAIRES
/// explicites ; l'instant est injecté ; aucun test ne demande l'heure ni ne lit le dossier de l'app.</para>
/// </summary>
public class LecteurAppBureauTests : IDisposable
{
    private const string GesteB = "session-courante-geste-b.json";
    private const string Bloquee = "fin-de-tour-blocked.json";
    private const string Terminee = "fin-de-tour-completed.json";
    private const string PreteARevue = "fin-de-tour-review-ready.json";
    private const string SansResume = "sans-postTurnSummary.json";
    private const string SansId = "sans-cliSessionId.json";

    private const string IdGesteB = "11456cab-d447-42c7-aa85-9920ce64f7ba";
    private const string IdBloquee = "adac2711-86a4-4b4d-b71c-9a594c9d959e";
    private const string IdPreteARevue = "c17a1b03-3c8d-4c05-a747-dd77bc702e1b";

    /// <summary>L'instant de référence des fixtures du 2026-09-25. Fixe : jamais l'horloge.</summary>
    private static readonly DateTimeOffset M = new(2026, 9, 25, 18, 40, 0, TimeSpan.Zero);

    /// <summary>L'instant de référence de la fixture « blocked » (fin de tour le 2026-09-24 à 09:00:06 UTC).</summary>
    private static readonly DateTimeOffset MBloquee = new(2026, 9, 24, 9, 5, 0, TimeSpan.Zero);

    private readonly List<string> _racines = new();

    public void Dispose()
    {
        foreach (var r in _racines)
            try { Directory.Delete(r, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private static IssueLecture Interpreter(byte[] octets, out MetadonneesAppBureau? meta)
        => LecteurAppBureau.Interpreter(octets, out meta);

    private static MetadonneesAppBureau Valide(byte[] octets)
    {
        var issue = Interpreter(octets, out var meta);
        Assert.Equal(IssueLecture.Valide, issue);
        Assert.NotNull(meta);
        return meta!;
    }

    private static byte[] Utf8(string texte) => System.Text.Encoding.UTF8.GetBytes(texte);

    // ------------------------------------------------------------------ Tâche 1 : l'interprétation pure (APP-01)

    /// <summary>La session du geste B (phase 27) : tout ce que Chronos lit, champ par champ, en UTC exact.</summary>
    [Fact]
    public void La_session_du_geste_B_donne_son_titre_son_dossier_et_son_dernier_focus()
    {
        var m = Valide(RacineAppBureau.Fixture(GesteB));

        Assert.Equal(IdGesteB, m.CliSessionId);
        Assert.Equal("Session A", m.Titre);
        Assert.Equal("auto", m.SourceDuTitre);
        Assert.Equal(@"C:\Projets\Projet-A", m.Dossier);
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 14, 5, 46, 164, TimeSpan.Zero), m.Creation);
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 18, 30, 44, 976, TimeSpan.Zero), m.DernierFocus);
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 18, 34, 46, 677, TimeSpan.Zero), m.DerniereActivite);
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 18, 34, 43, 93, TimeSpan.Zero), m.DernierMessageUtilisateur);
        Assert.Equal(5, m.ToursTermines);
        Assert.False(m.Archivee);
        Assert.Equal(ClassificationFinDeTour.Inconnue, m.ClassificationFinDeTour);
        Assert.Null(m.CategorieBrute);
        Assert.Null(m.MotifBlocage);
        Assert.Null(m.ResumePour);
        Assert.Null(m.InstantClassification);
        Assert.Empty(m.ChampsAbsents);
    }

    /// <summary>« blocked » : une question posée à l'utilisateur, son motif, et l'instant de l'épisode.</summary>
    [Fact]
    public void Blocked_donne_la_question_son_motif_et_l_instant_de_l_episode()
    {
        var m = Valide(RacineAppBureau.Fixture(Bloquee));

        Assert.Equal(ClassificationFinDeTour.Bloquee, m.ClassificationFinDeTour);
        Assert.Equal("blocked", m.CategorieBrute);
        Assert.Equal("(anonymisé) réponse attendue", m.MotifBlocage);
        Assert.Equal("5eec0de2-f19c-4e38-8db5-9c5d0be233f1", m.ResumePour);
        var finDeTour = new DateTimeOffset(2026, 9, 24, 9, 0, 6, 552, TimeSpan.Zero);
        Assert.Equal(finDeTour, m.DerniereActivite);
        Assert.Equal(finDeTour, m.InstantClassification);
        Assert.Equal("Session E", m.Titre);
        Assert.Equal(70, m.ToursTermines);
        Assert.Equal(IdBloquee, m.CliSessionId);
    }

    /// <summary>« completed » : une fin de tour terminée ; <c>needs_action</c> vide ne fait pas un motif.</summary>
    [Fact]
    public void Completed_est_une_fin_de_tour_terminee_sans_motif()
    {
        var m = Valide(RacineAppBureau.Fixture(Terminee));

        Assert.Equal(ClassificationFinDeTour.Terminee, m.ClassificationFinDeTour);
        Assert.Equal("completed", m.CategorieBrute);
        Assert.Null(m.MotifBlocage);
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 7, 24, 15, 962, TimeSpan.Zero), m.InstantClassification);
    }

    /// <summary>« review_ready » : prête à revue, sans motif.</summary>
    [Fact]
    public void Review_ready_est_une_fin_de_tour_prete_a_revue_sans_motif()
    {
        var m = Valide(RacineAppBureau.Fixture(PreteARevue));

        Assert.Equal(ClassificationFinDeTour.PreteARevue, m.ClassificationFinDeTour);
        Assert.Equal("review_ready", m.CategorieBrute);
        Assert.Null(m.MotifBlocage);
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 18, 34, 6, 752, TimeSpan.Zero), m.InstantClassification);
        Assert.Equal(IdPreteARevue, m.CliSessionId);
    }

    /// <summary>Le cas majoritaire (127/138 sur disque) : aucun résumé, donc ni classification ni instant.</summary>
    [Fact]
    public void Sans_postTurnSummary_aucune_classification_et_aucun_instant()
    {
        var m = Valide(RacineAppBureau.Fixture(SansResume));

        Assert.Equal(ClassificationFinDeTour.Inconnue, m.ClassificationFinDeTour);
        Assert.Null(m.CategorieBrute);
        Assert.Null(m.InstantClassification);
        Assert.Equal("Session D", m.Titre);
        Assert.Empty(m.ChampsAbsents);   // l'absence de résumé est NORMALE (transitoire) : elle ne se compte pas
    }

    /// <summary>23 fichiers sur 138 n'ont pas de <c>cliSessionId</c> : reconnus, sans aucune métadonnée joignable.</summary>
    [Fact]
    public void Sans_cliSessionId_le_fichier_est_reconnu_et_non_joignable()
    {
        var issue = Interpreter(RacineAppBureau.Fixture(SansId), out var meta);

        Assert.Equal(IssueLecture.SansCliSessionId, issue);
        Assert.Null(meta);
    }

    /// <summary>Les six fixtures portent un bloc <c>_fixture</c> que l'app n'écrit jamais : ignoré comme tout champ
    /// inconnu, et aucune ne lève.</summary>
    [Theory]
    [InlineData(GesteB)]
    [InlineData(Bloquee)]
    [InlineData(Terminee)]
    [InlineData(PreteARevue)]
    [InlineData(SansResume)]
    [InlineData(SansId)]
    public void Les_six_fixtures_reelles_se_lisent_malgre_les_champs_inconnus(string fixture)
    {
        var issue = Interpreter(RacineAppBureau.Fixture(fixture), out _);

        Assert.NotEqual(IssueLecture.Illisible, issue);
    }

    /// <summary>Le code de l'app connaît <c>need_input</c> et <c>failed</c> ; le disque ne les a jamais montrées
    /// (Piège 7). Une catégorie non relevée reste « inconnue », sa valeur brute est gardée pour le diagnostic —
    /// jamais une question inventée.</summary>
    [Theory]
    [InlineData("need_input")]
    [InlineData("failed")]
    public void Une_categorie_que_le_disque_n_a_jamais_montree_reste_inconnue(string categorie)
    {
        var texte = RacineAppBureau.Deriver(Bloquee,
            ("\"status_category\": \"blocked\"", $"\"status_category\": \"{categorie}\""));

        var m = Valide(Utf8(texte));

        Assert.Equal(ClassificationFinDeTour.Inconnue, m.ClassificationFinDeTour);
        Assert.NotEqual(ClassificationFinDeTour.Bloquee, m.ClassificationFinDeTour);
        Assert.Equal(categorie, m.CategorieBrute);
    }

    /// <summary>Un champ de mauvais type vaut « absent » — et se compte, pour que le diagnostic le dise.</summary>
    [Fact]
    public void Un_champ_de_mauvais_type_vaut_absent_et_se_compte()
    {
        var texte = RacineAppBureau.Deriver(Bloquee, ("\"title\": \"Session E\"", "\"title\": 42"));

        var m = Valide(Utf8(texte));

        Assert.Null(m.Titre);
        Assert.Contains("title", m.ChampsAbsents);
        Assert.Equal(IdBloquee, m.CliSessionId);   // le reste du fichier se lit toujours
    }

    public static TheoryData<string> ContenusIllisibles() => new() { "moitie", "[]", "pas du JSON {{{" };

    /// <summary>Un fichier tronqué (réécriture en place en cours), un tableau, du texte : « illisible », jamais une
    /// exception.</summary>
    [Theory]
    [MemberData(nameof(ContenusIllisibles))]
    public void Un_contenu_illisible_est_illisible_jamais_une_exception(string cas)
    {
        byte[] octets;
        if (cas == "moitie")
        {
            var complet = RacineAppBureau.Fixture(GesteB);
            octets = complet[..(complet.Length / 2)];
        }
        else octets = Utf8(cas);

        var issue = Interpreter(octets, out var meta);

        Assert.Equal(IssueLecture.Illisible, issue);
        Assert.Null(meta);
    }

    /// <summary>Une marque d'ordre d'octets UTF-8 en tête n'empêche pas la lecture.</summary>
    [Fact]
    public void Un_BOM_UTF8_n_empeche_pas_la_lecture()
    {
        var octets = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(RacineAppBureau.Fixture(GesteB)).ToArray();

        var m = Valide(octets);

        Assert.Equal("Session A", m.Titre);
    }

    /// <summary>La fenêtre de LECTURE de 24 h couvre l'horizon d'abandon (8 h) : l'inégalité est assertée AVANT la
    /// valeur, pour que le rouge dise laquelle des deux a bougé.</summary>
    [Fact]
    public void La_fenetre_de_lecture_couvre_l_horizon_d_abandon()
    {
        Assert.True(HorizonsSessions.Abandon <= HorizonsSessions.LectureAppBureau,
            $"Abandon ({HorizonsSessions.Abandon}) <= LectureAppBureau ({HorizonsSessions.LectureAppBureau}) défait : "
            + "un signal encore lisible viendrait d'un fichier que le lecteur n'ouvre plus.");
        Assert.Equal(TimeSpan.FromHours(24), HorizonsSessions.LectureAppBureau);
    }
}
