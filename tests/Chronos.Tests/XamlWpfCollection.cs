using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Collection SÉRIALISÉE des classes de test qui chargent du BAML, c'est-à-dire qui construisent un
/// type WPF déclaré en XAML (<c>MainWindow</c>, <c>SettingsWindow</c>…).
///
/// POURQUOI : par défaut xUnit exécute les classes de test en PARALLÈLE, chaque <c>[WpfFact]</c> sur
/// son propre thread STA. Or le chargeur BAML de WPF (<c>WpfXamlType.FindKnownMember</c>) peuple
/// paresseusement une table de membres connus qui n'est pas sûre en accès concurrent : deux chargements
/// de XAML simultanés peuvent faire lever
/// <c>XamlParseException : The given key 'ColumnDefinitions' was not present in the dictionary</c>
/// sur un XAML pourtant parfaitement valide. Le symptôme est INTERMITTENT et dépend de la vitesse
/// relative des classes de test — donc indétectable par relance isolée, et trompeur : il désigne un
/// fichier XAML alors que le défaut est dans le parallélisme.
///
/// <c>DisableParallelization</c> empêche cette collection de tourner en même temps que les autres :
/// les chargements de BAML sont sérialisés entre eux, le reste de la suite garde son parallélisme.
/// </summary>
[CollectionDefinition("XAML WPF", DisableParallelization = true)]
public sealed class XamlWpfCollection
{
}
