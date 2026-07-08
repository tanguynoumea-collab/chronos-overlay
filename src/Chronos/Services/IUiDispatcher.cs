namespace Chronos.Services;

/// <summary>Point unique de franchissement vers le thread UI (testable, sans type WPF côté Services).</summary>
public interface IUiDispatcher
{
    bool CheckAccess();
    void Post(Action action);
}
