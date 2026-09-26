# Phase 31 — Éléments hors périmètre relevés en cours d'exécution

Relevés, non corrigés : ils sortent du périmètre du plan qui les a vus.

## Relevé par 31-01 (2026-09-26)

1. **Commentaire de `src/Chronos/Services/RacinesEtat.cs`.** Le commentaire de la classe dit que les métadonnées de
   l'app bureau sont lues dans la première racine qui existe « (`PremiereExistante`) ». Le lecteur
   (`LecteurAppBureau.Lire`) ne l'appelle pas : il teste l'existence de ses candidats par sa propre méthode privée
   `PremiereRacine()`, de même logique. Le comportement est le même ; seule la référence du commentaire est inexacte.
   `docs/desktop-app-sessions.md` §1 décrit le comportement sans citer la méthode. 31-01 ne touche pas `src/`.
2. **README, section « Installation ».** La liste « Clic droit dessus pour le menu » (Arrière-plan, Recalibrer…,
   Calibrer les plafonds…, Lancer au démarrage, Usage exact (OAuth), Quitter) date d'avant la fenêtre de réglages
   (le bouton s'appelle aujourd'hui « Quitter Chronos »), et le paragraphe du token renvoie encore au « menu
   « Usage exact (OAuth) » ». 31-01 n'a changé du README que la section du widget et le compte de tests.
