import 'package:material_ui/material_ui.dart';

import '../../game/cards.dart';
import '../../game/view.dart';
import '../theme.dart';
import '../prefs.dart';
import '../sound.dart';
import 'playing_card.dart';
import 'turn_countdown.dart';

/// Bandeau du haut : quitter, numéro de manche, carte du tour.
class TableHeader extends StatelessWidget {
  final GameView view;
  final VoidCallback onLeave;
  const TableHeader({super.key, required this.view, required this.onLeave});

  @override
  Widget build(BuildContext context) {
    final v = view;
    final rank = v.tableRank;
    return Padding(
      padding: const EdgeInsets.fromLTRB(4, 4, 12, 0),
      child: Row(
        children: [
          IconButton(onPressed: onLeave, icon: const Icon(Icons.close)),
          Text('Manche ${v.round}',
              style: const TextStyle(color: Palette.muted, fontWeight: FontWeight.w600)),
          const SizedBox(width: 6),
          TurnCountdown(view: v),
          const Spacer(),
          const SoundToggle(),
          if (rank != null)
            Container(
              padding: const EdgeInsets.fromLTRB(12, 6, 6, 6),
              decoration: BoxDecoration(
                color: Colors.black45,
                borderRadius: BorderRadius.circular(30),
                border: Border.all(color: Palette.gold),
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      const Text('CARTE DU TOUR',
                          style: TextStyle(
                              fontSize: 9, color: Palette.muted, letterSpacing: 1.2)),
                      Text(rank.plural.toUpperCase(),
                          style: const TextStyle(
                              fontSize: 18,
                              fontWeight: FontWeight.w900,
                              color: Palette.gold,
                              letterSpacing: 1)),
                    ],
                  ),
                  const SizedBox(width: 8),
                  PlayingCardView(rank: rank, width: 30),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// Bouton pour couper / remettre le son.
class SoundToggle extends StatefulWidget {
  const SoundToggle({super.key});

  @override
  State<SoundToggle> createState() => _SoundToggleState();
}

class _SoundToggleState extends State<SoundToggle> {
  @override
  Widget build(BuildContext context) {
    return IconButton(
      tooltip: soundOn ? 'Couper le son' : 'Activer le son',
      icon: Icon(soundOn ? Icons.volume_up : Icons.volume_off, color: Palette.muted),
      onPressed: () {
        setState(() => soundOn = !soundOn);
        Sound.setOn(soundOn);
        saveSettings();
      },
    );
  }
}
