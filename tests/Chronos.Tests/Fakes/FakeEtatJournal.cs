using Chronos.Services.Historique;

namespace Chronos.Tests;

/// <summary>Faux d'<see cref="IEtatJournal"/> (JRN-04) : ce que le journal des relevés DIT de lui-même, entièrement en
/// mémoire et réglable APRÈS construction du consommateur — le ViewModel relit l'état à chaque tick, c'est précisément
/// ce que l'on veut observer (une écriture qui arrive, une horloge qui avance, une panne qui se déclare). Forme reprise
/// de <see cref="FakeEtatServeur"/>. Aucun fichier, aucun dossier : rien n'est écrit nulle part.</summary>
public sealed class FakeEtatJournal : IEtatJournal
{
    public string Dossier { get; set; } = "<temp>";
    public DateTimeOffset? DerniereEcriture { get; set; }
    public string? DerniereErreur { get; set; }
    public int RelevesEcrits { get; set; }
    public DateTimeOffset? JournalOuvertLe { get; set; }
}
