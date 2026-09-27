using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// LA GARDE DE NON-DÉRIVE des agrégats de tokens (TOK-05, D-33-24) : <c>docs/data-sources.md</c> §8 et les types qui écrivent
/// <c>tokens-AAAA-MM.jsonl</c>, <c>ids-AAAA-MM.jsonl</c>, <c>curseurs.json</c> et <c>couverture.json</c> ne peuvent plus diverger
/// en silence. Sœur de <see cref="ContratJournalDocumenteTests"/> (§7), qu'elle ne touche pas.
///
/// <para><b>Pourquoi elle existe.</b> Ces fichiers sont faits pour être relus dans des mois (phases 34-35), par quelqu'un qui n'aura
/// que le document et les fichiers. Un champ ajouté en v1.9 (TOK-06 : dimension projet) sans sa ligne de doc rougit ici ; une borne
/// changée dans le code (<c>HorizonIndex</c>, <c>RetentionIndexMois</c>, <c>HorizonPurge</c>, rétention) sans son chiffre dans le
/// texte rougit ici ; un périmètre reformulé d'un côté seulement rougit ici.</para>
///
/// <para><b>Deux sens.</b> Le document doit nommer chaque champ de <see cref="LigneAgregat.Champs"/> et de
/// <see cref="IndexMessages.Champs"/> ; et ces deux tableaux publics doivent être ceux que le code écrit VRAIMENT : les
/// <c>JsonPropertyName</c> des DTO privés <c>LigneAgregatDto</c> et <c>LigneIdDto</c>, dans l'ordre du fil. Sinon la garde
/// comparerait le document à une liste qui ne garde rien.</para>
///
/// <para><b>Le périmètre, mot pour mot.</b> <see cref="LigneAgregat.Perimetre"/> vit en UN endroit du code (D-33-06, D-33-23) ; le
/// document le cite tel quel, le diagnostic l'affiche tel quel. Et l'hypothèse HYP-4 (horizon de purge de Claude Code) s'écrit
/// comme une hypothèse (D-33-25), comme HYP-1/2/3 au §7.</para>
///
/// <para>Ces tests ne lisent que des fichiers du dépôt : aucun réseau, aucun <c>%APPDATA%</c>, aucun <c>~/.claude</c>,
/// aucune horloge.</para>
/// </summary>
public sealed class ContratAgregatsDocumenteTests
{
    private const string TitreSection = "## 8. Agrégats de tokens";
    private const string TitreJournal = "## 7. Journal d'historique";
    private const int LignesMinimum = 40;

    /// <summary>Chemin de docs/ INJECTÉ par MSBuild (<c>AssemblyMetadata("CheminDocsChronos")</c>), jamais deviné.</summary>
    private static string CheminDocs()
        => typeof(ContratAgregatsDocumenteTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminDocsChronos")?.Value
           ?? "";

    /// <summary>Lit un document de docs/. AUCUNE mise en sourdine : chemin absent ou fichier absent = rouge explicite.</summary>
    private static string LireDocument(string nom)
    {
        var racine = CheminDocs();
        Assert.False(string.IsNullOrWhiteSpace(racine),
            "L'attribut AssemblyMetadata(\"CheminDocsChronos\") manque : le .csproj de tests doit injecter "
            + "le chemin de docs/, sans quoi cette garde ne lit rien et ne garde rien.");

        var chemin = Path.Combine(racine, nom);
        Assert.True(File.Exists(chemin), $"Document introuvable : {chemin}");
        return File.ReadAllText(chemin);
    }

    /// <summary>Le texte du §8, de son titre au séparateur <c>---</c> final (ou à la fin du document).</summary>
    private static string SectionAgregats(string texte)
    {
        var section = ContratHooksDocumenteTests.SectionDe(texte, TitreSection);
        var lignes = section.Split('\n');
        var fin = Array.FindLastIndex(lignes, l => l.Trim() == "---");
        return fin > 0 ? string.Join("\n", lignes.Take(fin)) : section;
    }

    private static string Code(string mot) => "`" + mot + "`";

    /// <summary>Les <c>JsonPropertyName</c> d'un DTO privé imbriqué, dans l'ordre du fil (<c>JsonPropertyOrder</c>, sinon l'ordre
    /// de déclaration — <c>OrderBy</c> est stable).</summary>
    private static string?[] NomsDuFil(Type conteneur, string nomDto)
    {
        var dto = conteneur.GetNestedType(nomDto, BindingFlags.NonPublic);
        Assert.True(dto is not null, $"{conteneur.Name}.{nomDto} introuvable : la garde ne peut plus lier Champs au fil.");
        return dto!.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Nom: p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name,
                          Ordre: p.GetCustomAttribute<JsonPropertyOrderAttribute>()?.Order ?? int.MaxValue))
            .OrderBy(x => x.Ordre)
            .Select(x => x.Nom)
            .ToArray();
    }

    [Fact]
    public void Le_document_a_une_section_Agregats_de_tokens_d_au_moins_quarante_lignes_et_le_paragraphe_7_reste_unique()
    {
        var texte = LireDocument("data-sources.md").Replace("\r\n", "\n");
        var lignes = texte.Split('\n');

        var titres = lignes.Count(l => l.StartsWith(TitreSection, StringComparison.Ordinal));
        Assert.True(titres == 1, $"Le document doit porter EXACTEMENT une ligne « {TitreSection} » ; trouvé : {titres}.");

        // La garde de 32-07 continue de tenir : le §8 s'insère APRÈS le --- qui clôt le §7, sans le dupliquer.
        var titresJournal = lignes.Count(l => l.StartsWith(TitreJournal, StringComparison.Ordinal));
        Assert.True(titresJournal == 1, $"Le document doit porter EXACTEMENT une ligne « {TitreJournal} » ; trouvé : {titresJournal}.");

        var section = SectionAgregats(texte);
        var nombre = section.Split('\n').Length;
        Assert.True(nombre >= LignesMinimum,
            $"Le §8 fait {nombre} lignes ; il en faut au moins {LignesMinimum} — une section vide rendrait "
            + "toutes les assertions ci-dessous sans objet.");
    }

    [Fact]
    public void Le_document_nomme_chaque_champ_reel_des_agregats_et_de_l_index()
    {
        var section = SectionAgregats(LireDocument("data-sources.md"));

        // Anti-mutisme : les deux tableaux publics ont la taille du schéma (9 champs chacun).
        Assert.Equal(9, LigneAgregat.Champs.Length);
        Assert.Equal(9, IndexMessages.Champs.Length);

        // Sens 1 — les tableaux publics sont bien ce que le code ÉCRIT : les JsonPropertyName des DTO privés, dans l'ordre du fil.
        Assert.Equal(LigneAgregat.Champs, NomsDuFil(typeof(LigneAgregat), "LigneAgregatDto"));
        Assert.Equal(IndexMessages.Champs, NomsDuFil(typeof(IndexMessages), "LigneIdDto"));

        // Sens 2 — le document nomme chacun d'eux, entre accents graves, ainsi que les quatre fichiers.
        var manquants = new List<string>();
        foreach (var champ in LigneAgregat.Champs)
            if (!section.Contains(Code(champ), StringComparison.Ordinal)) manquants.Add("champ d'agrégat " + Code(champ));
        foreach (var champ in IndexMessages.Champs)
            if (!section.Contains(Code(champ), StringComparison.Ordinal)) manquants.Add("champ d'index " + Code(champ));
        foreach (var fichier in new[] { "tokens-AAAA-MM.jsonl", "ids-AAAA-MM.jsonl", Curseurs.NomFichier, CouvertureTokens.NomFichier })
            if (!section.Contains(Code(fichier), StringComparison.Ordinal)) manquants.Add("fichier " + Code(fichier));

        Assert.True(manquants.Count == 0,
            "Le §8 de docs/data-sources.md ne nomme pas : " + string.Join(", ", manquants)
            + ". Chaque champ de LigneAgregat.Champs et d'IndexMessages.Champs, et chacun des quatre fichiers, doit y figurer.");
    }

    [Fact]
    public void Le_document_ecrit_le_perimetre_mot_pour_mot_et_jamais_un_pourcentage()
    {
        var section = SectionAgregats(LireDocument("data-sources.md"));

        // Le périmètre vit dans le code (D-33-06) ; le document le cite tel quel — une reformulation d'un seul côté rougit.
        Assert.True(section.Contains(LigneAgregat.Perimetre, StringComparison.Ordinal),
            "Le §8 doit citer LigneAgregat.Perimetre mot pour mot : « " + LigneAgregat.Perimetre + " ».");

        Assert.Contains("jamais", section, StringComparison.Ordinal);
        Assert.Contains("pourcentage", section, StringComparison.Ordinal);

        // Les trois états de couverture (mots du §4 du plan de design) et la règle de dédup partagée avec CPT-01.
        Assert.Contains("hors couverture", section, StringComparison.Ordinal);
        Assert.Contains("transcripts absents", section, StringComparison.Ordinal);
        Assert.Contains("max par champ", section, StringComparison.Ordinal);
        Assert.Contains("DedupUsage", section, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_document_porte_les_bornes_du_code()
    {
        var section = SectionAgregats(LireDocument("data-sources.md"));

        // Le document porte les valeurs du CODE : chaque borne est lue ici ET cherchée dans le texte.
        Assert.Equal(TimeSpan.FromDays(45), IndexMessages.HorizonIndex);
        Assert.Contains("45 j", section, StringComparison.Ordinal);

        Assert.Equal(3, IndexMessages.RetentionIndexMois);
        Assert.Contains("3 mois", section, StringComparison.Ordinal);

        Assert.Equal(TimeSpan.FromDays(30), CouvertureTokens.HorizonPurge);
        Assert.Contains("30 j", section, StringComparison.Ordinal);

        Assert.Equal(JournalReleves.RetentionMois, MagasinAgregats.RetentionMois);
        Assert.Equal(24, MagasinAgregats.RetentionMois);
        Assert.Contains("24 mois", section, StringComparison.Ordinal);

        Assert.Equal(TimeSpan.FromSeconds(60), ReconstructionTokens.CadenceIncrementale);
        Assert.Contains("60 s", section, StringComparison.Ordinal);

        Assert.Contains("15 min", section, StringComparison.Ordinal);      // la tranche
        Assert.Contains("BelowNormal", section, StringComparison.Ordinal);   // la priorité du thread
    }

    [Fact]
    public void Le_document_ecrit_HYP_4_et_le_paragraphe_7_renvoie_au_8()
    {
        var texte = LireDocument("data-sources.md").Replace("\r\n", "\n");
        var section = SectionAgregats(texte);

        // HYP-4 s'écrit comme une hypothèse (D-33-25) : nommée, avec le réglage de Claude Code qu'elle suppose et sa valeur.
        Assert.Contains("HYP-4", section, StringComparison.Ordinal);
        Assert.Contains("cleanupPeriodDays", section, StringComparison.Ordinal);
        Assert.Contains("30", section, StringComparison.Ordinal);

        // Le §7 renvoie au §8 au lieu d'annoncer une phase à venir.
        var journal = ContratHooksDocumenteTests.SectionDe(texte, TitreJournal);
        Assert.Contains("§8", journal, StringComparison.Ordinal);
        Assert.DoesNotContain("arrivent en phase 33", journal, StringComparison.Ordinal);

        // La ligne finale du document date le §8.
        var derniere = texte.Split('\n').Last(l => !string.IsNullOrWhiteSpace(l));
        Assert.Contains("§8", derniere, StringComparison.Ordinal);
    }
}
