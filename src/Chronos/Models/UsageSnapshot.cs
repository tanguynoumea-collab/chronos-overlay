namespace Chronos.Models;

/// <summary>Snapshot immuable des deux fenêtres + fraîcheur de la donnée sous-jacente.</summary>
public sealed record UsageSnapshot
{
    public required WindowState FiveHour { get; init; }
    public required WindowState SevenDay { get; init; }

    /// <summary>Ancienneté de SOURCE : à quand remonte la donnée sous-jacente du snapshot, tous providers
    /// confondus. Distincte de la fraîcheur PAR FENÊTRE, que portent <c>WindowState.CapturedAt</c> et
    /// <c>WindowState.Provenance</c> — seule cette dernière fait autorité pour juger un chiffre.
    ///
    /// Conservée en phase 20 parce qu'elle est assertée par les tests de plusieurs providers : ce champ
    /// DIT quelque chose de vrai. Ce qui a été supprimé, c'est la seconde notion de « périmé » que le
    /// ViewModel en dérivait avec son propre seuil, concurrente de celle de la doctrine.</summary>
    public DateTimeOffset? SourceCapturedAt { get; init; }

    /// <summary>EXA-05 — un relevé exact a-t-il déjà été obtenu au moins une fois ? <c>null</c> = non
    /// évalué (tout snapshot produit SOUS la couche de doctrine, dont <see cref="Empty"/>). <c>false</c> =
    /// jamais : l'overlay doit inviter à se connecter et ne JAMAIS afficher de pourcentage.
    ///
    /// bool? et non bool : avec bool, Empty vaudrait false, c'est-à-dire « jamais eu d'exact » — une
    /// AFFIRMATION produite par une absence, exactement ce que le projet proscrit.
    ///
    /// Sur UsageSnapshot et non WindowState : c'est un fait de COMPTE, pas de fenêtre. Sans danger malgré
    /// la recomposition par « new » de CompositeUsageProvider.GetAsync, parce que ce champ est posé par la
    /// couche de doctrine, qui est AU-DESSUS du composite : aucun composite ne le reverra jamais.</summary>
    public bool? UnExactADejaEteObtenu { get; init; }

    /// <summary>Snapshot « données indisponibles » : deux fenêtres Unavailable, aucun crash (ROB-01).</summary>
    public static UsageSnapshot Empty => new()
    {
        FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
        SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
    };
}
