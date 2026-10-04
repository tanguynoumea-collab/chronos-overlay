namespace Chronos.Services.Historique;

/// <summary>
/// D-34-15 — l'accès du ViewModel de la fenêtre Historique aux réglages persistés (style de la vue Semaine, géométrie de la
/// fenêtre), enveloppé pour que la galerie <c>--historique</c> et les tests n'écrivent JAMAIS le vrai <c>settings.json</c>.
/// <see cref="Modifier"/> suit le motif GAP-1 : <c>Save(mutation(Load()))</c> — relire avant d'écrire, pour ne pas écraser une
/// préférence changée entre-temps par la fenêtre de réglages. Contrat NEUTRE (aucun WPF).
/// </summary>
public interface IReglagesHistorique
{
    /// <summary>L'état courant des réglages (relu à chaque appel sur disque).</summary>
    ChronosSettings Lire();

    /// <summary>Applique <paramref name="mutation"/> à l'état courant et persiste le résultat.</summary>
    void Modifier(Func<ChronosSettings, ChronosSettings> mutation);
}

/// <summary>La vraie persistance : <see cref="SettingsService"/> (lecture tolérante, écriture atomique).</summary>
public sealed class ReglagesHistoriqueSurDisque(SettingsService settings) : IReglagesHistorique
{
    private readonly SettingsService _settings = settings;

    /// <inheritdoc />
    public ChronosSettings Lire() => _settings.Load();

    /// <inheritdoc />
    public void Modifier(Func<ChronosSettings, ChronosSettings> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        _settings.Modifier(mutation);   // lire-modifier-écrire sous verrou (42.2-02, MAT-3)
    }
}

/// <summary>En mémoire, pour la galerie et les tests : rien n'est écrit nulle part. <see cref="Courant"/> est observable par les tests.</summary>
public sealed class ReglagesHistoriqueMemoire : IReglagesHistorique
{
    /// <summary>L'état courant (défauts au départ, ou l'état passé au constructeur).</summary>
    public ChronosSettings Courant { get; private set; } = new();

    public ReglagesHistoriqueMemoire() { }

    public ReglagesHistoriqueMemoire(ChronosSettings initial) => Courant = initial;

    /// <inheritdoc />
    public ChronosSettings Lire() => Courant;

    /// <inheritdoc />
    public void Modifier(Func<ChronosSettings, ChronosSettings> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        Courant = mutation(Courant);
    }
}
