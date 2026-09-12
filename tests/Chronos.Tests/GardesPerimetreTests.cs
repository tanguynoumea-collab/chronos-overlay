using System.IO;
using System.Reflection;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// GARDE DE NON-RETOUR de la phase 21 (SRC-01). Le widget de sessions ne parle que de Claude Code : la
/// source app-bureau par UI Automation, son poll de fond et l'observateur de focus OS ont été retirés.
///
/// La garde est par RÉFLEXION sur l'assembly, comme le précédent
/// <c>Aucun_type_de_plafond_ne_subsiste_dans_l_assembly</c> (phase 16) : elle attrape un rétablissement
/// depuis l'historique, une réimplémentation sous un autre fichier, ou un copier-coller — trois voies
/// qu'une recherche textuelle sur un chemin de fichier manquerait.
///
/// Pourquoi cette suppression n'est PAS du ménage opportuniste : l'acquittement par focus exigeait une
/// session d'origine « app bureau » ET un identifiant synthétique de cette source. Une session Claude Code
/// a une origine ligne de commande et un UUID — le mécanisme ne l'atteignait jamais, et le sondage de
/// focus OS était payé à chaque tick pour un résultat jamais lu.
///
/// Ces tests ne lisent que l'assembly et des fichiers du dépôt : aucun réseau, aucun %APPDATA%, aucun jeton.
/// </summary>
public class GardesPerimetreTests
{
    [Fact]
    public void Aucun_type_de_la_source_app_bureau_ne_subsiste_dans_l_assembly()
    {
        var asm = typeof(Chronos.Services.IUsageProvider).Assembly;

        var revenants = asm.GetTypes()
            .Where(t => t.Name.Contains("Uia", StringComparison.Ordinal)
                     || t.Name.EndsWith("ForegroundWatch", StringComparison.Ordinal)
                     || t.Name == "DesktopHealth"
                     || t.Name == "SessionKind"
                     || t.Name == "SessionOrigin")
            .Select(t => t.FullName)
            .ToList();

        Assert.True(revenants.Count == 0,
            "La source app-bureau par UI Automation a été retirée en phase 21 (SRC-01) : le widget ne "
            + "montre que des sessions Claude Code. Ces types ne doivent pas revenir.\n  "
            + string.Join("\n  ", revenants!));
    }

    // Une garde qui ne verrait AUCUN type serait muette (assembly mal résolu, réflexion cassée).
    [Fact]
    public void La_garde_voit_bien_l_assembly_Chronos()
    {
        var asm = typeof(Chronos.Services.IUsageProvider).Assembly;
        Assert.True(asm.GetTypes().Length >= 50, "réflexion muette : l'assembly Chronos n'est pas résolu");
        Assert.Contains(asm.GetTypes(), t => t.Name == "SessionMonitor");
    }

    /// <summary>
    /// Le libellé de type de session (Chat / Code / Cowork) était bindé dans le SEUL template Pastilles —
    /// mesuré : 3 occurrences, toutes entre les lignes 70 et 79 de SessionStyles.xaml, contrairement à ce
    /// qu'annonçait le contexte de phase. Son producteur a disparu avec la source app-bureau : un binding
    /// survivant afficherait une case vide ou décalerait la rangée. La réflexion ne voit pas un binding XAML,
    /// d'où ce contrôle de SOURCE.
    /// </summary>
    [Fact]
    public void Aucun_style_de_session_ne_binde_plus_un_libelle_de_type()
    {
        var fichier = Path.Combine(CheminSources(), "Resources", "SessionStyles.xaml");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        // Une garde qui lirait un fichier vide serait muette : on exige les 8 templates.
        var templates = System.Text.RegularExpressions.Regex.Matches(texte, "DataTemplate x:Key=").Count;
        Assert.Equal(8, templates);

        Assert.DoesNotContain("KindLabel", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE CÂBLAGE (SRC-02). <c>PurgerPrefixe</c> peut être parfaitement testée et n'être jamais
    /// appelée : le défaut ne se verrait alors que chez l'utilisateur, sur ses propres données, et
    /// silencieusement. Le démarrage en mode overlay doit invoquer la purge du préfixe de l'ancienne
    /// source app-bureau. Contrôle de SOURCE : <c>OnStartup</c> monte un host WPF complet, il n'est pas
    /// instanciable sous test sans lancer l'application — ce que la phase interdit.
    /// </summary>
    [Fact]
    public void Le_demarrage_purge_les_identifiants_fantomes_du_magasin_d_archives()
    {
        var fichier = Path.Combine(CheminSources(), "App.xaml.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        // Une garde qui lirait un fichier vide serait muette.
        Assert.Contains("OnStartup", texte, StringComparison.Ordinal);
        Assert.Contains("PurgerPrefixe(\"desktop:\")", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE NON-RETOUR (OBS-01, phase 22) — le diagnostic ne peut plus fabriquer son propre moniteur.
    ///
    /// <para>Le défaut corrigé n'était pas une faute de frappe mais une classe d'erreur : un rapport qui
    /// décrit un système reconstruit à la volée, donc différent de celui qui tourne. Relevé le 2026-09-12 :
    /// le moniteur fabriqué dans le rapport n'avait ni magasin d'archives, ni filtre « traité », ni
    /// détecteur. L'utilisateur lisait des sessions vieilles de plusieurs semaines dans le RAPPORT, jamais
    /// dans le widget — c'est très probablement pourquoi le problème n'a jamais été élucidé.</para>
    ///
    /// <para>Cette garde vaut surtout pour l'AVENIR : les phases 23 à 26 modifient le widget, et elles sont
    /// vérifiées avec cet instrument. Qu'il redevienne autonome un seul commit, et elles seraient vérifiées
    /// avec un instrument à nouveau faussé, sans que rien ne le signale.</para>
    /// </summary>
    [Fact]
    public void Le_diagnostic_ne_fabrique_aucun_moniteur_de_sessions()
    {
        var fichier = Path.Combine(CheminSources(), "Services", "DiagnosticService.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        // Une garde qui lirait un fichier vide, ou un fichier où la section a disparu, serait muette.
        Assert.Contains("BuildReportAsync", texte, StringComparison.Ordinal);
        Assert.Contains("_moniteurSessions.Inspecter(_clock.UtcNow)", texte, StringComparison.Ordinal);

        Assert.DoesNotContain("new SessionMonitor", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE PARTAGE D'INSTANCE (OBS-01). La garde précédente empêche la FABRICATION ; celle-ci exige
    /// l'INJECTION. Sans elle, supprimer l'argument de composition laisserait le rapport parfaitement
    /// honnête — il dirait « moniteur non injecté » — et parfaitement inutile, en silence.
    ///
    /// <para>L'assertion porte sur le fragment d'enregistrement du DiagnosticService et non sur le fichier
    /// entier : `GetRequiredService&lt;SessionMonitor&gt;()` figure aussi dans l'enregistrement du contrôleur
    /// du widget, et une recherche globale resterait verte alors que le diagnostic aurait été débranché.</para>
    /// </summary>
    [Fact]
    public void Le_diagnostic_recoit_le_moniteur_du_conteneur()
    {
        var fichier = Path.Combine(CheminSources(), "App.xaml.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        var debut = texte.IndexOf("new DiagnosticService(", StringComparison.Ordinal);
        Assert.True(debut >= 0, "Enregistrement du DiagnosticService introuvable dans App.xaml.cs");
        var fin = texte.IndexOf("));", debut, StringComparison.Ordinal);
        Assert.True(fin > debut, "Fin de l'enregistrement du DiagnosticService introuvable");

        var enregistrement = texte[debut..fin];
        Assert.Contains("moniteurSessions: sp.GetRequiredService<SessionMonitor>()", enregistrement,
                        StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE CÂBLAGE (CYC-02, phase 23). <c>EcritureEtatSession</c> peut être parfaitement testée et
    /// n'être jamais appelée : le hook continuerait alors de déplacer un fichier temporaire par-dessus la
    /// cible, de perdre une écriture sur deux sous lecteur concurrent, et de le taire. Contrôle de SOURCE :
    /// <c>OnStartup</c> monte un host WPF, il n'est pas instanciable sous test.
    /// </summary>
    [Fact]
    public void Le_mode_hook_ecrit_par_le_service_teste_et_signale_son_echec()
    {
        var fichier = Path.Combine(CheminSources(), "App.xaml.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        // Une garde qui lirait un fichier vide serait muette.
        Assert.Contains("RunSessionHook", texte, StringComparison.Ordinal);

        Assert.Contains("EcritureEtatSession.Appliquer(", texte, StringComparison.Ordinal);
        Assert.Contains("OpenStandardError", texte, StringComparison.Ordinal);

        // Le défaut mesuré : un fichier temporaire déplacé par-dessus la cible, et un échec avalé.
        Assert.DoesNotContain("System.IO.File.Move", texte, StringComparison.Ordinal);
        Assert.DoesNotContain(".tmp-", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE CÂBLAGE (CYC-01, phase 23). Un balayage parfaitement testé mais jamais appelé ne balaie
    /// rien, et le défaut ne se verrait que chez l'utilisateur, sur ses propres données, en silence — c'est
    /// exactement ce qui s'est produit pendant deux mois avec le magasin de sessions. La seconde assertion
    /// est la plus importante : le balayage doit viser le dossier DU MONITEUR du widget, jamais un chemin
    /// déduit dans son coin.
    /// </summary>
    [Fact]
    public void Le_demarrage_balaie_le_magasin_de_sessions_sur_le_dossier_du_moniteur()
    {
        var fichier = Path.Combine(CheminSources(), "App.xaml.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        Assert.Contains("OnStartup", texte, StringComparison.Ordinal);   // garde muette sinon
        Assert.Contains("GetRequiredService<BalayageMagasinSessions>().Balayer()", texte, StringComparison.Ordinal);
        Assert.Contains("GetRequiredService<SessionMonitor>().Directory", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE NON-RETOUR (FUS-01, phase 24) — le moniteur ne fusionne plus en réaffectant une entrée
    /// indexée par identifiant, source après source.
    ///
    /// <para>Le défaut mesuré le 2026-09-12 n'était pas une faute de frappe mais une classe d'erreur :
    /// l'ordre du code faisait loi. Étape 1 les transcripts, étape 2 les hooks — donc un signal de 7 heures
    /// battait un signal de 10 secondes, et « à toi » s'affichait pendant que le modèle travaillait.</para>
    ///
    /// <para>Cette garde vaut surtout pour l'AVENIR : les phases 25 et 26 ajoutent des signaux et une notion
    /// de « traité » adossée à la fraîcheur. Qu'une source redevienne prioritaire par construction un seul
    /// commit, et les deux phases suivantes reposeraient à nouveau sur un arbitrage faussé, sans rien signaler.</para>
    /// </summary>
    [Fact]
    public void Le_moniteur_n_arbitre_plus_par_ordre_d_insertion()
    {
        var fichier = Path.Combine(CheminSources(), "Services", "SessionMonitor.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        // Une garde qui lirait un fichier vide, ou dont la méthode aurait disparu, serait muette.
        Assert.Contains("public LectureSessions Inspecter(", texte, StringComparison.Ordinal);
        Assert.Contains("ArbitrageSessions.Trancher(", texte, StringComparison.Ordinal);

        // L'idiome EXACT de l'écrasement mesuré.
        Assert.DoesNotContain("byId[", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("Dictionary<string, SessionSnapshot>", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE PURETÉ (FUS-01). L'arbitrage ne compare QUE les instants que les signaux portent. Qu'il
    /// acquière une horloge, un chemin ou un magasin, et il pourrait « rafraîchir » un signal muet — c'est-à-dire
    /// présenter comme observé ce qui ne l'a pas été, le seul interdit absolu de ce milestone.
    /// La réflexion ne lit pas les commentaires : elle lit la surface du type.
    /// </summary>
    [Fact]
    public void L_arbitrage_ne_connait_ni_horloge_ni_magasin_ni_chemin()
    {
        var t = typeof(Chronos.Services.ArbitrageSessions);

        Assert.True(t.IsAbstract && t.IsSealed, "ArbitrageSessions doit rester une classe statique");

        var declarees = t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        var trancher = Assert.Single(declarees);                       // une seule porte d'entrée
        Assert.Equal("Trancher", trancher.Name);
        var parametre = Assert.Single(trancher.GetParameters());       // …et un seul argument
        Assert.Equal(typeof(IEnumerable<Chronos.Services.SignalSession>), parametre.ParameterType);

        // Aucun champ statique ne peut cacher une horloge, un magasin de verdict ou un chemin de dossier.
        var contrebande = t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(Chronos.Services.IClock)
                     || f.FieldType == typeof(Chronos.Services.TreatedStore)
                     || f.FieldType == typeof(Chronos.Services.ArchiveStore)
                     || f.FieldType == typeof(string))
            .Select(f => f.Name)
            .ToList();

        Assert.True(contrebande.Count == 0,
            "L'arbitrage doit rester pur : ni horloge, ni magasin de verdict, ni chemin.\n  "
            + string.Join("\n  ", contrebande));
    }

    /// <summary>Le chemin des sources est INJECTÉ par MSBuild, jamais deviné (Assembly.Location est VIDE
    /// en publication mono-fichier). Motif recopié de <c>GardesDoctrineTests</c>.</summary>
    internal static string CheminSources()
        => typeof(GardesPerimetreTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminSourcesChronos")?.Value
           ?? "";
}
