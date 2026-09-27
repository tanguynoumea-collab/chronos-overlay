using Chronos.Models.Historique.Tokens;
using Chronos.Services.Historique.Tokens;

namespace Chronos.Tests;

/// <summary>Faux d'<see cref="IEtatReconstruction"/> (TOK-02) : ce que la reconstruction des agrégats DIT d'elle-même, en
/// mémoire et réglable APRÈS construction du consommateur — le ViewModel (bandeau F2) et le diagnostic (33-05) relisent
/// l'état à chaque tick. Moule <see cref="FakeEtatJournal"/>. Aucun fichier, aucun thread : <see cref="Declencher"/> lève
/// <see cref="Changement"/> sur le thread appelant.</summary>
public sealed class FakeEtatReconstruction : IEtatReconstruction
{
    public PhaseReconstruction Phase { get; set; } = PhaseReconstruction.JamaisLancee;
    public int FichiersTraites { get; set; }
    public int FichiersTotal { get; set; }
    public int FichiersOuvertsDernierePasse { get; set; }
    public bool SemaineCouranteDisponible { get; set; }
    public string? DernierFichier { get; set; }
    public string? DerniereErreur { get; set; }
    public int FichiersDisparus { get; set; }
    public int LignesIgnorees { get; set; }
    public int IdsConnus { get; set; }
    public TimeSpan? DureeMurDernierePasse { get; set; }
    public TimeSpan? DureeCpuProcessusDernierePasse { get; set; }
    public DateTimeOffset? DerniereReconstructionTerminee { get; set; }

    public event EventHandler? Changement;

    public void Declencher() => Changement?.Invoke(this, EventArgs.Empty);
}
