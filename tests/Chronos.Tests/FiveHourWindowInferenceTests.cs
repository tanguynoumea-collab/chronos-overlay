using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve l'inférence PURE du début de la fenêtre 5 h (EST-01/EST-02), algorithme « A » verrouillé :
/// début = le plus ancien message contigu (aucun trou ≥ 5 h) en remontant depuis le plus récent ;
/// reset = début + 5 h ; reset ≤ now ⇒ fenêtre expirée/inactive ⇒ null. Tests PURS : now passé en
/// paramètre, aucun I/O, aucun type WPF. now de référence = 2026-07-08T12:00:00Z (cohérent avec l'existant).
/// L'appelant garantit un ordre croissant (le provider trie en 08-02) : ici on passe des listes triées.
/// </summary>
public class FiveHourWindowInferenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 08, 12, 00, 00, TimeSpan.Zero);

    private static DateTimeOffset At(double hoursBeforeNow) => Now - TimeSpan.FromHours(hoursBeforeNow);

    [Fact]
    public void Liste_vide_renvoie_null_inactive()
    {
        var result = FiveHourWindowInference.InferWindowStart(Array.Empty<DateTimeOffset>(), Now);

        Assert.Null(result); // aucune activité → inactive (fraction = 1 en aval)
    }

    [Fact]
    public void Message_unique_recent_renvoie_ce_message_avec_reset_futur()
    {
        var msg = At(1); // now - 1 h
        var result = FiveHourWindowInference.InferWindowStart(new[] { msg }, Now);

        Assert.Equal(msg, result);
        Assert.True(result!.Value + FiveHourWindowInference.Window > Now); // reset = start + 5 h > now
    }

    [Fact]
    public void Message_unique_expire_renvoie_null()
    {
        var msg = At(6); // now - 6 h → reset = now - 1 h ≤ now (EST-02)
        var result = FiveHourWindowInference.InferWindowStart(new[] { msg }, Now);

        Assert.Null(result);
    }

    [Fact]
    public void Trou_exactement_5h_est_une_borne_stricte()
    {
        // 06:00 et 11:00 : trou = 5 h EXACT → borne (>=) → seul le plus récent est retenu.
        var older = At(6);   // 06:00
        var recent = At(1);  // 11:00
        var result = FiveHourWindowInference.InferWindowStart(new[] { older, recent }, Now);

        Assert.Equal(recent, result); // le trou ≥ 5 h coupe : start = message le plus récent seul
    }

    [Fact]
    public void Rafale_contigue_courte_recule_jusquau_plus_ancien()
    {
        // 4 messages sur 1 h 30 (aucun trou ≥ 5 h) → start = le plus ancien de la rafale.
        var oldest = At(2);              // 10:00
        var burst = new[] { oldest, At(1.5), At(1), At(0.5) }; // 10:00 → 11:30, trié croissant
        var result = FiveHourWindowInference.InferWindowStart(burst, Now);

        Assert.Equal(oldest, result);
        Assert.True(result!.Value + FiveHourWindowInference.Window > Now); // fenêtre active
    }

    [Fact]
    public void Activite_continue_au_dela_de_5h_est_inactive()
    {
        // Activité toutes les 30 min de 05:00 à 11:30 (aucun trou ≥ 5 h) : start remonte à 05:00,
        // reset = 10:00 ≤ now → null (comportement « A » verrouillé, Open Question 1).
        var ts = new List<DateTimeOffset>();
        for (double h = 7; h >= 0.5; h -= 0.5) ts.Add(At(h)); // 05:00 → 11:30, trié croissant
        var result = FiveHourWindowInference.InferWindowStart(ts, Now);

        Assert.Null(result);
    }

    [Fact]
    public void Suppose_une_liste_triee_croissante_fournie_par_lappelant()
    {
        // Documente le contrat : l'appelant (provider en 08-02) trie ; ici on passe une liste triée.
        var ts = new[] { At(4), At(3), At(2), At(1) }; // 08:00 → 11:00, croissant
        var result = FiveHourWindowInference.InferWindowStart(ts, Now);

        Assert.Equal(At(4), result); // rafale contiguë < 5 h → plus ancien
    }
}
