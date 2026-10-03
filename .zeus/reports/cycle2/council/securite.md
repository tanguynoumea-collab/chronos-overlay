# Audit Sécurité desktop — Chronos, cycle ZEUS 2 (2026-10-03)

Rôle : Sécurité desktop (Tier 2), dev-team-council. Lecture seule. Aucun secret lu ni affiché : `oauth.dat` n'a pas
été ouvert, et aucun jeton n'apparaît dans ce rapport.
Modèle de menace : application desktop locale mono-utilisateur. Les surfaces examinées sont les secrets (coffre
OAuth DPAPI), les entrées externes (stdin des hooks, transcripts JSONL, `~/.claude/settings.json`), les écritures
dans un fichier qu'un autre programme exécute (`settings.json` de Claude Code), les dépendances et la distribution
du binaire. Les classes de faille web (CSRF, XSS, sessions, rate limiting, multi-tenant) sont écartées d'office.

## Phase 1 — Vérité-terrain (outils)

| Contrôle | Résultat |
|---|---|
| `dotnet list Chronos.sln package --vulnerable --include-transitive` | **0 paquet vulnérable** (Chronos et Chronos.Tests, source nuget.org). Dépendances directes : CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting 8.0.1, System.Security.Cryptography.ProtectedData 8.0.0. |
| Security Code Scan | **NON VÉRIFIÉE** : le paquet `SecurityCodeScan.VS2019` est absent du csproj. |
| Recherche de secrets (`git grep`) : `sk-ant-*`, JWT `eyJ…`, `password=`, `apikey`, `client_secret` ; fichiers `oauth.dat`, `.credentials`, `.pfx`, `.snk`, `.env` suivis par git | **Aucun résultat.** Seule constante : `ChronosOAuthClient.ClientId`, l'identifiant de client OAuth **public** (PKCE, sans secret client), qui n'est pas un secret. `bin/` est ignoré par git. |
| Désérialisation dangereuse (`BinaryFormatter`, `TypeNameHandling`, `JsonPolymorphic`, `XmlReader`, `XmlDocument`, `XamlReader`) | **Aucune occurrence** dans `src/`. Tout le parsing passe par System.Text.Json en DOM ou en types fermés. |
| Signature Authenticode (`Get-AuthenticodeSignature`) | `bin/Release/.../publish/Chronos.exe` : **NotSigned**. |
| Distribution (`gh repo view`, `gh release list`/`view`) | Dépôt **PUBLIC** `tanguynoumea-collab/chronos-overlay`. La release v2.8.1 publie l'asset `Chronos-v2.8.1.exe`. Les notes de release ne contiennent ni empreinte ni mention de signature. |

---

## Phase 2 — Findings (schéma §7)

### SEC-1 — Exe non signé distribué publiquement, sans empreinte publiée
- **Sévérité** : Majeur
- **Rôle** : Sécurité desktop
- **Localisation** : `docs/publish.md:79-81` ; pipeline de release (GitHub Releases) ; `src/Chronos/Chronos.csproj` (aucune étape de signature).
- **Preuve** : `Get-AuthenticodeSignature` renvoie `NotSigned` sur `src\Chronos\bin\Release\net8.0-windows\win-x64\publish\Chronos.exe`. `gh release view v2.8.1` liste l'asset `Chronos-v2.8.1.exe` sur un dépôt `PUBLIC`. Le corps de la release ne contient ni « sha », ni « hash », ni « checksum », ni « sign ». La doc l'assume : « exe non signé — c'est normal ».
- **Constat** : le binaire est diffusé à des tiers sans signature Authenticode et sans empreinte SHA-256. L'utilisateur n'a donc aucun moyen de vérifier qu'un `Chronos-vX.Y.exe` téléchargé est bien celui qui a été compilé.
- **Impact** : le rayon d'action dépasse le seul overlay. L'exe s'inscrit lui-même comme commande de hook dans `~/.claude/settings.json` (`SessionHookInstaller.cs:137`, repointé à chaque démarrage par `ClaudeSettingsReconciler`). Il est donc **exécuté par Claude Code à chaque événement de session**, et lancé au boot via `shell:startup`. Un binaire substitué (compte GitHub compromis, miroir, copie partagée) obtiendrait une exécution persistante, et l'avertissement SmartScreen « éditeur inconnu », banalisé par la doc, ne distinguerait plus l'original du faux.
- **Recommandation** : au minimum, publier l'empreinte SHA-256 de chaque exe dans les notes de release (et/ou une attestation GitHub `actions/attest-build-provenance`). Mieux : signer l'exe (certificat Authenticode ou Azure Trusted Signing) et horodater la signature. Retirer de `docs/publish.md` la formule qui présente l'alerte SmartScreen comme normale.
- **Applicabilité** : desktop. Il s'agit de l'intégrité d'un exécutable téléchargé et installé localement (calibrage « binaire non signé distribué »), pas d'un réflexe web. La sévérité est proportionnée : le transport GitHub en HTTPS protège le transit, la faiblesse porte sur l'authenticité de l'origine.
- **Statut challenge** : non challengé

### SEC-2 — `session_id` du stdin des hooks concaténé sans validation dans un chemin d'écriture et de suppression
- **Sévérité** : Mineur
- **Rôle** : Sécurité desktop
- **Localisation** : `src/Chronos/Services/SessionHookProcessor.cs:79` (lecture brute de `session_id`), `src/Chronos/Services/EcritureEtatSession.cs:67-71` (`Path.Combine(dossier, resultat.SessionId + ".json")`, puis `File.Delete` sur `SessionEnd` ou `FileMode.OpenOrCreate` en écriture), `src/Chronos/App.xaml.cs:175-182`.
- **Preuve** : aucune validation de forme (`Guid.TryParse`, liste blanche de caractères, `GetFullPath` borné au dossier) dans `src/`. Un `grep` de `GetInvalidFileNameChars|IsPathRooted|GetFullPath` ne renvoie rien. Avec `Path.Combine`, un `session_id` absolu (`C:\x\y`) **remplace** le dossier, et un `..\..\z` en sort.
- **Constat** : la valeur vient du JSON stdin fourni par le processus appelant le mode `--hook`. Une valeur forgée permettrait de créer ou d'écraser n'importe quel fichier `*.json` accessible à l'utilisateur avec un petit JSON d'état au contenu contraint, ou de **supprimer n'importe quel `*.json`** via un événement `SessionEnd`.
- **Impact** : faible en pratique. Le stdin est produit par Claude Code, qui génère des UUID. Le seul autre émetteur possible est un processus qui tourne déjà sous le même compte, et qui a donc déjà ces droits. Aucun vecteur d'élévation n'est démontré. Le risque est celui d'une régression si Claude Code relâchait un jour le format, ou d'un `session_id` influencé par un contenu externe.
- **Recommandation** : valider `session_id` avant toute E/S. Rejeter (`Ignored`) tout ce qui n'est pas `^[A-Za-z0-9_-]{1,128}$` (ou `Guid.TryParse`), puis vérifier que `Path.GetFullPath(fichier)` commence par `Path.GetFullPath(dossier)`. Ajouter un test avec `..\`, un chemin absolu et `:`.
- **Applicabilité** : desktop. C'est du path traversal sur une donnée d'entrée inter-processus qui pilote une écriture et une suppression de fichiers locaux. Ce n'est pas un modèle web. La sévérité est abaissée faute de vecteur d'attaque prouvé hors du même compte.
- **Statut challenge** : non challengé

### SEC-3 — Les records `OAuthTokens` et `ResultatRafraichissement` exposent les jetons par leur `ToString()` généré
- **Sévérité** : Mineur
- **Rôle** : Sécurité desktop
- **Localisation** : `src/Chronos/Services/ChronosOAuthClient.cs:12` (`public sealed record OAuthTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)`) et `:37` (`record ResultatRafraichissement(..., OAuthTokens? Jetons)`).
- **Preuve** : un `record` C# génère `ToString()`/`PrintMembers` qui impriment toutes les propriétés publiques. `$"{jetons}"` produirait donc `OAuthTokens { AccessToken = …, RefreshToken = … }`, et `$"{resultat}"` imbrique ce même texte. Aucun site actuel ne journalise ces objets (vérifié par `grep` : `AccessToken`/`RefreshToken` n'apparaissent que dans l'autorité et la construction de l'en-tête).
- **Constat** : le code documente avec soin « jamais de champ texte, rien n'est journalisé ». Mais le type lui-même ne protège rien : une seule interpolation dans `DiagnosticService` (qui écrit `chronos.log` à **chaque démarrage**, `DiagnosticService.cs:115`) ou dans un message d'assertion de test écrirait le refresh token en clair sur disque.
- **Impact** : aujourd'hui, aucun, il n'y a pas de fuite. C'est un piège latent à l'endroit le plus sensible de l'app : le refresh token donne un accès durable au compte.
- **Recommandation** : surcharger `ToString()` (ou `PrintMembers`) des deux records pour masquer les jetons (`AccessToken = ***`), et verrouiller ce comportement par un test.
- **Applicabilité** : desktop. Le risque est l'écriture accidentelle d'un secret dans un journal local (`%APPDATA%\Chronos\chronos.log`, `diagnostic.txt`, ouvert par l'utilisateur et potentiellement partagé pour du support). C'est du durcissement en profondeur.
- **Statut challenge** : non challengé

### SEC-4 — Coffre OAuth DPAPI : conforme au modèle de menace (posture)
- **Sévérité** : Info
- **Rôle** : Sécurité desktop
- **Localisation** : `src/Chronos/Services/ChronosOAuthStore.cs:32-54` ; `src/Chronos/Services/ChronosTokenAuthority.cs`.
- **Preuve** : `ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser)`. Le fichier temporaire est écrit **déjà chiffré** puis déplacé (`File.Move`, écriture atomique). Toute erreur de `Load` renvoie `null` sans trace. Le refresh token ne sort de l'autorité que vers `RefreshAsync` ; la sonde et le provider ne reçoivent que l'access token. Les corps de réponse ne sont jamais lus par la sonde (`RateLimitHeaderUsageProvider.cs:36-39`) ni transportés en cas d'échec (`ResultatRafraichissement`).
- **Constat** : la portée `CurrentUser` est la bonne pour une app mono-utilisateur sans droit admin. L'absence d'entropie supplémentaire signifie que tout processus du même compte Windows peut déchiffrer `oauth.dat`, ce qui est inhérent à DPAPI et reste au niveau de Claude Code lui-même (qui conserve ses propres identifiants dans le profil). Aucune régression constatée sur le cycle 2.
- **Impact** : aucun.
- **Recommandation** : aucune action requise. Une entropie fixe dérivée de l'app n'apporterait qu'une obscurité marginale.
- **Applicabilité** : desktop (secret stocké localement, chiffrement DPAPI demandé par le rôle).
- **Statut challenge** : non challengé

### SEC-5 — Le `state` OAuth collé n'est pas comparé au `state` émis
- **Sévérité** : Info
- **Rôle** : Sécurité desktop
- **Localisation** : `src/Chronos/Services/ChronosOAuthClient.cs:99-111, 161-166`.
- **Preuve** : `SplitCodeState` renvoie le `state` présent après `#` dans le texte collé et l'envoie tel quel au point de terminaison de jeton. Aucune égalité avec le `state` généré par `CreatePkce` n'est vérifiée.
- **Constat** : dans le flux « code à copier », une injection de code (l'utilisateur colle un code obtenu par un tiers) est déjà neutralisée par PKCE S256 : le code d'un tiers est lié à un autre `code_challenge`, et l'échange échoue sans le `code_verifier` local. Le contrôle du `state` serait redondant mais peu coûteux.
- **Impact** : aucun impact exploitable démontré.
- **Recommandation** : facultatif. Refuser localement un code dont le suffixe `#state` diffère du `state` émis, avec un message « code d'une autre tentative, recommence l'étape 1 ».
- **Applicabilité** : desktop. Il s'agit du login OAuth natif d'une app locale (RFC 8252), pas d'une session web. C'est noté Info parce que PKCE couvre déjà la menace.
- **Statut challenge** : non challengé

### SEC-6 — La réconciliation peut écrire dans `statusLine.command` une commande lue depuis les réglages Chronos (héritage)
- **Sévérité** : Info
- **Rôle** : Sécurité desktop
- **Localisation** : `src/Chronos/Services/ClaudeSettingsReconciler.cs:111, 118-144, 195-199`.
- **Preuve** : `RetirerBarreChronos` fait `sl["command"] = commandeHeritee`, où `commandeHeritee` provient de la clé héritée `InnerStatusLineCommand` de `%APPDATA%\Chronos\settings.json`. Claude Code **exécute** `statusLine.command`.
- **Constat** : un fichier que Chronos ne possède pas reçoit une commande exécutable issue d'un autre fichier. Les garde-fous sont solides : rien n'est écrit si le JSON est inexploitable, le fichier n'est jamais créé, une sauvegarde horodatée est faite avant toute écriture (sinon abandon), l'écriture est atomique, une barre tierce n'est jamais touchée, et une commande Chronos n'est jamais restaurée. Les deux fichiers relèvent du même compte, donc il n'y a pas de franchissement de frontière de confiance.
- **Impact** : aucun gain pour un attaquant : qui peut écrire `%APPDATA%\Chronos\settings.json` peut aussi écrire `~/.claude/settings.json`.
- **Recommandation** : aucune action. Si l'on veut resserrer, limiter la restauration au premier passage (la clé disparaît de toute façon au premier `Save`) et faire figurer la commande restaurée en clair dans le bilan `chronos.log`, pour que l'utilisateur la voie.
- **Applicabilité** : desktop. Il s'agit d'écrire dans la configuration exécutable d'un autre outil local.
- **Statut challenge** : non challengé

### SEC-7 — Commande de hook entre guillemets doubles : expansion shell théorique du chemin de l'exe
- **Sévérité** : Info
- **Rôle** : Sécurité desktop
- **Localisation** : `src/Chronos/Services/SessionHookInstaller.cs:137` (`"\"" + exePath.Replace('\', '/') + "\" --hook " + ev`).
- **Preuve** : le chemin est entouré de `"`. Sous Windows, Claude Code peut exécuter les commandes de hook via un shell POSIX (Git Bash), qui développe `$…` et les backticks à l'intérieur de guillemets doubles. Windows autorise ces caractères dans les noms de dossiers. En revanche, `"` y est interdit, ce qui exclut une sortie des guillemets.
- **Constat** : un exe placé dans un dossier dont le nom contient `$(` ou un backtick produirait une commande interprétée. C'est l'utilisateur qui choisit l'emplacement de l'exe ; aucun tiers ne le contrôle.
- **Impact** : aucun vecteur d'attaque.
- **Recommandation** : aucune action. On peut éventuellement refuser l'installation des hooks (avec un message) si `exePath` contient `$` ou un backtick.
- **Applicabilité** : desktop (commande locale inscrite dans la configuration d'un outil local).
- **Statut challenge** : non challengé

### SEC-8 — Lecture des transcripts JSONL : longueur de ligne non bornée en mémoire
- **Sévérité** : Info
- **Rôle** : Sécurité desktop
- **Localisation** : `src/Chronos/Services/Historique/Tokens/LecteurTranscript.cs:101, 116, 137-141`.
- **Preuve** : un fragment sans `\n` s'accumule dans un `MemoryStream` (`reste`) sans plafond, puis il est copié (`ToArray`) avant le parsing.
- **Constat** : les transcripts de `~/.claude/projects` peuvent contenir des lignes très longues (sorties d'outils, images en base64, contenus récupérés sur le web). Le parsing est sûr (System.Text.Json, extraction de champs numériques, pré-filtre sur `"type":"assistant"`, aucune désérialisation polymorphe). Le seul effet possible est un pic mémoire sur une ligne de plusieurs centaines de Mo.
- **Impact** : déni de service local par auto-intoxication, peu plausible. Rien n'est exécuté et aucun contenu n'est réinjecté.
- **Recommandation** : facultatif. Au-delà d'un plafond (par exemple 16 Mo), abandonner la ligne, la compter comme ignorée et resynchroniser au `\n` suivant.
- **Applicabilité** : desktop (parsing d'un fichier local dont une partie du contenu est d'origine externe).
- **Statut challenge** : non challengé

### SEC-9 — Points de posture vérifiés (aucune action)
- **Sévérité** : Info
- **Rôle** : Sécurité desktop
- **Localisation** : transverse.
- **Preuve et constat** :
  - Dépendances : 0 CVE, transitives incluses (Phase 1).
  - Écriture de `~/.claude/settings.json` (`ClaudeSettingsReconciler`, `ClaudeSettingsJson.ParseOrNull`) : un contenu inexploitable donne **null, donc rien n'est écrit**. Les clés dupliquées sont détectées. Le fichier n'est jamais créé. La sauvegarde est faite avant écriture, et un échec de sauvegarde annule l'écriture. L'écriture passe par un temporaire puis `Move`. Les sauvegardes restent sous `%APPDATA%\Chronos\backups` avec une rétention de 5. L'identité est établie par le marqueur plus le nom de fichier `Chronos*.exe`, ce qui borne le rayon d'action sur les hooks des autres outils. Le mode `--hook` ne réconcilie jamais, ce qui écarte les courses à 5 processus.
  - Arguments de démarrage (`ArgumentsDemarrage.cs`) : liste blanche, et tout `--xxx` inconnu provoque une sortie silencieuse avant le verrou.
  - Réseau : `HttpClient` par défaut (validation TLS du système, pas de `ServerCertificateCustomValidationCallback`), URL HTTPS en constantes, jeton uniquement en en-tête `Authorization` (jamais dans l'URL), délais bornés (8 et 15 s), aucun corps de réponse lu par la sonde, codes d'état aiguillés sans remonter de texte venu du réseau (`NomsEnTetesRecus` = constantes locales).
  - Sorties d'erreur du mode `--hook` (`App.xaml.cs:185-190`) : type et message d'exception (chemins locaux), sans contenu de stdin ni secret.
  - `Process.Start(UseShellExecute)` n'est appelé que sur l'URL d'autorisation construite en interne (`OAuthLogin.cs:88`) et sur `diagnostic.txt` (`DiagnosticService.cs:134`). Aucun chemin ni URL d'origine externe n'est lancé.
  - Aucun droit admin, tous les chemins sont sous `%APPDATA%`/`%USERPROFILE%`, et l'autostart passe par un `.lnk` dans `shell:startup`.
- **Impact** : aucun.
- **Recommandation** : aucune.
- **Applicabilité** : desktop.
- **Statut challenge** : non challengé

---

## Synthèse

| Sévérité | Nombre | IDs |
|---|---|---|
| Bloquant | 0 | — |
| Majeur | 1 | SEC-1 |
| Mineur | 2 | SEC-2, SEC-3 |
| Info | 6 | SEC-4, SEC-5, SEC-6, SEC-7, SEC-8, SEC-9 |

**Points non vérifiés faute d'outil**
- Security Code Scan : **NON VÉRIFIÉ**, paquet `SecurityCodeScan.VS2019` absent.
- Signature et empreinte des assets déjà publiés sur GitHub : seul le binaire local a été contrôlé (`NotSigned`). L'asset `Chronos-v2.8.1.exe` n'a pas été téléchargé.
- Comportement réel du shell utilisé par Claude Code pour exécuter les hooks sous Windows (SEC-7) : déduit, non observé (consigne : ne jamais lancer l'exe).
- Contenu réel de `oauth.dat` et de `~/.claude/settings.json` : volontairement non lus (consigne de non-exposition des secrets).
