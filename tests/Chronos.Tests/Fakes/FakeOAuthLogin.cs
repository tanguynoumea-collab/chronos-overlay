using System.Threading.Tasks;
using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Fake d'<see cref="IOAuthLogin"/> : bascule un état de connexion en mémoire, sans navigateur ni réseau.
/// Compte les appels de login ET de déconnexion : <see cref="LogoutCount"/> est la preuve décisive de TOK-03
/// (la pastille de reconnexion ne doit JAMAIS supprimer le coffre de jetons de l'utilisateur).</summary>
public sealed class FakeOAuthLogin : IOAuthLogin
{
    public bool LoggedIn { get; set; }

    /// <summary>Permet de simuler un login qui ÉCHOUE (navigateur fermé, code invalide).</summary>
    public bool LoginDoitReussir { get; set; } = true;

    public int LoginCount { get; private set; }
    public int LogoutCount { get; private set; }

    public bool IsLoggedIn => LoggedIn;

    public Task<bool> LoginAsync()
    {
        LoginCount++;
        if (LoginDoitReussir) LoggedIn = true;
        return Task.FromResult(LoginDoitReussir);
    }

    public void Logout() { LogoutCount++; LoggedIn = false; }
}
