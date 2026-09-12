using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// DEUX GARDES DE NON-RETOUR de la phase 19, plus une preuve de comportement.
///
/// Le chemin des sources est INJECTÉ par MSBuild (attribut <c>AssemblyMetadata("CheminSourcesChronos")</c>
/// posé dans le .csproj de tests) et jamais deviné : CLAUDE.md proscrit de localiser un assembly par son
/// chemin de fichier — cette propriété est VIDE en publication mono-fichier — et une remontée de dossiers
/// depuis <c>AppContext.BaseDirectory</c> casserait en silence au premier changement d'agencement de
/// sortie. Motif recopié de <c>NormalisationUniqueTests</c>.
///
/// Ces tests ne lisent que des fichiers .cs du dépôt : aucun réseau, aucun %APPDATA%, aucun jeton.
/// </summary>
public class GardesDoctrineTests
{
    /// <summary>
    /// GARDE 1 — le piège de la recomposition cesse d'être silencieux.
    ///
    /// <c>CompositeUsageProvider.GetAsync</c> reconstruit <see cref="UsageSnapshot"/> par
    /// <c>new UsageSnapshot { … }</c> et non par <c>with</c>. Toute propriété non nommée dans ce « new »
    /// est donc DÉTRUITE à chaque passage de la chaîne — et la chaîne réelle en compte trois imbriqués.
    /// Le <c>new</c> est le bon choix (un <c>with</c> ferait hériter silencieusement du primaire tout
    /// champ futur, c'est-à-dire produire une valeur FAUSSE là où le <c>new</c> produit un <c>null</c>) ;
    /// ce qui manquait était une garde. La voici.
    ///
    /// Seules les propriétés d'INSTANCE sont exigées : <c>Empty</c> est une fabrique statique, pas un
    /// champ d'état, et n'a rien à faire dans une recomposition.
    /// </summary>
    [Fact]
    public void Toute_propriete_de_UsageSnapshot_est_nommee_dans_la_recomposition_du_composite()
    {
        var fichier = Path.Combine(CheminSources(), "Services", "CompositeUsageProvider.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        var proprietes = typeof(UsageSnapshot)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        // Un record dont on ne verrait aucune propriété rendrait la garde muette.
        Assert.True(proprietes.Count >= 3, $"Seulement {proprietes.Count} propriétés vues sur UsageSnapshot.");

        var manquantes = proprietes.Where(n => !texte.Contains(n, StringComparison.Ordinal)).ToList();

        Assert.True(manquantes.Count == 0,
            "CompositeUsageProvider.GetAsync reconstruit UsageSnapshot par « new » : toute propriété qui "
            + "n'y est pas nommée est détruite EN SILENCE à chaque passage de la chaîne (trois composites "
            + "imbriqués en production). Nommer ces propriétés dans le « new », ou — si le champ est posé "
            + "AU-DESSUS du composite par la couche de doctrine — le dire en commentaire dans ce fichier.\n  "
            + string.Join("\n  ", manquantes));
    }

    /// <summary>
    /// GARDE 2 (EXA-04) — aucun rapport entre un comptage de tokens et un plafond ne peut réapparaître.
    ///
    /// La phase 16 a déjà interdit aux transcripts d'implémenter <c>IUsageProvider</c> ; cette garde ferme
    /// l'AUTRE voie, celle d'un taux points-par-token — qui n'est qu'un plafond auto-calibré, c'est-à-dire
    /// le calibrateur de plafonds supprimé en phase 16. Trois faits la rendent nécessaire : les limites
    /// Anthropic pondèrent par modèle ; les transcripts ignorent l'app de bureau et Cowork, qui consomment
    /// le même pool ; et la mesure du 2026-09-12 donne 643 649 933 tokens sur 5 h là où l'ancien plafond
    /// valait 230 000 000, soit 280 %.
    ///
    /// CONSIGNE DE RÉDACTION, impérative, pour tout fichier de Services/ et Models/ — y compris les
    /// commentaires : formuler toujours « un rapport entre un comptage de tokens et un plafond », et
    /// JAMAIS les deux termes accolés par un opérateur, sinon un simple commentaire déclencherait le motif.
    /// </summary>
    [Fact]
    public void Aucun_rapport_entre_un_comptage_de_tokens_et_un_plafond_dans_Services_et_Models()
    {
        const string motif =
            @"(Tokens?\w*\s*[/*]\s*\w*(Plafond|Budget|Limite|Capacite))|((Plafond|Budget)\w*\s*[/*]\s*\w*Tokens?)";

        var racine = CheminSources();

        var fichiers = new[] { "Services", "Models" }
            .Select(d => Path.Combine(racine, d))
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs"))   // dossiers plats, non récursif
            .ToList();

        // Un chemin valide pointant sur un dossier vide rendrait la garde muette.
        Assert.True(fichiers.Count >= 40,
            $"Seulement {fichiers.Count} fichiers balayés sous {racine} : la garde ne voit manifestement "
            + "pas la vraie arborescence des sources.");

        var infractions = new List<string>();

        foreach (var fichier in fichiers)
        {
            var texte = File.ReadAllText(fichier);

            foreach (Match m in Regex.Matches(texte, motif, RegexOptions.IgnoreCase))
            {
                var ligne = texte.Take(m.Index).Count(c => c == '\n') + 1;
                infractions.Add($"{Path.GetFileName(fichier)}:{ligne} — « {m.Value.Trim()} »");
            }
        }

        Assert.True(infractions.Count == 0,
            "EXA-04 : un rapport entre un comptage de tokens et un plafond est réapparu. Il n'a AUCUNE "
            + "réponse honnête : les limites pondèrent par modèle, les transcripts ne voient ni l'app de "
            + "bureau ni Cowork, et la mesure terrain le falsifie d'un facteur supérieur à 2,8. Le delta "
            + "change la NATURE du chiffre (« au moins X »), il ne s'y ajoute jamais.\n  "
            + string.Join("\n  ", infractions));
    }

    /// <summary>
    /// PREUVE DE COMPORTEMENT, dans l'esprit de la garde de position de la phase 18.
    ///
    /// L'équivalence « porte d'âge au-dessus du composite ≡ porte d'âge dans Best() » repose sur un fait
    /// d'ORDRE : la seule source à ancienneté non bornée (l'objet d'usage sur disque) est le repli le plus
    /// interne, donc elle ne peut gagner que lorsque tout ce qui est au-dessus est indisponible. Un
    /// réordonnancement futur de la chaîne casserait cette équivalence EN SILENCE. Ce test transforme la
    /// fragilité en signal : trois sources exactes aux chiffres différents, c'est la plus externe qui sort.
    /// </summary>
    [Fact]
    public async Task Le_repli_le_plus_interne_est_bien_celui_a_anciennete_non_bornee()
    {
        var externe = new FakeUsageProvider { Next = Snap(0.11) };
        var median = new FakeUsageProvider { Next = Snap(0.22) };
        var interne = new FakeUsageProvider { Next = Snap(0.33) };

        var chaine = new CompositeUsageProvider(
            externe,
            new CompositeUsageProvider(median, interne));

        var snap = await chaine.GetAsync();

        Assert.Equal(0.11, snap.FiveHour.Utilization);
        Assert.Equal(0.11, snap.SevenDay.Utilization);
    }

    // ==================== Phase 20 : le champ mort ne revient pas ====================

    /// <summary>
    /// GARDE 3, STRUCTURELLE — le champ qui portait la somme de l'estimation ABSOLUE ne peut pas
    /// réapparaître dans le modèle ni dans les services.
    ///
    /// Elle REMPLACE une garde de COMPORTEMENT (<c>CadranBindingTests</c>, phase 19) qui vérifiait que
    /// ce champ ne surfaçait plus rien au cadran. Le champ a été supprimé en phase 20, donc ce test-là
    /// ne compilait plus ; il n'a pas été retiré sans remplaçant, il a CHANGÉ DE NIVEAU. Un champ absent
    /// est une garantie plus forte qu'un champ mort surveillé : la garde de comportement laissait
    /// subsister l'emplacement, avec sa tentation de réemploi.
    ///
    /// Balayage du TEXTE source, comme les deux gardes ci-dessus, via le chemin injecté par MSBuild.
    /// Le littéral vit ICI, en chaîne de caractères, et NULLE PART dans src/Chronos : c'est ce qui rend
    /// le critère « zéro occurrence » atteignable (doctrine phase 19 — un commentaire qui reproduit
    /// l'expression fautive tue le critère de non-retour).
    /// </summary>
    [Fact]
    public void Aucun_champ_nomme_EstimatedTokens_ne_reapparait_dans_Models_ni_Services()
    {
        const string interdit = "EstimatedTokens";

        var racine = CheminSources();

        var fichiers = new[] { "Services", "Models" }
            .Select(d => Path.Combine(racine, d))
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs"))   // dossiers plats, non récursif
            .ToList();

        // Un chemin valide pointant sur un dossier vide rendrait la garde muette.
        Assert.True(fichiers.Count >= 40,
            $"Seulement {fichiers.Count} fichiers balayés sous {racine} : la garde ne voit manifestement "
            + "pas la vraie arborescence des sources.");

        var infractions = new List<string>();

        foreach (var fichier in fichiers)
        {
            var texte = File.ReadAllText(fichier);
            var index = texte.IndexOf(interdit, StringComparison.Ordinal);
            while (index >= 0)
            {
                var ligne = texte.Take(index).Count(c => c == '\n') + 1;
                infractions.Add($"{Path.GetFileName(fichier)}:{ligne}");
                index = texte.IndexOf(interdit, index + 1, StringComparison.Ordinal);
            }
        }

        Assert.True(infractions.Count == 0,
            "Ce champ portait la somme de l'estimation ABSOLUE supprimée en phase 16 : un nombre de "
            + "tokens rapporté à un plafond, c'est-à-dire un chiffre que la mesure terrain falsifie d'un "
            + "facteur supérieur à 2,8. Il est mort en production depuis, et supprimé du modèle depuis la "
            + "phase 20. Le chiffre de DEL-04 est TokensDepuisReleve — les tokens observés DEPUIS le "
            + "relevé exact, matière d'une BORNE INFÉRIEURE et jamais d'un pourcentage. Réutiliser "
            + "l'ancien nom rattacherait au nouveau chiffre la sémantique que ce milestone a tuée.\n  "
            + string.Join("\n  ", infractions));
    }

    // ==================== EXA-06 (phase 20) : le NOM de la source ====================

    /// <summary>
    /// PREUVE PAR RÉFÉRENCE, jumelle de <see cref="Le_repli_le_plus_interne_est_bien_celui_a_anciennete_non_bornee"/>.
    ///
    /// Le nom de la source est posé sur <c>WindowState</c> et NON sur <c>UsageSnapshot</c>, parce que
    /// <c>Best()</c> rend l'INSTANCE gagnante par référence alors que la recomposition du snapshot se
    /// fait par « new » — un champ de snapshot serait détruit à chaque passage. La chaîne réelle compte
    /// TROIS composites imbriqués : ce test monte la même profondeur, fait gagner la source la plus
    /// interne (les deux au-dessus sont indisponibles), et exige que son nom arrive intact.
    ///
    /// Ce test TOMBE si quelqu'un déplace ce champ vers <c>UsageSnapshot</c>, ou si <c>Best()</c> se met
    /// un jour à recomposer une fenêtre par « new » au lieu de rendre l'instance.
    /// </summary>
    [Fact]
    public async Task La_source_survit_aux_TROIS_composites_imbriques()
    {
        var externe = new FakeUsageProvider { Next = SnapMuet() };
        var median = new FakeUsageProvider { Next = SnapMuet() };
        var interne = new FakeUsageProvider { Next = Snap(0.33, SourceUsage.PontStatusLine) };

        var chaine = new CompositeUsageProvider(
            externe,
            new CompositeUsageProvider(median, interne));

        var snap = await chaine.GetAsync();

        Assert.Equal(SourceUsage.PontStatusLine, snap.FiveHour.Source);
        Assert.Equal(SourceUsage.PontStatusLine, snap.SevenDay.Source);
        Assert.Equal(0.33, snap.FiveHour.Utilization);   // c'est bien la fenêtre interne qui a gagné
    }

    /// <summary>
    /// EXA-06 — une fenêtre que la doctrine déclare indisponible ne nomme AUCUN producteur.
    ///
    /// Les deux assertions vont ENSEMBLE, et c'est le point du test : c'est la cohérence de la source et
    /// de la provenance qu'on garde. Effacer le chiffre tout en conservant « alimenté par la sonde
    /// d'en-têtes » ferait dire au diagnostic qu'une source fonctionne sous un cadran qui n'affiche
    /// rien — exactement la panne silencieuse que ce milestone traque.
    ///
    /// Le cas monté est celui de la première moitié d'EXA-02 : un relevé marqué exact dont personne ne
    /// sait QUAND il a été pris est incertifiable, donc démonté.
    /// </summary>
    [Fact]
    public void Une_fenetre_indisponible_ne_nomme_AUCUNE_source()
    {
        var vivante = new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = 0.42,
            Source = SourceUsage.SondeEnTetes,
            CapturedAt = null,                  // incertifiable : ni âge mesurable, ni question posable
        };

        var w = DoctrineFraicheur.Statuer(vivante, memorisee: null, journal: null,
                                          new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(SourceReliability.Unavailable, w.Reliability);
        Assert.Null(w.Source);
        Assert.Null(w.Provenance);
    }

    /// <summary>
    /// EXA-06 × DEL-04 — un PLANCHER conserve le nom de la source qui a produit le relevé mémorisé.
    ///
    /// L'héritage est obtenu gratuitement par le « candidat with { … } » de <c>Qualifier</c> : aucune
    /// ligne n'a été ajoutée pour cela, et c'est ce test qui le prouve plutôt que de le supposer. La
    /// distinction est celle des deux axes : la SOURCE reste le magasin (c'est lui qui a fourni le
    /// chiffre), l'ÉTAT devient « borne inférieure » (de l'activité est survenue depuis). Les fusionner
    /// ferait perdre l'un des deux.
    /// </summary>
    [Fact]
    public void Un_plancher_conserve_la_source_du_releve_memorise()
    {
        var now = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

        var memorisee = new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = 0.42,
            ResetsAt = now.AddHours(3),
            CapturedAt = now.AddHours(-3),          // hors limite d'âge
            Source = SourceUsage.MagasinDernierExact,
        };

        var journal = new TranscriptActivityLog(now, now - TimeSpan.FromDays(8),
                                                new[] { (now.AddHours(-1), 9_000L) });

        var w = DoctrineFraicheur.Statuer(WindowState.Unavailable(WindowKind.FiveHour), memorisee,
                                          journal, now);

        Assert.Equal(SourceReliability.Estimated, w.Reliability);
        Assert.Equal(ProvenanceReleve.PlancherAvecActivite, w.Provenance);
        Assert.Equal(SourceUsage.MagasinDernierExact, w.Source);   // la source, elle, n'a pas changé
        Assert.Equal(0.42, w.Utilization);                          // et le chiffre n'est pas gonflé
    }

    private static UsageSnapshot Snap(double util) => new()
    {
        FiveHour = new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = util,
        },
        SevenDay = new WindowState
        {
            Kind = WindowKind.SevenDay,
            Reliability = SourceReliability.Exact,
            Utilization = util,
        },
    };

    /// <summary>Variante nommant sa source — EXA-06.</summary>
    private static UsageSnapshot Snap(double util, SourceUsage source) => new()
    {
        FiveHour = new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = util,
            Source = source,
        },
        SevenDay = new WindowState
        {
            Kind = WindowKind.SevenDay,
            Reliability = SourceReliability.Exact,
            Utilization = util,
            Source = source,
        },
    };

    /// <summary>Snapshot dont les deux fenêtres sont indisponibles : un maillon de chaîne en panne.</summary>
    private static UsageSnapshot SnapMuet() => new()
    {
        FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
        SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
    };

    // ==================== EXA-03 (phase 20) : UNE SEULE notion de « périmé » ====================

    /// <summary>
    /// GARDE DE NON-RETOUR — aucun seuil d'ancienneté ne se recalcule dans les ViewModels de la doctrine.
    ///
    /// Avant la phase 20, deux notions de « périmé » coexistaient : celle de la doctrine (dérivée de la
    /// cadence de la sonde) et un seuil de deux minutes calculé par <c>MainViewModel</c>, bindé nulle part.
    /// La bonne forme n'était pas d'aligner l'une sur l'autre, c'était de supprimer le calcul concurrent :
    /// la doctrine seule a l'autorité sur la provenance, le ViewModel la RAPPORTE.
    ///
    /// PORTÉE DÉLIBÉRÉMENT RESTREINTE À DEUX FICHIERS. <c>SessionsViewModel</c> compare légitimement des
    /// durées pour formater l'ancienneté d'une session du widget — sujet distinct de la doctrine d'usage.
    /// Élargir cette garde au dossier entier la rendrait rouge sur du code correct, donc inexploitable :
    /// une garde qu'on apprend à ignorer ne garde rien.
    /// </summary>
    [Fact]
    public void Aucun_seuil_d_anciennete_n_est_calcule_dans_les_ViewModels_de_la_doctrine()
    {
        var racine = CheminSources();

        var fichiers = new[] { "MainViewModel.cs", "WindowGaugeViewModel.cs" }
            .Select(n => Path.Combine(racine, "ViewModels", n))
            .ToList();

        // Une garde qui ne lit rien est muette : on exige que les deux fichiers existent ET aient du corps.
        foreach (var fichier in fichiers)
        {
            Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");
            Assert.True(new FileInfo(fichier).Length > 1000,
                $"{Path.GetFileName(fichier)} est suspicieusement court : la garde ne lit manifestement "
                + "pas le vrai fichier source.");
        }

        // Les DEUX sens de la comparaison : « âge > seuil » et « seuil < âge ».
        var motif = new Regex(@"[<>]=?\s*(System\.)?TimeSpan\.From|TimeSpan\.From\w+\([^)]*\)\s*[<>]=?");

        var infractions = new List<string>();

        foreach (var fichier in fichiers)
        {
            var lignes = File.ReadAllLines(fichier);
            for (var i = 0; i < lignes.Length; i++)
                if (motif.IsMatch(lignes[i]))
                    infractions.Add($"{Path.GetFileName(fichier)}:{i + 1} : {lignes[i].Trim()}");
        }

        Assert.True(infractions.Count == 0,
            "La limite d'âge d'un relevé appartient à DoctrineFraicheur.LimiteAge : elle est DÉRIVÉE de la "
            + "cadence de la sonde, et non réglable, parce qu'EXA-02 est une propriété de sûreté et non une "
            + "préférence. Un second seuil calculé dans un ViewModel recréerait exactement le défaut que ce "
            + "milestone corrige — le précédent est documenté dans ce dépôt : une source de plafond passée "
            + "en « Manual » a gelé à vie un chiffre faux, parce que deux autorités se disputaient la même "
            + "décision. Le ViewModel RAPPORTE ce que la doctrine a statué (WindowState.Provenance → "
            + "EstDate), il ne le redéduit jamais.\n  "
            + string.Join("\n  ", infractions));
    }

    /// <summary>Chemin des sources tel qu'injecté par MSBuild (jamais deviné depuis la sortie de build).</summary>
    private static string CheminSources()
        => typeof(GardesDoctrineTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminSourcesChronos")?.Value
           ?? "";
}
