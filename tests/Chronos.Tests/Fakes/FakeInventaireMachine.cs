using System;
using System.Collections.Generic;
using Chronos.Services;

namespace Chronos.Tests;

/// <summary>
/// Faux inventaire de machine : rend une liste VIDE, instantanément.
///
/// Raison d'être MESURÉE (2026-09-12) : les 7 tests de <c>DiagnosticServiceTests</c> payaient
/// 2 min 8 s à eux seuls (98,4 % de la suite complète), dont 17 703 ms par balayage de coffres OAuth.
/// Ce sondage interroge la MACHINE de celui qui lance les tests — son résultat n'est donc ni
/// reproductible, ni asserté.
///
/// <para>Phase 21 : le second membre substitué ici (poll de la source app-bureau, 936 ms) a disparu
/// avec cette source.</para>
///
/// AUCUNE des classes de test qui montent un <c>DiagnosticService</c> n'asserte le contenu de cette
/// section du rapport : la substitution ne change DONC aucune assertion existante. Elle retire
/// seulement de la boucle de rétroaction un balayage de disque.
/// </summary>
public sealed class FakeInventaireMachine : IInventaireMachine
{
    public IReadOnlyList<string> CoffresOAuth(string racine) => Array.Empty<string>();
}
