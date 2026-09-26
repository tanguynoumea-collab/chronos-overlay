using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// LA LECTURE, versant MONITEUR (phase 30, plan 03) : une session LUE quitte le widget d'elle-même, et une session qui
/// TRAVAILLE n'en disparaît plus jamais.
///
/// <para><b>LUE-05 (V01 à V04).</b> Le filtre « traitée » du moniteur ne masque qu'une session dont l'état RETENU est une
/// attente (<see cref="AffichageSessions.EstUneAttente"/>). Répondue, lue ou marquée traitée, une session qui se remet à
/// travailler s'affiche « Réflexion » ; son prochain épisode d'attente plus récent la ramène (NET-03, détecteur) ; le
/// filtre ne purge rien.</para>
///
/// <para><b>Classes RÉELLES.</b> Un vrai <see cref="SessionMonitor"/>, un vrai <see cref="SessionTreatmentTracker"/> sur
/// un vrai <see cref="TreatedStore"/> et, quand le cas le demande, un vrai <see cref="LecteurAppBureau"/> sur une racine
/// temporaire à la forme exacte de l'app. Seules la source de transcripts (elle lirait le vrai <c>~/.claude/projects</c>)
/// et la sonde du premier plan (elle lirait le vrai premier plan de la machine) sont substituées.</para>
///
/// <para><b>Aucune écriture hors du dossier temporaire.</b> <see cref="ArchiveStore"/> et <see cref="TreatedStore"/>
/// TOUJOURS temporaires, datés par une horloge FIXE proche de l'instant du cas : la rétention de 24 h du magasin se
/// mesure à SON horloge. Tout ce qui est créé est supprimé à la fin de chaque test.</para>
/// </summary>
public sealed class MoniteurLectureTests : IDisposable
{
    /// <summary>L'instant de référence des cas LUE-05.</summary>
    private static readonly DateTimeOffset T = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    // ── Ce que chaque test crée, et qu'il supprime ─────────────────────────────────────────────────────────────
    private readonly List<string> _aSupprimer = new();

    public void Dispose()
    {
        foreach (var d in _aSupprimer)
            try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { }
    }

    private string Dossier()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-moniteur-lecture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // garde anti-accident
        _aSupprimer.Add(d);
        return d;
    }

    /// <summary>Un magasin « traité » temporaire, daté par une horloge fixe.</summary>
    private TreatedStore Traitees(DateTimeOffset horloge) => new(Path.Combine(Dossier(), "treated.json"), new FakeClock(horloge));

    /// <summary>Un magasin d'archives temporaire (le défaut lirait le vrai %APPDATA%).</summary>
    private ArchiveStore Archive(DateTimeOffset horloge) => new(Path.Combine(Dossier(), "archived.json"), new FakeClock(horloge));

    /// <summary>Le moniteur de production, aux racines près : le détecteur est TOUJOURS construit sur
    /// <paramref name="treated"/>, le lecteur de l'app est passé par argument nommé.</summary>
    private SessionMonitor Moniteur(ISessionSource transcripts, TreatedStore treated, LecteurAppBureau? lecteur = null,
        ArchiveStore? archive = null, string? hooks = null)
        => new(hooks ?? Dossier(), transcripts, archive ?? Archive(T), treated, new SessionTreatmentTracker(treated),
               appBureau: lecteur);

    /// <summary>La source de transcripts SUBSTITUÉE, réaffectable entre deux cycles.</summary>
    private sealed class SourceModifiable : ISessionSource
    {
        public IReadOnlyList<SessionSnapshot> Sessions { get; set; }
        public SourceModifiable(params SessionSnapshot[] sessions) => Sessions = sessions;
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => Sessions;
    }

    private static SessionSnapshot Snap(string id, string projet, SessionActivity a, DateTimeOffset maj)
        => new(id, projet, a, null, maj);

    // ═══ LUE-05 — le filtre ne masque que les attentes ═══════════════════════════════════════════════════════

    /// <summary>V01 — une session marquée traitée qui se remet à travailler s'affiche « Réflexion » ; son entrée reste
    /// dans le magasin (le filtre ne purge rien : seul le détecteur ajoute et retire).</summary>
    [Fact]
    public void Une_session_traitee_qui_travaille_est_visible_Reflexion()
    {
        var treated = Traitees(T);
        treated.Set("s", T.ToUnixTimeMilliseconds());   // le geste « Marquer traitée », pour l'attente de T
        var source = new SourceModifiable(Snap("s", "Proj", SessionActivity.Working, T.AddMinutes(1)));
        var moniteur = Moniteur(source, treated);

        var lecture = moniteur.Inspecter(T.AddMinutes(1));

        var visible = Assert.Single(lecture.Visibles);
        Assert.Equal("s", visible.SessionId);
        Assert.Equal(SessionActivity.Working, visible.Activity);
        Assert.Empty(lecture.Masquees);
        Assert.True(treated.Load().ContainsKey("s"), "le filtre ne purge rien : l'entrée reste jusqu'au prochain épisode");
    }

    /// <summary>V02 — suite de V01 : le prochain tour fini, plus récent que l'épisode traité, la ramène « En attente »
    /// et le détecteur purge son entrée (NET-03, inchangé).</summary>
    [Fact]
    public void Son_prochain_tour_fini_la_ramene_En_attente()
    {
        var treated = Traitees(T);
        treated.Set("s", T.ToUnixTimeMilliseconds());
        var source = new SourceModifiable(Snap("s", "Proj", SessionActivity.Working, T.AddMinutes(1)));
        var moniteur = Moniteur(source, treated);

        var enTravail = moniteur.Inspecter(T.AddMinutes(1));
        Assert.Contains(enTravail.Visibles, s => s.SessionId == "s" && s.Activity == SessionActivity.Working);

        source.Sessions = new[] { Snap("s", "Proj", SessionActivity.WaitingTurn, T.AddMinutes(5)) };
        var lecture = moniteur.Inspecter(T.AddMinutes(5));

        var visible = Assert.Single(lecture.Visibles);
        Assert.Equal("s", visible.SessionId);
        Assert.Equal(SessionActivity.WaitingTurn, visible.Activity);
        Assert.Empty(lecture.Masquees);
        Assert.False(treated.Load().ContainsKey("s"), "un épisode plus récent que le traitement purge l'entrée (NET-03)");
    }

    /// <summary>V03 — traitée ET indéterminée : l'état retenu n'est pas une attente, le filtre « traitée » ne la prend
    /// pas ; elle est masquée parce qu'elle n'a pas de ligne (LIB-01), avec ce motif-là.</summary>
    [Fact]
    public void Traitee_et_indeterminee_est_masquee_indeterminee()
    {
        var treated = Traitees(T);
        treated.Set("u", T.ToUnixTimeMilliseconds());
        var moniteur = Moniteur(new SourceModifiable(Snap("u", "Proj", SessionActivity.Unknown, T)), treated);

        var lecture = moniteur.Inspecter(T);

        Assert.Empty(lecture.Visibles);
        var masquee = Assert.Single(lecture.Masquees);
        Assert.Equal("u", masquee.Session.SessionId);
        Assert.Equal(MotifMasquage.Indeterminee, masquee.Motif);
    }

    /// <summary>V04 — une attente DÉDUITE (silence de 25 min) plus ancienne que le traitement est une attente : elle
    /// reste masquée, motif résiduel <see cref="MotifMasquage.Traitee"/>, sans cause (un geste que le détecteur n'a pas
    /// constaté).</summary>
    [Fact]
    public void Traitee_et_deduite_plus_ancienne_que_le_traitement_reste_masquee()
    {
        var treated = Traitees(T);
        treated.Set("d", T.ToUnixTimeMilliseconds());
        var moniteur = Moniteur(new SourceModifiable(Snap("d", "Proj", SessionActivity.Working, T.AddMinutes(-25))), treated);

        var lecture = moniteur.Inspecter(T);

        Assert.Empty(lecture.Visibles);
        var masquee = Assert.Single(lecture.Masquees);
        Assert.Equal("d", masquee.Session.SessionId);
        Assert.Equal(SessionActivity.WaitingDeduced, masquee.Session.Activity);   // déduite par le moniteur (SIL-01)
        Assert.Equal(MotifMasquage.Traitee, masquee.Motif);
        Assert.Null(masquee.Cause);
        Assert.True(treated.Load().ContainsKey("d"));
    }
}
