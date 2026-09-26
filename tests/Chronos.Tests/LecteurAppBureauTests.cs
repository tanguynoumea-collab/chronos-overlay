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

    // ------------------------------------------------------------------ Tâche 2 : Lire — énumérer, filtrer, cacher, dater

    /// <summary>Une racine peuplée des fixtures données (écrites une minute avant <paramref name="maintenant"/>),
    /// nettoyée en fin de test.</summary>
    private string Racine(DateTimeOffset maintenant, params string[] fixtures)
    {
        var racine = RacineAppBureau.Creer(maintenant, fixtures);
        _racines.Add(racine);
        return racine;
    }

    private string RacineVide()
    {
        var racine = RacineAppBureau.NouvelleRacine();
        _racines.Add(racine);
        return racine;
    }

    /// <summary>Un chemin qui n'existe pas, sous le dossier temporaire.</summary>
    private static string Absente()
        => Path.Combine(Path.GetTempPath(), "chronos-appbureau-absente-" + Guid.NewGuid().ToString("N"));

    private static string Chemin(string racine, string fixture)
        => Path.Combine(RacineAppBureau.DossierUtilisateur(racine), RacineAppBureau.NomFichier(fixture));

    /// <summary>Le bruit réel du dossier de l'app (Piège 3) : seul un <c>local_*.json</c> à la profondeur exacte
    /// <c>&lt;org&gt;\&lt;user&gt;</c> est énuméré — ni l'index, ni le temporaire d'écriture atomique (qui contient
    /// pourtant un vrai fichier « blocked »), ni <c>backlog</c>, ni la trace de suppression, ni un fichier posé trop
    /// haut.</summary>
    [Fact]
    public void Seuls_les_local_json_a_profondeur_deux_sont_enumeres()
    {
        var racine = Racine(M, GesteB);
        RacineAppBureau.AjouterBruit(racine, M);

        var l = new LecteurAppBureau(new[] { racine }).Lire(M);

        Assert.Equal(1, l.Enumeres);
        Assert.Equal(new[] { IdGesteB }, l.ParSession.Keys.ToArray());
        Assert.Equal(1, l.RelusSurDisque);
        Assert.Equal(0, l.Illisibles);
    }

    /// <summary>La fenêtre de 24 h : un fichier écrit il y a 25 h est énuméré, jamais ouvert.</summary>
    [Fact]
    public void Un_fichier_modifie_il_y_a_plus_de_24_h_n_est_pas_ouvert()
    {
        var racine = Racine(M, GesteB, Bloquee);
        File.SetLastWriteTimeUtc(Chemin(racine, Bloquee), M.AddHours(-25).UtcDateTime);

        var l = new LecteurAppBureau(new[] { racine }).Lire(M);

        Assert.Equal(2, l.Enumeres);
        Assert.Equal(1, l.Recents);
        Assert.Equal(1, l.RelusSurDisque);
        Assert.False(l.ParSession.ContainsKey(IdBloquee));
        Assert.True(l.ParSession.ContainsKey(IdGesteB));
    }

    /// <summary>Le cache (date d'écriture, taille) : rien n'est relu si rien n'a changé ; seul le fichier dont la
    /// date d'écriture a bougé l'est — sa taille, elle, ne change pas à la réécriture (Piège 4).</summary>
    [Fact]
    public void Le_cache_ne_relit_que_ce_qui_a_change()
    {
        var racine = Racine(M, GesteB, Bloquee, PreteARevue);
        var lecteur = new LecteurAppBureau(new[] { racine });

        var l1 = lecteur.Lire(M);
        Assert.Equal(3, l1.RelusSurDisque);
        Assert.Equal(3, l1.Valides);

        var l2 = lecteur.Lire(M.AddSeconds(2));
        Assert.Equal(0, l2.RelusSurDisque);
        Assert.Equal(3, l2.Valides);
        Assert.Equal(l1.ParSession.ToDictionary(kv => kv.Key, kv => kv.Value.Titre),
                     l2.ParSession.ToDictionary(kv => kv.Key, kv => kv.Value.Titre));

        File.SetLastWriteTimeUtc(Chemin(racine, PreteARevue), M.UtcDateTime);   // une minute plus tard, même taille
        var l3 = lecteur.Lire(M.AddSeconds(4));
        Assert.Equal(1, l3.RelusSurDisque);
        Assert.Equal(3, l3.Valides);
        Assert.Equal("Session B", l3.ParSession[IdPreteARevue].Titre);
    }

    /// <summary>La réécriture EN PLACE de l'app (repli après trois renommages ratés) peut laisser voir un fichier à
    /// moitié écrit : la relecture ratée garde la DERNIÈRE lecture valide, et la clé du cache n'est pas mise à jour —
    /// le fichier est retenté au cycle suivant. Aucun titre ne clignote.</summary>
    [Fact]
    public void Un_fichier_tronque_pendant_sa_reecriture_ne_fait_pas_clignoter_le_titre()
    {
        var racine = Racine(M, GesteB);
        var lecteur = new LecteurAppBureau(new[] { racine });
        var complet = RacineAppBureau.Fixture(GesteB);
        var nom = RacineAppBureau.NomFichier(GesteB);

        var l1 = lecteur.Lire(M);
        Assert.Equal("Session A", l1.ParSession[IdGesteB].Titre);

        RacineAppBureau.EcrireOctets(racine, nom, complet[..(complet.Length / 2)], M);   // date + 1 min
        var l2 = lecteur.Lire(M.AddSeconds(2));
        Assert.Equal("Session A", l2.ParSession[IdGesteB].Titre);
        Assert.Equal(1, l2.Illisibles);
        Assert.Equal(1, l2.RelusSurDisque);

        var l3 = lecteur.Lire(M.AddSeconds(4));   // inchangé sur le disque : relu quand même, la clé n'a pas bougé
        Assert.Equal(1, l3.RelusSurDisque);
        Assert.Equal(1, l3.Illisibles);
        Assert.Equal("Session A", l3.ParSession[IdGesteB].Titre);

        RacineAppBureau.EcrireOctets(racine, nom, complet, M.AddMinutes(1));   // date + 2 min, fichier complet
        var l4 = lecteur.Lire(M.AddSeconds(6));
        Assert.Equal(0, l4.Illisibles);
        Assert.Equal(1, l4.RelusSurDisque);
        Assert.Equal("Session A", l4.ParSession[IdGesteB].Titre);
    }

    /// <summary>Une première lecture ratée ne donne rien, se compte, et ne lève pas.</summary>
    [Fact]
    public void Une_premiere_lecture_illisible_ne_donne_rien_et_se_compte()
    {
        var racine = RacineVide();
        RacineAppBureau.Ecrire(racine, "local_x.json", "pas du JSON {{{", M.AddMinutes(-1));

        var l = new LecteurAppBureau(new[] { racine }).Lire(M);

        Assert.Equal(1, l.Enumeres);
        Assert.Equal(1, l.Illisibles);
        Assert.Equal(0, l.Valides);
        Assert.Empty(l.ParSession);
    }

    /// <summary>Un fichier sans <c>cliSessionId</c> se compte — au premier cycle comme depuis le cache — et ne donne
    /// aucune session.</summary>
    [Fact]
    public void Un_fichier_sans_cliSessionId_est_compte_et_ignore()
    {
        var racine = Racine(M, SansId, GesteB);
        var lecteur = new LecteurAppBureau(new[] { racine });

        var l = lecteur.Lire(M);
        Assert.Equal(1, l.SansCliSessionId);
        Assert.Equal(1, l.Valides);
        Assert.Single(l.ParSession);

        var depuisLeCache = lecteur.Lire(M.AddSeconds(2));
        Assert.Equal(0, depuisLeCache.RelusSurDisque);
        Assert.Equal(1, depuisLeCache.SansCliSessionId);
        Assert.Single(depuisLeCache.ParSession);
    }

    /// <summary>Deux fichiers pour une même session (aucun aujourd'hui, possible demain) : le plus récent par
    /// <c>lastActivityAt</c> l'emporte, quel que soit l'ordre des noms, et le doublon se compte.</summary>
    [Fact]
    public void Deux_fichiers_pour_une_meme_session_gardent_le_plus_recent_et_se_comptent()
    {
        var ancienne = RacineAppBureau.Deriver(GesteB,
            ("1790361286677", "1790361000000"),
            ("\"title\": \"Session A\"", "\"title\": \"Session A (ancienne)\""));

        var racine = RacineVide();
        RacineAppBureau.EcrireOctets(racine, "local_a.json", RacineAppBureau.Fixture(GesteB), M.AddMinutes(-1));
        RacineAppBureau.Ecrire(racine, "local_b.json", ancienne, M.AddMinutes(-1));

        var l = new LecteurAppBureau(new[] { racine }).Lire(M);

        Assert.Equal("Session A", l.ParSession[IdGesteB].Titre);
        Assert.Equal(1, l.Doublons);
        Assert.Equal(2, l.Valides);
        Assert.Single(l.ParSession);

        // Noms inversés : l'ordre d'énumération ne décide pas, la dernière activité si.
        var inversee = RacineVide();
        RacineAppBureau.Ecrire(inversee, "local_a.json", ancienne, M.AddMinutes(-1));
        RacineAppBureau.EcrireOctets(inversee, "local_b.json", RacineAppBureau.Fixture(GesteB), M.AddMinutes(-1));

        var li = new LecteurAppBureau(new[] { inversee }).Lire(M);

        Assert.Equal("Session A", li.ParSession[IdGesteB].Titre);
        Assert.Equal(1, li.Doublons);
    }

    /// <summary>Les racines se résolvent par candidats, dans l'ordre : le premier qui existe est lu, et lui seul.</summary>
    [Fact]
    public void La_racine_est_le_premier_candidat_qui_existe()
    {
        var absente = Absente();
        var r2 = Racine(M, GesteB);
        var r3 = Racine(M, Bloquee);

        var l = new LecteurAppBureau(new[] { absente, r2, r3 }).Lire(M);

        Assert.True(l.DossierTrouve);
        Assert.Equal(r2, l.Racine);
        Assert.Equal(new[] { absente, r2, r3 }, l.RacinesCherchees);
        Assert.Equal(new[] { IdGesteB }, l.ParSession.Keys.ToArray());
    }

    /// <summary>Aucune racine : une lecture ABSENTE, entièrement à zéro, qui dit encore ce qu'elle a cherché.</summary>
    [Fact]
    public void Aucune_racine_rend_une_lecture_absente_sans_exception()
    {
        var l = new LecteurAppBureau(new[] { Absente(), Absente() }).Lire(M);

        Assert.False(l.DossierTrouve);
        Assert.Null(l.Racine);
        Assert.Equal(2, l.RacinesCherchees.Count);
        Assert.Empty(l.ParSession);
        Assert.Empty(l.ChampsAbsents);
        Assert.Equal(0, l.Enumeres);
        Assert.Equal(0, l.Recents);
        Assert.Equal(0, l.Valides);
        Assert.Equal(0, l.RelusSurDisque);
        Assert.Equal(0, l.Illisibles);
        Assert.Equal(0, l.SansCliSessionId);
        Assert.Equal(0, l.Doublons);
    }

    /// <summary>TRT-02, décision verrouillée : l'instant d'une question est <c>lastActivityAt</c> lu à la PREMIÈRE
    /// apparition de son résumé, mémorisé par (<c>cliSessionId</c>, <c>postTurnSummaryFor</c>). L'activité de fond
    /// qui fait avancer <c>lastActivityAt</c> (+3 min 10 s mesurés) ne la rajeunit pas ; un NOUVEL épisode, si.</summary>
    [Fact]
    public void L_instant_d_une_question_est_fige_par_episode()
    {
        var racine = Racine(MBloquee, Bloquee);
        var lecteur = new LecteurAppBureau(new[] { racine });
        var nom = RacineAppBureau.NomFichier(Bloquee);
        var finDeTour = new DateTimeOffset(2026, 9, 24, 9, 0, 6, 552, TimeSpan.Zero);

        var l1 = lecteur.Lire(MBloquee);
        Assert.Equal(finDeTour, l1.ParSession[IdBloquee].InstantClassification);

        // Activité de fond : +3 min 10 s, même épisode (même taille de fichier : seule la date d'écriture change).
        RacineAppBureau.Ecrire(racine, nom, RacineAppBureau.Deriver(Bloquee, ("1790240406552", "1790240596552")),
                               MBloquee);   // date + 1 min
        var l2 = lecteur.Lire(MBloquee.AddSeconds(2));
        Assert.Equal(1, l2.RelusSurDisque);
        Assert.Equal(new DateTimeOffset(2026, 9, 24, 9, 3, 16, 552, TimeSpan.Zero), l2.ParSession[IdBloquee].DerniereActivite);
        Assert.Equal(finDeTour, l2.ParSession[IdBloquee].InstantClassification);

        // Nouvel épisode : un autre postTurnSummaryFor, l'instant suit.
        RacineAppBureau.Ecrire(racine, nom, RacineAppBureau.Deriver(Bloquee,
                                   ("1790240406552", "1790240896552"),
                                   ("5eec0de2-f19c-4e38-8db5-9c5d0be233f1", "0f0f0f0f-0000-4000-8000-000000000001")),
                               MBloquee.AddMinutes(1));   // date + 2 min
        var l3 = lecteur.Lire(MBloquee.AddSeconds(4));
        Assert.Equal("0f0f0f0f-0000-4000-8000-000000000001", l3.ParSession[IdBloquee].ResumePour);
        Assert.Equal(new DateTimeOffset(2026, 9, 24, 9, 8, 16, 552, TimeSpan.Zero), l3.ParSession[IdBloquee].InstantClassification);
    }

    /// <summary>Les champs absents s'agrègent sur les fichiers retenus, et seulement eux : un champ retiré d'un seul
    /// fichier se compte une fois, le résumé transitoire jamais.</summary>
    [Fact]
    public void Les_champs_absents_sont_comptes_sur_les_fichiers_lus()
    {
        var racine = RacineVide();
        RacineAppBureau.Ecrire(racine, RacineAppBureau.NomFichier(GesteB),
                               RacineAppBureau.Deriver(GesteB, ("\"latestUserFrameAt\": 1790361283093,", "")),
                               M.AddMinutes(-1));
        RacineAppBureau.EcrireOctets(racine, RacineAppBureau.NomFichier(Bloquee), RacineAppBureau.Fixture(Bloquee),
                                     M.AddMinutes(-1));

        var l = new LecteurAppBureau(new[] { racine }).Lire(M);

        Assert.Equal(2, l.Valides);
        var absent = Assert.Single(l.ChampsAbsents);
        Assert.Equal("latestUserFrameAt", absent.Key);
        Assert.Equal(1, absent.Value);
    }

    // ------------------------------------------------------------------ Phase 30 : la session sélectionnée (LUE-02, LUE-04)

    /// <summary>Le dernier focus de la session du geste B (<c>1790361044976</c>) : 20:30:44.976 à Paris.</summary>
    private static readonly DateTimeOffset FocusGesteB = new(2026, 9, 25, 18, 30, 44, 976, TimeSpan.Zero);

    /// <summary>Un focus dérivé (<c>1790361100000</c>), plus récent que celui de toutes les fixtures.</summary>
    private static readonly DateTimeOffset FocusDerive = new(2026, 9, 25, 18, 31, 40, TimeSpan.Zero);

    /// <summary>La session du geste B, plus un fichier SANS <c>cliSessionId</c> focalisé plus récemment : une session
    /// neuve que rien ne joint encore, et que l'utilisateur regarde.</summary>
    private string RacineAvecUneSessionNeuveSelectionnee()
    {
        var racine = Racine(M, GesteB);
        RacineAppBureau.Ecrire(racine, "local_sansid.json",
                               RacineAppBureau.Deriver(SansId, ("1781190743865", "1790361100000")), M.AddMinutes(-1));
        return racine;
    }

    /// <summary>S01 — la session sélectionnée est celle dont le <c>lastFocusedAt</c> est le plus récent.</summary>
    [Fact]
    public void La_selection_est_le_dernier_focus_de_tous_les_fichiers()
    {
        var racine = Racine(M, GesteB, PreteARevue, SansResume, Bloquee);

        var l = new LecteurAppBureau(new[] { racine }).Lire(M);

        Assert.NotNull(l.Selection);
        Assert.Equal(IdGesteB, l.Selection!.CliSessionId);
        Assert.Equal(FocusGesteB, l.Selection.DernierFocus);
    }

    /// <summary>S02 — un doublon ÉCARTÉ (plus ancien par activité) peut porter le focus le plus récent : il compte pour
    /// la sélection, sans changer le fichier retenu.</summary>
    [Fact]
    public void Le_focus_d_un_doublon_ecarte_compte_pour_la_selection()
    {
        var racine = RacineVide();
        RacineAppBureau.EcrireOctets(racine, "local_a.json", RacineAppBureau.Fixture(GesteB), M.AddMinutes(-1));
        RacineAppBureau.Ecrire(racine, "local_b.json",
                               RacineAppBureau.Deriver(GesteB, ("1790361286677", "1790361000000"), ("1790361044976", "1790361100000")),
                               M.AddMinutes(-1));
        RacineAppBureau.EcrireOctets(racine, RacineAppBureau.NomFichier(PreteARevue), RacineAppBureau.Fixture(PreteARevue),
                                     M.AddMinutes(-1));

        var l = new LecteurAppBureau(new[] { racine }).Lire(M);

        Assert.Equal(1, l.Doublons);
        Assert.Equal(FocusGesteB, l.ParSession[IdGesteB].DernierFocus);   // le fichier RETENU ne change pas
        Assert.NotNull(l.Selection);
        Assert.Equal(IdGesteB, l.Selection!.CliSessionId);
        Assert.Equal(FocusDerive, l.Selection.DernierFocus);
    }

    /// <summary>S03 — D-30-04 : si le dernier focus appartient à un fichier sans <c>cliSessionId</c>, la sélection existe
    /// mais ne désigne AUCUNE session — sinon la session précédente serait déclarée lue pendant qu'on en regarde une
    /// neuve.</summary>
    [Fact]
    public void Le_dernier_focus_sur_un_fichier_sans_cliSessionId_ne_selectionne_personne()
    {
        var l = new LecteurAppBureau(new[] { RacineAvecUneSessionNeuveSelectionnee() }).Lire(M);

        Assert.NotNull(l.Selection);
        Assert.Null(l.Selection!.CliSessionId);
        Assert.Equal(FocusDerive, l.Selection.DernierFocus);
        Assert.Equal(1, l.SansCliSessionId);
        Assert.Equal(new[] { IdGesteB }, l.ParSession.Keys.ToArray());
    }

    /// <summary>S04 — LUE-04 : sans racine, pas de sélection.</summary>
    [Fact]
    public void Une_racine_absente_n_a_pas_de_selection()
    {
        Assert.Null(new LecteurAppBureau(new[] { Absente() }).Lire(M).Selection);
    }

    /// <summary>S05 — LUE-04 : sans aucun focus lisible, pas de sélection ; le champ se compte absent une seule fois.</summary>
    [Fact]
    public void Sans_aucun_focus_lisible_la_selection_est_nulle()
    {
        var racine = RacineVide();
        RacineAppBureau.Ecrire(racine, RacineAppBureau.NomFichier(GesteB),
                               RacineAppBureau.Deriver(GesteB, ("\"lastFocusedAt\": 1790361044976", "\"lastFocusedAt\": \"illisible\"")),
                               M.AddMinutes(-1));

        var l = new LecteurAppBureau(new[] { racine }).Lire(M);

        Assert.Null(l.Selection);
        Assert.Null(l.ParSession[IdGesteB].DernierFocus);
        Assert.Equal(1, l.ChampsAbsents["lastFocusedAt"]);
    }

    /// <summary>S06 — à focus égal, le premier chemin dans l'ordre ordinal (la règle des doublons) : l'ordre
    /// d'énumération ne décide pas.</summary>
    [Fact]
    public void A_focus_egal_la_selection_est_le_premier_chemin_ordinal()
    {
        var revueAuMemeFocus = RacineAppBureau.Deriver(PreteARevue, ("1790361032950", "1790361044976"));

        var racine = RacineVide();
        RacineAppBureau.Ecrire(racine, "local_a.json", revueAuMemeFocus, M.AddMinutes(-1));
        RacineAppBureau.EcrireOctets(racine, "local_b.json", RacineAppBureau.Fixture(GesteB), M.AddMinutes(-1));

        var l = new LecteurAppBureau(new[] { racine }).Lire(M);
        Assert.Equal(IdPreteARevue, l.Selection?.CliSessionId);
        Assert.Equal(FocusGesteB, l.Selection!.DernierFocus);

        var inversee = RacineVide();
        RacineAppBureau.EcrireOctets(inversee, "local_a.json", RacineAppBureau.Fixture(GesteB), M.AddMinutes(-1));
        RacineAppBureau.Ecrire(inversee, "local_b.json", revueAuMemeFocus, M.AddMinutes(-1));

        var li = new LecteurAppBureau(new[] { inversee }).Lire(M);
        Assert.Equal(IdGesteB, li.Selection?.CliSessionId);
        Assert.Equal(FocusGesteB, li.Selection!.DernierFocus);
    }

    /// <summary>S07 — le focus se lit AVANT l'identifiant : un fichier sans <c>cliSessionId</c> le rend quand même ;
    /// sur un fichier valide, c'est le même que celui des métadonnées.</summary>
    [Fact]
    public void Interpreter_rend_le_focus_meme_sans_cliSessionId()
    {
        var issue = LecteurAppBureau.Interpreter(RacineAppBureau.Fixture(SansId), out var meta, out var focus);

        Assert.Equal(IssueLecture.SansCliSessionId, issue);
        Assert.Null(meta);
        Assert.Equal(new DateTimeOffset(2026, 6, 11, 15, 12, 23, 865, TimeSpan.Zero), focus);

        var issueB = LecteurAppBureau.Interpreter(RacineAppBureau.Fixture(GesteB), out var metaB, out var focusB);

        Assert.Equal(IssueLecture.Valide, issueB);
        Assert.Equal(FocusGesteB, focusB);
        Assert.Equal(metaB!.DernierFocus, focusB);
    }

    /// <summary>S08 — le focus vit dans l'entrée du cache : un fichier non relu (même sans identifiant) garde sa place
    /// dans la sélection.</summary>
    [Fact]
    public void Le_cache_garde_le_focus_pour_la_selection()
    {
        var lecteur = new LecteurAppBureau(new[] { RacineAvecUneSessionNeuveSelectionnee() });

        var l1 = lecteur.Lire(M);
        var l2 = lecteur.Lire(M.AddSeconds(2));

        Assert.Equal(0, l2.RelusSurDisque);
        Assert.NotNull(l2.Selection);
        Assert.Equal(l1.Selection, l2.Selection);
        Assert.Null(l2.Selection!.CliSessionId);
        Assert.Equal(FocusDerive, l2.Selection.DernierFocus);
    }
}
