namespace Chronos.Services;

/// <summary>Décision du <see cref="LimiteurIncidents"/> pour une occurrence d'incident.</summary>
public enum DecisionIncident
{
    /// <summary>Écrire la ligne telle quelle (une des premières occurrences de sa clé).</summary>
    Ecrire,
    /// <summary>Ne rien écrire : l'occurrence est comptée, elle sera résumée plus tard.</summary>
    Taire,
    /// <summary>Écrire UNE ligne « répété N fois » (N = occurrences tues depuis le dernier résumé, celle-ci comprise).</summary>
    Resume,
    /// <summary>Le plafond de lignes du processus vient d'être atteint : écrire l'avis de plafond, puis plus rien.</summary>
    Plafond,
}

/// <summary>
/// FIAB-R3 (42.2-11) — borne ce que le journal d'incidents écrit pour un même processus. Le filet Dispatcher marque l'exception
/// « traitée » : une exception récurrente (tick 1 s) écrivait ~86 400 lignes par jour, toutes relues au démarrage suivant.
/// <list type="bullet">
///   <item>par clé : les <see cref="OccurrencesPleines"/> premières occurrences s'écrivent ; les suivantes se taisent et sont
///   résumées par UNE ligne « répété N fois » au plus toutes les <see cref="IntervalleResume"/> ;</item>
///   <item>tous incidents confondus : au plus <see cref="PlafondLignes"/> lignes, puis un avis unique, puis plus rien.</item>
/// </list>
/// Pur et sans horloge propre : l'instant est passé par l'appelant (testable). Sûr entre threads.
/// </summary>
public sealed class LimiteurIncidents
{
    /// <summary>Occurrences identiques écrites en clair avant le passage au résumé.</summary>
    public const int OccurrencesPleines = 3;

    /// <summary>Plafond de lignes écrites par processus (résumés compris), avant l'avis de plafond.</summary>
    public const int PlafondLignes = 200;

    /// <summary>Intervalle minimal entre deux lignes « répété N fois » d'une même clé.</summary>
    public static readonly TimeSpan IntervalleResume = TimeSpan.FromMinutes(10);

    private sealed class Etat
    {
        public int Occurrences;
        public int Tues;
        public DateTimeOffset DebutFenetre;
    }

    private readonly Dictionary<string, Etat> _parCle = new(StringComparer.Ordinal);
    private readonly object _verrou = new();
    private int _lignes;
    private bool _plafondAnnonce;

    /// <summary>Décide du sort d'une occurrence de <paramref name="cle"/> survenue à <paramref name="quand"/>.</summary>
    public (DecisionIncident Decision, int Repetitions) Evaluer(string cle, DateTimeOffset quand)
    {
        lock (_verrou)
        {
            if (_plafondAnnonce) return (DecisionIncident.Taire, 0);

            if (!_parCle.TryGetValue(cle, out var e)) _parCle[cle] = e = new Etat();
            e.Occurrences++;

            DecisionIncident decision;
            var repetitions = 0;
            if (e.Occurrences <= OccurrencesPleines)
                decision = DecisionIncident.Ecrire;
            else if (e.Tues == 0 && e.Occurrences == OccurrencesPleines + 1)
            {
                e.Tues = 1;
                e.DebutFenetre = quand;
                return (DecisionIncident.Taire, 0);
            }
            else
            {
                e.Tues++;
                if (quand - e.DebutFenetre < IntervalleResume) return (DecisionIncident.Taire, 0);
                decision = DecisionIncident.Resume;
                repetitions = e.Tues;
                e.Tues = 0;
                e.DebutFenetre = quand;
            }

            if (_lignes >= PlafondLignes)
            {
                _plafondAnnonce = true;
                return (DecisionIncident.Plafond, 0);
            }
            _lignes++;
            return (decision, repetitions);
        }
    }
}
