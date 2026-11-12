import 'dart:math';

import 'package:material_ui/material_ui.dart';

import '../../game/cards.dart';
import '../theme.dart';

Color rankColor(Rank r) => switch (r) {
      Rank.king => const Color(0xFFB0302A),
      Rank.queen => const Color(0xFF6A3D9A),
      Rank.ace => const Color(0xFF1F3B5C),
      Rank.joker => const Color(0xFF2E7D4F),
    };

Widget rankEmblem(Rank r, Color color, double size) => switch (r) {
      Rank.king => CustomPaint(size: Size.square(size), painter: CrownPainter(color)),
      Rank.queen => Icon(Icons.local_florist, color: color, size: size),
      Rank.ace => Icon(Icons.star_rounded, color: color, size: size),
      Rank.joker => Icon(Icons.theater_comedy, color: color, size: size),
    };

class CrownPainter extends CustomPainter {
  final Color color;
  CrownPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width, h = size.height;
    final paint = Paint()..color = color;
    final path = Path()
      ..moveTo(w * 0.12, h * 0.78)
      ..lineTo(w * 0.06, h * 0.3)
      ..lineTo(w * 0.32, h * 0.52)
      ..lineTo(w * 0.5, h * 0.18)
      ..lineTo(w * 0.68, h * 0.52)
      ..lineTo(w * 0.94, h * 0.3)
      ..lineTo(w * 0.88, h * 0.78)
      ..close();
    canvas.drawPath(path, paint);
    canvas.drawRRect(
      RRect.fromLTRBR(w * 0.12, h * 0.82, w * 0.88, h * 0.92, Radius.circular(w * 0.03)),
      paint,
    );
    for (final x in [0.06, 0.5, 0.94]) {
      canvas.drawCircle(Offset(w * x, h * (x == 0.5 ? 0.15 : 0.27)), w * 0.06, paint);
    }
  }

  @override
  bool shouldRepaint(CrownPainter old) => old.color != color;
}

class PlayingCardView extends StatelessWidget {
  final Rank? rank; // null = dos de carte
  final double width;
  final bool selected;
  final bool highlight;

  const PlayingCardView({
    super.key,
    required this.rank,
    this.width = 64,
    this.selected = false,
    this.highlight = false,
  });

  double get height => width * 1.45;

  @override
  Widget build(BuildContext context) {
    final r = rank;
    return AnimatedContainer(
      duration: const Duration(milliseconds: 160),
      width: width,
      height: height,
      decoration: BoxDecoration(
        color: r == null ? null : Palette.cream,
        gradient: r == null
            ? const LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [Color(0xFF7A1F1A), Color(0xFF3E0E0B)],
              )
            : null,
        borderRadius: BorderRadius.circular(width * 0.12),
        border: Border.all(
          color: selected
              ? Palette.gold
              : highlight
                  ? Palette.truth
                  : Colors.black26,
          width: selected || highlight ? 3 : 1,
        ),
        boxShadow: [
          BoxShadow(
            color: selected ? Palette.gold.withAlpha(110) : Colors.black45,
            blurRadius: selected ? 14 : 6,
            offset: const Offset(0, 3),
          ),
        ],
      ),
      child: r == null ? _back() : _face(r),
    );
  }

  Widget _back() {
    return Padding(
      padding: EdgeInsets.all(width * 0.1),
      child: DecoratedBox(
        decoration: BoxDecoration(
          border: Border.all(color: Palette.gold.withAlpha(150), width: 1.2),
          borderRadius: BorderRadius.circular(width * 0.07),
        ),
        child: Center(
          child: Transform.rotate(
            angle: pi / 4,
            child: Icon(Icons.crop_square,
                color: Palette.gold.withAlpha(170), size: width * 0.42),
          ),
        ),
      ),
    );
  }

  Widget _face(Rank r) {
    final color = rankColor(r);
    final corner = TextStyle(
      color: color,
      fontWeight: FontWeight.w900,
      fontSize: width * 0.22,
      height: 1,
    );
    return Stack(
      children: [
        Positioned(left: width * 0.08, top: width * 0.07, child: Text(r.short, style: corner)),
        Positioned(
          right: width * 0.08,
          bottom: width * 0.07,
          child: Transform.rotate(angle: pi, child: Text(r.short, style: corner)),
        ),
        Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              rankEmblem(r, color, width * 0.46),
              Text(
                r.label.toUpperCase(),
                style: TextStyle(
                  color: color,
                  fontSize: width * 0.13,
                  fontWeight: FontWeight.w800,
                  letterSpacing: 0.5,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// Carte qui se retourne (dos → face) à l'apparition.
class FlippingCard extends StatelessWidget {
  final Rank rank;
  final double width;
  final Duration delay;
  final bool highlight;

  const FlippingCard({
    super.key,
    required this.rank,
    this.width = 64,
    this.delay = Duration.zero,
    this.highlight = false,
  });

  @override
  Widget build(BuildContext context) {
    final total = delay + const Duration(milliseconds: 450);
    final start = delay.inMilliseconds / total.inMilliseconds;
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: total,
      builder: (context, t, _) {
        final p = ((t - start) / (1 - start)).clamp(0.0, 1.0);
        final angle = pi * (1 - p);
        final showFace = angle < pi / 2;
        return Transform(
          alignment: Alignment.center,
          transform: Matrix4.identity()
            ..setEntry(3, 2, 0.002)
            ..rotateY(showFace ? angle : angle - pi),
          child: PlayingCardView(
            rank: showFace ? rank : null,
            width: width,
            highlight: showFace && highlight,
          ),
        );
      },
    );
  }
}
