// chronos-statusline-bridge.js — Pont statusLine -> fichier (source PRIMAIRE de Chronos).
//
// ROLE : Claude Code POUSSE le JSON de session (dont le bloc `rate_limits`) sur le stdin de la
//   commande statusLine. Rien n'est persiste sur disque. Ce pont materialise `rate_limits` dans
//   %APPDATA%\Chronos\usage.json pour qu'un overlay externe puisse le lire (FileSystemWatcher).
//
// NON DESTRUCTIF : une seule commande statusLine est configurable et l'utilisateur en a deja une
//   active (gsd-statusline.js). Ce pont l'ENVELOPPE : il bufferise le stdin, ecrit usage.json, puis
//   RE-EXECUTE gsd-statusline.js avec LE MEME stdin et RE-EMET sa sortie intacte. Il ne remplace
//   jamais la barre existante.
//
// ATOMIQUE AVANT SPAWN : l'ecriture (temp + renameSync) se fait AVANT de relancer la statusLine
//   enfant. Claude Code annule l'execution en vol si un nouveau rendu arrive (debounce 300 ms) ;
//   ecrire d'abord garantit que usage.json aboutit meme si la re-emission est annulee.
//
// SECURITE : ce pont ne lit QUE `data.rate_limits` du stdin. Il ne touche jamais aux jetons OAuth du
//   profil utilisateur ni au contenu des conversations. Toute l'ecriture est en try/catch : une
//   erreur d'ecriture ne doit JAMAIS casser la statusLine.

const fs = require("fs");
const path = require("path");
const { spawnSync } = require("child_process");

// Commande statusLine existante a re-executer (slashes avant : impératif sous Git Bash).
const CHILD_STATUSLINE = "C:/Users/Tanguy/.claude/hooks/gsd-statusline.js";

// Le stdin ne peut etre lu qu'UNE fois : on bufferise tout avant de traiter.
let input = "";
process.stdin.setEncoding("utf8");
process.stdin.on("data", (chunk) => {
  input += chunk;
});

process.stdin.on("end", () => {
  // 1) Parser en try/catch : un stdin invalide ne doit rien casser.
  let data = null;
  try {
    data = JSON.parse(input);
  } catch {
    data = null;
  }

  // 2) ECRIRE usage.json ATOMIQUEMENT AVANT tout spawn (survie a l'annulation en vol).
  //    Best-effort : envelopper dans un try/catch pour ne jamais casser la statusLine.
  try {
    const dir = path.join(process.env.APPDATA, "Chronos");
    fs.mkdirSync(dir, { recursive: true }); // le dossier peut ne pas exister encore.

    const usageFinal = path.join(dir, "usage.json");
    const tmp = path.join(dir, "usage.json.tmp-" + process.pid);

    // Payload SANS invention : fenetre par fenetre, null si absente. capturedAt = epoch MILLISECONDES.
    const rl = data && data.rate_limits;
    const payload = {
      five_hour: (rl && rl.five_hour) || null,
      seven_day: (rl && rl.seven_day) || null,
      capturedAt: Date.now(),
    };

    fs.writeFileSync(tmp, JSON.stringify(payload));
    fs.renameSync(tmp, usageFinal); // remplacement atomique (MoveFileEx REPLACE_EXISTING sur Windows).
  } catch {
    // Ecriture impossible (droits, disque, %APPDATA% absent) : on continue sans casser la barre.
  }

  // 3) RE-EXECUTER la statusLine existante avec LE MEME stdin bufferise.
  let child;
  try {
    child = spawnSync("node", [CHILD_STATUSLINE], { input: input, encoding: "utf8" });
  } catch {
    child = null;
  }

  // 4) RE-EMETTRE la sortie enfant TELLE QUELLE (ANSI/multi-lignes preserves) + propager le code.
  if (child) {
    if (child.stdout) process.stdout.write(child.stdout);
    if (child.stderr) process.stderr.write(child.stderr);
    process.exit(child.status || 0);
  }
  process.exit(0);
});
