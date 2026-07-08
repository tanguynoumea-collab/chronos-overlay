namespace Chronos.Services;

/// <summary>Pilote le lancement au démarrage Windows via un raccourci shell:startup (DEP-02).</summary>
public interface IAutostartService
{
    /// <summary>Vrai si le raccourci d'autostart existe.</summary>
    bool IsEnabled();

    /// <summary>Crée le raccourci .lnk dans le dossier startup, ciblant l'exe courant.</summary>
    void Enable();

    /// <summary>Supprime le raccourci .lnk s'il existe (idempotent).</summary>
    void Disable();
}
