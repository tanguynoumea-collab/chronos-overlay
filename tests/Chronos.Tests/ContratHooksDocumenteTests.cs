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
    private const string EtatsDebut = "ETATS-AFFICHES:debut";
    private const string EtatsFin = "ETATS-AFFICHES:fin";
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
    private static IReadOnlyList<string[]> TableDocumentee(string texte) => TableEntre(texte, MarqueurDebut, MarqueurFin);

    /// <summary>Les lignes de DONNÉES d'une table balisée par deux marqueurs (§1 : événements câblés ; §3 :
    /// états affichés), en-tête et ligne de séparation retirées. Backticks retirés de chaque cellule.</summary>
    private static IReadOnlyList<string[]> TableEntre(string texte, string marqueurDebut, string marqueurFin)
    {
        var debut = texte.IndexOf(marqueurDebut, StringComparison.Ordinal);
        var fin = texte.IndexOf(marqueurFin, StringComparison.Ordinal);
        Assert.True(debut >= 0, $"Marqueur « {marqueurDebut} » absent du document.");
        Assert.True(fin > debut, $"Marqueur « {marqueurFin} » absent, ou posé avant le marqueur d'ouverture.");

        var bloc = texte.Substring(debut + marqueurDebut.Length, fin - debut - marqueurDebut.Length);

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
            $"La table balisée par « {marqueurDebut} » ne porte pas même un en-tête et une ligne : un "
            + "document tronqué rendrait toutes les comparaisons vertes, faute de matière à comparer.");

        return lignes.Skip(1).ToList();   // la première ligne retenue est l'EN-TÊTE
    }

    /// <summary>Le texte de la seule section §5 (« ce qui n'est pas garanti »), de son titre au titre de
    /// niveau deux suivant.</summary>
    private static string SectionNonGarantie(string texte) => SectionDe(texte, TitreNonGaranti);

    /// <summary>Le texte d'UNE section de niveau deux, de son titre (repéré par son préfixe, p. ex.
    /// « ## 3. ») au titre de niveau deux suivant.</summary>
    private static string SectionDe(string texte, string titre)
    {
        var lignes = texte.Replace("\r\n", "\n").Split('\n');

        var debut = Array.FindIndex(lignes, l => l.StartsWith(titre, StringComparison.Ordinal));
        Assert.True(debut >= 0, $"Section « {titre} » introuvable dans le document.");

        var fin = Array.FindIndex(lignes, debut + 1, l => l.StartsWith("## ", StringComparison.Ordinal));
        if (fin < 0) fin = lignes.Length;

        return string.Join("\n", lignes.Skip(debut).Take(fin - debut));
    }

    private static string[] LigneDe(IReadOnlyList<string[]> table, string cle)
    {
        var ligne = table.FirstOrDefault(l => string.Equals(l[0], cle, StringComparison.Ordinal));
        Assert.True(ligne is not null, $"Aucune ligne documentée pour « {cle} ».");
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

    /// <summary>
    /// GARDE DE NON-DÉRIVE (phase 26). Le §3 portait, en toutes lettres, une divergence connue LAISSÉE
    /// OUVERTE et léguée à la phase 26 : le détecteur d'hystérésis ne reconnaissait pas l'attente déduite.
    /// Les plans 26-01 à 26-03 l'ont refermée. Un document qui annonce encore un défaut corrigé est aussi
    /// faux qu'un document qui tait un défaut réel — dans les deux cas, il fait croire qu'on sait.
    ///
    /// <para>Les deux assertions ANTI-MUETTES viennent d'abord : sans elles, « la chaîne est absente »
    /// serait vraie pour la pire des raisons — un document vidé ou tronqué.</para>
    /// </summary>
    [Fact]
    public void Le_document_ne_transmet_plus_de_divergence_a_la_phase_26()
    {
        var texte = LireDocument();
        var lignes = texte.Replace("\r\n", "\n").Split('\n').Length;

        Assert.True(lignes >= LignesMinimum,
            $"docs/hooks-contract.md ne fait que {lignes} lignes (minimum {LignesMinimum}) : un document "
            + "vidé rendrait cette garde verte pour la pire des raisons.");
        Assert.Contains("## 3.", texte, StringComparison.Ordinal);

        Assert.False(texte.Contains("transmise à la phase 26", StringComparison.Ordinal),
            "Le §3 annonce encore une divergence TRANSMISE à la phase 26. Elle y a été refermée dès le "
            + "plan 26-01 : le détecteur reconnaît l'attente déduite. Annoncer un défaut corrigé, c'est "
            + "décrire un câblage qui n'existe plus — la faute même que ce document existe pour empêcher.");
        Assert.False(texte.Contains("NON corrigée ici", StringComparison.Ordinal),
            "Le §3 annonce encore un défaut NON corrigé dans le document. Il l'est.");

        Assert.Contains("TRT-01", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE CROISÉE DOCUMENT ↔ CODE (phase 26). Les trois affirmations du §3 sur la règle de traitement
    /// doivent avoir chacune leur contrepartie dans <c>SessionTreatmentTracker</c>. Ce que la garde du §1
    /// fait pour la table des événements, celle-ci le fait pour la règle qui décide ce que « traité »
    /// veut dire — et c'est la règle la plus coûteuse du projet quand elle ment : une session réellement
    /// en attente disparaît de l'écran sans que rien ne le dise.
    ///
    /// <para><b>Pourquoi la chaîne cherchée porte des capitales.</b> Le §5 emploie déjà deux fois les mots
    /// « même source », dans un sens sans aucun rapport (le relevé de la référence officielle, lu deux
    /// fois à la même source). Une garde qui se contenterait de ces deux mots aurait été VERTE avant même
    /// que le §3 ne soit réécrit — c'est-à-dire vacueuse. Elle porte donc sur la formulation exacte de la
    /// règle, et le compte des deux occurrences du §5 reste inchangé.</para>
    /// </summary>
    [Fact]
    public void Le_document_dit_la_regle_de_traitement_reellement_cablee()
    {
        var texte = LireDocument();

        var racineSources = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racineSources),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : sans lui, cette garde croisée "
            + "ne lit pas le détecteur et ne croise donc rien.");

        var fichier = Path.Combine(racineSources, "Services", "SessionTreatmentTracker.cs");
        Assert.True(File.Exists(fichier), $"Détecteur introuvable : {fichier}");

        var code = File.ReadAllText(fichier);
        Assert.False(string.IsNullOrWhiteSpace(code), $"Détecteur vide : {fichier}");
        Assert.Contains("public void Observe(", code, StringComparison.Ordinal);

        // 1. La transition doit être observée sur la MÊME source — le document l'écrit, le code l'applique.
        Assert.Contains("transition observée sur la MÊME source", texte, StringComparison.Ordinal);
        Assert.Contains("prec.Source == v.Source", code, StringComparison.Ordinal);

        // 2. L'attente déduite EST une attente. Le prédicat est DÉCOUPÉ avant d'être lu : le nom de l'état
        //    figure aussi dans les commentaires du fichier, et un commentaire ne câble rien.
        Assert.Contains("`WaitingDeduced` est une attente pour le détecteur", texte, StringComparison.Ordinal);

        var debutPredicat = code.IndexOf("EstAttente(SessionActivity", StringComparison.Ordinal);
        Assert.True(debutPredicat >= 0,
            "Le prédicat EstAttente a disparu du détecteur : le document décrirait alors une règle qui "
            + "n'a plus de siège dans le code.");
        var finPredicat = code.IndexOf(';', debutPredicat);
        Assert.True(finPredicat > debutPredicat, "Le corps du prédicat EstAttente est illisible.");
        var predicat = code.Substring(debutPredicat, finPredicat - debutPredicat);

        Assert.True(predicat.Contains("WaitingDeduced", StringComparison.Ordinal),
            "Le §3 écrit que l'attente déduite est une attente pour le détecteur, et le prédicat "
            + "EstAttente ne la reconnaît pas. C'est exactement la divergence que la phase 26 a refermée : "
            + "l'exclure fait lire « l'attente est devenue déduite » comme « l'utilisateur a répondu », "
            + "c'est-à-dire masquer une session qui attend. Prédicat lu : " + predicat);

        // 3. L'épisode d'attente est daté par l'instant que le SIGNAL porte — document ET code.
        Assert.Contains("daté par l'instant que le SIGNAL porte", texte, StringComparison.Ordinal);
        Assert.Contains("InstantDuSignal", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE CROISÉE DOCUMENT ↔ CODE (R3, cran 1). Le §5 porte désormais une règle LIVRÉE — l'écriture
    /// d'état est monotone — et un chemin LAISSÉ OUVERT. Les deux doivent rester vrais ensemble.
    ///
    /// <para><b>Le piège que cette garde vise est asymétrique.</b> Qu'on retire la monotonie du code sans
    /// toucher au document, et le §5 annoncerait une protection qui n'existe plus. Qu'on livre un jour le
    /// second cran sans toucher au document, et le §5 réclamerait encore une observation déjà faite. Dans
    /// les deux cas le document ferait croire qu'on sait — la faute exacte que ce fichier existe pour
    /// empêcher.</para>
    ///
    /// <para>Elle ne lit que le dépôt : aucun réseau, aucun <c>%APPDATA%</c>, aucune horloge.</para>
    /// </summary>
    [Fact]
    public void Le_document_dit_la_monotonie_livree_ET_le_chemin_de_R3_reste_ouvert()
    {
        var section = SectionNonGarantie(LireDocument());

        // ANTI-MUET d'abord : une section vidée rendrait tout le reste vert faute de matière.
        Assert.True(section.Replace("\r\n", "\n").Split('\n').Length >= 40,
            "La section « " + TitreNonGaranti + " » est trop courte pour porter ce qu'elle annonce.");

        // 1. La règle LIVRÉE — et sa contrepartie dans le code, qui est la comparaison elle-même.
        Assert.Contains("FUS-01 vaut aussi à l'INTÉRIEUR d'une source", section, StringComparison.Ordinal);

        var racineSources = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racineSources),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : sans lui, cette garde croisée "
            + "ne lit pas l'écriture et ne croise donc rien.");

        var fichier = Path.Combine(racineSources, "Services", "EcritureEtatSession.cs");
        Assert.True(File.Exists(fichier), $"Écriture d'état introuvable : {fichier}");
        var code = File.ReadAllText(fichier);

        Assert.Contains("neuf < present", code, StringComparison.Ordinal);   // STRICTEMENT antérieure
        Assert.Contains("IgnoreeCarPerimee", code, StringComparison.Ordinal); // un refus qui n'est pas un échec

        // 2. Le chemin PRINCIPAL de R3 reste ouvert, et la raison de ne pas l'avoir refermé est écrite :
        //    sans elle, un successeur livrerait le second cran en croyant combler un oubli.
        Assert.Contains("n'est pas livré", section, StringComparison.Ordinal);
        Assert.Contains("aucun `UserPromptSubmit` ne survient", section, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE CROISÉE DOCUMENT ↔ CODE (LIB-04, réserve R4 de l'audit v1.6). Depuis la phase 28 il y a DEUX
    /// ordres : l'ordre d'écran, où une attente déduite passe devant un travail, et le rang d'arbitrage, figé
    /// aux valeurs de la phase 24, où une déduction ne bat jamais une observation. Le §3 doit dire les deux ;
    /// l'ancienne phrase (« derrière `Working` (2) ») disait un seul ordre pour deux usages, et elle serait
    /// désormais FAUSSE pour l'écran.
    ///
    /// <para>Les trois fragments cherchés tiennent chacun sur une seule ligne du document, sans mise en forme
    /// au milieu : la garde lit le texte brut. Et le rang d'arbitrage doit exister dans le code sous le nom
    /// que le document lui donne — sinon le document décrirait un siège vide.</para>
    /// </summary>
    [Fact]
    public void Le_paragraphe_3_dit_l_ordre_d_ecran_et_le_rang_d_arbitrage_fige()
    {
        var texte = LireDocument();
        var section = SectionDe(texte, "## 3.");

        Assert.Contains("`RangArbitrage`", section, StringComparison.Ordinal);
        Assert.Contains("ordre d'écran", section, StringComparison.Ordinal);
        Assert.Contains("une déduction ne bat jamais une observation", section, StringComparison.Ordinal);
        Assert.DoesNotContain("derrière `Working` (2)", texte, StringComparison.Ordinal);

        var racineSources = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racineSources),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : sans lui, cette garde croisée "
            + "ne lit pas l'arbitrage et ne croise donc rien.");

        var fichier = Path.Combine(racineSources, "Services", "ArbitrageSessions.cs");
        Assert.True(File.Exists(fichier), $"Arbitrage introuvable : {fichier}");
        Assert.Contains("private static int RangArbitrage(SessionActivity", File.ReadAllText(fichier),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE CROISÉE DOCUMENT ↔ PRODUCTEUR (LIB-01, LIB-03). La table du §3 dit, pour chaque valeur de
    /// <c>SessionActivity</c>, le mot que l'utilisateur lit. Ce mot a UN producteur,
    /// <c>AffichageSessions.Etat</c>, partagé par le widget et le rapport ; si le document cessait de
    /// l'égaler, il décrirait un écran qui n'existe pas — ce que la garde du §1 empêche déjà pour les rôles
    /// des hooks, celle-ci l'empêche pour les mots.
    ///
    /// <para>Exactement cinq lignes, une par valeur : une ligne en trop ou en moins passerait sinon la
    /// comparaison cellule à cellule. L'état indéterminé n'a pas de mot à l'écran : sa cellule le dit.</para>
    /// </summary>
    [Fact]
    public void Le_paragraphe_3_affiche_les_libelles_du_producteur()
    {
        var texte = LireDocument();
        Assert.Contains(EtatsDebut, SectionDe(texte, "## 3."), StringComparison.Ordinal);

        var table = TableEntre(texte, EtatsDebut, EtatsFin);
        var valeurs = Enum.GetValues<SessionActivity>();
        Assert.Equal(5, valeurs.Length);   // garde anti-muette
        Assert.Equal(valeurs.Length, table.Count);

        foreach (var a in valeurs)
        {
            var ligne = LigneDe(table, a.ToString());
            Assert.True(ligne.Length >= 2, $"Ligne « {a} » du §3 : moins de deux colonnes.");

            if (AffichageSessions.AUneLigne(a))
                Assert.True(string.Equals(ligne[1], AffichageSessions.Etat(a), StringComparison.Ordinal),
                    $"« {a} » : le libellé documenté au §3 n'est plus celui que le producteur affiche.\n"
                    + $"  producteur : {AffichageSessions.Etat(a)}\n"
                    + $"  documenté  : {ligne[1]}");
            else
                Assert.Contains("aucune ligne", ligne[1], StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// GARDE CROISÉE DOCUMENT ↔ CODE (SIL-01, décision D-28-01). Le §3 nomme les quatre horizons par leur siège
    /// unique, <c>HorizonsSessions</c>, dit que la règle de silence est appliquée en UN point
    /// (<c>AppliquerSilence</c>) à toutes les sources, et consigne qu'un transcript est daté par son dernier
    /// message. Les anciens noms privés ne peuvent plus y survivre : ils désigneraient des constantes qui
    /// n'existent plus — un document qui décrit un câblage disparu.
    ///
    /// <para>Chaque fragment cherché tient sur UNE ligne du document, sans mise en forme au milieu : la garde lit
    /// le texte brut. Et chaque siège nommé doit exister dans le code sous ce nom.</para>
    /// </summary>
    [Fact]
    public void Le_contrat_nomme_les_horizons_du_type_unique_et_date_le_transcript_par_son_message()
    {
        var texte = LireDocument();

        foreach (var fragment in new[]
                 {
                     "HorizonsSessions.Silence", "HorizonsSessions.Abandon", "HorizonsSessions.RetentionTraitees",
                     "HorizonsSessions.ExpirationEtat", "`AppliquerSilence`", "Un transcript est daté par son dernier MESSAGE",
                 })
            Assert.Contains(fragment, texte, StringComparison.Ordinal);

        Assert.DoesNotContain("`DropAfter`", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("`SilenceDesBattements`", texte, StringComparison.Ordinal);

        var racineSources = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racineSources),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : sans lui, cette garde croisée "
            + "ne lit pas le code et ne croise donc rien.");

        var services = Path.Combine(racineSources, "Services");
        Assert.True(File.Exists(Path.Combine(services, "HorizonsSessions.cs")),
            "Le document nomme HorizonsSessions, et le type n'existe plus : il décrirait un siège vide.");

        var source = Path.Combine(services, "TranscriptSessionSource.cs");
        Assert.True(File.Exists(source), $"Source transcripts introuvable : {source}");
        Assert.Contains("UsageNormalization.InstantDepuisIso(", File.ReadAllText(source), StringComparison.Ordinal);

        var moniteur = Path.Combine(services, "SessionMonitor.cs");
        Assert.True(File.Exists(moniteur), $"Moniteur introuvable : {moniteur}");
        Assert.Contains("private static SessionSnapshot AppliquerSilence(", File.ReadAllText(moniteur),
            StringComparison.Ordinal);
    }
}
