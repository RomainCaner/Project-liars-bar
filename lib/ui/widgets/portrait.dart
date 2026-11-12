import 'package:material_ui/material_ui.dart';

import '../../game/cards.dart';
import '../theme.dart';

/// Avatar d'un personnage (images générées depuis les modèles 3D).
class Portrait extends StatelessWidget {
  final int skin;
  final double size;
  final bool selected;
  final bool dimmed;

  const Portrait({
    super.key,
    required this.skin,
    this.size = 48,
    this.selected = false,
    this.dimmed = false,
  });

  @override
  Widget build(BuildContext context) {
    return AnimatedContainer(
      duration: const Duration(milliseconds: 150),
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: const Color(0xFF3A2A1E),
        border: Border.all(
          color: selected ? Palette.gold : Colors.white24,
          width: selected ? 3 : 1,
        ),
        boxShadow: selected ? [BoxShadow(color: Palette.gold.withAlpha(100), blurRadius: 10)] : null,
      ),
      clipBehavior: Clip.antiAlias,
      child: Opacity(
        opacity: dimmed ? 0.4 : 1,
        child: Image.asset(
          'assets/portraits/${skin % skinCount}.png',
          fit: BoxFit.cover,
          errorBuilder: (_, _, _) => const Icon(Icons.person, color: Palette.muted),
        ),
      ),
    );
  }
}

/// Choix du personnage dans le salon.
class SkinPicker extends StatelessWidget {
  final int selected;
  final ValueChanged<int> onSelect;
  const SkinPicker({super.key, required this.selected, required this.onSelect});

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 96,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: skinCount,
        separatorBuilder: (_, _) => const SizedBox(width: 10),
        itemBuilder: (context, i) => GestureDetector(
          onTap: () => onSelect(i),
          child: Column(
            children: [
              Portrait(skin: i, size: 66, selected: i == selected),
              const SizedBox(height: 4),
              Text(skinNames[i],
                  style: TextStyle(
                    fontSize: 11,
                    color: i == selected ? Palette.gold : Palette.muted,
                    fontWeight: i == selected ? FontWeight.w800 : FontWeight.w500,
                  )),
            ],
          ),
        ),
      ),
    );
  }
}
