using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Chronos.Services;

/// <summary>
/// Accès LECTURE SEULE au Gestionnaire d'identifiants Windows (advapi32 CredEnumerate/CredRead/CredFree).
/// Claude Code sous Windows peut y ranger le jeton de compte principal (cible « Claude Code-credentials »
/// ou variante) plutôt que dans ~/.claude/.credentials.json. On énumère les identifiants de l'utilisateur
/// courant, on ne retient QUE les cibles dont le nom contient « claude » ou « anthropic », et on renvoie
/// le blob brut au lecteur. Aucune écriture. Le contenu (potentiellement sensible) n'est jamais journalisé.
/// </summary>
internal static class WindowsCredentialStore
{
    // Un identifiant lu : nom de cible + blob binaire (à décoder/parser par l'appelant).
    internal readonly record struct Entry(string TargetName, byte[] Blob);

    [StructLayout(LayoutKind.Sequential)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public nint TargetName;      // LPWSTR
        public nint Comment;         // LPWSTR
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public nint CredentialBlob;  // LPBYTE
        public int Persist;
        public int AttributeCount;
        public nint Attributes;
        public nint TargetAlias;     // LPWSTR
        public nint UserName;        // LPWSTR
    }

    [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredEnumerate(string? filter, int flags, out int count, out nint credentialsPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(nint buffer);

    /// <summary>
    /// Renvoie les identifiants dont la cible contient « claude »/« anthropic » (insensible à la casse).
    /// Toute anomalie (API absente, droits, marshaling) → liste vide, jamais d'exception propagée.
    /// </summary>
    internal static IReadOnlyList<Entry> ReadClaudeEntries()
    {
        var result = new List<Entry>();
        nint ptr = 0;
        try
        {
            // filter=null + flag 0x1 (CRED_ENUMERATE_ALL_CREDENTIALS) → tous les identifiants de l'utilisateur.
            if (!CredEnumerate(null, 0, out int count, out ptr) || ptr == 0) return result;

            for (int i = 0; i < count; i++)
            {
                nint credPtr = Marshal.ReadIntPtr(ptr, i * nint.Size);
                if (credPtr == 0) continue;

                var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                var target = cred.TargetName != 0 ? Marshal.PtrToStringUni(cred.TargetName) : null;
                if (string.IsNullOrEmpty(target)) continue;

                var t = target.ToLowerInvariant();
                if (!t.Contains("claude") && !t.Contains("anthropic")) continue;

                byte[] blob = System.Array.Empty<byte>();
                if (cred.CredentialBlob != 0 && cred.CredentialBlobSize > 0)
                {
                    blob = new byte[cred.CredentialBlobSize];
                    Marshal.Copy(cred.CredentialBlob, blob, 0, cred.CredentialBlobSize);
                }
                result.Add(new Entry(target, blob));
            }
        }
        catch
        {
            // API indisponible / droits insuffisants / structure inattendue → on renvoie ce qu'on a.
        }
        finally
        {
            if (ptr != 0) CredFree(ptr);
        }
        return result;
    }
}
