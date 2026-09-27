using System.Collections.Concurrent;
using System.Threading;

namespace Chronos.Services;

/// <summary>
/// Résultat d'une tentative de verrou. <see cref="Obtenu"/> = le thread appelant POSSÈDE le mutex ;
/// <see cref="Abandonne"/> = l'instance précédente est morte sans libérer et l'OS nous a donné le mutex (D-32-10 :
/// un abandon est un acquis, pas un refus — sinon un crash de l'overlay interdirait tout redémarrage).
/// </summary>
public sealed class ResultatVerrou
{
    public string Nom { get; }
    public bool Obtenu { get; }
    public Mutex? Mutex { get; }
    public bool Abandonne { get; }

    internal ResultatVerrou(string nom, bool obtenu, Mutex? mutex, bool abandonne)
    {
        Nom = nom;
        Obtenu = obtenu;
        Mutex = mutex;
        Abandonne = abandonne;
    }

    /// <summary>
    /// À appeler sur le THREAD qui a acquis (<c>ReleaseMutex</c> l'exige ; dans l'overlay, c'est le thread UI dans
    /// <c>OnExit</c>). Idempotent et tolérant : un appel depuis un autre thread (ApplicationException) ou après une
    /// première libération (ObjectDisposedException) ne lève pas — l'OS fait de toute façon le ménage à la mort du processus.
    /// </summary>
    public void Liberer()
    {
        if (Mutex is null) return;
        try { Mutex.ReleaseMutex(); }
        catch (ApplicationException) { }      // pas le thread propriétaire : rien à libérer d'ici
        catch (ObjectDisposedException) { }   // déjà libéré
        try { Mutex.Dispose(); } catch (ObjectDisposedException) { }
        VerrouInstanceUnique.Oublier(Nom);
    }
}

/// <summary>
/// CPT-03 — UNE SEULE instance de l'overlay par session Windows.
///
/// <para>POURQUOI un mutex nommé et pas un fichier-verrou : l'OS libère le mutex à la mort du processus (un fichier
/// survivrait à un crash et bloquerait le redémarrage). POURQUOI <c>Local\</c> et pas <c>Global\</c> : aucun droit
/// requis, et un autre utilisateur de la même machine peut avoir son propre Chronos (D-32-09).</para>
///
/// <para>Posé dans <c>App.OnStartup</c> APRÈS les court-circuits <c>--statusline</c>, <c>--hook</c>, <c>--cadrans</c>,
/// <c>--sessions</c> et AVANT le Host : les hooks (Claude Code en lance jusqu'à 5 en parallèle) et les modes CLI restent
/// multi-instances par construction, et la seconde instance se retire sans avoir démarré un seul service.</para>
///
/// <para>Type NEUTRE (aucun WPF) : la mécanique se prouve par <c>VerrouInstanceUniqueTests</c> sur de vrais threads.
/// Le 2026-09-27, trois exécutables tournaient ensemble et réécrivaient les mêmes fichiers : c'est ce que ce verrou interdit.</para>
/// </summary>
public static class VerrouInstanceUnique
{
    /// <summary>Nom du verrou de l'overlay. Les anciens exe (3.1.0, 3.2.0, 3.2.1) ne le connaissent pas (D-32-12).</summary>
    public const string NomOverlay = @"Local\Chronos-overlay";

    // Noms tenus par CE processus : c'est cette table qui fait foi pour « tenu par ce processus » au diagnostic,
    // car un mutex nommé ne sait pas dire qui le possède sans tenter de l'acquérir.
    private static readonly ConcurrentDictionary<string, byte> Tenus = new(StringComparer.Ordinal);

    /// <summary>
    /// Tente d'acquérir le verrou SANS attendre. Docs Microsoft (<c>Mutex(bool, string, out bool)</c>) : avec
    /// <c>initiallyOwned: true</c>, le thread appelant possède le mutex SEULEMENT si <c>createdNew</c> ; sinon on tente
    /// <c>WaitOne(0)</c>. Une <see cref="AbandonedMutexException"/> levée par <c>WaitOne</c> signifie que le mutex est
    /// désormais POSSÉDÉ par l'appelant : l'instance précédente est morte sans libérer → on démarre et on le note.
    /// </summary>
    public static ResultatVerrou Acquerir(string nom)
    {
        var m = new Mutex(initiallyOwned: true, nom, out var cree);
        if (cree)
        {
            Tenus[nom] = 1;
            return new ResultatVerrou(nom, obtenu: true, m, abandonne: false);
        }

        try
        {
            if (m.WaitOne(0))
            {
                Tenus[nom] = 1;
                return new ResultatVerrou(nom, obtenu: true, m, abandonne: false);
            }

            m.Dispose();   // quelqu'un d'autre le tient : on ne garde aucun handle ouvert
            return new ResultatVerrou(nom, obtenu: false, null, abandonne: false);
        }
        catch (AbandonedMutexException)
        {
            Tenus[nom] = 1;
            return new ResultatVerrou(nom, obtenu: true, m, abandonne: true);
        }
    }

    /// <summary>
    /// « tenu par ce processus » | « tenu par un autre processus » | « libre » — sans JAMAIS acquérir : on se contente
    /// d'ouvrir l'objet noyau s'il existe. Limite documentée : cette ouverture réussit dès que l'objet existe, c'est-à-dire
    /// dès qu'un handle est ouvert quelque part, même si personne ne le possède ; pour le diagnostic de l'overlay c'est
    /// donc <c>Tenus</c> qui fait foi pour « ce processus », et « un autre processus » est un constat d'existence,
    /// vérifié en vrai par 32-08 (non observable depuis un seul processus de test).
    /// </summary>
    public static string EtatPourDiagnostic(string nom)
    {
        if (Tenus.ContainsKey(nom)) return "tenu par ce processus";

        if (Mutex.TryOpenExisting(nom, out var autre))
        {
            autre.Dispose();   // handle ouvert par l'inspection seulement : on le referme aussitôt
            return "tenu par un autre processus";
        }

        return "libre";
    }

    internal static void Oublier(string nom) => Tenus.TryRemove(nom, out _);
}
