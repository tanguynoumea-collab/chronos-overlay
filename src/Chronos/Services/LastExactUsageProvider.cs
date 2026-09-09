using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Décorateur d'<see cref="IUsageProvider"/> en TÊTE de chaîne (EXA-01). Écrivain UNIQUE du magasin
/// persistant : il voit exactement le snapshot fusionné qui sera affiché, donc ni les providers ni
/// l'orchestrateur n'ont à connaître la persistance.
///
/// Rôle en phase 16, et RIEN DE PLUS :
/// 1. délègue à l'inner ;
/// 2. écrit dans le magasin chaque fenêtre <see cref="SourceReliability.Exact"/> du résultat ;
/// 3. si une fenêtre du résultat est <see cref="SourceReliability.Unavailable"/> ET que le magasin
///    détient pour elle un relevé exact encore valide, il la SUBSTITUE — pur rebouchage de trou.
///
/// Il ne remplace JAMAIS une réponse vivante : la doctrine de choix de source
/// (<see cref="CompositeUsageProvider"/>) n'est pas modifiée d'une ligne. La limite d'âge et la
/// correction par delta viendront se greffer ICI, dans une phase ultérieure.
///
/// Choix de conception : un DÉCORATEUR, pas un abonnement à un événement de l'orchestrateur — ce
/// dernier motif exigeait une résolution forcée du service avant le démarrage de l'hôte (invisible
/// au compilateur), et c'est précisément celui que cette phase démolit par ailleurs.
///
/// Type NEUTRE (aucun type WPF) : la garde de pureté Services/Models reste verte.
/// </summary>
public sealed class LastExactUsageProvider : IUsageProvider
{
    private readonly IUsageProvider _inner;
    private readonly LastExactStore _store;
    private readonly IClock _clock;

    public LastExactUsageProvider(IUsageProvider inner, LastExactStore store, IClock clock)
    {
        _inner = inner;
        _store = store;
        _clock = clock;
    }

    public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        var snap = await _inner.GetAsync(ct);
        var now = _clock.UtcNow;

        LastExactWindows? dernier = null;
        try
        {
            // On n'écrit QUE lorsqu'il y a de l'exact à mémoriser : deux passages à vide ne doivent
            // même pas créer le fichier. Le magasin gère lui-même la fusion par fenêtre.
            if (snap.FiveHour.Reliability == SourceReliability.Exact
                || snap.SevenDay.Reliability == SourceReliability.Exact)
                _store.Save(snap);

            dernier = _store.Load(now);
        }
        catch
        {
            // Un magasin en échec (disque plein, fichier verrouillé, contenu illisible) ne doit
            // jamais priver l'utilisateur de son affichage : on rend simplement le snapshot vivant.
            dernier = null;
        }

        if (dernier is null) return snap;

        var five = Reboucher(snap.FiveHour, dernier.FiveHour);
        var seven = Reboucher(snap.SevenDay, dernier.SevenDay);

        // Aucune substitution → on rend l'instance d'origine (aucune allocation, aucun effet de bord).
        // Sinon on reconstruit SANS toucher à SourceCapturedAt : inventer un horodatage de snapshot
        // masquerait l'âge réel du relevé, que porte honnêtement le CapturedAt de la fenêtre.
        return ReferenceEquals(five, snap.FiveHour) && ReferenceEquals(seven, snap.SevenDay)
            ? snap
            : snap with { FiveHour = five, SevenDay = seven };
    }

    /// <summary>
    /// Rebouchage strict : la fenêtre du magasin ne prend la place de la fenêtre vivante que si
    /// celle-ci est INDISPONIBLE. Une fenêtre estimée reste estimée — substituer un relevé mémorisé
    /// à une source vivante, fût-elle moins fiable, serait un choix de doctrine, pas un rebouchage.
    /// </summary>
    private static WindowState Reboucher(WindowState vivante, WindowState? memorisee)
        => vivante.Reliability == SourceReliability.Unavailable && memorisee is not null
            ? memorisee
            : vivante;
}
