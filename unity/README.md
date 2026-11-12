# Menteur ! — version Unity

Portage Unity (C#) du jeu, en parallèle de la version Flutter à la racine du dépôt.
Mêmes règles, bots, réseau local, personnages KayKit animés et sons, mais en **3D native** :
APK Android **et** version Windows depuis le même projet.

> ⚠️ Cette version a été écrite sans pouvoir lancer Unity. Le code compile contre les
> bibliothèques de référence d'Unity et la logique est testée (voir « Vérifications »), mais le
> rendu n'a jamais été vu : attends-toi à quelques réglages (positions, tailles, lumières).

## Ouvrir le projet

1. Installe **Unity Hub**, puis un éditeur **Unity 2021.3 LTS ou plus récent** (Unity 6 convient).
   Coche le module **Android Build Support** (avec *OpenJDK* et *Android SDK & NDK Tools*) si tu
   veux compiler l'APK.
2. Dans Unity Hub : **Add → Add project from disk**, choisis le dossier `unity/` de ce dépôt.
   Si Hub demande une version d'éditeur, prends celle que tu as installée.
3. À la première ouverture, le projet se configure tout seul :
   - il crée la scène `Assets/Scenes/Main.unity` et règle le mode portrait ;
   - il propose de **télécharger les personnages 3D** (pack KayKit Adventurers, CC0, ~100 Mo).
     Accepte : sans eux le jeu marche, mais avec des silhouettes simplifiées.
4. Appuie sur **Play ▶**. Tout est créé en code au lancement : il n'y a aucun objet à placer
   dans la scène.

Dans l'éditeur, règle la vue *Game* sur un format portrait (par ex. 1080×2400) pour voir le
jeu comme sur téléphone. La souris remplace le doigt.

## Menu « Menteur »

| Entrée | Effet |
| --- | --- |
| Télécharger les personnages 3D | Récupère les `.fbx` et textures dans `Assets/Resources/Characters` |
| Réimporter les personnages | À utiliser si les personnages restent figés (animations non importées) |
| Compiler l'APK Android | Produit `Builds/menteur.apk` |
| Compiler pour Windows | Produit `Builds/Windows/Menteur.exe` |

## Jouer à plusieurs

Comme la version Flutter : un joueur crée la partie, les autres la rejoignent sur le même Wi-Fi
(ou le partage de connexion de l'hôte). La version Unity utilise les ports **4050** (TCP) et
**4051** (UDP). Les versions Unity et Flutter **ne sont pas compatibles entre elles** : tout le
monde doit utiliser la même.

Si la partie n'apparaît pas automatiquement dans « Rejoindre », tape l'adresse IP affichée chez
l'hôte. Sur Windows, autorise le jeu dans le pare-feu quand il le demande.

## Organisation

```
Assets/
  Scripts/
    Core/      règles, réglages, bots, vue par joueur, JSON   (C# pur)
    Net/       TCP en lignes JSON + découverte UDP             (C# pur)
    Session/   hôte (moteur, bots, serveur, minuteur) / invité (C# pur)
    Game/      Unity : App (écrans IMGUI), TableScene (3D), CharacterRig (animations, IK), Sfx, Ui
  Editor/      configuration automatique, import des modèles, menu « Menteur »
  Resources/   sons, portraits (commités) ; personnages (téléchargés)
Checks~/       projets .NET de vérification, ignorés par Unity
```

Choix techniques, faits pour limiter les surprises sans éditeur sous la main :

- **Pas de scène ni de prefab à maintenir** : `App` se crée tout seul au lancement
  (`RuntimeInitializeOnLoadMethod`) et construit la salle, la table et le revolver avec des
  primitives.
- **Interface en IMGUI** (`OnGUI`) : incluse dans le moteur, identique sur toutes les versions
  d'Unity.
- **Animations « Legacy »** pilotées en code : pose assise en boucle, gestes du haut du corps
  par-dessus (`AddMixingTransform`), cinématique inverse pour le revolver à la tempe.

## Vérifications (sans Unity)

```bash
cd unity/Checks~/Logic  && dotnet run     # 12 tests : règles, 2000 parties de bots, partie réseau
cd unity/Checks~/Engine && dotnet build   # scripts du jeu contre UnityEngine (référence 2021.3)
cd unity/Checks~/Editor && dotnet build   # scripts d'éditeur contre UnityEditor
```

## Crédits

Personnages : *KayKit Adventurers* par Kay Lousberg (kaylousberg.com), licence CC0.
Sons synthétisés par `tool/make_sounds.py`.
