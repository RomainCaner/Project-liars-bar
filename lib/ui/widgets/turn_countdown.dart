import 'dart:async';

import 'package:flutter/services.dart';
import 'package:material_ui/material_ui.dart';

import '../../game/view.dart';
import '../theme.dart';

/// Compte à rebours du minuteur de tour (rouge et vibrant à la fin).
class TurnCountdown extends StatefulWidget {
  final GameView view;
  const TurnCountdown({super.key, required this.view});

  @override
  State<TurnCountdown> createState() => _TurnCountdownState();
}

class _TurnCountdownState extends State<TurnCountdown> {
  Timer? _tick;
  int? _last;

  @override
  void initState() {
    super.initState();
    _tick = Timer.periodic(const Duration(milliseconds: 250), (_) {
      final s = widget.view.secondsLeft;
      if (s != _last) {
        // Petite vibration sur les 3 dernières secondes de mon tour.
        final mine = widget.view.isMyTurn || widget.view.iMustShoot;
        if (mine && s != null && s <= 3 && s > 0) HapticFeedback.selectionClick();
        setState(() => _last = s);
      }
    });
  }

  @override
  void dispose() {
    _tick?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final s = widget.view.secondsLeft;
    if (s == null) return const SizedBox.shrink();
    final urgent = s <= 5;
    return AnimatedContainer(
      duration: const Duration(milliseconds: 200),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: urgent ? Palette.blood : Colors.black45,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.timer, size: 14, color: Colors.white),
          const SizedBox(width: 3),
          Text('$s s',
              style: const TextStyle(
                  fontSize: 13, fontWeight: FontWeight.w800, color: Colors.white)),
        ],
      ),
    );
  }
}
