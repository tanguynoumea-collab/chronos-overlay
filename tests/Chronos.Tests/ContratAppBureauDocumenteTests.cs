using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// LA GARDE DE NON-DÉRIVE du contrat de la source app-bureau (VAL-02) : le document
/// <c>docs/desktop-app-sessions.md</c> et le lecteur réellement livré (<c>LecteurAppBureau</c>) ne peuvent plus
/// diverger en silence.
///
/// <para><b>Pourquoi par extraction (D-31-03).</b> Les champs que le lecteur lit sont extraits du TEXTE de
/// <c>LecteurAppBureau.cs</c> — les littéraux passés à <c>Texte</c>, <c>Instant</c>, <c>Entier</c>, <c>Booleen</c>
/// et <c>TryGetProperty</c> —, jamais d'une liste recopiée ici. Une liste recopiée dériverait avec le code sans rien
/// garder ; une garde de seul comportement serait aveugle à un champ ajouté. Si la FORME des appels change,
/// l'anti-muet rougit bruyamment : adapter la garde, jamais la désarmer.</para>
///
/// <para><b>Ce que cette garde NE PEUT PAS faire.</b> Le format de l'app est interne et non documenté : si l'app
/// renomme un champ, change le sens de <c>lastFocusedAt</c> ou cesse d'écrire ces fichiers, tous ces tests restent
/// verts et le document devient faux en silence. Seul un re-relevé hors de l'arbre de l'app le verrait (§7 du
/// document). Cette garde tient la moitié qu'une machine peut tenir : que ce que nous ÉCRIVONS corresponde à ce que
/// nous LISONS.</para>
///
/// <para>Ces tests ne lisent que des fichiers du dépôt : aucun réseau, aucun <c>%APPDATA%</c>, aucun
/// <c>%LOCALAPPDATA%</c>, aucun <c>~/.claude</c>, aucune horloge.</para>
/// </summary>
public sealed class ContratAppBureauDocumenteTests
{
    private const string NomDocument = "desktop-app-sessions.md";
    private const string ChampsDebut = "CHAMPS-LUS:debut";
    private const string ChampsFin = "CHAMPS-LUS:fin";
    private const string CategoriesDebut = "CATEGORIES-LUES:debut";
    private const string CategoriesFin = "CATEGORIES-LUES:fin";
    private const string DateDuReleve = "2026-09-25";
    private const string TitreNonGaranti = "## 5.";
    private const int LignesMinimum = 100;
    private const int LignesMinimumNonGaranti = 30;

    // Ce qui n'est PAS garanti, un marqueur par trou daté : la date et la version du relevé, le format non documenté,
    // la réécriture intégrale, le résumé transitoire, ce qui met lastFocusedAt à jour (alt-tab) et ce qui ne le met pas
    // (la fin d'un tour), la réserve de 16:23:51, l'accueil au premier plan. Les taire redonnerait au document l'air de
    // certitude qu'une source non documentée ne mérite pas.
    private static readonly string[] MarqueursNonGaranti =
    {
        "2026-09-25", "2.9939.2.0", "non documenté", "réécrit", "transitoire", "alt-tab", "fin d'un tour", "16:23:51",
        "accueil",
    };

    // Les appels du lecteur qui NOMMENT un champ : le premier argument est l'objet lu (la racine du fichier, ou le
    // résumé de fin de tour), le second le littéral du champ. Les assistants internes appellent TryGetProperty(champ, …)
    // SANS littéral : ils ne sont pas capturés, et c'est voulu.
    private static readonly Regex AppelLecteur =
        new(@"\b(?:Texte|Instant|Entier|Booleen)\((?<objet>racine|resume),\s*""(?<champ>[A-Za-z_]+)""");
    private static readonly Regex Propriete = new(@"TryGetProperty\(""(?<champ>[A-Za-z_]+)""");
    private static readonly Regex BrasCategorie = new(@"""(?<cat>[a-z_]+)""\s*=>\s*ClassificationFinDeTour\.");

    /// <summary>Le chemin du dossier docs/ est INJECTÉ par MSBuild, jamais deviné (motif de
    /// <c>ContratHooksDocumenteTests</c>).</summary>
    private static string CheminDocs()
        => typeof(ContratAppBureauDocumenteTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminDocsChronos")?.Value
           ?? "";

    private static string CheminDocument() => Path.Combine(CheminDocs(), NomDocument);

    /// <summary>Lit le document. AUCUNE mise en sourdine : une garde qui se désarme quand son chemin manque ne garde
    /// rien.</summary>
    private static string LireDocument()
    {
        var racine = CheminDocs();
        Assert.False(string.IsNullOrWhiteSpace(racine),
            "L'attribut AssemblyMetadata(\"CheminDocsChronos\") manque : le .csproj de tests doit injecter "
            + "le chemin de docs/, sans quoi cette garde ne lit rien et ne garde rien.");

        var chemin = CheminDocument();
        Assert.True(File.Exists(chemin),
            $"VAL-02 exige docs/desktop-app-sessions.md : introuvable ({chemin}). C'est le document qui rend la "
            + "prochaine dérive de la source app-bureau détectable.");

        return File.ReadAllText(chemin);
    }

    /// <summary>Lit un fichier de src/Chronos par son chemin relatif. ANTI-MUET : attribut présent, fichier présent,
    /// contenu non vide — une garde croisée qui ne lit pas le code ne croise rien.</summary>
    private static string LireSource(params string[] relatif)
    {
        var racine = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : sans lui, cette garde croisée ne lit "
            + "pas le code et ne croise donc rien.");

        var chemin = Path.Combine(new[] { racine }.Concat(relatif).ToArray());
        Assert.True(File.Exists(chemin), $"Source introuvable : {chemin}");

        var code = File.ReadAllText(chemin);
        Assert.False(string.IsNullOrWhiteSpace(code), $"Source vide : {chemin}");
        return code;
    }

    // Les champs que le lecteur LIT, extraits de son texte (jamais d'une liste recopiée ici, qui dériverait avec lui).
    private static SortedSet<string> ChampsLusParLeLecteur()
    {
        var code = LireSource("Services", "LecteurAppBureau.cs");
        var champs = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match m in AppelLecteur.Matches(code))
            champs.Add(m.Groups["objet"].Value == "resume" ? "postTurnSummary." + m.Groups["champ"].Value : m.Groups["champ"].Value);
        foreach (Match m in Propriete.Matches(code)) champs.Add(m.Groups["champ"].Value);
        Assert.True(champs.Contains("cliSessionId") && champs.Contains("lastFocusedAt") && champs.Count >= 12,
            "La garde ne sait plus lire le lecteur (forme des appels changée) : adapter la garde, pas la désarmer. Lu : "
            + string.Join(", ", champs));
        return champs;
    }

    // Les catégories de fin de tour que le lecteur RECONNAÎT : les bras du switch de classification, extraits de son
    // texte. Toute autre catégorie reste inconnue — le document le dit, et ne peut donc citer que celles-ci.
    private static SortedSet<string> CategoriesReconnues()
    {
        var code = LireSource("Services", "LecteurAppBureau.cs");
        var categories = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match m in BrasCategorie.Matches(code)) categories.Add(m.Groups["cat"].Value);
        Assert.True(categories.Contains("blocked") && categories.Count >= 3,
            "La garde ne sait plus lire le switch de classification du lecteur (forme changée) : adapter la garde, "
            + "pas la désarmer. Lu : " + string.Join(", ", categories));
        return categories;
    }

    /// <summary>Égalité d'ensembles dans les DEUX sens, chaque écart nommé : ce que le code lit sans que le document le
    /// dise, et ce que le document dit sans que le code le lise.</summary>
    private static void AssertMemesNoms(ISet<string> lus, ISet<string> documentes, string quoi)
    {
        var manquants = lus.Except(documentes, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var surnumeraires = documentes.Except(lus, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(manquants.Count == 0 && surnumeraires.Count == 0,
            $"docs/desktop-app-sessions.md ne décrit plus {quoi} que LecteurAppBureau lit réellement.\n"
            + "  LUS mais NON DOCUMENTÉS : "
            + (manquants.Count == 0 ? "(aucun)" : string.Join(", ", manquants)) + "\n"
            + "  DOCUMENTÉS mais NON LUS : "
            + (surnumeraires.Count == 0 ? "(aucun)" : string.Join(", ", surnumeraires)));
    }

    [Fact]
    public void Le_chemin_du_document_est_injecte_et_le_fichier_existe()
    {
        var racine = CheminDocs();

        Assert.False(string.IsNullOrWhiteSpace(racine),
            "Le chemin de docs/ doit être injecté par MSBuild (AssemblyMetadata \"CheminDocsChronos\").");
        Assert.True(Directory.Exists(racine), $"Dossier docs/ introuvable : {racine}");
        Assert.True(File.Exists(CheminDocument()),
            $"VAL-02 exige docs/desktop-app-sessions.md : introuvable ({CheminDocument()}).");

        var lignes = File.ReadAllText(CheminDocument()).Replace("\r\n", "\n").Split('\n').Length;
        Assert.True(lignes >= LignesMinimum,
            $"docs/desktop-app-sessions.md ne fait que {lignes} lignes (minimum {LignesMinimum}) : un document tronqué "
            + "rendrait toutes les assertions de cette classe vertes, faute de matière.");
    }

    [Fact]
    public void La_table_documentee_liste_EXACTEMENT_les_champs_lus_par_le_lecteur()
    {
        var table = ContratHooksDocumenteTests.TableEntre(LireDocument(), ChampsDebut, ChampsFin);
        var lus = ChampsLusParLeLecteur();

        var documentes = new HashSet<string>(table.Select(l => l[0]), StringComparer.Ordinal);
        AssertMemesNoms(lus, documentes, "les champs");

        // Aucun doublon : deux lignes pour un même champ passeraient l'égalité d'ensembles.
        Assert.Equal(lus.Count, table.Count);
    }

    [Fact]
    public void Les_categories_documentees_sont_celles_que_le_lecteur_reconnait()
    {
        var table = ContratHooksDocumenteTests.TableEntre(LireDocument(), CategoriesDebut, CategoriesFin);
        var reconnues = CategoriesReconnues();

        var documentees = new HashSet<string>(table.Select(l => l[0]), StringComparer.Ordinal);
        AssertMemesNoms(reconnues, documentees, "les catégories de fin de tour");

        Assert.Equal(reconnues.Count, table.Count);
    }

    /// <summary>OÙ le lecteur lit, et COMMENT il ouvre : chaque affirmation du document a son siège dans le code. Le
    /// mot « jonction » du libellé de VAL-02 est écrit NIÉ (D-31-01) : c'est la virtualisation d'AppData du paquet
    /// MSIX. Et le document ne porte aucun chemin de profil réel.</summary>
    [Fact]
    public void Le_document_dit_ou_il_lit_et_le_resolveur_construit_ce_chemin()
    {
        var texte = LireDocument();

        foreach (var fragment in new[]
                 {
                     @"LocalCache\Roaming\Claude\claude-code-sessions", @"%APPDATA%\Claude\claude-code-sessions",
                     "n'est pas une jonction", "`RacinesEtat`", "`HorizonsSessions.LectureAppBureau`",
                     "FileShare.ReadWrite | FileShare.Delete",
                 })
            Assert.Contains(fragment, texte, StringComparison.Ordinal);

        Assert.DoesNotContain(@"C:\Users\", texte, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:/Users/", texte, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("\"LocalCache\", \"Roaming\", \"Claude\", \"claude-code-sessions\"",
            LireSource("Services", "RacinesEtat.cs"), StringComparison.Ordinal);
        Assert.Contains("FileShare.ReadWrite | FileShare.Delete",
            LireSource("Services", "LecteurAppBureau.cs"), StringComparison.Ordinal);
        Assert.Equal(TimeSpan.FromHours(24), HorizonsSessions.LectureAppBureau);
    }

    /// <summary>La section la plus importante du document : ce qui n'est PAS garanti, DATÉ. La date doit être dans la
    /// section elle-même — une date posée en tête ne date pas les trous.</summary>
    [Fact]
    public void Le_document_porte_ce_qui_n_est_pas_garanti_avec_sa_date()
    {
        var texte = LireDocument();
        Assert.Contains("Relevé le " + DateDuReleve, texte, StringComparison.Ordinal);

        var section = ContratHooksDocumenteTests.SectionDe(texte, TitreNonGaranti);
        var lignes = section.Split('\n').Length;
        Assert.True(lignes >= LignesMinimumNonGaranti,
            $"La section « {TitreNonGaranti} » ne fait que {lignes} lignes (minimum {LignesMinimumNonGaranti}) : "
            + "trop courte pour porter ce qu'elle annonce.");

        var manquants = MarqueursNonGaranti
            .Where(m => section.IndexOf(m, StringComparison.Ordinal) < 0)
            .ToList();
        Assert.True(manquants.Count == 0,
            $"La section « {TitreNonGaranti} » doit porter chaque trou daté de la source app-bureau. Manquants : "
            + string.Join(" | ", manquants));
    }

    /// <summary>
    /// La règle « lue » n'est écrite qu'UNE fois (D-31-02) : au §3 du contrat des hooks, sous sa propre garde. Le contrat
    /// de l'app y renvoie sans la recopier ; et les trois documents voisins — le §3 des hooks, les sources d'usage, le
    /// README — renvoient au contrat de l'app : un document que rien ne cite n'est lu par personne.
    ///
    /// <para>Le renvoi du contrat des hooks est cherché DANS son §3 : l'introduction et le §9 le citent aussi, et une garde
    /// qui lirait le document entier resterait verte si le §3 le perdait.</para>
    /// </summary>
    [Fact]
    public void Les_documents_voisins_renvoient_au_contrat_de_l_app_et_la_regle_n_est_ecrite_qu_une_fois()
    {
        var texte = LireDocument();
        var docs = CheminDocs();

        var hooks = LireVoisin("docs/hooks-contract.md", Path.Combine(docs, "hooks-contract.md"));
        Assert.Contains(NomDocument, ContratHooksDocumenteTests.SectionDe(hooks, "## 3."), StringComparison.Ordinal);

        var sources = LireVoisin("docs/data-sources.md", Path.Combine(docs, "data-sources.md"));
        Assert.Contains(NomDocument, ContratHooksDocumenteTests.SectionDe(sources, "## 6."), StringComparison.Ordinal);

        var readme = LireVoisin("README.md", Path.GetFullPath(Path.Combine(docs, "..", "README.md")));
        Assert.Contains("## Widget de sessions Claude Code", readme, StringComparison.Ordinal);
        Assert.Contains(NomDocument, readme, StringComparison.Ordinal);

        Assert.Contains("hooks-contract.md", texte, StringComparison.Ordinal);
        Assert.Contains("§3", texte, StringComparison.Ordinal);
        Assert.False(texte.Contains("HorizonsSessions.GraceLecture", StringComparison.Ordinal),
            "docs/desktop-app-sessions.md cite HorizonsSessions.GraceLecture : la règle « lue » vit au §3 du contrat des "
            + "hooks ; deux copies dériveraient.");
    }

    /// <summary>Lit un document voisin. ANTI-MUET : présent et non vide.</summary>
    private static string LireVoisin(string nom, string chemin)
    {
        Assert.True(File.Exists(chemin), $"{nom} introuvable : {chemin}");
        var texte = File.ReadAllText(chemin);
        Assert.False(string.IsNullOrWhiteSpace(texte), $"{nom} est vide : {chemin}");
        return texte;
    }
}
