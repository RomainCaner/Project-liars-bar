import 'package:material_ui/material_ui.dart';

import '../../game/cards.dart';
import '../../game/engine.dart' show Phase;
import '../../game/view.dart';
import '../../session/session.dart';
import '../theme.dart';
import '../widgets/player_badge.dart';
import '../widgets/playing_card.dart';
import '../widgets/revolver.dart';

class GameTableView extends StatefulWidget {
  final GameSession session;
  final GameView view;
  final VoidCallback onLeave;

  const GameTableView({
    super.key,
    required this.session,
    required this.view,
    required this.onLeave,
  });

  @override
  State<GameTableView> createState() => _GameTableViewState();
}

class _GameTableViewState extends State<GameTableView> {
  final Set<int> _selected = {};

  @override
  void didUpdateWidget(GameTableView old) {
    super.didUpdateWidget(old);
    final ids = widget.view.hand.map((c) => c.id).toSet();
    _selected.removeWhere((id) => !ids.contains(id));
    if (widget.view.round != old.view.round) _selected.clear();
  }

  void _toggle(int id) {
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
    final v = widget.view;
    final others = v.players.where((p) => p.id != v.myId).toList();
    return Column(
      children: [
        _header(v),
        const SizedBox(height: 6),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8),
          child: Wrap(
            alignment: WrapAlignment.center,
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final p in others)
                PlayerBadge(
                  player: p,
                  isCurrent: v.phase == Phase.playing && p.id == v.currentId,
                  width: others.length > 3 ? 100 : 108,
                ),
            ],
          ),
        ),
        Expanded(child: Center(child: _center(v))),
        _logLines(v),
        _myArea(v),
      ],
    );
  }

  Widget _header(GameView v) {
    final rank = v.tableRank;
    return Padding(
      padding: const EdgeInsets.fromLTRB(4, 4, 12, 0),
      child: Row(
        children: [
          IconButton(onPressed: widget.onLeave, icon: const Icon(Icons.close)),
          Text('Manche ${v.round}',
              style: const TextStyle(color: Palette.muted, fontWeight: FontWeight.w600)),
          const Spacer(),
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

  Widget _center(GameView v) {
    final reveal = v.reveal;
    if (reveal != null && (v.phase == Phase.reveal || v.phase == Phase.roulette)) {
      return _revealArea(v, reveal);
    }
    final current = v.player(v.currentId);
    final status = v.isMyTurn
        ? (v.mustCall ? 'Tu es le dernier avec des cartes : accuse !' : 'À toi de jouer !')
        : 'Au tour de ${current?.name ?? '…'}';
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        SizedBox(
          height: 110,
          width: 160,
          child: v.pile == 0
              ? Center(
                  child: Container(
                    width: 74,
                    height: 104,
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(9),
                      border: Border.all(color: Colors.white24, width: 1.5),
                    ),
                    child: const Center(
                        child: Icon(Icons.style, color: Colors.white24, size: 30)),
                  ),
                )
              : Stack(
                  alignment: Alignment.center,
                  children: [
                    for (var i = 0; i < v.pile.clamp(0, 7); i++)
                      Transform.translate(
                        offset: Offset((i - 3) * 6.0, (i % 2) * -3.0),
                        child: Transform.rotate(
                          angle: ((i * 37) % 11 - 5) * 0.04,
                          child: const PlayingCardView(rank: null, width: 70),
                        ),
                      ),
                  ],
                ),
        ),
        const SizedBox(height: 12),
        if (v.lastPlayBy != null)
          Text.rich(
            TextSpan(children: [
              TextSpan(
                  text: v.lastPlayBy == v.myId ? 'Tu as posé ' : '${v.nameOf(v.lastPlayBy)} a posé '),
              TextSpan(
                text: '${v.lastPlayCount} ${v.lastPlayCount > 1 ? v.tableRank!.plural : v.tableRank!.label}',
                style: const TextStyle(color: Palette.gold, fontWeight: FontWeight.w800),
              ),
              const TextSpan(text: '…'),
            ]),
            style: const TextStyle(fontSize: 16),
          ),
        const SizedBox(height: 6),
        AnimatedDefaultTextStyle(
          duration: const Duration(milliseconds: 200),
          style: TextStyle(
            fontFamily: 'Roboto',
            fontSize: v.isMyTurn ? 20 : 15,
            fontWeight: v.isMyTurn ? FontWeight.w900 : FontWeight.w500,
            color: v.isMyTurn ? Palette.gold : Palette.muted,
          ),
          child: Text(status, textAlign: TextAlign.center),
        ),
      ],
    );
  }

  Widget _revealArea(GameView v, RevealView r) {
    final table = v.tableRank!;
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('${v.nameOf(r.accuserId)} accuse ${v.nameOf(r.accusedId)} !',
            style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600)),
        const SizedBox(height: 14),
        Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (final (i, c) in r.cards.indexed)
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 5),
                child: FlippingCard(
                  key: ValueKey('reveal-${v.round}-${c.id}'),
                  rank: c.rank,
                  width: 72,
                  delay: Duration(milliseconds: 250 + 350 * i),
                  highlight: c.matches(table),
                ),
              ),
          ],
        ),
        const SizedBox(height: 16),
        TweenAnimationBuilder<double>(
          key: ValueKey('stamp-${v.round}'),
          tween: Tween(begin: 0, end: 1),
          duration: Duration(milliseconds: 600 + 350 * r.cards.length),
          curve: const Interval(0.7, 1, curve: Curves.elasticOut),
          builder: (context, t, child) => Transform.scale(scale: t, child: child),
          child: Transform.rotate(
            angle: -0.08,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
              decoration: BoxDecoration(
                border: Border.all(
                    color: r.wasLie ? Palette.blood : Palette.truth, width: 3),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Text(
                r.wasLie ? 'MENSONGE !' : 'VÉRITÉ !',
                style: TextStyle(
                  fontSize: 30,
                  fontWeight: FontWeight.w900,
                  letterSpacing: 2,
                  color: r.wasLie ? Palette.blood : Palette.truth,
                ),
              ),
            ),
          ),
        ),
        const SizedBox(height: 10),
        Text('${v.nameOf(r.loserId)} va tirer…',
            style: const TextStyle(color: Palette.muted, fontSize: 15)),
      ],
    );
  }

  Widget _logLines(GameView v) {
    final lines = v.log.length > 2 ? v.log.sublist(v.log.length - 2) : v.log;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Column(
        children: [
          for (final l in lines)
            Text(l,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 12, color: Colors.white38)),
        ],
      ),
    );
  }

  Widget _myArea(GameView v) {
    final me = v.me;
    final alive = me?.alive ?? false;
    final myTurn = v.isMyTurn;
    return Container(
      padding: const EdgeInsets.fromLTRB(12, 10, 12, 14),
      decoration: BoxDecoration(
        color: Colors.black.withAlpha(90),
        borderRadius: const BorderRadius.vertical(top: Radius.circular(24)),
        border: Border(
          top: BorderSide(color: myTurn ? Palette.gold : Colors.white12, width: myTurn ? 2 : 1),
        ),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Text(me == null ? 'Spectateur' : 'Ton barillet',
                  style: const TextStyle(color: Palette.muted, fontSize: 12)),
              const SizedBox(width: 8),
              if (me != null) ShotDots(shots: me.shots, size: 9, dead: !alive),
              const Spacer(),
              if (me != null && alive)
                Text('Risque : 1/${revolverChambers - me.shots}',
                    style: const TextStyle(color: Palette.muted, fontSize: 12)),
            ],
          ),
          const SizedBox(height: 10),
          if (!alive)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 30),
              child: Text('Tu es éliminé… regarde les autres s\'entretuer.',
                  style: TextStyle(color: Palette.muted)),
            )
          else ...[
            SizedBox(
              height: 112,
              child: v.hand.isEmpty
                  ? const Center(
                      child: Text('Plus de cartes : tu attends la fin de la manche.',
                          style: TextStyle(color: Palette.muted)))
                  : LayoutBuilder(builder: (context, c) {
                      final n = v.hand.length;
                      final w = ((c.maxWidth - 6 * (n - 1)) / n).clamp(40.0, 66.0);
                      return Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          for (final card in v.hand)
                            GestureDetector(
                              onTap: v.canPlay ? () => _toggle(card.id) : null,
                              child: AnimatedPadding(
                                duration: const Duration(milliseconds: 150),
                                padding: EdgeInsets.only(
                                  left: 3,
                                  right: 3,
                                  bottom: _selected.contains(card.id) ? 16 : 0,
                                ),
                                child: PlayingCardView(
                                  rank: card.rank,
                                  width: w,
                                  selected: _selected.contains(card.id),
                                ),
                              ),
                            ),
                        ],
                      );
                    }),
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: FilledButton(
                    onPressed: v.canPlay && _selected.isNotEmpty ? _play : null,
                    child: Text(_selected.isEmpty
                        ? 'POSER'
                        : 'POSER ${_selected.length} ${_selected.length > 1 ? v.tableRank!.plural.toUpperCase() : v.tableRank!.label.toUpperCase()}'),
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
          ],
        ],
      ),
    );
  }
}
