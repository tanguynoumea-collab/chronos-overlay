using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Chronos.Models.Historique;
using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// LA GARDE DE NON-DÉRIVE du journal d'historique (JRN-06, D-32-31) : <c>docs/data-sources.md</c> §7 et les types
/// qui écrivent et relisent le journal ne peuvent plus diverger en silence.
///
/// <para><b>Pourquoi elle existe.</b> Le journal est fait pour être relu dans des mois (phases 34-35), par quelqu'un qui
/// n'aura que le document et les fichiers. Un champ ajouté en phase 33 sans sa ligne de doc, un événement renommé sans
/// que le §7 suive : la ligne serait lue sans être comprise. Motif recopié de <see cref="ContratHooksDocumenteTests"/> :
/// le document est lu comme TEXTE, les types sont lus par RÉFLEXION, et l'un doit nommer l'autre.</para>
///
/// <para><b>Deux sens.</b> Le document doit nommer chaque champ de <see cref="LigneJournal.ChampsReleve"/> et chaque nom
/// de <see cref="TypeEvenementTexte.NomsDeFil"/> ; et ces deux tableaux publics doivent être ceux que le code écrit
/// VRAIMENT : les <c>JsonPropertyName</c> du DTO d'écriture d'un côté, l'aller-retour <c>Nom</c>/<c>Depuis</c> de l'enum
/// de l'autre. Sinon la garde comparerait le document à une liste qui ne garde rien.</para>
///
/// <para><b>Les hypothèses aussi.</b> HYP-1, HYP-2 et HYP-3 sont les trois questions que seul le journal tranchera
/// (D-32-32). Un document qui les perdrait redonnerait l'air de certitude que la phase 32 a voulu retirer.</para>
///
/// <para>Ces tests ne lisent que des fichiers du dépôt : aucun réseau, aucun <c>%APPDATA%</c>, aucun <c>~/.claude</c>,
/// aucune horloge.</para>
/// </summary>
public sealed class ContratJournalDocumenteTests
{
    private const string TitreSection = "## 7. Journal d'historique";
    private const int LignesMinimum = 40;
    private const string PhraseRetiree = "il n'existe aucun verrou mono-instance";

    /// <summary>Chemin de docs/ INJECTÉ par MSBuild (<c>AssemblyMetadata("CheminDocsChronos")</c>), jamais deviné.</summary>
    private static string CheminDocs()
        => typeof(ContratJournalDocumenteTests).Assembly
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

    /// <summary>Le texte du §7, de son titre au séparateur <c>---</c> final (ou à la fin du document).</summary>
    private static string SectionJournal(string texte)
    {
        var section = ContratHooksDocumenteTests.SectionDe(texte, TitreSection);
        var lignes = section.Split('\n');
        var fin = Array.FindLastIndex(lignes, l => l.Trim() == "---");
        return fin > 0 ? string.Join("\n", lignes.Take(fin)) : section;
    }

    private static string Code(string mot) => "`" + mot + "`";

    [Fact]
    public void Le_document_a_une_section_Journal_d_historique_d_au_moins_quarante_lignes()
    {
        var texte = LireDocument("data-sources.md").Replace("\r\n", "\n");

        var titres = texte.Split('\n').Count(l => l.StartsWith(TitreSection, StringComparison.Ordinal));
        Assert.True(titres == 1, $"Le document doit porter EXACTEMENT une ligne « {TitreSection} » ; trouvé : {titres}.");

        var section = SectionJournal(texte);
        var nombre = section.Split('\n').Length;
        Assert.True(nombre >= LignesMinimum,
            $"Le §7 fait {nombre} lignes ; il en faut au moins {LignesMinimum} — une section vide rendrait "
            + "toutes les assertions ci-dessous sans objet.");
    }

    [Fact]
    public void Le_document_nomme_chaque_champ_du_releve_et_chaque_evenement_reels()
    {
        var section = SectionJournal(LireDocument("data-sources.md"));

        // Anti-mutisme : les deux tableaux publics ont la taille du contrat de données §3.
        Assert.Equal(11, LigneJournal.ChampsReleve.Length);
        Assert.Equal(6, TypeEvenementTexte.NomsDeFil.Length);

        // Sens 1 — les tableaux publics sont bien ce que le code ÉCRIT : les JsonPropertyName du DTO de relevé,
        // dans l'ordre du fil ; et chaque nom de fil fait l'aller-retour Nom → Depuis → Nom.
        var dto = typeof(LigneJournal).GetNestedType("LigneReleveDto", BindingFlags.NonPublic);
        Assert.True(dto is not null, "LigneJournal.LigneReleveDto introuvable : la garde ne peut plus lier ChampsReleve au fil.");
        var nomsDuFil = dto!.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Nom: p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name,
                          Ordre: p.GetCustomAttribute<JsonPropertyOrderAttribute>()?.Order ?? int.MaxValue))
            .OrderBy(x => x.Ordre)
            .Select(x => x.Nom)
            .ToArray();
        Assert.Equal(LigneJournal.ChampsReleve, nomsDuFil);

        var typesEcrits = Enum.GetValues<TypeEvenement>().Where(t => t != TypeEvenement.NonReconnu).ToArray();
        Assert.Equal(typesEcrits.Select(TypeEvenementTexte.Nom), TypeEvenementTexte.NomsDeFil);
        foreach (var nom in TypeEvenementTexte.NomsDeFil)
            Assert.Equal(nom, TypeEvenementTexte.Nom(TypeEvenementTexte.Depuis(nom)));
        Assert.Null(TypeEvenementTexte.Nom(TypeEvenement.NonReconnu));

        // Sens 2 — le document nomme chacun d'eux, entre accents graves.
        var manquants = new List<string>();
        foreach (var champ in LigneJournal.ChampsReleve)
            if (!section.Contains(Code(champ), StringComparison.Ordinal)) manquants.Add("champ " + Code(champ));
        foreach (var ev in TypeEvenementTexte.NomsDeFil)
            if (!section.Contains(Code(ev), StringComparison.Ordinal)) manquants.Add("événement " + Code(ev));

        Assert.True(manquants.Count == 0,
            "Le §7 de docs/data-sources.md ne nomme pas : " + string.Join(", ", manquants)
            + ". Chaque champ de LigneJournal.ChampsReleve et chaque nom de TypeEvenementTexte.NomsDeFil doit y figurer.");
    }

    [Fact]
    public void Le_document_ecrit_les_trois_hypotheses_a_verifier_avec_le_journal()
    {
        var section = SectionJournal(LireDocument("data-sources.md"));

        var attendus = new (string Marqueur, string[] Mots)[]
        {
            ("HYP-1", new[] { "granularité" }),
            ("HYP-2", new[] { "recalcul rétroactif" }),
            ("HYP-3", new[] { "25/10/2026", "2026-10-30T23:00Z" }),
        };

        foreach (var (marqueur, mots) in attendus)
        {
            Assert.True(section.Contains(marqueur, StringComparison.Ordinal),
                $"Le §7 ne porte plus l'hypothèse « {marqueur} » : les trois questions que le journal doit trancher y sont écrites comme des hypothèses.");
            foreach (var mot in mots)
                Assert.True(section.Contains(mot, StringComparison.Ordinal),
                    $"L'hypothèse « {marqueur} » doit nommer « {mot} ».");
        }
    }

    [Fact]
    public void Le_document_ecrit_la_retention_la_dedup_et_les_deux_vues()
    {
        var section = SectionJournal(LireDocument("data-sources.md"));

        Assert.Equal(24, JournalReleves.RetentionMois);
        Assert.Contains("24 mois", section, StringComparison.Ordinal);
        Assert.Contains("strictement croissant", section, StringComparison.Ordinal);
        Assert.Contains("`(t, source)`", section, StringComparison.Ordinal);
        Assert.Contains("FileMode.Append", section, StringComparison.Ordinal);   // nommé comme motif ÉCARTÉ
        Assert.Contains("virtualisée", section, StringComparison.Ordinal);
        Assert.Contains("2026-09-13", section, StringComparison.Ordinal);

        var publish = LireDocument("publish.md");
        Assert.DoesNotContain(PhraseRetiree, publish, StringComparison.Ordinal);
        Assert.Contains("une seule instance", publish, StringComparison.Ordinal);
    }
}
