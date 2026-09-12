using System.IO;
using System.Text.Json;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// CYC-02 — une écriture d'état de hook ne peut plus disparaître sans que personne ne le sache.
///
/// Le scénario central est celui MESURÉ sur la machine le 2026-09-12 : le widget relit le dossier toutes
/// les deux secondes en <c>FileShare.ReadWrite</c> (<c>SessionMonitor.TryRead</c>), et l'écriture par
/// fichier temporaire déplacé par-dessus la cible échouait alors 290 fois sur 500, en silence, en laissant
/// un débris derrière elle. Les 12 orphelins relevés dans le magasin en sont la trace.
///
/// Tous les tests écrivent dans des dossiers TEMPORAIRES : aucun ne touche le vrai %APPDATA%\Chronos.
/// </summary>
public class EcritureEtatSessionTests
{
    private static string TempDossier()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-ecriture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // ce code SUPPRIME et TRONQUE des fichiers
        return d;
    }

    private const string Sid = "11111111-2222-3333-4444-555555555555";

    // Instant LITTÉRAL : rien ici ne se compare à l'horloge du système, et les horodatages écrits en
    // dérivent tous (piège de test à retardement des plans de la phase 22).
    private static readonly DateTimeOffset Maintenant = new(2026, 9, 12, 13, 28, 0, TimeSpan.Zero);

    private static string StdinDe(string sid)
        => $$"""{"session_id":"{{sid}}","cwd":"C:/Users/x/PROJET OVERLAY"}""";

    private static SessionHookResult Ordre(string evenement, string sid, long ms)
        => SessionHookProcessor.Process(evenement, StdinDe(sid), ms);

    private sealed class SourceVide : ISessionSource
    {
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => System.Array.Empty<SessionSnapshot>();
    }

    /// <summary>
    /// LE test de ce plan : 200 écritures pendant qu'un lecteur tient la cible ouverte dans le mode EXACT
    /// de <c>SessionMonitor.TryRead</c>. Mesuré à 290 pertes sur 500 avec l'ancienne mécanique.
    /// </summary>
    [Fact]
    public void Sous_un_lecteur_concurrent_aucune_ecriture_n_est_perdue()
    {
        var dossier = TempDossier();
        var fic = Path.Combine(dossier, Sid + ".json");
        var baseMs = Maintenant.ToUnixTimeMilliseconds();

        // La cible doit exister pour qu'un lecteur puisse l'ouvrir : première écriture, sans lecteur.
        Assert.True(EcritureEtatSession.Appliquer(dossier, Ordre("Notification", Sid, baseMs)).Reussi);

        var echecs = 0;
        long dernier = 0;
        using (var verrou = new FileStream(fic, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            Assert.True(verrou.CanRead);   // le lecteur tient RÉELLEMENT le fichier pendant la boucle
            for (var i = 1; i <= 200; i++)
            {
                dernier = baseMs + i;
                if (!EcritureEtatSession.Appliquer(dossier, Ordre("Notification", Sid, dernier)).Reussi) echecs++;
            }
        }

        Assert.Equal(0, echecs);

        // Et ce n'est pas seulement « aucune erreur » : c'est bien la DERNIÈRE écriture qui est sur disque.
        using var doc = JsonDocument.Parse(File.ReadAllText(fic));
        Assert.Equal(dernier, doc.RootElement.GetProperty("updated_at").GetInt64());
    }

    /// <summary>
    /// Le second défaut corrigé : le silence. Une écriture impossible rend sa CAUSE — et ne lève pas, car
    /// un hook ne doit jamais casser la session Claude Code.
    /// </summary>
    [Fact]
    public void Un_echec_d_ecriture_est_rendu_avec_sa_cause_et_ne_leve_jamais()
    {
        var dossier = TempDossier();
        var fic = Path.Combine(dossier, Sid + ".json");
        File.WriteAllText(fic, "{}");

        ResultatEcritureEtat res;
        using (var exclusif = new FileStream(fic, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.True(exclusif.CanWrite);
            // Que le test atteigne l'assertion suivante prouve à lui seul qu'aucune exception n'a traversé.
            res = EcritureEtatSession.Appliquer(dossier, Ordre("Notification", Sid, Maintenant.ToUnixTimeMilliseconds()));
        }

        Assert.False(res.Reussi);
        Assert.False(string.IsNullOrWhiteSpace(res.Cause));
        // La cause n'est pas fabriquée : c'est le TYPE réellement levé qui la porte.
        Assert.Contains("Exception", res.Cause!, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionEnd_supprime_le_fichier_et_un_fichier_absent_n_est_pas_un_echec()
    {
        var dossier = TempDossier();
        var fic = Path.Combine(dossier, Sid + ".json");
        var baseMs = Maintenant.ToUnixTimeMilliseconds();

        Assert.True(EcritureEtatSession.Appliquer(dossier, Ordre("Notification", Sid, baseMs)).Reussi);
        Assert.True(File.Exists(fic));

        var suppression = EcritureEtatSession.Appliquer(dossier, Ordre("SessionEnd", Sid, baseMs));
        Assert.True(suppression.Reussi);
        Assert.False(File.Exists(fic));

        // Rien à faire n'est pas un échec (précédent ArchiveStore.PurgerPrefixe).
        var deuxieme = EcritureEtatSession.Appliquer(dossier, Ordre("SessionEnd", Sid, baseMs));
        Assert.True(deuxieme.Reussi);
        Assert.Null(deuxieme.Cause);
    }

    [Fact]
    public void Un_evenement_ignore_n_ecrit_rien_et_n_est_pas_un_echec()
    {
        var dossier = TempDossier();

        var res = EcritureEtatSession.Appliquer(dossier, SessionHookResult.Ignored);

        Assert.True(res.Reussi);
        Assert.Null(res.Cause);
        Assert.Empty(Directory.GetFiles(dossier));
    }

    /// <summary>La garde des 12 orphelins mesurés : plus aucun débris n'accompagne une écriture.</summary>
    [Fact]
    public void Une_ecriture_ne_laisse_aucun_debris()
    {
        var dossier = TempDossier();

        Assert.True(EcritureEtatSession.Appliquer(
            dossier, Ordre("Notification", Sid, Maintenant.ToUnixTimeMilliseconds())).Reussi);

        var noms = Directory.GetFiles(dossier).Select(f => Path.GetFileName(f)!).ToArray();
        Assert.Equal(new[] { Sid + ".json" }, noms);
    }

    /// <summary>
    /// Écrire sans être relu ne vaudrait rien : le consommateur RÉEL relit ce que le service vient d'écrire.
    /// </summary>
    [Fact]
    public void Le_contenu_ecrit_est_relisible_par_le_moniteur()
    {
        var dossier = TempDossier();

        Assert.True(EcritureEtatSession.Appliquer(
            dossier, Ordre("Notification", Sid, Maintenant.ToUnixTimeMilliseconds())).Reussi);

        var moniteur = new SessionMonitor(dossier, new SourceVide(),
                                          new ArchiveStore(Path.Combine(TempDossier(), "a.json")));

        var vue = Assert.Single(moniteur.Read(Maintenant));
        Assert.Equal(Sid, vue.SessionId);
        Assert.Equal(SessionActivity.WaitingAttention, vue.Activity);
    }
}
