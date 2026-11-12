/// IA des bots. Elle ne voit que ce qu'un joueur humain verrait (GameView).
library;

import 'dart:math';

import 'cards.dart';
import 'view.dart';

sealed class BotAction {}

class BotPlay extends BotAction {
  final List<int> cardIds;
  BotPlay(this.cardIds);
}

class BotCall extends BotAction {}

class BotShoot extends BotAction {}

class BotBrain {
  final Random _rng;

  /// 0 = prudent (bluffe peu, accuse peu), 1 = téméraire.
  final double boldness;

  BotBrain({Random? random, double? boldness})
      : _rng = random ?? Random(),
        boldness = boldness ?? (random ?? Random()).nextDouble();

  BotAction? decide(GameView v) {
    if (v.iMustShoot) return BotShoot();
    if (!v.isMyTurn) return null;
    final table = v.tableRank!;
    if (v.mustCall) return BotCall();

    final matching = v.hand.where((c) => c.matches(table)).toList();
    final others = v.hand.where((c) => !c.matches(table)).toList();

    if (v.canCall && _shouldCall(v, table, matching.length)) return BotCall();
    if (v.hand.isEmpty) return BotCall();
    return BotPlay(_choosePlay(matching, others));
  }

  bool _shouldCall(GameView v, Rank table, int myMatching) {
    final n = v.lastPlayCount;
    final totalMatching = (v.deck[table] ?? 0) + (v.deck[Rank.joker] ?? 0);
    final possibleElsewhere = totalMatching - myMatching;
    if (n > possibleElsewhere) return true; // mensonge certain

    var p = 0.12 + 0.12 * (n - 1);
    p += 0.35 * (myMatching / max(1, totalMatching));
    p += 0.03 * max(0, v.pile - n);
    final accused = v.player(v.lastPlayBy);
    if (accused != null && accused.cardCount == 0) p += 0.12;
    if (myMatching == 0) p += 0.15; // je devrais bluffer moi-même
    p += (boldness - 0.5) * 0.2;
    p = p.clamp(0.05, 0.9);
    return _rng.nextDouble() < p;
  }

  List<int> _choosePlay(List<GameCard> matching, List<GameCard> others) {
    if (matching.isNotEmpty) {
      // On garde les jokers pour la fin : on pose d'abord les vraies cartes.
      matching.sort((a, b) =>
          (a.rank == Rank.joker ? 1 : 0).compareTo(b.rank == Rank.joker ? 1 : 0));
      final maxTrue = min(maxCardsPerPlay, matching.length);
      var k = 1 + _rng.nextInt(maxTrue);
      final chosen = matching.take(k).map((c) => c.id).toList();
      // Petit bluff glissé au milieu de vraies cartes.
      if (k < maxCardsPerPlay &&
          others.isNotEmpty &&
          _rng.nextDouble() < 0.15 + 0.25 * boldness) {
        chosen.add(others[_rng.nextInt(others.length)].id);
      }
      return chosen;
    }
    // Aucune carte valable : bluff obligatoire, le plus discret possible.
    final k = min(others.length, _rng.nextDouble() < 0.7 - 0.3 * boldness ? 1 : 2);
    others.shuffle(_rng);
    return others.take(k).map((c) => c.id).toList();
  }
}
