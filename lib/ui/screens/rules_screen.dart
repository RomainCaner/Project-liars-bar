import 'package:material_ui/material_ui.dart';

import '../theme.dart';

class RulesScreen extends StatelessWidget {
  const RulesScreen({super.key});

  static const _sections = [
    ('Le but', 'Être le dernier joueur en vie. Bluffe, démasque les menteurs '
        'et prie pour que le barillet soit clément.'),
    ('Le paquet', '6 Rois, 6 Dames, 6 As et 2 Jokers (un peu plus au-delà de '
        '4 joueurs). Chaque manche, tout le monde reçoit 5 cartes et une '
        '« carte du tour » est tirée au hasard : Roi, Dame ou As.'),
    ('À ton tour', 'Pose face cachée 1 à 3 cartes en annonçant qu\'elles sont '
        'toutes la carte du tour. Tu peux mentir ! Les Jokers comptent comme '
        'n\'importe quelle carte.'),
    ('MENTEUR !', 'Au lieu de jouer, tu peux accuser le joueur précédent. '
        'Ses cartes sont révélées : s\'il y a au moins une carte qui n\'est ni '
        'la carte du tour ni un Joker, il a menti et c\'est lui qui tire. '
        'Sinon, c\'est toi.'),
    ('Le barillet', 'Chaque joueur a son propre revolver : 6 chambres, '
        '1 seule balle placée au hasard. À chaque tir raté, le risque augmente '
        '(au 6e, c\'est certain). Un « PAN » et tu es éliminé.'),
    ('Mains vides', 'Un joueur sans carte est sauté. Si tu es le dernier à '
        'avoir des cartes, tu es obligé d\'accuser.'),
    ('Nouvelle manche', 'Après chaque tir, on redistribue les cartes, une '
        'nouvelle carte du tour est tirée et le perdant (s\'il survit) commence.'),
    ('Jouer en classe', 'Un joueur crée la partie, les autres la rejoignent '
        'depuis le même Wi-Fi. Si le Wi-Fi de l\'école bloque les connexions, '
        'l\'hôte active le partage de connexion et les autres s\'y connectent. '
        'Si la partie n\'apparaît pas, tape l\'adresse IP affichée chez l\'hôte.'),
    ('Crédits', 'Personnages 3D : KayKit Adventurers par Kay Lousberg '
        '(kaylousberg.com), licence CC0. Moteur 3D : three.js.'),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Règles')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
        children: [
          for (final (title, body) in _sections) ...[
            Text(title,
                style: const TextStyle(
                    color: Palette.gold, fontSize: 18, fontWeight: FontWeight.w800)),
            const SizedBox(height: 6),
            Text(body,
                style: const TextStyle(color: Palette.cream, fontSize: 15, height: 1.4)),
            const SizedBox(height: 20),
          ],
        ],
      ),
    );
  }
}
