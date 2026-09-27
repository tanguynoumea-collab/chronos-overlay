using System.Runtime.InteropServices;
using System.Windows;
using Chronos.Services;

namespace Chronos.Views;

/// <summary>
/// Quick 260927-reglages-v2 — <see cref="IPressePapiers"/> sur le presse-papiers WPF (« ⧉ Copier » du diagnostic). Un presse-papiers
/// tenu par une autre application lève <see cref="COMException"/> (CLIPBRD_E_CANT_OPEN) : on rend <c>false</c>, la vue le dit, rien
/// ne remonte.
/// </summary>
public sealed class PressePapiersWpf : IPressePapiers
{
    public bool Copier(string texte)
    {
        try
        {
            Clipboard.SetText(texte ?? "");
            return true;
        }
        catch (ExternalException) { return false; }   // COMException en dérive : presse-papiers tenu ailleurs
    }
}
