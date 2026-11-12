import 'package:material_ui/material_ui.dart';

import '../../game/view.dart';
import '../theme.dart';
import 'revolver.dart';

class PlayerBadge extends StatelessWidget {
  final PlayerView player;
  final bool isCurrent;
  final bool isMe;
  final double width;

  const PlayerBadge({
    super.key,
    required this.player,
    this.isCurrent = false,
    this.isMe = false,
    this.width = 96,
  });

  @override
  Widget build(BuildContext context) {
    final p = player;
    final dead = !p.alive;
    final color = dead
        ? Colors.white24
        : isCurrent
            ? Palette.gold
            : Colors.white38;
    return AnimatedOpacity(
      duration: const Duration(milliseconds: 300),
      opacity: dead ? 0.45 : 1,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 250),
        width: width,
        padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 6),
        decoration: BoxDecoration(
          color: Colors.black.withAlpha(isCurrent ? 110 : 70),
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: color, width: isCurrent ? 2.2 : 1),
          boxShadow: isCurrent
              ? [BoxShadow(color: Palette.gold.withAlpha(90), blurRadius: 14)]
              : null,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Stack(
              clipBehavior: Clip.none,
              children: [
                CircleAvatar(
                  radius: 18,
                  backgroundColor: _avatarColor(p.name),
                  child: dead
                      ? const Icon(Icons.close, color: Colors.white70)
                      : Text(
                          p.name.characters.first.toUpperCase(),
                          style: const TextStyle(
                              fontWeight: FontWeight.w900, color: Colors.white),
                        ),
                ),
                if (p.isBot || !p.connected)
                  Positioned(
                    right: -6,
                    bottom: -4,
                    child: Icon(
                      p.isBot ? Icons.smart_toy : Icons.wifi_off,
                      size: 15,
                      color: p.isBot ? Palette.muted : Palette.blood,
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 4),
            Text(
              isMe ? '${p.name} (toi)' : p.name,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: 12.5,
                color: isCurrent ? Palette.gold : Palette.cream,
              ),
            ),
            const SizedBox(height: 3),
            if (dead)
              const Text('Éliminé',
                  style: TextStyle(fontSize: 11, color: Palette.blood))
            else
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.style, size: 13, color: Palette.muted),
                  const SizedBox(width: 2),
                  Text('${p.cardCount}',
                      style: const TextStyle(fontSize: 12, color: Palette.muted)),
                ],
              ),
            const SizedBox(height: 4),
            ShotDots(shots: p.shots, size: 6, dead: dead),
          ],
        ),
      ),
    );
  }

  static Color _avatarColor(String name) {
    const colors = [
      Color(0xFF8E3B2F), Color(0xFF2F5D8E), Color(0xFF6B8E2F), Color(0xFF8E6B2F),
      Color(0xFF5E2F8E), Color(0xFF2F8E7A), Color(0xFF8E2F63), Color(0xFF4A4A4A),
    ];
    return colors[name.hashCode.abs() % colors.length];
  }
}
