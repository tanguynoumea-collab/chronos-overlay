using System;
using System.Collections.Generic;
using Chronos.Services;

namespace Chronos.Tests;

/// <summary>
/// Faux inventaire de machine : rend des listes VIDES, instantanément.
///
/// Raison d'être MESURÉE (2026-09-12) : les 7 tests de <c>DiagnosticServiceTests</c> payaient
/// 2 min 8 s à eux seuls (98,4 % de la suite complète), dont 17 703 ms par balayage de coffres OAuth
/// et 936 ms par poll UI Automation. Ces deux sondages interrogent la MACHINE de celui qui lance les
/// tests — leur résultat n'est donc ni reproductible, ni asserté.
///
/// AUCUNE des classes de test qui montent un <c>DiagnosticService</c> n'asserte le contenu de ces
/// deux sections du rapport : la substitution ne change DONC aucune assertion existante. Elle retire
/// seulement de la boucle de rétroaction un balayage de disque et un appel COM.
/// </summary>
public sealed class FakeInventaireMachine : IInventaireMachine
{
    public IReadOnlyList<string> CoffresOAuth(string racine) => Array.Empty<string>();

    public (IReadOnlyList<SessionSnapshot> Sessions, DesktopHealth Sante) SessionsBureau(DateTimeOffset now)
        => (Array.Empty<SessionSnapshot>(), DesktopHealth.Unknown);
}
