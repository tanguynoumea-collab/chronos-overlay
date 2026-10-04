namespace Chronos.Services;

/// <summary>Bilan de la convergence du raccourci d'autostart vers l'exe courant (PKG-1).</summary>
public enum BilanAutostart
{
    /// <summary>Aucun raccourci : l'autostart est désactivé, rien n'est créé.</summary>
    Absent,
    /// <summary>Le raccourci vise déjà l'exe courant : rien n'est réécrit.</summary>
    Conforme,
    /// <summary>Le raccourci visait un autre exe (version précédente) : il a été réécrit vers l'exe courant.</summary>
    Repointe,
    /// <summary>Le raccourci vise un autre exe (ou est illisible) et n'a pas pu être réécrit.</summary>
    Echec,
}

/// <summary>Pilote le lancement au démarrage Windows via un raccourci shell:startup (DEP-02).</summary>
public interface IAutostartService
{
    /// <summary>Vrai si le raccourci d'autostart existe ET vise l'exe courant (PKG-1 : un raccourci vers une
    /// version précédente se lit « désactivé », un clic le réécrit).</summary>
    bool IsEnabled();

    /// <summary>Crée le raccourci .lnk dans le dossier startup, ciblant l'exe courant.</summary>
    void Enable();

    /// <summary>Supprime le raccourci .lnk s'il existe (idempotent).</summary>
    void Disable();

    /// <summary>Repointe un raccourci EXISTANT vers l'exe courant ; n'en crée jamais. Ne lève jamais.
    /// Membre par défaut (rien à converger) pour les implémentations de test.</summary>
    BilanAutostart ConvergerVersExeCourant() => BilanAutostart.Absent;
}
