using System.Collections.Generic;

namespace Chronos.Services;

/// <summary>
/// DTO NEUTRE d'un nœud d'arbre d'accessibilité (UI Automation), sans aucune dépendance à
/// System.Windows.Automation ni type WPF. Toute la logique de <see cref="DesktopUiaSessionSource"/>
/// travaille sur ce DTO → testable par un FAUX arbre, aucune fenêtre Claude réelle requise.
///
/// Le walk réel (AutomationElement/TreeWalker) est isolé dans WindowsUiaTreeProvider (Plan 02),
/// qui projette chaque élément vers un <see cref="UiaNode"/>.
///
/// <see cref="AutomationId"/> est SIGNIFICATIF : il porte le littéral d'ancre "RootWebArea"
/// (rôle a11y Chromium/IA2, ControlType "Document"). C'est le SEUL cas où l'AutomationId est
/// matché (cf. DesktopUiaSessionSource) — car ce rôle est stable, contrairement aux identifiants
/// volatils « base-ui-_r_XXX_ » qu'on ne code JAMAIS en dur. Le provider réel DOIT donc renseigner
/// AutomationId depuis Current.AutomationId.
/// </summary>
public sealed record UiaNode(
    string ControlType,
    string Name,
    string? AutomationId,
    bool Enabled,
    IReadOnlyList<UiaNode> Children);
