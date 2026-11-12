import 'dart:math';

import 'package:material_ui/material_ui.dart';

import '../../game/cards.dart';
import '../../game/view.dart';
import '../../session/session.dart';
import '../theme.dart';
import '../widgets/revolver.dart';
import '../widgets/turn_countdown.dart';

/// Moment de vérité : le perdant appuie sur la détente.
class RouletteOverlay extends StatefulWidget {
  final GameSession session;
  final GameView view;

  /// En 3D, la scène montre le revolver : on garde seulement textes et bouton.
  final bool over3d;
  const RouletteOverlay({
    super.key,
    required this.session,
    required this.view,
    this.over3d = false,
  });

  @override
  State<RouletteOverlay> createState() => _RouletteOverlayState();
}

class _RouletteOverlayState extends State<RouletteOverlay>
    with TickerProviderStateMixin {
  late final AnimationController _spin = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 950),
  );
  late final AnimationController _flash = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 700),
  );
  bool _resultShown = false;
  bool _pressed = false;

  @override
  void initState() {
    super.initState();
    // Arrivée en cours de route (ex. reconnexion) : on montre le résultat.
    if (widget.view.shotDied != null) _resultShown = true;
    _spin.addStatusListener((s) {
      if (s == AnimationStatus.completed && mounted) {
        setState(() => _resultShown = true);
        if (widget.view.shotDied == true) _flash.forward(from: 0);
      }
    });
  }

  @override
  void didUpdateWidget(RouletteOverlay old) {
    super.didUpdateWidget(old);
    if (old.view.shotDied == null && widget.view.shotDied != null) {
      _resultShown = false;
      _spin.forward(from: 0);
    }
    if (old.view.shooterId != widget.view.shooterId ||
        old.view.round != widget.view.round) {
      _pressed = false;
      _resultShown = widget.view.shotDied != null;
    }
  }

  @override
  void dispose() {
    _spin.dispose();
    _flash.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final v = widget.view;
    final shooter = v.player(v.shooterId);
    final isMe = v.shooterId == v.myId;
    final died = v.shotDied;
    // Pendant l'animation on affiche encore l'état « avant le tir ».
    final shotsShown = (shooter?.shots ?? 0) - (died != null && !_resultShown ? 1 : 0);
    final name = isMe ? 'Tu' : shooter?.name ?? '?';

    String title;
    Color titleColor = Palette.cream;
    if (died == null || !_resultShown) {
      title = isMe ? 'À toi de tirer…' : '$name prend le revolver…';
    } else if (died) {
      title = 'PAN !';
      titleColor = Palette.blood;
    } else {
      title = 'clic…';
      titleColor = Palette.truth;
    }

    String subtitle;
    if (died == null || !_resultShown) {
      final risk = (v.settings.risk(shotsShown < 0 ? 0 : shotsShown) * 100).round();
      subtitle = v.settings.respin
          ? 'Barillet re-tourné — $risk % de risque'
          : 'Chambre ${shotsShown + 1} sur $revolverChambers — $risk % de risque';
    } else if (died) {
      subtitle = isMe ? 'Tu es éliminé.' : '${shooter?.name} est éliminé.';
    } else {
      subtitle = isMe ? 'Tu survis… pour l\'instant.' : '${shooter?.name} survit.';
    }

    final titleWidget = AnimatedSwitcher(
      duration: const Duration(milliseconds: 200),
      transitionBuilder: (c, a) => ScaleTransition(scale: a, child: c),
      child: Text(
        title,
        key: ValueKey(title),
        textAlign: TextAlign.center,
        style: TextStyle(
          fontSize: title == 'PAN !' ? 64 : 26,
          fontWeight: FontWeight.w900,
          color: titleColor,
          letterSpacing: title == 'PAN !' ? 6 : 0.5,
          shadows: const [Shadow(color: Colors.black, blurRadius: 12)],
        ),
      ),
    );
    final shootButton = v.iMustShoot
        ? SizedBox(
            width: double.infinity,
            child: FilledButton(
              style: FilledButton.styleFrom(
                backgroundColor: Palette.blood,
                foregroundColor: Colors.white,
                minimumSize: const Size(0, 64),
              ),
              onPressed: _pressed
                  ? null
                  : () {
                      setState(() => _pressed = true);
                      widget.session.shoot();
                    },
              child: const Text('APPUYER SUR LA DÉTENTE', style: TextStyle(fontSize: 18)),
            ),
          )
        : const SizedBox(height: 64);
    final flashLayer = IgnorePointer(
      child: AnimatedBuilder(
        animation: _flash,
        builder: (context, _) => Container(
          color: Palette.blood.withAlpha(
              ((1 - _flash.value) * 180).round() * (_flash.isAnimating ? 1 : 0)),
        ),
      ),
    );

    if (widget.over3d) {
      return Stack(
        children: [
          SafeArea(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(20, 70, 20, 20),
              child: Column(
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                    decoration: BoxDecoration(
                      color: Colors.black54,
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: Column(
                      children: [
                        titleWidget,
                        const SizedBox(height: 4),
                        Text(subtitle,
                            textAlign: TextAlign.center,
                            style: const TextStyle(color: Palette.cream, fontSize: 14)),
                      ],
                    ),
                  ),
                  const SizedBox(height: 8),
                  TurnCountdown(view: v),
                  const Spacer(),
                  shootButton,
                ],
              ),
            ),
          ),
          flashLayer,
        ],
      );
    }

    return Stack(
      children: [
        Positioned.fill(
          child: Container(color: Colors.black.withAlpha(238)),
        ),
        Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                titleWidget,
                const SizedBox(height: 28),
                AnimatedBuilder(
                  animation: _spin,
                  builder: (context, _) {
                    final t = Curves.easeOutCubic.transform(_spin.value);
                    return CustomPaint(
                      size: const Size.square(200),
                      painter: CylinderPainter(
                        shots: shotsShown < 0 ? 0 : shotsShown,
                        rotation: t * 4 * pi,
                        bang: _resultShown && died == true,
                      ),
                    );
                  },
                ),
                const SizedBox(height: 24),
                Text(subtitle,
                    textAlign: TextAlign.center,
                    style: const TextStyle(color: Palette.muted, fontSize: 15)),
                const SizedBox(height: 12),
                TurnCountdown(view: v),
                const SizedBox(height: 16),
                shootButton,
              ],
            ),
          ),
        ),
        flashLayer,
      ],
    );
  }
}

class GameOverOverlay extends StatelessWidget {
  final GameView view;
  final VoidCallback? onReplay;
  final VoidCallback onLeave;

  const GameOverOverlay({
    super.key,
    required this.view,
    required this.onReplay,
    required this.onLeave,
  });

  @override
  Widget build(BuildContext context) {
    final won = view.winnerId == view.myId;
    return Container(
      color: Colors.black.withAlpha(220),
      padding: const EdgeInsets.all(28),
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Icon(won ? Icons.emoji_events : Icons.sentiment_very_dissatisfied,
                size: 90, color: Palette.gold),
            const SizedBox(height: 16),
            Text(
              won ? 'VICTOIRE !' : '${view.nameOf(view.winnerId)} gagne',
              textAlign: TextAlign.center,
              style: const TextStyle(
                  fontSize: 36, fontWeight: FontWeight.w900, color: Palette.gold),
            ),
            const SizedBox(height: 8),
            Text(
              won ? 'Dernier survivant de la table.' : 'Le barillet a parlé.',
              textAlign: TextAlign.center,
              style: const TextStyle(color: Palette.muted, fontSize: 16),
            ),
            const SizedBox(height: 36),
            if (onReplay != null)
              FilledButton(onPressed: onReplay, child: const Text('NOUVELLE PARTIE'))
            else
              const Text('En attente de l\'hôte pour une nouvelle partie…',
                  textAlign: TextAlign.center, style: TextStyle(color: Palette.muted)),
            const SizedBox(height: 12),
            OutlinedButton(onPressed: onLeave, child: const Text('Quitter la table')),
          ],
        ),
      ),
    );
  }
}
