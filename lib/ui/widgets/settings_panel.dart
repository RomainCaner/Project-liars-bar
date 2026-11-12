import 'package:material_ui/material_ui.dart';

import '../../game/engine.dart';
import '../theme.dart';

/// Réglages de la partie : modifiables par l'hôte, en lecture seule sinon.
class SettingsPanel extends StatelessWidget {
  final GameSettings settings;
  final ValueChanged<GameSettings>? onChanged;
  const SettingsPanel({super.key, required this.settings, this.onChanged});

  @override
  Widget build(BuildContext context) {
    final s = settings;
    final edit = onChanged;
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.black26,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _Row(
            label: 'Jokers',
            hint: 'Cartes qui valent n\'importe quelle carte du tour',
            options: const {0: 'Aucun', 2: '2', 4: '4'},
            value: s.jokers,
            onSelect: edit == null ? null : (v) => edit(s.copyWith(jokers: v)),
          ),
          _Row(
            label: 'Balles',
            hint: 'Dans chaque barillet de 6 chambres',
            options: const {1: '1', 2: '2 (hardcore)'},
            value: s.bullets,
            onSelect: edit == null ? null : (v) => edit(s.copyWith(bullets: v)),
          ),
          _Row(
            label: 'Barillet',
            hint: s.respin
                ? 'Re-tourné avant chaque tir : risque constant'
                : 'Classique : le risque augmente à chaque tir',
            options: const {0: 'Classique', 1: 'Re-tourné'},
            value: s.respin ? 1 : 0,
            onSelect: edit == null ? null : (v) => edit(s.copyWith(respin: v == 1)),
          ),
          _Row(
            label: 'Minuteur',
            hint: 'Temps pour jouer ou tirer ; ensuite l\'IA joue à ta place',
            options: const {0: 'Aucun', 20: '20 s', 30: '30 s', 45: '45 s'},
            value: s.turnSeconds,
            onSelect: edit == null ? null : (v) => edit(s.copyWith(turnSeconds: v)),
          ),
          if (edit == null)
            const Padding(
              padding: EdgeInsets.only(top: 4),
              child: Text('Seul l\'hôte peut modifier les règles.',
                  style: TextStyle(color: Colors.white38, fontSize: 12)),
            ),
        ],
      ),
    );
  }
}

class _Row extends StatelessWidget {
  final String label;
  final String hint;
  final Map<int, String> options;
  final int value;
  final ValueChanged<int>? onSelect;

  const _Row({
    required this.label,
    required this.hint,
    required this.options,
    required this.value,
    required this.onSelect,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(fontWeight: FontWeight.w700)),
          const SizedBox(height: 4),
          Wrap(
            spacing: 6,
            runSpacing: 6,
            children: [
              for (final e in options.entries)
                ChoiceChip(
                  label: Text(e.value),
                  selected: e.key == value,
                  showCheckmark: false,
                  selectedColor: Palette.gold,
                  labelStyle: TextStyle(
                    color: e.key == value ? Palette.ink : Palette.cream,
                    fontWeight: FontWeight.w700,
                  ),
                  onSelected: onSelect == null ? null : (_) => onSelect!(e.key),
                ),
            ],
          ),
          const SizedBox(height: 2),
          Text(hint, style: const TextStyle(color: Palette.muted, fontSize: 11.5)),
        ],
      ),
    );
  }
}
