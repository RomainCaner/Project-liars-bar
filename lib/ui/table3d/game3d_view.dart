import 'package:material_ui/material_ui.dart';

import '../../game/cards.dart';
import '../../game/engine.dart' show Phase;
import '../../game/view.dart';
import '../../session/session.dart';
import '../theme.dart';
import '../widgets/revolver.dart';
import '../widgets/table_header.dart';
import 'scene_view.dart';

/// Table en 3D : la scène three.js en fond, l'interface Flutter par-dessus.
class Game3DView extends StatefulWidget {
  final GameSession session;
  final GameView view;
  final VoidCallback onLeave;
  final VoidCallback onFailure;

  const Game3DView({
    super.key,
    required this.session,
    required this.view,
    required this.onLeave,
    required this.onFailure,
  });

  @override
  State<Game3DView> createState() => _Game3DViewState();
}

class _Game3DViewState extends State<Game3DView> {
  final Set<int> _selected = {};
  final _topKey = GlobalKey();
  final _bottomKey = GlobalKey();
  double _insetTop = 70;
  double _insetBottom = 130;

  @override
  void didUpdateWidget(Game3DView old) {
    super.didUpdateWidget(old);
    final ids = widget.view.hand.map((c) => c.id).toSet();
    _selected.removeWhere((id) => !ids.contains(id));
    if (widget.view.round != old.view.round || !widget.view.canPlay) _selected.clear();
  }

  void _measure() {
    final top = (_topKey.currentContext?.findRenderObject() as RenderBox?)?.size.height;
    final bottom = (_bottomKey.currentContext?.findRenderObject() as RenderBox?)?.size.height;
    final pad = MediaQuery.paddingOf(context);
    final t = (top ?? _insetTop) + pad.top;
    final b = (bottom ?? _insetBottom) + pad.bottom;
    if ((t - _insetTop).abs() > 1 || (b - _insetBottom).abs() > 1) {
      setState(() {
        _insetTop = t;
        _insetBottom = b;
      });
    }
  }

  void _toggle(int id) {
    if (!widget.view.canPlay) return;
    setState(() {
      if (_selected.contains(id)) {
        _selected.remove(id);
      } else if (_selected.length < maxCardsPerPlay) {
        _selected.add(id);
      }
    });
  }

  void _play() {
    if (_selected.isEmpty) return;
    widget.session.play(_selected.toList());
    setState(_selected.clear);
  }

  @override
  Widget build(BuildContext context) {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _measure();
    });
    final v = widget.view;
    return Stack(
      children: [
        Positioned.fill(
          child: Table3DScene(
            view: v,
            selected: _selected,
            canSelect: v.canPlay,
            insetTop: _insetTop,
            insetBottom: _insetBottom,
            onCardTap: _toggle,
            onFailure: widget.onFailure,
          ),
        ),
        SafeArea(
          child: Column(
            children: [
              Container(
                key: _topKey,
                decoration: const BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    colors: [Color(0xCC000000), Color(0x00000000)],
                  ),
                ),
                child: Column(
                  children: [
                    TableHeader(view: v, onLeave: widget.onLeave),
                    const SizedBox(height: 6),
                    _statusPill(v),
                  ],
                ),
              ),
              const Spacer(),
              if (v.reveal != null && v.phase == Phase.reveal) _stamp(v),
              const Spacer(),
              Container(key: _bottomKey, child: _bottomPanel(v)),
            ],
          ),
        ),
      ],
    );
  }

  Widget _statusPill(GameView v) {
    String text;
    var highlight = false;
    switch (v.phase) {
      case Phase.playing:
        if (v.isMyTurn) {
          highlight = true;
          text = v.mustCall
              ? 'Tu es le dernier avec des cartes : accuse !'
              : v.lastPlayBy == null
                  ? 'À toi de commencer : choisis tes cartes'
                  : '${v.nameOf(v.lastPlayBy)} a posé ${v.lastPlayCount} '
                      '${v.lastPlayCount > 1 ? v.tableRank!.plural : v.tableRank!.label}… à toi !';
        } else if (v.lastPlayBy != null) {
          final who = v.lastPlayBy == v.myId ? 'Tu as posé' : '${v.nameOf(v.lastPlayBy)} a posé';
          text = '$who ${v.lastPlayCount} '
              '${v.lastPlayCount > 1 ? v.tableRank!.plural : v.tableRank!.label} · '
              'au tour de ${v.nameOf(v.currentId)}';
        } else {
          text = 'Au tour de ${v.nameOf(v.currentId)}';
        }
      case Phase.reveal:
        text = '${v.nameOf(v.reveal!.accuserId)} accuse ${v.nameOf(v.reveal!.accusedId)} !';
      case Phase.roulette:
        text = '';
      default:
        text = '';
    }
    if (text.isEmpty) return const SizedBox(height: 8);
    return AnimatedContainer(
      duration: const Duration(milliseconds: 200),
      margin: const EdgeInsets.symmetric(horizontal: 16),
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      decoration: BoxDecoration(
        color: highlight ? Palette.gold : Colors.black54,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        text,
        textAlign: TextAlign.center,
        style: TextStyle(
          color: highlight ? Palette.ink : Palette.cream,
          fontWeight: highlight ? FontWeight.w800 : FontWeight.w600,
          fontSize: 14,
        ),
      ),
    );
  }

  Widget _stamp(GameView v) {
    final r = v.reveal!;
    return TweenAnimationBuilder<double>(
      key: ValueKey('stamp3d-${v.round}'),
      tween: Tween(begin: 0, end: 1),
      duration: Duration(milliseconds: 900 + 300 * r.cards.length),
      curve: const Interval(0.65, 1, curve: Curves.elasticOut),
      builder: (context, t, child) => Transform.scale(scale: t, child: child),
      child: Transform.rotate(
        angle: -0.08,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
          decoration: BoxDecoration(
            color: Colors.black54,
            border: Border.all(color: r.wasLie ? Palette.blood : Palette.truth, width: 3),
            borderRadius: BorderRadius.circular(8),
          ),
          child: Text(
            r.wasLie ? 'MENSONGE !' : 'VÉRITÉ !',
            style: TextStyle(
              fontSize: 34,
              fontWeight: FontWeight.w900,
              letterSpacing: 2,
              color: r.wasLie ? Palette.blood : Palette.truth,
            ),
          ),
        ),
      ),
    );
  }

  Widget _bottomPanel(GameView v) {
    final me = v.me;
    final alive = me?.alive ?? false;
    final showButtons = alive && v.phase == Phase.playing;
    return Container(
      padding: const EdgeInsets.fromLTRB(12, 18, 12, 12),
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.bottomCenter,
          end: Alignment.topCenter,
          colors: [Color(0xDD000000), Color(0x00000000)],
        ),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Text(me == null ? 'Spectateur' : alive ? 'Ton barillet' : 'Éliminé',
                  style: TextStyle(color: alive ? Palette.muted : Palette.blood, fontSize: 12)),
              const SizedBox(width: 8),
              if (me != null) ShotDots(shots: me.shots, size: 9, dead: !alive),
              const Spacer(),
              if (me != null && alive)
                Text('Risque : 1/${revolverChambers - me.shots}',
                    style: const TextStyle(color: Palette.muted, fontSize: 12)),
            ],
          ),
          if (showButtons) ...[
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: FilledButton(
                    onPressed: v.canPlay && _selected.isNotEmpty ? _play : null,
                    child: Text(_selected.isEmpty
                        ? (v.canPlay ? 'TOUCHE TES CARTES' : 'POSER')
                        : 'POSER ${_selected.length} '
                            '${(_selected.length > 1 ? v.tableRank!.plural : v.tableRank!.label).toUpperCase()}'),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: FilledButton(
                    style: FilledButton.styleFrom(
                      backgroundColor: Palette.blood,
                      foregroundColor: Colors.white,
                    ),
                    onPressed: v.canCall ? widget.session.callLiar : null,
                    child: const Text('MENTEUR !'),
                  ),
                ),
              ],
            ),
          ] else
            const SizedBox(height: 62),
        ],
      ),
    );
  }
}
