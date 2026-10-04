namespace Chronos.Services.Historique.Tokens;

/// <summary>
/// MAT-3 / DATA-3 (phase 42.2) — une brique de l'historique des tokens (index d'ids, agrégats) n'a pas pu LIRE son fichier :
/// la passe de reconstruction doit s'interrompre SANS flush, pour ne rien écrire par-dessus ce qu'elle n'a pas lu.
///
/// <para><b>POURQUOI ne pas dériver d'<c>IOException</c></b> : <c>LecteurTranscript.Lire</c> avale <c>IOException</c> autour de
/// sa boucle de lecture (un transcript qui disparaît n'est pas une panne) ; une exception levée par le rappel de traitement
/// y serait engloutie et la passe continuerait. Celle-ci traverse le lecteur et remonte jusqu'au <c>catch</c> de
/// <c>ExecuterUnePasse</c> — qui ne flushe pas.</para>
/// </summary>
public sealed class LectureIllisibleException(string message) : Exception(message);
