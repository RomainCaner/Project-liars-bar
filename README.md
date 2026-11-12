# Menteur !

Jeu de cartes de bluff avec roulette russe, pour jouer entre amis **en réseau local**
(même Wi-Fi ou partage de connexion), sur Android. Sans serveur, sans compte, sans Internet.

<p>
  <img src="docs/screenshots/1_home.png" width="190">
  <img src="docs/screenshots/3_my_turn.png" width="190">
  <img src="docs/screenshots/4_reveal.png" width="190">
  <img src="docs/screenshots/5_roulette.png" width="190">
</p>

## Règles

- Paquet : 6 Rois, 6 Dames, 6 As, 2 Jokers (agrandi automatiquement à 5–6 joueurs).
- Chaque manche : 5 cartes par joueur et une **carte du tour** tirée au hasard (Roi, Dame ou As).
- À ton tour, tu poses 1 à 3 cartes face cachée en prétendant que ce sont des cartes du tour.
  Les Jokers comptent comme n'importe quelle carte. Tu peux mentir.
- Le joueur suivant peut jouer à son tour ou crier **MENTEUR !** : les cartes sont révélées,
  et le menteur (ou l'accusateur, s'il avait tort) appuie sur la détente.
- Chaque joueur a son revolver : 6 chambres, 1 balle placée au hasard. Le risque monte à chaque tir.
- Un joueur sans carte est sauté ; le dernier à avoir des cartes doit accuser.
- Dernier survivant = vainqueur.

## Jouer en classe

1. Un joueur appuie sur **Créer une partie** : l'écran du salon affiche son adresse IP.
2. Les autres appuient sur **Rejoindre** : la partie apparaît automatiquement
   (sinon, ils tapent l'adresse IP).
3. L'hôte peut compléter la table avec des bots (2 à 6 joueurs), puis lance la partie.

Si le Wi-Fi de l'école isole les appareils (fréquent), l'hôte active le **partage de connexion**
et les autres s'y connectent : le jeu ne consomme pas de données mobiles, tout passe en local.

Un joueur qui perd la connexion est remplacé par un bot et reprend sa place s'il revient.

## Récupérer l'APK

Chaque push déclenche la CI GitHub Actions (`.github/workflows/build-apk.yml`) :
analyse, tests, puis compilation de l'APK.

- **APK d'un commit** : onglet *Actions* → dernier run → artefact `menteur-apk`.
- **Version à partager** : pousser un tag, la CI crée une *Release* avec l'APK téléchargeable
  sans compte GitHub :
  ```bash
  git tag v1.0.0 && git push origin v1.0.0
  ```

Sur le téléphone, il faut autoriser l'installation d'applications de sources inconnues.
Tous les APK sont signés avec la même clé (`android/app/menteur-shared.keystore`, volontairement
commitée), donc une nouvelle version s'installe par-dessus l'ancienne. Pour une publication sur
le Play Store, il faudrait une clé privée.

## Développement

Prérequis : Flutter 3.50 (stable) ; le SDK Android pour compiler l'APK en local.

```bash
flutter pub get
flutter test                 # règles, 2000 parties de bots, partie réseau complète
flutter run                  # sur un téléphone branché en USB
flutter build apk --release  # APK dans build/app/outputs/flutter-apk/
```

Pour tester le multijoueur sur un seul PC : lancer deux émulateurs, ou un émulateur et un téléphone,
puis rejoindre par IP.

### Architecture

```
lib/
  game/      Moteur pur Dart, sans Flutter ni réseau : testable
    cards.dart     cartes, composition du paquet
    engine.dart    machine à états (salon → jeu → révélation → roulette → fin)
    view.dart      vue d'un joueur (mains adverses et balles masquées)
    bot.dart       IA, qui ne voit que ce qu'un humain verrait
  net/       Réseau local
    protocol.dart  messages JSON, adresses locales
    discovery.dart annonce UDP broadcast (port 4041) + écoute
    platform.dart  verrou multicast Android (MainActivity.kt)
  session/   Ce que l'UI consomme
    host_session.dart   moteur + bots + serveur WebSocket (port 4040)
    client_session.dart client WebSocket avec reconnexion automatique
  ui/        Écrans Flutter (accueil, rejoindre, salon, table, roulette, fin)
```

L'**hôte fait autorité** : lui seul fait tourner le moteur et envoie à chaque joueur sa propre vue.
Les clients ne reçoivent jamais les cartes des autres ni la position des balles, donc on ne peut pas
tricher en inspectant le trafic.

## Pistes pour la suite

- Sons (déclic, coup de feu, ambiance de bar), via le paquet `audioplayers`
- Minuteur de tour configurable (un bot joue si un joueur est absent)
- Variantes : nombre de jokers, re-rotation du barillet à chaque manche, mode « dés »
- Chat rapide / émotes pendant la partie
- Statistiques locales (victoires, bluffs réussis)
- Version iOS (le code est compatible, il faut un Mac pour compiler)
