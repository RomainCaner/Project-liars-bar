import 'dart:math';

import 'package:material_ui/material_ui.dart';

import '../../game/cards.dart';
import '../theme.dart';

/// Barillet vu de face : les chambres déjà « tirées » sont vides.
class CylinderPainter extends CustomPainter {
  final int shots;
  final double rotation;
  final bool bang;

  CylinderPainter({required this.shots, this.rotation = 0, this.bang = false});

  @override
  void paint(Canvas canvas, Size size) {
    final c = size.center(Offset.zero);
    final r = size.shortestSide / 2;
    canvas.save();
    canvas.translate(c.dx, c.dy);
    canvas.rotate(rotation);

    final body = Paint()
      ..shader = const RadialGradient(
        colors: [Color(0xFF9EA3A8), Color(0xFF4A4F55), Color(0xFF26292D)],
        stops: [0, 0.7, 1],
      ).createShader(Rect.fromCircle(center: Offset.zero, radius: r));
    canvas.drawCircle(Offset.zero, r, body);

    // Encoches extérieures, découpées dans le cylindre.
    canvas.save();
    canvas.clipPath(Path()..addOval(Rect.fromCircle(center: Offset.zero, radius: r)));
    final groove = Paint()..color = const Color(0xFF1C1E21);
    for (var i = 0; i < revolverChambers; i++) {
      final a = (i + 0.5) * 2 * pi / revolverChambers - pi / 2;
      canvas.drawCircle(Offset(cos(a), sin(a)) * r * 1.02, r * 0.13, groove);
    }
    canvas.restore();

    for (var i = 0; i < revolverChambers; i++) {
      final a = i * 2 * pi / revolverChambers - pi / 2;
      final o = Offset(cos(a), sin(a)) * r * 0.55;
      final fired = i < shots;
      canvas.drawCircle(o, r * 0.24, Paint()..color = const Color(0xFF111214));
      if (!fired) {
        // Culot de cartouche (on ne sait pas laquelle est chargée).
        canvas.drawCircle(o, r * 0.19, Paint()..color = const Color(0xFFB08D57));
        canvas.drawCircle(o, r * 0.07, Paint()..color = const Color(0xFF7A5F35));
      }
      if (bang && i == shots - 1) {
        canvas.drawCircle(o, r * 0.24, Paint()..color = Palette.blood);
      }
    }
    canvas.drawCircle(Offset.zero, r * 0.14, Paint()..color = const Color(0xFF2E3236));
    canvas.restore();
  }

  @override
  bool shouldRepaint(CylinderPainter old) =>
      old.shots != shots || old.rotation != rotation || old.bang != bang;
}

/// Petit indicateur : 6 points, rouges pour les coups déjà tirés.
class ShotDots extends StatelessWidget {
  final int shots;
  final double size;
  final bool dead;
  const ShotDots({super.key, required this.shots, this.size = 7, this.dead = false});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (var i = 0; i < revolverChambers; i++)
          Container(
            margin: EdgeInsets.symmetric(horizontal: size * 0.15),
            width: size,
            height: size,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: dead
                  ? (i == shots - 1 ? Palette.blood : Colors.white12)
                  : i < shots
                      ? Palette.blood.withAlpha(200)
                      : Colors.white24,
            ),
          ),
      ],
    );
  }
}
