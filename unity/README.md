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
2. Récupère le dépôt sur ton PC : sur GitHub, **Code → Download ZIP** puis dézippe
   (ou `git clone`). N'utilise pas « Ajouter un projet depuis un dépôt » dans Unity Hub : il ne
   cherche le projet qu'à la racine du dépôt, alors qu'il est dans le sous-dossier `unity/`.
3. Dans Unity Hub : **Ajouter → Ajouter un projet depuis le disque**, et choisis le dossier
   **`unity`** (celui qui contient `Assets`, `Packages` et `ProjectSettings`), pas la racine du
   dépôt. Si Hub signale que la version d'éditeur du projet (2021.3) n'est pas installée,
   choisis la tienne : Unity met le projet à niveau tout seul.

   **Si Hub répond « Veuillez ajouter un projet valide »** (certaines versions récentes de Hub
   sont plus exigeantes) : crée un projet neuf depuis Hub (**Nouveau projet → 3D**, Built-In ou
   Universal), ferme Unity, puis copie les dossiers `Assets/Scripts`, `Assets/Editor` et
   `Assets/Resources` de ce dépôt dans le dossier `Assets` du nouveau projet, et rouvre-le.
4. À la première ouverture, le projet se configure tout seul :
   - il crée la scène `Assets/Scenes/Main.unity` et règle le mode portrait ;
   - il propose de **télécharger les personnages 3D** (pack KayKit Adventurers, CC0, ~100 Mo).
     Accepte : sans eux le jeu marche, mais avec des silhouettes simplifiées.
5. Appuie sur **Play ▶**. Tout est créé en code au lancement : il n'y a aucun objet à placer
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
  (`RuntimeInitializeOnLoadMethod`) et construit la taverne avec les accessoires KayKit
  (`Props.cs` les met à l'échelle et les oriente automatiquement), vue depuis ta place au bout
  de la table.
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

- Personnages : *KayKit Adventurers* et *KayKit Skeletons* par Kay Lousberg (kaylousberg.com),
  licence CC0 (téléchargés par le menu « Menteur »).
- Décor de la taverne : *KayKit Dungeon Remastered* (même auteur, CC0), dans `Assets/Resources/Props`.
- Police des titres : *Rye* par Sorkin Type, licence SIL Open Font License (`Assets/Resources/Fonts/OFL-Rye.txt`).
- Images des cartes : `web3d/tools/render-cards.mjs` ; sons : `tool/make_sounds.py`.
