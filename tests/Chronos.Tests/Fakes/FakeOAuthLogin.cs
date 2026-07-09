using System.Threading.Tasks;
using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Fake d'<see cref="IOAuthLogin"/> : bascule un état de connexion en mémoire, sans navigateur ni réseau.</summary>
public sealed class FakeOAuthLogin : IOAuthLogin
{
    public bool LoggedIn { get; set; }
    public bool IsLoggedIn => LoggedIn;
    public Task<bool> LoginAsync() { LoggedIn = true; return Task.FromResult(true); }
    public void Logout() => LoggedIn = false;
}
