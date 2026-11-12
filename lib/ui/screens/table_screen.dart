import 'dart:async';
import 'dart:math';

import 'package:material_ui/material_ui.dart';
import 'package:flutter/services.dart';

import '../../game/engine.dart' show Phase;
import '../../game/view.dart';
import '../../session/host_session.dart';
import '../../session/session.dart';
import '../theme.dart';
import 'game_view.dart';
import 'lobby_view.dart';
import 'overlays.dart';

/// Écran unique d'une session : salon puis partie.
class TableScreen extends StatefulWidget {
  final GameSession session;
  const TableScreen({super.key, required this.session});

  @override
  State<TableScreen> createState() => _TableScreenState();
}

class _TableScreenState extends State<TableScreen>
    with SingleTickerProviderStateMixin {
  late final AnimationController _shake = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 600),
  );
  StreamSubscription<String>? _toastSub;
  GameView? _prev;

  GameSession get session => widget.session;

  @override
  void initState() {
    super.initState();
    session.addListener(_onSession);
    _toastSub = session.toasts.listen((msg) {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(msg), duration: const Duration(seconds: 2)));
    });
  }

  void _onSession() {
    final v = session.view;
    final p = _prev;
    if (v != null && p != null) {
      if (p.shotDied == null && v.shotDied != null) {
        // Le son et la vibration arrivent après la rotation du barillet.
        Future.delayed(const Duration(milliseconds: 950), () {
          if (!mounted) return;
          if (v.shotDied == true) {
            HapticFeedback.heavyImpact();
            Future.delayed(const Duration(milliseconds: 120), HapticFeedback.heavyImpact);
            _shake.forward(from: 0);
          } else {
            HapticFeedback.lightImpact();
          }
        });
      }
      if (!p.isMyTurn && v.isMyTurn) HapticFeedback.mediumImpact();
      if (p.phase == Phase.playing && v.phase == Phase.reveal) {
        HapticFeedback.mediumImpact();
      }
    }
    _prev = v;
    setState(() {});
  }

  @override
  void dispose() {
    session.removeListener(_onSession);
    _toastSub?.cancel();
    _shake.dispose();
    session.close();
    session.dispose();
    super.dispose();
  }

  Future<bool> _confirmLeave() async {
    if (session.status == ConnectionStatus.closed) return true;
    final res = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Quitter la table ?'),
        content: Text(session.isHost
            ? 'La partie sera fermée pour tout le monde.'
            : 'Un bot jouera à ta place.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Rester')),
          TextButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('Quitter')),
        ],
      ),
    );
    return res ?? false;
  }

  Future<void> _leave() async {
    if (await _confirmLeave() && mounted) Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    final v = session.view;
    Widget body;
    if (session.status == ConnectionStatus.closed && !session.isHost) {
      body = _Message(
        icon: Icons.link_off,
        text: session.closedReason ?? 'Connexion perdue',
        action: FilledButton(
            onPressed: () => Navigator.of(context).pop(), child: const Text('RETOUR')),
      );
    } else if (v == null) {
      body = const _Message(icon: Icons.hourglass_top, text: 'Connexion à la table…');
    } else if (v.phase == Phase.lobby) {
      body = LobbyView(session: session, view: v, onLeave: _leave);
    } else {
      body = Stack(
        children: [
          GameTableView(session: session, view: v, onLeave: _leave),
          if (v.phase == Phase.roulette)
            Positioned.fill(child: RouletteOverlay(session: session, view: v)),
          if (v.phase == Phase.gameOver)
            Positioned.fill(
              child: GameOverOverlay(
                view: v,
                onReplay: session is HostSession
                    ? () => (session as HostSession).backToLobby()
                    : null,
                onLeave: () => Navigator.of(context).pop(),
              ),
            ),
        ],
      );
    }

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _leave();
      },
      child: Scaffold(
        body: AnimatedBuilder(
          animation: _shake,
          builder: (context, child) {
            final t = _shake.value;
            final dx = sin(t * pi * 10) * 14 * (1 - t);
            return Transform.translate(offset: Offset(dx, 0), child: child);
          },
          child: FeltBackground(
            child: SafeArea(
              child: Column(
                children: [
                  if (session.status == ConnectionStatus.reconnecting)
                    Container(
                      width: double.infinity,
                      color: Palette.blood,
                      padding: const EdgeInsets.all(6),
                      child: const Text('Connexion perdue — reconnexion…',
                          textAlign: TextAlign.center),
                    ),
                  Expanded(child: body),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _Message extends StatelessWidget {
  final IconData icon;
  final String text;
  final Widget? action;
  const _Message({required this.icon, required this.text, this.action});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 56, color: Palette.gold),
            const SizedBox(height: 16),
            Text(text, textAlign: TextAlign.center, style: const TextStyle(fontSize: 17)),
            if (action != null) ...[const SizedBox(height: 24), action!],
          ],
        ),
      ),
    );
  }
}
