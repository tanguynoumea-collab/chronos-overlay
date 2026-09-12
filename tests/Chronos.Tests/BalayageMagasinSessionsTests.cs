using System.IO;
using System.Reflection;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// CYC-01 — le magasin d'états de session cesse de ne faire que grandir, SANS qu'un balayage devienne une
/// façon détournée de conclure.
///
/// <para>Deux propriétés sont tenues ici, et elles ne se déduisent pas l'une de l'autre :</para>
/// <list type="number">
///   <item>le critère d'ÂGE n'est pas le seul — une session attestée vivante survit quel que soit l'âge de
///         son fichier, sinon un utilisateur qui laisse tourner une session plusieurs jours la verrait
///         effacée ;</item>
///   <item>expirer, c'est NE PLUS SAVOIR — après balayage, la session n'est ni visible ni masquée : aucun
///         verdict n'a été rendu, elle a simplement disparu.</item>
/// </list>
///
/// <para>SÉCURITÉ : ce code SUPPRIME des fichiers. Tout test passe par un dossier temporaire vérifié par
/// <c>Assert.StartsWith(Path.GetTempPath(), …)</c> AVANT toute construction du balayeur. Aucun test ne
/// peut viser le vrai magasin de l'utilisateur (%APPDATA%\Chronos\sessions).</para>
/// </summary>
public class BalayageMagasinSessionsTests
{
    private static readonly DateTimeOffset Maintenant = new(2026, 9, 12, 13, 0, 0, TimeSpan.Zero);

    private static string TempDossier()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-balayage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // ce code SUPPRIME des fichiers
        return d;
    }

    private static BalayageMagasinSessions Balayeur(string dossier, params SessionSnapshot[] vivantes)
    {
        var b = new BalayageMagasinSessions(dossier, new SourceFixe(vivantes), new FakeClock(Maintenant));
        Assert.StartsWith(Path.GetTempPath(), b.Dossier);   // garde anti-accident, AVANT tout balayage
        return b;
    }

    // TOUT horodatage est ancré sur l'horloge INJECTÉE (Maintenant), jamais sur celle du système : c'est
    // le remède au piège qui a rendu rouges trois plans de la phase 22 sans qu'aucun code ne bouge.
    private static string EcrireEtat(string dossier, string sid, SessionActivity activite, TimeSpan age)
    {
        var instant = Maintenant - age;
        var f = Path.Combine(dossier, sid + ".json");
        File.WriteAllText(f, SessionHookProcessor.BuildStateJson(
            sid, "Projet-" + sid, activite, null, instant.ToUnixTimeMilliseconds()));
        File.SetLastWriteTimeUtc(f, instant.UtcDateTime);
        return f;
    }

    private static string EcrireDebris(string dossier, string sid, int pid, TimeSpan age)
    {
        var f = Path.Combine(dossier, sid + ".json.tmp-" + pid);
        File.WriteAllText(f, "{}");
        File.SetLastWriteTimeUtc(f, (Maintenant - age).UtcDateTime);
        return f;
    }

    // Fichier d'état ILLISIBLE : il ne porte aucune date déclarée, donc seule sa date d'écriture peut le
    // dater — un fait observé sur le fichier, jamais une date inventée.
    private static string EcrireEtatIllisible(string dossier, string sid, TimeSpan age)
    {
        var f = Path.Combine(dossier, sid + ".json");
        File.WriteAllText(f, "ceci n'est pas du JSON {{{");
        File.SetLastWriteTimeUtc(f, (Maintenant - age).UtcDateTime);
        return f;
    }

    private static SessionSnapshot Snap(string id, SessionActivity a, DateTimeOffset maj)
        => new(id, "Proj-" + id, a, null, maj);

    private sealed class SourceFixe : ISessionSource
    {
        private readonly IReadOnlyList<SessionSnapshot> _snaps;
        public SourceFixe(params SessionSnapshot[] snaps) => _snaps = snaps;
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => _snaps;
    }

    /// <summary>
    /// Le seuil de balayage (72 h) vaut NEUF FOIS celui au-delà duquel le moniteur cesse d'afficher un
    /// fichier (8 h) : on ne balaie jamais quelque chose que le widget pourrait encore montrer. Le fichier
    /// de 10 h est le cas qui le prouve — il a quitté l'écran, il reste sur le disque.
    /// </summary>
    [Fact]
    public void Un_etat_perime_et_sans_attestation_est_retire_un_etat_encore_affichable_reste()
    {
        var dossier = TempDossier();
        var tresVieux = EcrireEtat(dossier, "quarante-jours", SessionActivity.Working, TimeSpan.FromDays(40));
        var vieux = EcrireEtat(dossier, "dix-jours", SessionActivity.Working, TimeSpan.FromDays(10));
        var dixHeures = EcrireEtat(dossier, "dix-heures", SessionActivity.WaitingTurn, TimeSpan.FromHours(10));
        var uneHeure = EcrireEtat(dossier, "une-heure", SessionActivity.Working, TimeSpan.FromHours(1));

        var bilan = Balayeur(dossier).Balayer();

        Assert.Equal(2, bilan.EtatsRetires);
        Assert.Equal(2, bilan.EtatsConserves);
        Assert.False(File.Exists(tresVieux));
        Assert.False(File.Exists(vieux));
        Assert.True(File.Exists(dixHeures), "un fichier de 10 h n'est plus AFFICHÉ mais reste bien en deçà des 72 h");
        Assert.True(File.Exists(uneHeure));
    }

    /// <summary>
    /// LE CRITÈRE N°4 DU ROADMAP, dans son versant « le critère d'âge n'est pas le seul ». Balayer par
    /// l'âge seul et balayer sur le critère double produisent le MÊME écran le jour de la livraison ; au
    /// bout de trois jours, l'un a effacé une session réellement vivante et l'autre non.
    /// </summary>
    [Fact]
    public void Une_session_vivante_depuis_des_jours_survit_au_nettoyage()
    {
        var dossier = TempDossier();
        var fichier = EcrireEtat(dossier, "vivante", SessionActivity.Working, TimeSpan.FromDays(40));

        var bilan = Balayeur(dossier, Snap("vivante", SessionActivity.Working, Maintenant)).Balayer();

        Assert.Equal(0, bilan.EtatsRetires);
        Assert.Equal(1, bilan.EtatsConserves);
        Assert.True(File.Exists(fichier),
            "une session attestée vivante survit quel que soit l'âge de son fichier d'état");
    }

    /// <summary>
    /// Le cas MESURÉ le 2026-09-12 : cinq fichiers temporaires abandonnés portant le MÊME identifiant de
    /// session avec cinq identifiants de processus différents — débris du fan-out des hooks concurrents.
    /// Un débris n'est l'état de personne : il n'a jamais atteint sa destination, il n'y a rien à attester.
    /// Mais un débris tout frais peut être une écriture EN COURS.
    /// </summary>
    [Fact]
    public void Les_debris_temporaires_partent_et_un_debris_tout_frais_est_epargne()
    {
        var dossier = TempDossier();
        var debris = new[] { 3164, 8812, 12420, 22016, 31572 }
            .Select(pid => EcrireDebris(dossier, "meme-session", pid, TimeSpan.FromDays(3)))
            .ToList();
        var frais = EcrireDebris(dossier, "ecriture-en-cours", 41000, TimeSpan.FromMinutes(2));

        var bilan = Balayeur(dossier).Balayer();

        Assert.Equal(5, bilan.TemporairesRetires);
        Assert.All(debris, f => Assert.False(File.Exists(f)));
        Assert.True(File.Exists(frais), "un hook est peut-être en train d'écrire : on ne lui arrache pas son fichier");
    }

    /// <summary>
    /// Ne pas savoir quand un fichier a été écrit n'autorise pas à le supprimer — mais un fichier illisible
    /// porte tout de même un fait observable : sa date d'écriture.
    /// </summary>
    [Fact]
    public void Un_etat_illisible_est_date_par_son_ecriture_jamais_par_une_date_inventee()
    {
        var dossier = TempDossier();
        var recent = EcrireEtatIllisible(dossier, "illisible-recent", TimeSpan.FromHours(1));
        var vieux = EcrireEtatIllisible(dossier, "illisible-vieux", TimeSpan.FromDays(40));

        var bilan = Balayeur(dossier).Balayer();

        Assert.Equal(1, bilan.EtatsRetires);
        Assert.Equal(1, bilan.EtatsConserves);
        Assert.True(File.Exists(recent));
        Assert.False(File.Exists(vieux));
    }

    /// <summary>
    /// Précédent <c>Sans_fantome_le_fichier_de_l_utilisateur_n_est_pas_reecrit</c> (phase 21) : rien à
    /// retirer ⇒ rien n'est touché, date de dernière écriture comprise. Un balayage qui réécrirait les
    /// survivants rajeunirait les fichiers qu'il prétend dater.
    /// </summary>
    [Fact]
    public void Rien_a_retirer_ne_touche_rien()
    {
        var dossier = TempDossier();
        var fichier = EcrireEtat(dossier, "intact", SessionActivity.WaitingAttention, TimeSpan.FromHours(1));
        var dateAvant = File.GetLastWriteTimeUtc(fichier);
        var contenuAvant = File.ReadAllText(fichier);

        var bilan = Balayeur(dossier).Balayer();

        Assert.Equal(new BilanBalayage(0, 0, 1), bilan);
        Assert.Equal(dateAvant, File.GetLastWriteTimeUtc(fichier));
        Assert.Equal(contenuAvant, File.ReadAllText(fichier));
    }

    /// <summary>
    /// LE TEST DE DOCTRINE — critère n°4 du ROADMAP dans son versant « expirer, c'est ne plus savoir ».
    ///
    /// <para>Deux livraisons produisent le même écran : celle qui marque la session comme traitée ou
    /// archivée (un VERDICT rendu sur une session dont on ne sait plus rien) et celle qui supprime le
    /// fichier sans rien écrire ailleurs. Seul le comportement les distingue : après balayage, la session
    /// n'est NI visible NI masquée, et aucun magasin de verdict n'a été créé.</para>
    /// </summary>
    [Fact]
    public void Un_etat_balaye_disparait_sans_etre_declare_traite_ni_archive()
    {
        var dossier = TempDossier();
        var cheminArchives = Path.Combine(TempDossier(), "archived.json");
        var cheminTraitees = Path.Combine(TempDossier(), "treated.json");
        EcrireEtat(dossier, "balayee", SessionActivity.WaitingAttention, TimeSpan.FromDays(40));

        var bilan = Balayeur(dossier).Balayer();
        Assert.Equal(1, bilan.EtatsRetires);

        var moniteur = new SessionMonitor(dossier, new SourceFixe(),
            new ArchiveStore(cheminArchives), new TreatedStore(cheminTraitees));
        var lecture = moniteur.Inspecter(Maintenant);

        Assert.Empty(lecture.Visibles);
        Assert.Empty(lecture.Masquees);
        Assert.Equal(0, lecture.FichiersEcartesParAnciennete);
        Assert.False(File.Exists(cheminArchives), "balayer n'archive rien");
        Assert.False(File.Exists(cheminTraitees), "balayer ne déclare rien traité");
    }

    /// <summary>
    /// GARDE PAR RÉFLEXION, insensible au texte des commentaires : le balayeur ne CONNAÎT aucun magasin de
    /// verdict. Un commentaire peut jurer que « balayer ne conclut rien » pendant qu'un champ d'un type de
    /// verdict siège dans la classe ; la réflexion, elle, ne lit pas les intentions.
    /// </summary>
    [Fact]
    public void Le_balayage_ne_connait_aucun_magasin_de_verdict()
    {
        var t = typeof(BalayageMagasinSessions);
        var champs = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotEmpty(champs);                       // une garde qui ne verrait aucun champ serait muette
        Assert.DoesNotContain(champs, f => f.FieldType == typeof(TreatedStore)
                                        || f.FieldType == typeof(ArchiveStore));

        var parametres = t.GetConstructors().SelectMany(c => c.GetParameters()).ToList();
        Assert.NotEmpty(parametres);
        Assert.DoesNotContain(parametres, p => p.ParameterType == typeof(TreatedStore)
                                            || p.ParameterType == typeof(ArchiveStore));

        // L'horloge est injectable : c'est la condition pour que ce mécanisme n'ait pas de test à retardement.
        Assert.Contains(parametres, p => p.ParameterType == typeof(IClock));
    }

    /// <summary>
    /// Un balayage best-effort au démarrage ne doit jamais empêcher un lancement : un dossier absent est le
    /// cas nominal d'une première installation.
    /// </summary>
    [Fact]
    public void Dossier_absent_rend_un_bilan_nul_sans_exception()
    {
        var absent = Path.Combine(TempDossier(), "jamais-cree");
        Assert.False(Directory.Exists(absent));

        var bilan = Balayeur(absent).Balayer();

        Assert.Equal(new BilanBalayage(0, 0, 0), bilan);
    }
}
