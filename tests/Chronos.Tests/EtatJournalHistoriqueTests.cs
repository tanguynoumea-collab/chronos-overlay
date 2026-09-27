using System.IO;
using Chronos.Models.Historique;
using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// ACC-03 — ce que le dossier d'historique dit de lui-même, pour le diagnostic : l'inventaire de ses fichiers par familles
/// (relevés, agrégats de tokens, index des ids, curseurs, couverture), avec taille et date de modification, et les cinq
/// derniers événements du journal relus par le MÊME lecteur que la fenêtre. Un dossier absent ne lève jamais.
///
/// E/S temporaires uniquement (sous le dossier temp de l'utilisateur, supprimées au Dispose), jamais %APPDATA%.
/// </summary>
public sealed class EtatJournalHistoriqueTests : IDisposable
{
    private readonly string _dossier = Path.Combine(Path.GetTempPath(), "chronos-tests", "etat-journal-" + Guid.NewGuid().ToString("N"));

    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        try { if (Directory.Exists(_dossier)) Directory.Delete(_dossier, recursive: true); } catch { /* best-effort */ }
    }

    private void Fichier(string nom, int taille, DateTime modifieUtc)
    {
        Directory.CreateDirectory(_dossier);
        var chemin = Path.Combine(_dossier, nom);
        File.WriteAllBytes(chemin, new byte[taille]);
        File.SetLastWriteTimeUtc(chemin, modifieUtc);
    }

    private static DateTime Utc(int jour, int h) => new(2026, 9, jour, h, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void L_inventaire_liste_les_familles_dans_l_ordre_avec_taille_et_date()
    {
        // Créés dans un ordre qui n'est NI l'ordre attendu NI l'ordre alphabétique : le tri par familles est prouvé.
        Fichier("couverture.json", 6, Utc(26, 6));
        Fichier("autre.txt", 7, Utc(26, 7));
        Fichier("ids-2026-09.jsonl", 40, Utc(26, 8));
        Fichier("releves-2026-09.jsonl", 20, Utc(27, 11));
        Fichier("curseurs.json", 5, Utc(26, 9));
        Fichier("tokens-2026-09.jsonl", 30, Utc(26, 10));
        Fichier("releves-2026-08.jsonl", 10, new DateTime(2026, 8, 31, 23, 55, 0, DateTimeKind.Utc));

        var inventaire = EtatJournalHistorique.Inventaire(_dossier);

        Assert.Equal(new[] { "releves-2026-08.jsonl", "releves-2026-09.jsonl", "tokens-2026-09.jsonl", "ids-2026-09.jsonl", "curseurs.json", "couverture.json" },
                     inventaire.Select(f => f.Nom).ToArray());
        Assert.Equal(new long[] { 10, 20, 30, 40, 5, 6 }, inventaire.Select(f => f.Taille).ToArray());
        Assert.Equal(new DateTimeOffset(2026, 8, 31, 23, 55, 0, TimeSpan.Zero), inventaire[0].ModifieLe);
        Assert.Equal(new DateTimeOffset(Utc(27, 11)), inventaire[1].ModifieLe);
        Assert.Equal(new DateTimeOffset(Utc(26, 6)), inventaire[5].ModifieLe);
        Assert.All(inventaire, f => Assert.Equal(TimeSpan.Zero, f.ModifieLe.Offset));
        Assert.DoesNotContain(inventaire, f => f.Nom == "autre.txt");
    }

    [Fact]
    public void Un_dossier_absent_rend_un_inventaire_vide()
    {
        Assert.False(Directory.Exists(_dossier));

        Assert.Empty(EtatJournalHistorique.Inventaire(_dossier));
        Assert.Empty(EtatJournalHistorique.DerniersEvenements(_dossier, Now));
        Assert.False(Directory.Exists(_dossier));   // lire ne crée rien
    }

    [Fact]
    public void Les_cinq_derniers_evenements_dans_l_ordre()
    {
        var journal = new JournalReleves(_dossier, new FakeClock(Now));
        var evenements = new[]
        {
            new EvenementJournal(new DateTimeOffset(2026, 9, 25, 8, 0, 0, TimeSpan.Zero), TypeEvenement.Demarrage, Version: "3.2.1"),
            new EvenementJournal(new DateTimeOffset(2026, 9, 25, 20, 0, 0, TimeSpan.Zero), TypeEvenement.Arret),
            new EvenementJournal(new DateTimeOffset(2026, 9, 26, 7, 0, 0, TimeSpan.Zero), TypeEvenement.Reprise, Cause: "trou de 11 h 00"),
            new EvenementJournal(new DateTimeOffset(2026, 9, 26, 9, 0, 0, TimeSpan.Zero), TypeEvenement.JetonInvalide),
            new EvenementJournal(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero), TypeEvenement.Demarrage, Version: "3.2.2"),
            new EvenementJournal(new DateTimeOffset(2026, 9, 27, 8, 0, 0, TimeSpan.Zero), TypeEvenement.SondeRefusee, Cause: "SaturationEnTetesLus"),
            new EvenementJournal(new DateTimeOffset(2026, 9, 27, 11, 0, 0, TimeSpan.Zero), TypeEvenement.Arret),
        };
        foreach (var e in evenements) Assert.True(journal.AjouterEvenement(e));

        var derniers = EtatJournalHistorique.DerniersEvenements(_dossier, Now);

        Assert.Equal(5, derniers.Count);
        Assert.Equal(new[] { TypeEvenement.Reprise, TypeEvenement.JetonInvalide, TypeEvenement.Demarrage, TypeEvenement.SondeRefusee, TypeEvenement.Arret },
                     derniers.Select(e => e.Type).ToArray());
        Assert.Equal(evenements.Skip(2).Select(e => e.T).ToArray(), derniers.Select(e => e.T).ToArray());
        Assert.Equal("3.2.2", derniers[2].Version);
    }

    [Fact]
    public void Les_evenements_de_plus_de_sept_jours_sont_ignores()
    {
        var journal = new JournalReleves(_dossier, new FakeClock(Now));
        Assert.True(journal.AjouterEvenement(new EvenementJournal(Now - TimeSpan.FromDays(8), TypeEvenement.Demarrage, Version: "3.2.0")));
        Assert.True(journal.AjouterEvenement(new EvenementJournal(Now - TimeSpan.FromHours(1), TypeEvenement.Arret)));

        var derniers = EtatJournalHistorique.DerniersEvenements(_dossier, Now);

        var seul = Assert.Single(derniers);
        Assert.Equal(TypeEvenement.Arret, seul.Type);
        Assert.Equal(Now - TimeSpan.FromHours(1), seul.T);
    }
}
