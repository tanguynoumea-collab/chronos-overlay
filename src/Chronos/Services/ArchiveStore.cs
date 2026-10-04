using System.Collections.Generic;
using System.IO;

namespace Chronos.Services;

/// <summary>
/// Mémorise les sessions ARCHIVÉES dans %APPDATA%\Chronos\archived.json — une map
/// { session_id : archivedAt(ms) }. Le <see cref="SessionMonitor"/> les écarte, définitivement.
///
/// <para>CONTRAT UNIQUE (TRT-04) : ce qui est archivé NE REVIENT JAMAIS. Le magasin appliquait
/// auparavant une durée de vie de six heures pendant que son contrat annoncé et le libellé du menu
/// promettaient le définitif : le clic droit était une mise en sourdine, et l'utilisateur n'avait aucun
/// moyen de le savoir. Une durée de vie qui défait un geste explicite en silence n'est pas une
/// optimisation de taille, c'est une promesse non tenue.</para>
///
/// <para>Ce qui borne encore le fichier : rien d'automatique, et c'est assumé. Une entrée pèse une
/// ligne, et elle n'est écrite que sur un geste délibéré. Le seul retrait possible est un geste, lui
/// aussi, jamais une horloge. À NE PAS CONFONDRE avec
/// <see cref="TreatedStore"/>, dont le contrat est l'inverse : RÉVERSIBLE — une session traitée
/// revient dès qu'elle redemande quelque chose — et dont la seule borne temporelle est une RÉTENTION
/// de fichier, choisie supérieure au seuil au-delà duquel le moniteur cesse de lire la session.</para>
///
/// <para>L'horloge est INJECTÉE, jamais lue au système : elle ne sert plus qu'à horodater un geste.</para>
/// <para>MAT-3 / DATA-7 (phase 42.2) — « ce qui est archivé ne revient jamais » exige que le FICHIER ne soit jamais
/// perdu. Auparavant, <see cref="Add"/> relisait dans un <c>catch</c> muet : une lecture ratée (fichier tenu un instant)
/// donnait une map vide, réécrite avec la seule nouvelle entrée — toutes les archives réapparaissaient. Désormais
/// (mécanique commune <see cref="MagasinMapSessions"/>) :
/// <list type="bullet">
///   <item>illisible (vide, syntaxe, racine non objet) → l'original est mis en QUARANTAINE (renommé, jamais supprimé),
///   journalisé, visible au diagnostic, puis l'archive est écrite ; quarantaine impossible → rien n'est écrit ;</item>
///   <item>inaccessible (E/S passagère) → rien n'est écrit ni renommé : <see cref="Add"/> rend false ;</item>
///   <item>une lecture non aboutie rend le DERNIER ensemble lu avec succès, jamais un ensemble vide.</item>
/// </list></para>
/// Ne lève jamais. Aucun type WPF.
/// </summary>
public sealed class ArchiveStore : IEtatMagasin
{
    private readonly MagasinMapSessions _magasin;
    private readonly IClock _horloge;
    private HashSet<string> _dernierLu = new();

    public ArchiveStore(string? path = null, IClock? clock = null)
    {
        _magasin = new MagasinMapSessions(path ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Chronos", "archived.json"),
            NomsMagasins.SessionsArchivees);
        _horloge = clock ?? new SystemClock();
    }

    public string Nom => _magasin.Nom;
    public string Chemin => _magasin.Chemin;
    public System.DateTimeOffset? DerniereEcriture => _magasin.DerniereEcriture;
    public string? DerniereErreur => _magasin.DerniereErreur;
    public string? DerniereErreurLecture => _magasin.DerniereErreurLecture;
    public string? DerniereQuarantaine => _magasin.DerniereQuarantaine;

    /// <summary>Identifiants archivés. TOUS : aucune entrée n'est écartée par le temps qui passe. Lecture non aboutie →
    /// le dernier ensemble lu avec succès (une archive ne réapparaît pas parce qu'un antivirus tenait le fichier).
    /// N'écrit ni ne renomme JAMAIS rien.</summary>
    public ISet<string> Load()
    {
        lock (_magasin.Verrou)
        {
            var lu = _magasin.Lire();
            if (lu.EstFiable)
            {
                _magasin.LectureReussie();
                _dernierLu = new HashSet<string>(lu.Map.Keys);
            }
            return new HashSet<string>(_dernierLu);
        }
    }

    /// <summary>Archive une session (idempotent). Les entrées déjà présentes sont conservées telles quelles. Rend true si
    /// l'archive est écrite ; false si rien n'a pu l'être (fichier inaccessible, quarantaine ou écriture impossible) — la
    /// cause est alors dans <see cref="DerniereErreur"/> et dans chronos.log.</summary>
    public bool Add(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return false;
        lock (_magasin.Verrou)
        {
            var lu = _magasin.Lire();
            if (!_magasin.PreparerEcriture(lu, out var map)) return false;
            map[sessionId] = _horloge.UtcNow.ToUnixTimeMilliseconds();   // les autres : conservées TELLES QUELLES
            if (!_magasin.Ecrire(map)) return false;
            _dernierLu = new HashSet<string>(map.Keys);
            return true;
        }
    }
}
