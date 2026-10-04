using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// FIAB-1 / FIAB-4 (phase 42.2) — le journal d'incidents de <c>chronos.log</c> et le filet d'exceptions.
/// Parties pures (ligne, extraction, composition, classification, description) et E/S sur dossier TEMPORAIRE uniquement :
/// aucun test n'écrit sous le vrai %APPDATA%\Chronos.
/// </summary>
public class JournalIncidentsTests
{
    private static readonly DateTimeOffset Quand = new(2026, 10, 4, 12, 0, 0, TimeSpan.FromHours(11));

    private static string NouveauDossier()
        => Path.Combine(Path.GetTempPath(), "ChronosIncidents_" + Guid.NewGuid().ToString("N"));

    private static void Nettoyer(string racine)
    {
        try { if (Directory.Exists(racine)) Directory.Delete(racine, recursive: true); }
        catch { /* nettoyage best-effort */ }
        try { if (File.Exists(racine)) File.Delete(racine); }
        catch { /* nettoyage best-effort */ }
    }

    // ------------------------------------------------------------------------------------------
    // Ligne
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void La_ligne_est_datee_monoligne_et_marquee()
    {
        Assert.Equal("[incident] 2026-10-04 12:00:00 +11:00 — a ⏎ b", JournalIncidents.Ligne(Quand, "a\r\nb"));
        Assert.Equal("[incident] 2026-10-04 12:00:00 +11:00 — a ⏎ b ⏎ c", JournalIncidents.Ligne(Quand, "a\nb\rc"));
    }

    [Fact]
    public void La_ligne_tronque_un_message_trop_long()
    {
        var ligne = JournalIncidents.Ligne(Quand, new string('x', 5000));
        var prefixe = "[incident] 2026-10-04 12:00:00 +11:00 — ";
        Assert.StartsWith(prefixe, ligne);
        Assert.Equal(2000, ligne.Length - prefixe.Length);
    }

    // ------------------------------------------------------------------------------------------
    // Signaler (E/S sur dossier temporaire)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Signaler_cree_le_dossier_absent_et_ajoute_une_ligne()
    {
        var racine = NouveauDossier();
        var dossier = Path.Combine(racine, "Chronos");
        Assert.StartsWith(Path.GetTempPath(), dossier);
        try
        {
            Assert.True(JournalIncidents.Signaler(dossier, "x", Quand));

            var contenu = File.ReadAllText(Path.Combine(dossier, JournalIncidents.NomFichier));
            Assert.Equal("[incident] 2026-10-04 12:00:00 +11:00 — x" + Environment.NewLine, contenu);
        }
        finally { Nettoyer(racine); }
    }

    [Fact]
    public void Deux_signalements_donnent_deux_lignes_dans_l_ordre()
    {
        var racine = NouveauDossier();
        Assert.StartsWith(Path.GetTempPath(), racine);
        try
        {
            Assert.True(JournalIncidents.Signaler(racine, "premier", Quand));
            Assert.True(JournalIncidents.Signaler(racine, "second", Quand.AddMinutes(1)));

            var lignes = File.ReadAllLines(Path.Combine(racine, JournalIncidents.NomFichier));
            Assert.Equal(2, lignes.Length);
            Assert.EndsWith("— premier", lignes[0]);
            Assert.EndsWith("— second", lignes[1]);
        }
        finally { Nettoyer(racine); }
    }

    [Fact]
    public void Signaler_apres_un_journal_sans_fin_de_ligne_ne_colle_pas_l_incident()
    {
        var racine = NouveauDossier();
        Assert.StartsWith(Path.GetTempPath(), racine);
        try
        {
            Directory.CreateDirectory(racine);
            File.WriteAllText(Path.Combine(racine, JournalIncidents.NomFichier), "rapport sans fin de ligne");

            Assert.True(JournalIncidents.Signaler(racine, "x", Quand));

            var lignes = File.ReadAllLines(Path.Combine(racine, JournalIncidents.NomFichier));
            Assert.Equal(new[] { "rapport sans fin de ligne", "[incident] 2026-10-04 12:00:00 +11:00 — x" }, lignes);
        }
        finally { Nettoyer(racine); }
    }

    [Fact]
    public void Signaler_sans_dossier_rend_false_sans_lever()
    {
        Assert.False(JournalIncidents.Signaler(null, "x"));
    }

    [Fact]
    public void Signaler_dans_un_dossier_poison_rend_false_sans_lever()
    {
        var poison = NouveauDossier();
        Assert.StartsWith(Path.GetTempPath(), poison);
        try
        {
            File.WriteAllText(poison, "un FICHIER porte le nom du dossier");
            Assert.False(JournalIncidents.Signaler(poison, "x", Quand));
        }
        finally { Nettoyer(poison); }
    }

    // ------------------------------------------------------------------------------------------
    // Extraire / ComposerJournalDemarrage
    // ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("(log automatique au démarrage)\nrien de notable\n")]
    public void Extraire_sans_incident_rend_une_liste_vide(string? contenu)
    {
        Assert.Empty(JournalIncidents.Extraire(contenu));
    }

    [Fact]
    public void Extraire_garde_les_incidents_et_les_anciennes_lignes_d_arret_dans_l_ordre()
    {
        var contenu = "(log automatique au démarrage)\r\n"
                    + "bruit\r\n"
                    + "  [incident] 2026-10-01 08:00:00 +11:00 — exception non gérée (UI) : X\r\n"
                    + "\r\n"
                    + "[2026-10-02 09:00:00 +11:00] arrêt dépassé : arrêt du Host non terminé après 5 s — sortie forcée\r\n"
                    + "[Magasins persistants]\r\n"
                    + "Ligne qui parle d'un arrêt dépassé sans être datée\n"
                    + "[incident] 2026-10-03 10:00:00 +11:00 — y\n";

        var incidents = JournalIncidents.Extraire(contenu);

        Assert.Equal(new[]
        {
            "[incident] 2026-10-01 08:00:00 +11:00 — exception non gérée (UI) : X",
            "[2026-10-02 09:00:00 +11:00] arrêt dépassé : arrêt du Host non terminé après 5 s — sortie forcée",
            "[incident] 2026-10-03 10:00:00 +11:00 — y",
        }, incidents);
    }

    [Fact]
    public void Extraire_borne_aux_cinquante_derniers()
    {
        var contenu = string.Join("\n", Enumerable.Range(1, 70).Select(i => $"[incident] 2026-10-01 08:00:00 +11:00 — n{i}"));

        var incidents = JournalIncidents.Extraire(contenu);

        Assert.Equal(JournalIncidents.Conserves, incidents.Count);
        Assert.Equal(50, JournalIncidents.Conserves);
        Assert.EndsWith("— n21", incidents[0]);
        Assert.EndsWith("— n70", incidents[^1]);
    }

    [Fact]
    public void Composer_sans_incident_n_ajoute_aucune_section()
    {
        Assert.Equal("(log automatique au démarrage)\nRAPPORT", JournalIncidents.ComposerJournalDemarrage(null, "RAPPORT"));
        Assert.Equal("(log automatique au démarrage)\nRAPPORT", JournalIncidents.ComposerJournalDemarrage("bruit\n", "RAPPORT"));
    }

    [Fact]
    public void Composer_reporte_les_incidents_en_tete()
    {
        var ancien = "(log automatique au démarrage)\nbruit\n[incident] 2026-10-01 08:00:00 +11:00 — a\n[incident] 2026-10-01 09:00:00 +11:00 — b\n";

        var nouveau = JournalIncidents.ComposerJournalDemarrage(ancien, "RAPPORT");

        Assert.Equal("(log automatique au démarrage)\n"
                     + "[Incidents des lancements précédents]\n"
                     + "[incident] 2026-10-01 08:00:00 +11:00 — a\n"
                     + "[incident] 2026-10-01 09:00:00 +11:00 — b\n\n"
                     + "RAPPORT", nouveau);
    }

    // ------------------------------------------------------------------------------------------
    // FiletExceptions
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Les_exceptions_fatales_sont_reconnues()
    {
        Assert.True(FiletExceptions.EstFatale(new OutOfMemoryException()));
        Assert.True(FiletExceptions.EstFatale(new AccessViolationException()));
        Assert.True(FiletExceptions.EstFatale(new StackOverflowException()));
        Assert.True(FiletExceptions.EstFatale(new InvalidProgramException()));
        Assert.True(FiletExceptions.EstFatale(new BadImageFormatException()));

        Assert.False(FiletExceptions.EstFatale(new InvalidOperationException()));
        Assert.False(FiletExceptions.EstFatale(new IOException()));
        Assert.False(FiletExceptions.EstFatale(new NullReferenceException()));
    }

    [Fact]
    public void Decrire_une_exception_sans_pile_ne_leve_pas()
    {
        var texte = FiletExceptions.Decrire("UI", new InvalidOperationException("boum\r\nligne 2"));

        Assert.StartsWith("exception non gérée (UI) : InvalidOperationException: boum", texte);
        Assert.Contains("| pile :", texte);
        Assert.DoesNotContain("\n", texte);
        Assert.DoesNotContain("\r", texte);
    }

    [Fact]
    public void Decrire_chaine_les_causes_et_borne_la_pile()
    {
        Exception attrapee;
        try { Profond(12); throw new InvalidOperationException("inatteignable"); }
        catch (Exception ex) { attrapee = ex; }

        var enveloppe = new InvalidOperationException("haut",
            new IOException("milieu", new ArgumentException("bas", new FormatException("quatre", new TimeoutException("cinq")))));

        var texte = FiletExceptions.Decrire("démarrage", enveloppe);
        Assert.StartsWith("exception non gérée (démarrage) : InvalidOperationException: haut ← IOException: milieu ← ArgumentException: bas ← FormatException: quatre", texte);
        Assert.DoesNotContain("TimeoutException", texte);   // au plus 3 causes

        var avecPile = FiletExceptions.Decrire("UI", attrapee);
        Assert.DoesNotContain("\n", avecPile);
        var pile = avecPile[(avecPile.IndexOf("| pile :", StringComparison.Ordinal) + "| pile :".Length)..];
        Assert.Contains("Profond", pile);
        Assert.True(pile.Split(" ; ").Length <= 6, "au plus 6 cadres de pile");
    }

    private static void Profond(int n)
    {
        if (n == 0) throw new ArgumentException("fond");
        Profond(n - 1);
    }

    // ---------------------------------------------------------------- 42.2-11 : FIAB-R3, dédoublonnage, plafond, lecture bornée

    private static readonly DateTimeOffset T0 = new(2026, 10, 4, 12, 0, 0, TimeSpan.FromHours(11));

    /// <summary>FIAB-R3 : trois occurrences identiques s'écrivent, les suivantes se taisent, puis une ligne « répété N fois »
    /// toutes les 10 minutes au plus (horloge injectée par l'instant passé).</summary>
    [Fact]
    public void Le_limiteur_ecrit_trois_fois_puis_resume_toutes_les_dix_minutes()
    {
        var l = new LimiteurIncidents();

        for (int i = 0; i < 3; i++)
            Assert.Equal(DecisionIncident.Ecrire, l.Evaluer("k", T0.AddSeconds(i)).Decision);
        for (int i = 3; i < 100; i++)
            Assert.Equal(DecisionIncident.Taire, l.Evaluer("k", T0.AddSeconds(i)).Decision);   // 97 tues, < 10 min

        var resume = l.Evaluer("k", T0.AddSeconds(3).Add(LimiteurIncidents.IntervalleResume));
        Assert.Equal(DecisionIncident.Resume, resume.Decision);
        Assert.Equal(98, resume.Repetitions);   // les 97 tues + celle-ci

        Assert.Equal(DecisionIncident.Taire, l.Evaluer("k", T0.AddMinutes(14)).Decision);
        var second = l.Evaluer("k", T0.AddSeconds(3).Add(LimiteurIncidents.IntervalleResume * 2));
        Assert.Equal(DecisionIncident.Resume, second.Decision);
        Assert.Equal(2, second.Repetitions);
    }

    [Fact]
    public void Le_limiteur_compte_chaque_cle_separement()
    {
        var l = new LimiteurIncidents();
        for (int i = 0; i < 3; i++) l.Evaluer("a", T0);

        Assert.Equal(DecisionIncident.Taire, l.Evaluer("a", T0).Decision);
        Assert.Equal(DecisionIncident.Ecrire, l.Evaluer("b", T0).Decision);
    }

    /// <summary>FIAB-R3 : au plus 200 lignes par processus ; la 201e est l'avis de plafond, puis plus rien.</summary>
    [Fact]
    public void Le_limiteur_plafonne_a_deux_cents_lignes_puis_un_avis_unique()
    {
        var l = new LimiteurIncidents();
        for (int i = 0; i < LimiteurIncidents.PlafondLignes; i++)
            Assert.Equal(DecisionIncident.Ecrire, l.Evaluer("cle-" + i, T0).Decision);

        Assert.Equal(200, LimiteurIncidents.PlafondLignes);
        Assert.Equal(DecisionIncident.Plafond, l.Evaluer("encore", T0).Decision);
        Assert.Equal(DecisionIncident.Taire, l.Evaluer("et-encore", T0.AddHours(1)).Decision);
    }

    /// <summary>FIAB-R3 de bout en bout : une exception récurrente (tick 1 s) n'écrit plus une ligne par seconde.</summary>
    [Fact]
    public void Signaler_en_rafale_n_ecrit_que_trois_lignes_puis_un_resume()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "ChronosTests", Guid.NewGuid().ToString("N"));
        try
        {
            for (int i = 0; i < 60; i++)
                JournalIncidents.Signaler(dossier, "exception non gérée (UI) : InvalidOperationException: boum", T0.AddSeconds(i));
            var lignes = File.ReadAllLines(Path.Combine(dossier, JournalIncidents.NomFichier));
            Assert.Equal(3, lignes.Length);

            JournalIncidents.Signaler(dossier, "exception non gérée (UI) : InvalidOperationException: boum", T0.AddMinutes(11));
            lignes = File.ReadAllLines(Path.Combine(dossier, JournalIncidents.NomFichier));
            Assert.Equal(4, lignes.Length);
            Assert.Contains("répété 58 fois", lignes[3]);
            Assert.StartsWith(JournalIncidents.Marqueur, lignes[3]);
        }
        finally { try { Directory.Delete(dossier, recursive: true); } catch { } }
    }

    /// <summary>FIAB-R3 : la clé d'une exception est (origine, type, message, PREMIER cadre) — deux levées au même endroit
    /// se reconnaissent même si la suite de la pile diffère.</summary>
    [Fact]
    public void La_cle_d_une_exception_ignore_les_cadres_profonds()
    {
        static Exception Lever()
        {
            try { throw new InvalidOperationException("boum"); }
            catch (Exception e) { return e; }
        }
        Exception ParA() => Lever();
        Exception ParB() => Lever();

        var a = FiletExceptions.Cle("UI", ParA());
        var b = FiletExceptions.Cle("UI", ParB());

        Assert.Equal(a, b);
        Assert.Contains("InvalidOperationException", a);
        Assert.NotEqual(a, FiletExceptions.Cle("domaine", ParA()));
    }

    /// <summary>FIAB-R3 : au démarrage, seule la FIN du journal (≤ 1 Mo) est relue pour en extraire les incidents.</summary>
    [Fact]
    public void LireFin_ne_relit_que_le_dernier_mega_octet()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "ChronosTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        try
        {
            var chemin = Path.Combine(dossier, JournalIncidents.NomFichier);
            var remplissage = new string('x', 99) + "\n";
            using (var w = new StreamWriter(chemin))
            {
                w.Write(JournalIncidents.Ligne(T0, "incident du début") + "\n");
                for (int i = 0; i < 20_000; i++) w.Write(remplissage);   // ≈ 2 Mo
                w.Write(JournalIncidents.Ligne(T0, "incident de la fin") + "\n");
            }

            var fin = JournalIncidents.LireFin(chemin);

            Assert.NotNull(fin);
            Assert.True(fin!.Length <= JournalIncidents.LectureMaxOctets);
            var incidents = JournalIncidents.Extraire(fin);
            Assert.Single(incidents);
            Assert.Contains("incident de la fin", incidents[0]);
            Assert.Equal(1024 * 1024, JournalIncidents.LectureMaxOctets);
        }
        finally { try { Directory.Delete(dossier, recursive: true); } catch { } }
    }

    [Fact]
    public void LireFin_d_un_petit_journal_le_rend_entier_et_d_un_absent_rend_null()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "ChronosTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        try
        {
            var chemin = Path.Combine(dossier, JournalIncidents.NomFichier);
            Assert.Null(JournalIncidents.LireFin(chemin));
            File.WriteAllText(chemin, "a\nb\n");
            Assert.Equal("a\nb\n", JournalIncidents.LireFin(chemin));
        }
        finally { try { Directory.Delete(dossier, recursive: true); } catch { } }
    }

    /// <summary>FIAB-R3 (garde de source) : le journal de démarrage ne relit plus chronos.log en entier, et le filet Dispatcher
    /// passe une clé de dédoublonnage.</summary>
    [Fact]
    public void Garde_le_demarrage_lit_la_fin_du_journal_et_le_filet_passe_une_cle()
    {
        var src = Path.GetFullPath(Path.Combine(Dossier(), "..", "..", "src", "Chronos"));
        var diag = File.ReadAllText(Path.Combine(src, "Services", "DiagnosticService.cs"));
        var app = File.ReadAllText(Path.Combine(src, "App.xaml.cs"));

        Assert.Contains("JournalIncidents.LireFin(", diag);
        Assert.DoesNotContain("File.ReadAllText(chemin) : null", diag);
        Assert.Contains("FiletExceptions.Cle(", app);
    }

    private static string Dossier([System.Runtime.CompilerServices.CallerFilePath] string ceFichier = "")
        => Path.GetDirectoryName(ceFichier)!;
}
