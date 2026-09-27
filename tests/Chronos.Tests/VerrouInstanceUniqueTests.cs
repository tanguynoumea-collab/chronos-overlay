using System.Threading;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// CPT-03 — le verrou mono-instance de l'overlay, prouvé sur de VRAIS threads : la propriété d'un mutex Windows est
/// par thread (jamais <c>Task.Run</c>, dont le thread de pool pourrait être réutilisé et fausser le résultat).
///
/// <para>POURQUOI ces tests et pas un lancement de l'exe : le 2026-09-27, trois exécutables (3.1.0, 3.2.0, 3.2.1)
/// tournaient ensemble et réécrivaient les mêmes fichiers ; la mécanique se prouve ici, le constat en vrai est VAL-04
/// (32-08). Chaque test emploie un nom de mutex UNIQUE (<c>Local\Chronos-test-&lt;guid&gt;</c>) : aucun test ne peut en
/// gêner un autre, ni gêner un overlay en marche sur la machine (dont le nom est <c>Local\Chronos-overlay</c>).</para>
/// </summary>
public sealed class VerrouInstanceUniqueTests
{
    private static string NomUnique() => @"Local\Chronos-test-" + Guid.NewGuid().ToString("N");

    [Fact]
    public void La_premiere_acquisition_reussit_et_n_est_pas_un_abandon()
    {
        var nom = NomUnique();

        var r = VerrouInstanceUnique.Acquerir(nom);
        try
        {
            Assert.True(r.Obtenu);
            Assert.False(r.Abandonne);
            Assert.NotNull(r.Mutex);
            Assert.Equal(nom, r.Nom);
        }
        finally { r.Liberer(); }
    }

    [Fact]
    public void Une_seconde_acquisition_du_meme_nom_sur_un_autre_thread_echoue()
    {
        var nom = NomUnique();
        using var aTient = new ManualResetEventSlim(false);   // A signale : « je tiens le verrou »
        using var relache = new ManualResetEventSlim(false);  // le test signale à A : « libère et termine »
        ResultatVerrou? resultatA = null;
        ResultatVerrou? resultatB = null;

        var a = new Thread(() =>
        {
            resultatA = VerrouInstanceUnique.Acquerir(nom);
            aTient.Set();
            relache.Wait();
            resultatA.Liberer();   // sur le thread propriétaire, comme l'exige ReleaseMutex
        });
        a.Start();
        Assert.True(aTient.Wait(TimeSpan.FromSeconds(5)), "Le thread A n'a pas acquis le verrou dans le délai");
        Assert.True(resultatA!.Obtenu);

        var b = new Thread(() => resultatB = VerrouInstanceUnique.Acquerir(nom));
        b.Start();
        b.Join();

        relache.Set();
        a.Join();

        Assert.NotNull(resultatB);
        Assert.False(resultatB!.Obtenu);
        Assert.Null(resultatB.Mutex);
        Assert.False(resultatB.Abandonne);
    }

    [Fact]
    public void Apres_liberation_la_seconde_acquisition_reussit()
    {
        var nom = NomUnique();
        ResultatVerrou? resultatB = null;

        var a = new Thread(() =>
        {
            var r = VerrouInstanceUnique.Acquerir(nom);
            Assert.True(r.Obtenu);
            r.Liberer();   // libération PROPRE avant la fin du thread : ce n'est pas un abandon
        });
        a.Start();
        a.Join();

        var b = new Thread(() =>
        {
            resultatB = VerrouInstanceUnique.Acquerir(nom);
            resultatB.Liberer();
        });
        b.Start();
        b.Join();

        Assert.NotNull(resultatB);
        Assert.True(resultatB!.Obtenu);
        Assert.False(resultatB.Abandonne);
    }

    [Fact]
    public void Un_proprietaire_mort_sans_liberer_rend_le_verrou_comme_abandonne_mais_obtenu()
    {
        var nom = NomUnique();
        // On GARDE la référence au résultat de A : le handle reste ouvert, l'objet noyau survit au thread, et le GC ne
        // peut pas le collecter — c'est exactement la situation d'une instance dont le thread UI est mort sans OnExit.
        ResultatVerrou? resultatA = null;
        ResultatVerrou? resultatB = null;

        var a = new Thread(() => resultatA = VerrouInstanceUnique.Acquerir(nom));
        a.Start();
        a.Join();   // A se termine SANS Liberer() : mutex abandonné
        Assert.True(resultatA!.Obtenu);

        var b = new Thread(() =>
        {
            resultatB = VerrouInstanceUnique.Acquerir(nom);
            resultatB.Liberer();
        });
        b.Start();
        b.Join();

        Assert.NotNull(resultatB);
        Assert.True(resultatB!.Obtenu, "Un mutex abandonné doit être ACQUIS par le suivant (D-32-10), pas refusé");
        Assert.True(resultatB.Abandonne, "L'abandon doit être signalé pour être noté au démarrage");

        GC.KeepAlive(resultatA);
        resultatA.Liberer();   // ménage : ce thread n'est pas propriétaire, Liberer doit le tolérer
    }

    /// <summary>« tenu par un autre processus » n'est pas observable depuis un seul processus de test : il est
    /// documenté sur <see cref="VerrouInstanceUnique.EtatPourDiagnostic"/> et se constate en vrai en 32-08 (VAL-04).</summary>
    [Fact]
    public void L_etat_pour_le_diagnostic_dit_tenu_par_ce_processus_puis_libre()
    {
        var nom = NomUnique();

        Assert.Equal("libre", VerrouInstanceUnique.EtatPourDiagnostic(nom));

        var r = VerrouInstanceUnique.Acquerir(nom);
        Assert.True(r.Obtenu);
        Assert.Equal("tenu par ce processus", VerrouInstanceUnique.EtatPourDiagnostic(nom));

        r.Liberer();
        Assert.Equal("libre", VerrouInstanceUnique.EtatPourDiagnostic(nom));
    }
}
