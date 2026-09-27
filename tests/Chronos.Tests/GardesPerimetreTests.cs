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
    ///
    /// <para>Phase 29 (APP-06) : le moniteur a désormais PLUSIEURS racines (vue du paquet de l'app bureau, vue
    /// réelle). Le littéral passe de <c>.Directory</c> à <c>.Dossiers</c> sans changer d'intention ni de force :
    /// les racines balayées sont celles du moniteur, toutes, et aucune autre.</para>
    /// </summary>
    [Fact]
    public void Le_demarrage_balaie_le_magasin_de_sessions_sur_le_dossier_du_moniteur()
    {
        var fichier = Path.Combine(CheminSources(), "App.xaml.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        Assert.Contains("OnStartup", texte, StringComparison.Ordinal);   // garde muette sinon
        Assert.Contains("GetRequiredService<BalayageMagasinSessions>().Balayer()", texte, StringComparison.Ordinal);
        Assert.Contains("GetRequiredService<SessionMonitor>().Dossiers", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE CÂBLAGE (APP-06, phase 29). Le moniteur peut lire deux racines en test et une seule en production :
    /// il suffit qu'App.xaml.cs ne lui passe pas la liste — le paramètre est optionnel, et son défaut, résolu dans le
    /// moniteur, ne se verrait pas. Le widget resterait alors aveugle aux fichiers que les hooks de l'app bureau
    /// écrivent dans la vue du paquet, exactement comme avant cette phase, et en silence. Contrôle de SOURCE :
    /// <c>OnStartup</c> monte un host WPF, il n'est pas instanciable sous test.
    ///
    /// <para>Le fragment lu est l'enregistrement du moniteur, pas le fichier entier (même motif que
    /// <see cref="Le_diagnostic_recoit_le_moniteur_du_conteneur"/>). Il ne doit pas recevoir la racine de l'app bureau :
    /// les métadonnées de sessions ne sont pas des fichiers d'état, et le balayage suit les racines du moniteur.</para>
    /// </summary>
    [Fact]
    public void Le_moniteur_de_production_lit_les_deux_vues_d_AppData()
    {
        var fichier = Path.Combine(CheminSources(), "App.xaml.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        // Les racines sont résolues UNE fois, par candidats (paquet d'abord), et enregistrées dans le conteneur.
        Assert.Contains("RacinesEtat.ParDefaut()", texte, StringComparison.Ordinal);

        var debut = texte.IndexOf("new SessionMonitor(", StringComparison.Ordinal);
        Assert.True(debut >= 0, "Enregistrement du SessionMonitor introuvable dans App.xaml.cs");
        var fin = texte.IndexOf("));", debut, StringComparison.Ordinal);
        Assert.True(fin > debut, "Fin de l'enregistrement du SessionMonitor introuvable");

        var enregistrement = texte[debut..fin];
        Assert.Contains("dossiersEtat: sp.GetRequiredService<RacinesCandidates>().EtatsHooks", enregistrement,
                        StringComparison.Ordinal);
        Assert.DoesNotContain("SessionsAppBureau", enregistrement, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE CÂBLAGE (APP-01, APP-03, phase 29 — Piège 9 de la recherche). Le paramètre <c>appBureau</c> du moniteur
    /// est optionnel et nul par défaut : c'est ce qui garde les dizaines de tests existants en v1.6 exacte, et c'est
    /// aussi ce qui laisserait la PRODUCTION en v1.6 sans rien dire — aucune question de l'app, aucun titre, et aucun
    /// build ni démarrage en échec. Contrôle de SOURCE (<c>OnStartup</c> n'est pas instanciable sous test) : le lecteur
    /// est construit UNE fois sur les racines candidates de l'app bureau, et le moniteur du widget le reçoit par
    /// argument NOMMÉ. Le fragment lu est l'enregistrement du moniteur (même motif que la garde précédente).
    /// </summary>
    [Fact]
    public void Le_moniteur_de_production_recoit_le_lecteur_de_l_app_bureau()
    {
        var fichier = Path.Combine(CheminSources(), "App.xaml.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        Assert.Contains("new LecteurAppBureau(sp.GetRequiredService<RacinesCandidates>().SessionsAppBureau)", texte,
                        StringComparison.Ordinal);

        var debut = texte.IndexOf("new SessionMonitor(", StringComparison.Ordinal);
        Assert.True(debut >= 0, "Enregistrement du SessionMonitor introuvable dans App.xaml.cs");
        var fin = texte.IndexOf("));", debut, StringComparison.Ordinal);
        Assert.True(fin > debut, "Fin de l'enregistrement du SessionMonitor introuvable");

        Assert.Contains("appBureau: sp.GetRequiredService<LecteurAppBureau>()", texte[debut..fin], StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE CÂBLAGE (LUE-02, LUE-04, phase 30 — Piège 9 de la recherche). Le paramètre <c>premierPlan</c> du moniteur
    /// est optionnel et nul par défaut : c'est ce qui garde les tests existants sans sonde, et c'est aussi ce qui
    /// laisserait la PRODUCTION sans LUE-02 sans rien dire — la session regardée au premier plan ne serait jamais lue, et
    /// aucun build ni démarrage n'échouerait. Contrôle de SOURCE (<c>OnStartup</c> n'est pas instanciable sous test) : la
    /// sonde est enregistrée UNE fois, en singleton (son « depuis » vit avec l'app), et le moniteur du widget la reçoit
    /// par argument NOMMÉ. Le fragment lu est l'enregistrement du moniteur (même motif que les deux gardes précédentes) :
    /// il doit contenir la sonde ET toujours le lecteur de l'app, preuve que l'ajout n'a pas tronqué le fragment.
    /// </summary>
    [Fact]
    public void Le_moniteur_de_production_recoit_le_premier_plan()
    {
        var fichier = Path.Combine(CheminSources(), "App.xaml.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        Assert.Contains("services.AddSingleton<IPremierPlan>(_ => new PremierPlanWin32());", texte, StringComparison.Ordinal);

        var debut = texte.IndexOf("new SessionMonitor(", StringComparison.Ordinal);
        Assert.True(debut >= 0, "Enregistrement du SessionMonitor introuvable dans App.xaml.cs");
        var fin = texte.IndexOf("));", debut, StringComparison.Ordinal);
        Assert.True(fin > debut, "Fin de l'enregistrement du SessionMonitor introuvable");

        var enregistrement = texte[debut..fin];
        Assert.Contains("premierPlan: sp.GetRequiredService<IPremierPlan>()", enregistrement, StringComparison.Ordinal);
        Assert.Contains("appBureau: sp.GetRequiredService<LecteurAppBureau>()", enregistrement, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE SOURCE (APP-02, phase 29). Le titre de l'app n'est pas un signal : posé AVANT l'arbitrage, il
    /// deviendrait un critère de départage caché (l'égalité de record l'inclut) ; posé par une source d'activité, il
    /// ferait de l'app une source de lignes. Il n'a donc qu'un siège : le moniteur, sur les RETENUS, après
    /// <c>ArbitrageSessions.Trancher</c>. Parmi tous les fichiers de <c>Services/</c>, seul <c>SessionMonitor.cs</c>
    /// écrit <c>with { Titre</c>, une fois, et son enrichissement vient après l'arbitrage dans le texte.
    /// </summary>
    [Fact]
    public void Le_titre_est_pose_apres_l_arbitrage_et_nulle_part_ailleurs()
    {
        var services = Path.Combine(CheminSources(), "Services");
        var fichiers = Directory.GetFiles(services, "*.cs", SearchOption.TopDirectoryOnly);
        Assert.True(fichiers.Length >= 40,
            $"Seulement {fichiers.Length} fichiers dans {services} : la garde lirait le mauvais dossier et serait muette.");

        const string pose = "with { Titre";
        var porteurs = fichiers
            .Where(f => File.ReadAllText(f).Contains(pose, StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f)!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "SessionMonitor.cs" }, porteurs);

        var moniteur = File.ReadAllText(Path.Combine(services, "SessionMonitor.cs"));
        var occurrences = 0;
        for (var i = moniteur.IndexOf(pose, StringComparison.Ordinal); i >= 0; i = moniteur.IndexOf(pose, i + 1, StringComparison.Ordinal))
            occurrences++;
        Assert.Equal(1, occurrences);

        var trancher = moniteur.IndexOf("ArbitrageSessions.Trancher(", StringComparison.Ordinal);
        var enrichir = moniteur.IndexOf("Enrichir(", StringComparison.Ordinal);
        Assert.True(trancher >= 0, "ArbitrageSessions.Trancher( introuvable dans SessionMonitor.cs : la garde serait muette.");
        Assert.True(enrichir > trancher,
            $"Le titre est posé AVANT l'arbitrage (Enrichir( à {enrichir}, ArbitrageSessions.Trancher( à {trancher}).");
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

    /// <summary>
    /// GARDE DE DÉCOUPLAGE (LIB-04, réserve R4 de l'audit v1.6). L'ordre d'écran a changé en phase 28 : une
    /// attente déduite y passe devant un travail. Si l'arbitrage lisait encore cet ordre, ce changement
    /// d'affichage réécrirait EN SILENCE la règle de FUS-01. L'arbitrage a donc son propre rang, privé et
    /// figé ; cette garde lit le SOURCE, parce que la réflexion ne voit pas un appel de méthode.
    ///
    /// <para>Un <c>&lt;see cref="AffichageSessions.Urgence"/&gt;</c> sans parenthèse reste permis : un
    /// commentaire qui NOMME l'ordre d'écran pour dire qu'on ne le lit pas ne câble rien.</para>
    /// </summary>
    [Fact]
    public void L_arbitrage_ne_lit_pas_l_ordre_d_ecran()
    {
        var racine = CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");

        var fichier = Path.Combine(racine, "Services", "ArbitrageSessions.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");
        var texte = File.ReadAllText(fichier);

        // Anti-muet : le rang propre existe bien, sinon l'absence d'appel ci-dessous ne prouverait rien.
        Assert.Contains("private static int RangArbitrage(SessionActivity", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("AffichageSessions.Urgence(", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE CÂBLAGE (TRT-03, phase 26). Le geste explicite n'existe pour l'utilisateur que s'il est
    /// LIÉ : une commande peut être parfaitement testée et n'être offerte nulle part, et le défaut ne se
    /// verrait alors que chez lui, en silence — c'est le motif exact des gardes de câblage des phases 21
    /// et 23. La réflexion ne voit pas un binding XAML, d'où ce contrôle de SOURCE.
    ///
    /// <para>L'ORDRE et les libellés sont la sécurité, pas la décoration : le geste RÉVERSIBLE en tête, le
    /// geste DÉFINITIF en queue derrière un séparateur, et chacun annonce s'il revient. Le verrou établi
    /// dans ce projet — <c>LoginClaudeCommand</c> bascule, et un clic effacerait le coffre de jetons —
    /// s'applique ici mot pour mot.</para>
    /// </summary>
    [Fact]
    public void Les_huit_menus_offrent_le_geste_explicite_et_disent_la_verite()
    {
        var fichier = Path.Combine(CheminSources(), "Resources", "SessionStyles.xaml");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        // Une garde qui lirait un fichier vide serait muette : on exige d'abord les 8 templates.
        Assert.Equal(8, System.Text.RegularExpressions.Regex.Matches(texte, "DataTemplate x:Key=").Count);

        // Trois entrées par menu, sur les huit menus.
        Assert.Equal(24, System.Text.RegularExpressions.Regex.Matches(texte, "<MenuItem").Count);
        Assert.Equal(8, System.Text.RegularExpressions.Regex.Matches(texte, "MarquerTraiteeCommand").Count);
        Assert.Equal(8, System.Text.RegularExpressions.Regex.Matches(texte, "MarquerToutTraiteCommand").Count);
        Assert.Equal(8, System.Text.RegularExpressions.Regex.Matches(texte, "ArchiveCommand").Count);

        // Le séparateur écarte le geste définitif du geste ordinaire.
        Assert.Equal(8, System.Text.RegularExpressions.Regex.Matches(texte, "<Separator/>").Count);

        // L'ancien libellé promettait dans son menu ce que le code ne tenait pas (TRT-04).
        Assert.DoesNotContain("retirer de l'overlay", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE NON-RETOUR (OBS-01 étendu, APP-04, phase 29). La section « Source app-bureau » du rapport lit la lecture
    /// de l'app que le moniteur a rendue au SEUL appel <c>Inspecter</c> — celle qui a qualifié les lignes de l'écran.
    ///
    /// <para>Un second lecteur, un second <c>Lire</c> ou une résolution de racines propre au rapport décriraient une
    /// autre lecture que celle du widget : un autre instant, un autre cache, peut-être une autre racine. C'est la classe
    /// d'erreur que la phase 22 a fermée pour les sessions, rouverte une octave plus bas. Le rapport ne résout donc
    /// aucune racine lui-même : il lit celles que la lecture du moniteur a cherchées.</para>
    /// </summary>
    [Fact]
    public void Le_diagnostic_lit_la_source_app_bureau_dans_la_meme_lecture_que_le_widget()
    {
        var fichier = Path.Combine(CheminSources(), "Services", "DiagnosticService.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        // Anti-muet : le rapport et sa section existent, sinon les absences ci-dessous ne prouveraient rien.
        Assert.Contains("BuildReportAsync", texte, StringComparison.Ordinal);
        Assert.Contains("Source app-bureau", texte, StringComparison.Ordinal);
        Assert.Contains("lecture.AppBureau", texte, StringComparison.Ordinal);

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(texte, System.Text.RegularExpressions.Regex.Escape("Inspecter(")));   // xUnit2013 : même assertion (exactement une occurrence)
        Assert.DoesNotContain("new LecteurAppBureau", texte, StringComparison.Ordinal);
        Assert.DoesNotContain(".Lire(", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("RacinesEtat", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE NON-RETOUR (OBS-01 étendu, LUE-03, LUE-04, phase 30). La cause de chaque masquage et la section « Règle
    /// « lue » » du rapport se lisent dans la lecture que le moniteur a rendue au SEUL appel <c>Inspecter</c> : la cause
    /// sur <c>SessionMasquee</c>, la sélection sur <c>LectureAppBureau</c>, le premier plan sur
    /// <c>LectureSessions.PremierPlan</c>.
    ///
    /// <para>Un second appel à la sonde décrirait un autre échantillon que celui que le détecteur a reçu à ce cycle : le
    /// rapport pourrait alors dire « claude au premier plan » à côté d'une session qui n'a pas été lue, ou l'inverse. Le
    /// rapport ne connaît donc ni l'interface de la sonde ni son implémentation Win32 : il lit des valeurs.</para>
    /// </summary>
    [Fact]
    public void Le_diagnostic_dit_la_regle_lue_dans_la_meme_lecture_que_le_widget()
    {
        var fichier = Path.Combine(CheminSources(), "Services", "DiagnosticService.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        // Anti-muet : la section, sa lecture du premier plan et la cause des masquées existent, sinon les absences
        // ci-dessous ne prouveraient rien.
        Assert.Contains("Règle « lue »", texte, StringComparison.Ordinal);
        Assert.Contains("lecture.PremierPlan", texte, StringComparison.Ordinal);
        Assert.Contains("LibelleMasquage(m.Motif, m.Cause)", texte, StringComparison.Ordinal);

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(texte, System.Text.RegularExpressions.Regex.Escape("Inspecter(")));
        Assert.DoesNotContain("IPremierPlan", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("PremierPlanWin32", texte, StringComparison.Ordinal);
        Assert.DoesNotContain(".Lire(", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("new LecteurAppBureau", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE PLACEMENT (CPT-03, phase 32). Le verrou mono-instance ne vaut que par sa POSITION dans <c>OnStartup</c> :
    /// posé avant les court-circuits, il ferait échouer les hooks <c>--hook</c> (Claude Code en lance jusqu'à 5 en parallèle)
    /// et le mode <c>--statusline</c> ; posé après le Host, la seconde instance aurait déjà démarré ses services, réconcilié
    /// <c>~/.claude/settings.json</c> et écrasé <c>chronos.log</c> — exactement ce qui s'est produit le 2026-09-27 avec trois exe.
    /// Contrôle de SOURCE (<c>OnStartup</c> monte un host WPF, il n'est pas instanciable sous test) : l'acquisition est unique,
    /// vient après le DERNIER court-circuit et avant <c>Host.CreateApplicationBuilder()</c>, et entre les deux la seconde
    /// instance le dit (« tourne déjà ») et se retire (<c>Shutdown();</c>) sans jamais tuer l'autre.
    /// </summary>
    [Fact]
    public void Le_verrou_mono_instance_est_pose_apres_les_court_circuits_et_avant_le_Host()
    {
        var fichier = Path.Combine(CheminSources(), "App.xaml.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        const string acquisition = "VerrouInstanceUnique.Acquerir(VerrouInstanceUnique.NomOverlay)";
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(texte, System.Text.RegularExpressions.Regex.Escape(acquisition)));

        var iVerrou   = texte.IndexOf(acquisition, StringComparison.Ordinal);
        var iHost     = texte.IndexOf("Host.CreateApplicationBuilder()", StringComparison.Ordinal);
        var iStatus   = texte.LastIndexOf("\"--statusline\"", StringComparison.Ordinal);
        var iHook     = texte.LastIndexOf("\"--hook\"", StringComparison.Ordinal);
        var iCadrans  = texte.LastIndexOf("\"--cadrans\"", StringComparison.Ordinal);
        var iSessions = texte.LastIndexOf("\"--sessions\"", StringComparison.Ordinal);

        Assert.True(iVerrou >= 0, "Acquisition du verrou introuvable dans App.xaml.cs");
        Assert.True(iHost >= 0, "Construction du Host introuvable dans App.xaml.cs");
        Assert.True(iStatus >= 0 && iHook >= 0 && iCadrans >= 0 && iSessions >= 0, "Un court-circuit CLI a disparu d'App.xaml.cs");

        Assert.True(iStatus < iVerrou, "Le verrou doit venir APRÈS le court-circuit --statusline");
        Assert.True(iHook < iVerrou, "Le verrou doit venir APRÈS le court-circuit --hook (les hooks restent multi-instances)");
        Assert.True(iCadrans < iVerrou, "Le verrou doit venir APRÈS le court-circuit --cadrans");
        Assert.True(iSessions < iVerrou, "Le verrou doit venir APRÈS le court-circuit --sessions");
        Assert.True(iVerrou < iHost, "Le verrou doit venir AVANT Host.CreateApplicationBuilder()");

        // Entre l'acquisition et le Host : la seconde instance le DIT et se retire, sans tuer l'autre.
        var entre = texte[iVerrou..iHost];
        Assert.Contains("tourne déjà", entre, StringComparison.Ordinal);
        Assert.Contains("Shutdown();", entre, StringComparison.Ordinal);
        Assert.DoesNotContain(".Kill(", entre, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE SORTIE (CPT-03, phase 32). Quand la seconde instance se retire AVANT le Host, <c>OnExit</c> s'exécute quand
    /// même : un <c>_host.StopAsync()</c> inconditionnel y lèverait une <c>NullReferenceException</c> — le message « tourne
    /// déjà » serait suivi d'un plantage, et l'utilisateur en conclurait que la nouvelle version est cassée. La sortie doit
    /// donc tester le Host, puis libérer le verrou sur le thread UI (celui qui l'a acquis : <c>ReleaseMutex</c> l'exige).
    /// </summary>
    [Fact]
    public void La_sortie_tolere_un_Host_jamais_construit_et_libere_le_verrou()
    {
        var fichier = Path.Combine(CheminSources(), "App.xaml.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        var debut = texte.IndexOf("protected override void OnExit(", StringComparison.Ordinal);
        Assert.True(debut >= 0, "OnExit introuvable dans App.xaml.cs");
        var fin = texte.IndexOf("base.OnExit(e);", debut, StringComparison.Ordinal);
        Assert.True(fin > debut, "Fin d'OnExit (base.OnExit) introuvable");

        var onExit = texte[debut..fin];
        Assert.Contains("_host is not null", onExit, StringComparison.Ordinal);
        Assert.Contains("_verrou?.Liberer()", onExit, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE CÂBLAGE (JRN-01/JRN-02, CPT-02, phase 32 — 32-05). Le journal ne vaut que par sa POSITION dans la chaîne :
    /// au-dessus de la tête, il verrait les planchers et les fenêtres du magasin (jamais un exact frais) ; au-dessous du
    /// composite, il ne verrait qu'une source. Il doit envelopper le composite et être enveloppé par la tête. Et son service
    /// hébergé doit être inscrit AVANT l'orchestrateur : sinon « demarrage » suivrait le premier relevé et « arret »
    /// précéderait le dernier. Enfin, l'écriture ratée du dernier exact doit être ABONNÉE au journal — c'est le canal qui
    /// aurait dit, deux semaines plus tôt, que rien ne se figeait (CPT-02). Contrôle de SOURCE, comme les gardes voisines :
    /// <c>ConfigureServices</c> n'est pas instanciable sous test sans toucher au vrai %APPDATA%.
    /// </summary>
    [Fact]
    public void Le_journal_enveloppe_le_composite_et_la_tete_enveloppe_le_journal()
    {
        var fichier = Path.Combine(CheminSources(), "App.xaml.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        // 1) La chaîne exacte est l'inner du décorateur de journalisation.
        var iDecorateur = texte.IndexOf("new JournalisationUsageProvider(", StringComparison.Ordinal);
        var iComposite  = texte.IndexOf("inner: new CompositeUsageProvider(", StringComparison.Ordinal);
        Assert.True(iDecorateur >= 0, "Le décorateur de journalisation n'est plus instancié dans App.xaml.cs");
        Assert.True(iComposite >= 0, "La chaîne composite n'est plus l'inner de quiconque dans App.xaml.cs");
        Assert.True(iDecorateur < iComposite, "La chaîne exacte doit être l'inner du décorateur de journalisation (le journal voit l'inner BRUT)");

        // 2) La tête enveloppe le décorateur (et pas le composite directement).
        var iTete = texte.IndexOf("new LastExactUsageProvider(", StringComparison.Ordinal);
        Assert.True(iTete >= 0, "La tête LastExactUsageProvider n'est plus instanciée dans App.xaml.cs");
        var blocTete = texte.Substring(iTete, Math.Min(600, texte.Length - iTete));
        Assert.True(blocTete.Contains("inner: journalisation", StringComparison.Ordinal)
                    || blocTete.Contains("inner: sp.GetRequiredService<JournalisationUsageProvider>()", StringComparison.Ordinal),
                    "L'inner de la tête doit être le décorateur de journalisation");
        Assert.True(iComposite < iTete, "Le composite est construit AVANT la tête (il est enterré sous le journal)");

        // 3) Service hébergé du journal inscrit UNE fois, AVANT celui de l'orchestrateur.
        const string hebergeJournal = "AddHostedService(sp => sp.GetRequiredService<JournalisationUsageProvider>())";
        const string hebergeOrchestrateur = "AddHostedService(sp => sp.GetRequiredService<RefreshOrchestrator>())";
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(texte, System.Text.RegularExpressions.Regex.Escape(hebergeJournal)));
        var iHebergeJournal = texte.IndexOf(hebergeJournal, StringComparison.Ordinal);
        var iHebergeOrchestrateur = texte.IndexOf(hebergeOrchestrateur, StringComparison.Ordinal);
        Assert.True(iHebergeOrchestrateur >= 0, "L'orchestrateur n'est plus un service hébergé dans App.xaml.cs");
        Assert.True(iHebergeJournal < iHebergeOrchestrateur,
                    "Le service hébergé du journal doit être inscrit AVANT celui de l'orchestrateur (« demarrage » avant le premier relevé)");

        // 4) L'écriture ratée du dernier exact est abonnée au journal (CPT-02).
        Assert.Contains("EcritureRatee +=", texte, StringComparison.Ordinal);
        Assert.Contains("SignalerEcritureRatee(\"last-exact\"", texte, StringComparison.Ordinal);

        // 5) Un seul composite par niveau — trois au total, comme avant le journal : rien n'a été dupliqué en déplaçant la chaîne.
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(texte, System.Text.RegularExpressions.Regex.Escape("new CompositeUsageProvider(")).Count);
    }

    /// <summary>Le chemin des sources est INJECTÉ par MSBuild, jamais deviné (Assembly.Location est VIDE
    /// en publication mono-fichier). Motif recopié de <c>GardesDoctrineTests</c>.</summary>
    /// <summary>
    /// GARDE DE PLACEMENT (HIS-01, D-34-26). Le mode <c>--historique</c> ouvre la fenêtre Historique réelle sur la semaine de
    /// référence des maquettes : il doit être branché AVANT <c>VerrouInstanceUnique.Acquerir</c> (multi-instances, comme
    /// <c>--cadrans</c> / <c>--sessions</c>) et ne résoudre AUCUN service — ni Host, ni <c>GetRequiredService</c>, ni réconciliation
    /// des hooks : une galerie qui passerait par le Host écrirait <c>chronos.log</c>, réconcilierait <c>~/.claude/settings.json</c> et
    /// se ferait refuser par le verrou dès que l'overlay tourne. Contrôle de SOURCE : <c>OnStartup</c> n'est pas instanciable sous test.
    /// </summary>
    [Fact]
    public void Le_mode_historique_precede_le_verrou_et_ne_resout_aucun_service()
    {
        var fichier = Path.Combine(CheminSources(), "App.xaml.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        var i = texte.IndexOf("\"--historique\"", StringComparison.Ordinal);
        Assert.True(i > 0, "la branche --historique est absente de App.xaml.cs");
        var v = texte.IndexOf("VerrouInstanceUnique.Acquerir", StringComparison.Ordinal);
        Assert.True(v > 0, "le verrou d'instance unique est introuvable (garde muette)");
        Assert.True(i < v, "--historique doit précéder le verrou d'instance unique (multi-instances, comme --sessions)");

        var fin = texte.IndexOf("return;", i, StringComparison.Ordinal);
        Assert.True(fin > i, "fin de la branche --historique introuvable");
        var bloc = texte[i..fin];
        Assert.Contains("HistoriqueGalerie.Creer(", bloc, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRequiredService", bloc, StringComparison.Ordinal);
        Assert.DoesNotContain("Host", bloc, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfigureServices", bloc, StringComparison.Ordinal);
        Assert.DoesNotContain("Reconcil", bloc, StringComparison.Ordinal);
    }

    internal static string CheminSources()
        => typeof(GardesPerimetreTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminSourcesChronos")?.Value
           ?? "";
}
