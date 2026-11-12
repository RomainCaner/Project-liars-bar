# Menteur ! — version Unity (en cours)

Portage du jeu en Unity (C#), en parallèle de la version Flutter à la racine du dépôt.

État actuel :

- `Assets/Scripts/Core/` : règles, réglages, bots, vue par joueur, JSON (C# pur, sans Unity) ✅
- `Assets/Scripts/Net/` : réseau local TCP + découverte UDP (C# pur) ✅
- À venir : sessions hôte/invité, scène 3D, interface, personnages KayKit animés, sons,
  configuration automatique du projet (scène, Android).

Le code `Core` et `Net` compile avec .NET (C# 9, netstandard2.1), comme dans Unity 2021.3+ / Unity 6.
