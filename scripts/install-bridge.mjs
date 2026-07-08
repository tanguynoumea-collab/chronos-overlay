#!/usr/bin/env node
// install-bridge.mjs — Installeur IDEMPOTENT et NON DESTRUCTIF du pont statusLine de Chronos.
//
// ROLE : brancher `scripts/chronos-statusline-bridge.js` (cree au plan 03-02) dans la
//   commande statusLine de Claude Code (`~/.claude/settings.json` -> statusLine.command),
//   de facon a materialiser le bloc `rate_limits` dans %APPDATA%\Chronos\usage.json — sans
//   jamais casser la statusLine existante ni perdre la commande deja configuree.
//
// GARANTIES :
//   - SAUVEGARDE prealable de settings.json (settings.json.chronos.bak, jamais ecrase).
//   - IDEMPOTENT : relancer sur une config deja pontee = no-op (aucun double-wrapping).
//   - NON DESTRUCTIF : le pont RE-EXECUTE la commande statusLine existante (chainage) ;
//     l'installeur ne touche QUE la cle `statusLine`, toutes les autres cles sont preservees.
//   - REVERSIBLE : `--uninstall` restaure la commande d'origine depuis le backup.
//   - PRUDENT : si settings.json est illisible -> ABANDON sans rien ecrire.
//
// USAGE :
//   node scripts/install-bridge.mjs            # installe (ou no-op si deja installe)
//   node scripts/install-bridge.mjs --uninstall # desinstalle (restaure l'original)
//   node scripts/install-bridge.mjs --force     # reecrit meme si la commande existante
//                                               # ne correspond pas au child chaine par le pont
//
// SECURITE : le pont ne lit QUE `data.rate_limits` du stdin. Il ne touche jamais aux jetons
//   OAuth du profil utilisateur ni au contenu des conversations.

import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

// ---------------------------------------------------------------------------
// Constantes de chemins
// ---------------------------------------------------------------------------

// Dossier de CE script, resolu independamment du cwd (mono-fichier ESM).
const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url));

// Chemin ABSOLU du pont, avec slashes AVANT (imperatif Git Bash / coherence Windows).
const BRIDGE_PATH = path.join(SCRIPT_DIR, "chronos-statusline-bridge.js").replace(/\\/g, "/");

// Fichier de settings de Claude Code (profil utilisateur, HORS repo).
const SETTINGS_PATH = path.join(os.homedir(), ".claude", "settings.json");
const BACKUP_PATH = SETTINGS_PATH + ".chronos.bak";

// Marqueur qui identifie le wrapper Chronos dans une commande statusLine deja pontee.
const BRIDGE_MARKER = "chronos-statusline-bridge";

// Commande statusLine finale a ecrire.
const WRAPPER_COMMAND = `node "${BRIDGE_PATH}"`;

// ---------------------------------------------------------------------------
// Utilitaires
// ---------------------------------------------------------------------------

/** Lit le pont pour extraire la commande enfant qu'il RE-EXECUTE (CHILD_STATUSLINE). */
function readBridgeChildTarget() {
  try {
    const src = fs.readFileSync(path.join(SCRIPT_DIR, "chronos-statusline-bridge.js"), "utf8");
    const m = src.match(/CHILD_STATUSLINE\s*=\s*["'`]([^"'`]+)["'`]/);
    return m ? m[1] : null;
  } catch {
    return null;
  }
}

/** Ecriture ATOMIQUE (temp + rename) pour ne jamais laisser un settings.json a moitie ecrit. */
function writeSettingsAtomic(obj) {
  const dir = path.dirname(SETTINGS_PATH);
  fs.mkdirSync(dir, { recursive: true });
  const tmp = path.join(dir, `settings.json.tmp-${process.pid}`);
  fs.writeFileSync(tmp, JSON.stringify(obj, null, 2) + "\n");
  fs.renameSync(tmp, SETTINGS_PATH);
}

/** Normalise un chemin (slashes avant, minuscule) pour comparer deux commandes statusLine. */
function normalize(s) {
  return String(s || "").replace(/\\/g, "/").toLowerCase();
}

// ---------------------------------------------------------------------------
// Lecture / sauvegarde
// ---------------------------------------------------------------------------

/** Charge settings.json de facon tolerante. Renvoie { settings, existed }. ABANDON si illisible. */
function loadSettings() {
  if (!fs.existsSync(SETTINGS_PATH)) {
    return { settings: {}, existed: false };
  }
  let raw;
  try {
    raw = fs.readFileSync(SETTINGS_PATH, "utf8");
  } catch (e) {
    console.error(`[chronos] ERREUR : impossible de lire ${SETTINGS_PATH} (${e.message}).`);
    console.error("[chronos] ABANDON : rien n'a ete modifie.");
    process.exit(1);
  }
  try {
    return { settings: JSON.parse(raw), existed: true };
  } catch (e) {
    console.error(`[chronos] ERREUR : ${SETTINGS_PATH} est un JSON illisible (${e.message}).`);
    console.error("[chronos] ABANDON : aucune ecriture, aucune sauvegarde. Corrige le fichier a la main.");
    process.exit(1);
  }
}

/** Sauvegarde prealable de settings.json. Ne JAMAIS ecraser un backup existant. */
function backupSettings() {
  if (!fs.existsSync(SETTINGS_PATH)) return; // rien a sauvegarder (settings absent).
  let target = BACKUP_PATH;
  if (fs.existsSync(BACKUP_PATH)) {
    // Un backup existe deja -> suffixer un timestamp pour ne pas l'ecraser.
    const stamp = new Date().toISOString().replace(/[:.]/g, "-");
    target = `${BACKUP_PATH}.${stamp}`;
  }
  fs.copyFileSync(SETTINGS_PATH, target);
  console.log(`[chronos] Sauvegarde : ${target}`);
}

// ---------------------------------------------------------------------------
// Commandes
// ---------------------------------------------------------------------------

function install(force) {
  const { settings } = loadSettings();
  const current = settings.statusLine && settings.statusLine.command;

  // Cas 1 : deja ponte -> NO-OP (idempotent).
  if (current && normalize(current).includes(BRIDGE_MARKER)) {
    console.log("[chronos] deja installe : la statusLine pointe deja sur le pont Chronos. Rien a faire (idempotent).");
    process.exit(0);
  }

  // Cas 2 : une autre commande statusLine existe -> le pont doit la RE-EXECUTER (chainage).
  //   Le pont a son enfant code EN DUR (CHILD_STATUSLINE, plan 03-02). Si la commande
  //   existante ne correspond pas a cet enfant, l'ecraser aveuglement PERDRAIT la barre
  //   existante -> on avertit et on exige --force (ou une edition manuelle du pont).
  if (current) {
    const child = readBridgeChildTarget();
    const chained = child && normalize(current).includes(normalize(child));
    if (!chained && !force) {
      console.error("[chronos] AVERTISSEMENT : une commande statusLine differente est deja configuree :");
      console.error(`[chronos]   existante : ${current}`);
      console.error(`[chronos]   le pont re-execute (en dur) : ${child || "<inconnu>"}`);
      console.error("[chronos] Le pont NE re-executerait PAS ta commande actuelle -> tu perdrais ta statusLine.");
      console.error("[chronos] Solutions :");
      console.error("[chronos]   1) Edite CHILD_STATUSLINE dans scripts/chronos-statusline-bridge.js pour pointer sur ta commande,");
      console.error("[chronos]      puis relance l'installeur ; OU");
      console.error("[chronos]   2) Relance avec --force si tu acceptes que seule la commande du pont soit chainee.");
      console.error("[chronos] ABANDON : aucune modification effectuee.");
      process.exit(2);
    }
    if (chained) {
      console.log(`[chronos] Commande existante chainee par le pont : ${current}`);
    } else {
      console.log(`[chronos] --force : remplacement de la commande existante (${current}) par le seul pont.`);
    }
  } else {
    // Cas 3 : aucune commande statusLine -> installer le pont seul (sans chainage prealable).
    console.log("[chronos] Aucune statusLine existante : installation du pont seul (pas de chainage).");
  }

  // SAUVEGARDE avant toute ecriture.
  backupSettings();

  // Ecrire UNIQUEMENT la cle statusLine ; preserver toutes les autres cles.
  settings.statusLine = { type: "command", command: WRAPPER_COMMAND };
  writeSettingsAtomic(settings);

  console.log(`[chronos] installe : statusLine.command = ${WRAPPER_COMMAND}`);
  console.log("[chronos] Ouvre (ou continue) une session Claude Code : usage.json se remplira apres la 1re reponse.");
  process.exit(0);
}

function uninstall() {
  const { settings, existed } = loadSettings();
  if (!existed) {
    console.log("[chronos] Aucun settings.json : rien a desinstaller.");
    process.exit(0);
  }

  const current = settings.statusLine && settings.statusLine.command;
  if (!current || !normalize(current).includes(BRIDGE_MARKER)) {
    console.log("[chronos] La statusLine ne pointe pas sur le pont Chronos : rien a desinstaller.");
    process.exit(0);
  }

  // Restaurer la statusLine depuis le backup si disponible (source de verite d'origine).
  if (fs.existsSync(BACKUP_PATH)) {
    try {
      const backup = JSON.parse(fs.readFileSync(BACKUP_PATH, "utf8"));
      if (backup.statusLine) {
        settings.statusLine = backup.statusLine;
        console.log(`[chronos] statusLine restauree depuis le backup : ${backup.statusLine.command}`);
      } else {
        delete settings.statusLine;
        console.log("[chronos] Le backup n'avait pas de statusLine : cle supprimee.");
      }
      writeSettingsAtomic(settings);
      console.log("[chronos] desinstalle.");
      process.exit(0);
    } catch (e) {
      console.error(`[chronos] Backup illisible (${e.message}). Repli : restauration de la commande enfant chainee.`);
    }
  }

  // Repli : pas de backup exploitable -> restaurer la commande enfant que le pont chainait.
  const child = readBridgeChildTarget();
  if (child) {
    settings.statusLine = { type: "command", command: `node "${child}"` };
    console.log(`[chronos] statusLine restauree sur la commande enfant du pont : node "${child}"`);
  } else {
    delete settings.statusLine;
    console.log("[chronos] Aucune commande enfant identifiable : cle statusLine supprimee.");
  }
  writeSettingsAtomic(settings);
  console.log("[chronos] desinstalle (repli).");
  process.exit(0);
}

// ---------------------------------------------------------------------------
// Point d'entree
// ---------------------------------------------------------------------------

const args = process.argv.slice(2);
if (args.includes("--uninstall")) {
  uninstall();
} else {
  install(args.includes("--force"));
}
