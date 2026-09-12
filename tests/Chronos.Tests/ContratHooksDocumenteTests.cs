using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// LA GARDE DE NON-DÉRIVE du contrat des hooks (EVT-05) : le document <c>docs/hooks-contract.md</c> et le
/// câblage réellement installé ne peuvent plus diverger en silence.
///
/// <para><b>Pourquoi elle existe.</b> Un document qui décrit un câblage qu'il ne décrit plus est PIRE que
/// pas de document : il fait croire qu'on sait. C'est exactement le mécanisme de la panne que la phase 25
/// répare — une sémantique de source devenue fausse, que rien ne contredisait, et qui a donc survécu des
/// mois. Écrire le contrat sans le tenir, ce serait reproduire la faute une octave plus bas.</para>
///
/// <para><b>Ce que cette garde NE PEUT PAS faire.</b> Elle ne détecte pas une dérive de la source EXTERNE :
/// si Claude Code renomme un événement ou change la sémantique de <c>Stop</c>, tous ces tests restent
/// verts et le document devient faux en silence. Seule une relecture humaine de la référence officielle
/// le verrait — d'où la DATE du relevé en tête du document, et la procédure du §9. Cette garde tient la
/// moitié qu'une machine peut tenir : que ce que nous ÉCRIVONS corresponde à ce que nous INSTALLONS.</para>
///
/// <para><b>La troisième colonne est la plus importante.</b> Comparer le nom et le <c>matcher</c> ne
/// suffit pas : la colonne qui porte le SENS serait alors la seule à pouvoir mentir. On vérifierait le
/// filtre, et on laisserait la description dériver — c'est-à-dire précisément ce qu'un lecteur humain
/// lit en premier.</para>
///
/// <para>Ces tests ne lisent que des fichiers du dépôt : aucun réseau, aucun <c>%APPDATA%</c>, aucun
/// <c>~/.claude</c>, aucun jeton, aucune horloge.</para>
/// </summary>
public sealed class ContratHooksDocumenteTests
{
    private const string MarqueurDebut = "EVENEMENTS-CABLES:debut";
    private const string MarqueurFin = "EVENEMENTS-CABLES:fin";
    private const string DateDuReleve = "2026-09-12";
    private const string TitreNonGaranti = "## 5.";
    private const int LignesMinimum = 90;

    // Les trois TROUS DOCUMENTAIRES, repérés par un marqueur textuel chacun. Ce sont les trois questions
    // auxquelles la référence officielle ne répond pas ; les taire redonnerait au document l'air de
    // certitude qui a coûté cher.
    private static readonly (string Marqueur, string Trou)[] TrousDocumentaires =
    {
        ("Échap",             "l'interruption au clavier : aucun des 33 événements ne la couvre"),
        ("prompt_input_exit", "SessionEnd sur terminal tué / crash / redémarrage : non documenté"),
        ("liste blanche",     "le sort d'un nom d'événement inconnu : non documenté"),
    };

    // La barre verticale sépare les cellules d'une table Markdown : une barre qui appartient à la VALEUR
    // (le matcher de Notification en porte deux) s'y écrit échappée. On la met à l'abri avant de découper,
    // et on la rend à la cellule ensuite — sinon le matcher se briserait en trois fausses colonnes.
    private const char SentinelleBarre = (char)1;

    /// <summary>Le chemin du dossier docs/ est INJECTÉ par MSBuild, jamais deviné. Motif recopié de
    /// <c>GardesPerimetreTests.CheminSources()</c> : CLAUDE.md interdit de localiser un assembly par son
    /// chemin de fichier (vide en publication mono-fichier), et une remontée de dossiers depuis
    /// <c>AppContext.BaseDirectory</c> casserait en silence au premier changement d'agencement.</summary>
    private static string CheminDocs()
        => typeof(ContratHooksDocumenteTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminDocsChronos")?.Value
           ?? "";

    private static string CheminDocument() => Path.Combine(CheminDocs(), "hooks-contract.md");

    /// <summary>Lit le document. AUCUNE mise en sourdine : une garde qui se désarme quand son chemin
    /// manque ne garde rien — elle rendrait toutes les assertions vertes le jour où elle cesse de
    /// trouver le fichier.</summary>
    private static string LireDocument()
    {
        var racine = CheminDocs();
        Assert.False(string.IsNullOrWhiteSpace(racine),
            "L'attribut AssemblyMetadata(\"CheminDocsChronos\") manque : le .csproj de tests doit injecter "
            + "le chemin de docs/, sans quoi cette garde ne lit rien et ne garde rien.");

        var chemin = CheminDocument();
        Assert.True(File.Exists(chemin),
            $"Le contrat des hooks est introuvable : {chemin}. EVT-05 exige qu'il existe ; c'est le seul "
            + "critère de la phase 25 qui rende la PROCHAINE dérive détectable.");

        return File.ReadAllText(chemin);
    }

    /// <summary>Les lignes de DONNÉES de la table du §1, en-tête et ligne de séparation retirées.</summary>
    private static IReadOnlyList<string[]> TableDocumentee(string texte)
    {
        var debut = texte.IndexOf(MarqueurDebut, StringComparison.Ordinal);
        var fin = texte.IndexOf(MarqueurFin, StringComparison.Ordinal);
        Assert.True(debut >= 0, $"Marqueur « {MarqueurDebut} » absent du document.");
        Assert.True(fin > debut, $"Marqueur « {MarqueurFin} » absent, ou posé avant le marqueur d'ouverture.");

        var bloc = texte.Substring(debut + MarqueurDebut.Length, fin - debut - MarqueurDebut.Length);

        var lignes = new List<string[]>();
        foreach (var brute in bloc.Replace("\r\n", "\n").Split('\n'))
        {
            var ligne = brute.Trim();
            if (!ligne.StartsWith("|", StringComparison.Ordinal)) continue;

            var cellules = ligne
                .Replace("\\|", SentinelleBarre.ToString())
                .Trim('|')
                .Split('|')
                .Select(c => c.Replace(SentinelleBarre, '|').Trim().Trim('`').Trim())
                .ToArray();

            // La ligne de séparation Markdown (« |---|---| ») n'est pas une donnée.
            if (cellules.All(c => c.Length > 0 && c.All(ch => ch == '-' || ch == ':'))) continue;

            lignes.Add(cellules);
        }

        Assert.True(lignes.Count >= 2,
            "La table du §1 ne porte pas même un en-tête et une ligne : un document tronqué rendrait "
            + "toutes les comparaisons vertes, faute de matière à comparer.");

        return lignes.Skip(1).ToList();   // la première ligne retenue est l'EN-TÊTE
    }

    /// <summary>Le texte de la seule section §5 (« ce qui n'est pas garanti »), de son titre au titre de
    /// niveau deux suivant.</summary>
    private static string SectionNonGarantie(string texte)
    {
        var lignes = texte.Replace("\r\n", "\n").Split('\n');

        var debut = Array.FindIndex(lignes, l => l.StartsWith(TitreNonGaranti, StringComparison.Ordinal));
        Assert.True(debut >= 0, $"Section « {TitreNonGaranti} » introuvable dans le document.");

        var fin = Array.FindIndex(lignes, debut + 1, l => l.StartsWith("## ", StringComparison.Ordinal));
        if (fin < 0) fin = lignes.Length;

        return string.Join("\n", lignes.Skip(debut).Take(fin - debut));
    }

    private static string[] LigneDe(IReadOnlyList<string[]> table, string evenement)
    {
        var ligne = table.FirstOrDefault(l => string.Equals(l[0], evenement, StringComparison.Ordinal));
        Assert.True(ligne is not null, $"Aucune ligne documentée pour l'événement câblé « {evenement} ».");
        return ligne!;
    }

    [Fact]
    public void Le_chemin_du_document_est_injecte_et_le_fichier_existe()
    {
        var racine = CheminDocs();

        Assert.False(string.IsNullOrWhiteSpace(racine),
            "Le chemin de docs/ doit être injecté par MSBuild (AssemblyMetadata \"CheminDocsChronos\").");
        Assert.True(Directory.Exists(racine), $"Dossier docs/ introuvable : {racine}");
        Assert.True(File.Exists(CheminDocument()),
            $"docs/hooks-contract.md introuvable : {CheminDocument()}");
    }

    [Fact]
    public void La_table_documentee_liste_EXACTEMENT_les_evenements_cables()
    {
        var table = TableDocumentee(LireDocument());

        var documentes = new HashSet<string>(table.Select(l => l[0]), StringComparer.Ordinal);
        var cables = new HashSet<string>(
            SessionHookInstaller.Cablage.Select(c => c.Evenement), StringComparer.Ordinal);

        var manquants = cables.Except(documentes, StringComparer.Ordinal)
                              .OrderBy(n => n, StringComparer.Ordinal).ToList();
        var surnumeraires = documentes.Except(cables, StringComparer.Ordinal)
                                      .OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(manquants.Count == 0 && surnumeraires.Count == 0,
            "Le §1 de docs/hooks-contract.md ne décrit plus le câblage réel.\n"
            + "  CÂBLÉS mais NON DOCUMENTÉS : "
            + (manquants.Count == 0 ? "(aucun)" : string.Join(", ", manquants)) + "\n"
            + "  DOCUMENTÉS mais NON CÂBLÉS : "
            + (surnumeraires.Count == 0 ? "(aucun)" : string.Join(", ", surnumeraires)));

        // Aucun doublon non plus : deux lignes pour un même événement passeraient l'égalité d'ensembles.
        Assert.Equal(SessionHookInstaller.Cablage.Length, table.Count);
    }

    [Fact]
    public void Chaque_ligne_documentee_porte_le_matcher_reellement_installe()
    {
        var table = TableDocumentee(LireDocument());

        foreach (var c in SessionHookInstaller.Cablage)
        {
            var ligne = LigneDe(table, c.Evenement);
            Assert.True(ligne.Length >= 3, $"Ligne « {c.Evenement} » : moins de trois colonnes.");

            var attendu = c.Matcher ?? "(aucun)";
            Assert.True(string.Equals(ligne[1], attendu, StringComparison.Ordinal),
                $"« {c.Evenement} » : le filtre documenté ne correspond pas à celui qui est installé.\n"
                + $"  installé   : {attendu}\n"
                + $"  documenté  : {ligne[1]}");
        }
    }

    [Fact]
    public void Chaque_ligne_documentee_porte_le_role_reellement_cable()
    {
        var table = TableDocumentee(LireDocument());

        foreach (var c in SessionHookInstaller.Cablage)
        {
            var ligne = LigneDe(table, c.Evenement);
            Assert.True(ligne.Length >= 3, $"Ligne « {c.Evenement} » : moins de trois colonnes.");

            Assert.True(string.Equals(ligne[2], c.Role, StringComparison.Ordinal),
                $"« {c.Evenement} » : le RÔLE documenté ne correspond plus au câblage. C'est la colonne "
                + "que lit un humain, et la seule qui puisse mentir sans qu'un nom ni un filtre bouge.\n"
                + $"  câblé      : {c.Role}\n"
                + $"  documenté  : {ligne[2]}");
        }
    }

    [Fact]
    public void Le_document_porte_les_trente_trois_noms_du_catalogue()
    {
        var texte = LireDocument();

        var absents = CatalogueEvenementsHooks.Tous
            .Select(e => e.Nom)
            .Where(n => texte.IndexOf(n, StringComparison.Ordinal) < 0)
            .ToList();

        Assert.True(absents.Count == 0,
            "Le §8 doit porter le catalogue COMPLET : la liste blanche est la parade au seul mode de "
            + "défaillance non documenté (un hook mort et muet). Noms absents du document : "
            + string.Join(", ", absents));
    }

    [Fact]
    public void Le_document_porte_les_trois_trous_documentaires_avec_leur_date()
    {
        var section = SectionNonGarantie(LireDocument());

        var oublies = TrousDocumentaires
            .Where(t => section.IndexOf(t.Marqueur, StringComparison.Ordinal) < 0)
            .Select(t => $"{t.Marqueur} ({t.Trou})")
            .ToList();

        Assert.True(oublies.Count == 0,
            "Le §5 doit porter les TROIS trous documentaires — c'est la section qui rend la prochaine "
            + "dérive détectable. Manquants : " + string.Join(" | ", oublies));

        // La date doit être DANS la section, pas seulement quelque part dans le fichier : une date posée
        // en tête ne DATE pas les trous, et c'est précisément ce qu'on veut pouvoir relire dans six mois.
        Assert.True(section.IndexOf(DateDuReleve, StringComparison.Ordinal) >= 0,
            $"La date du relevé ({DateDuReleve}) n'apparaît pas dans la section « {TitreNonGaranti} » "
            + "elle-même. Un trou documentaire non daté ne se compare à rien.");
    }

    [Fact]
    public void Le_document_lu_n_est_ni_tronque_ni_vide()
    {
        var texte = LireDocument();
        var lignes = texte.Replace("\r\n", "\n").Split('\n').Length;

        Assert.True(lignes >= LignesMinimum,
            $"docs/hooks-contract.md ne fait que {lignes} lignes (minimum {LignesMinimum}). Un document "
            + "tronqué rendrait toutes les assertions de cette classe vertes, faute de matière.");

        Assert.NotEmpty(TableDocumentee(texte));
    }
}
