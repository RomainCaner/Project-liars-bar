import 'dart:math';

import 'package:flutter_test/flutter_test.dart';
import 'package:menteur/game/bot.dart';
import 'package:menteur/game/cards.dart';
import 'package:menteur/game/engine.dart';
import 'package:menteur/game/view.dart';

GameEngine twoPlayers({int seed = 1}) {
  final e = GameEngine(random: Random(seed))
    ..addPlayer('a', 'Alice')
    ..addPlayer('b', 'Bob');
  return e;
}

/// Force une main et la carte du tour pour des tests déterministes.
void rig(GameEngine e, Rank table, Map<String, List<Rank>> hands) {
  e.tableRank = table;
  var id = 1000;
  for (final entry in hands.entries) {
    e.seat(entry.key).hand = [for (final r in entry.value) GameCard(id++, r)];
  }
}

void main() {
  group('paquet', () {
    test('20 cartes jusqu\'à 4 joueurs', () {
      for (var n = 2; n <= 4; n++) {
        final c = deckComposition(n);
        expect(c.values.reduce((a, b) => a + b), 20);
        expect(c[Rank.joker], 2);
      }
    });

    test('assez de cartes pour 5 et 6 joueurs', () {
      for (var n = 5; n <= 6; n++) {
        final total = deckComposition(n).values.reduce((a, b) => a + b);
        expect(total, greaterThanOrEqualTo(n * cardsPerHand));
      }
    });
  });

  group('déroulement', () {
    test('distribution de 5 cartes et carte du tour', () {
      final e = twoPlayers()..startGame();
      expect(e.phase, Phase.playing);
      expect(e.tableRank, isIn(tableRanks));
      for (final s in e.seats) {
        expect(s.hand.length, cardsPerHand);
      }
      final ids = e.seats.expand((s) => s.hand).map((c) => c.id).toSet();
      expect(ids.length, 10, reason: 'aucune carte en double');
    });

    test('refuse les coups hors tour ou invalides', () {
      final e = twoPlayers()..startGame();
      final cur = e.currentSeat!;
      final other = e.seats.firstWhere((s) => s != cur);
      expect(() => e.playCards(other.id, [other.hand.first.id]), throwsA(isA<GameError>()));
      expect(() => e.playCards(cur.id, []), throwsA(isA<GameError>()));
      expect(() => e.playCards(cur.id, cur.hand.take(4).map((c) => c.id).toList()),
          throwsA(isA<GameError>()));
      expect(() => e.playCards(cur.id, [other.hand.first.id]), throwsA(isA<GameError>()));
      expect(() => e.callLiar(cur.id), throwsA(isA<GameError>()),
          reason: 'rien à contester en début de manche');
    });

    test('accusation à tort : l\'accusateur tire', () {
      final e = twoPlayers()..startGame();
      e.currentIndex = 0;
      rig(e, Rank.queen, {
        'a': [Rank.queen, Rank.joker, Rank.king],
        'b': [Rank.ace, Rank.ace],
      });
      final a = e.seat('a');
      e.playCards('a', [a.hand[0].id, a.hand[1].id]);
      expect(e.currentSeat!.id, 'b');
      e.callLiar('b');
      expect(e.phase, Phase.reveal);
      expect(e.reveal!.wasLie, isFalse);
      expect(e.reveal!.loserId, 'b');
      e.proceedToRoulette();
      expect(e.shooterId, 'b');
    });

    test('mensonge démasqué : le menteur tire', () {
      final e = twoPlayers()..startGame();
      e.currentIndex = 0;
      rig(e, Rank.king, {
        'a': [Rank.king, Rank.ace],
        'b': [Rank.queen],
      });
      final a = e.seat('a');
      e.playCards('a', a.hand.map((c) => c.id).toList());
      e.callLiar('b');
      expect(e.reveal!.wasLie, isTrue);
      expect(e.reveal!.loserId, 'a');
    });

    test('le dernier joueur avec des cartes doit accuser', () {
      final e = twoPlayers()..startGame();
      e.currentIndex = 0;
      rig(e, Rank.ace, {
        'a': [Rank.ace],
        'b': [Rank.king, Rank.queen],
      });
      e.playCards('a', [e.seat('a').hand.single.id]);
      final b = e.seat('b');
      expect(e.currentSeat, b);
      expect(e.mustCall(b), isTrue);
      expect(() => e.playCards('b', [b.hand.first.id]), throwsA(isA<GameError>()));
      expect(e.viewFor('b')['mustCall'], isTrue);
      e.callLiar('b');
      expect(e.phase, Phase.reveal);
    });

    test('les joueurs sans carte sont sautés', () {
      final e = GameEngine(random: Random(3))
        ..addPlayer('a', 'A')
        ..addPlayer('b', 'B')
        ..addPlayer('c', 'C')
        ..startGame();
      e.currentIndex = 0;
      rig(e, Rank.ace, {
        'a': [Rank.ace, Rank.ace],
        'b': [],
        'c': [Rank.king],
      });
      e.playCards('a', [e.seat('a').hand.first.id]);
      expect(e.currentSeat!.id, 'c');
    });

    test('le barillet tue exactement à la chambre chargée', () {
      final e = twoPlayers()..startGame();
      final b = e.seat('b')..bulletChambers = {2};
      for (var shot = 0; shot < 3; shot++) {
        e.phase = Phase.roulette;
        e.shooterId = 'b';
        e.lastShotDied = null;
        e.pullTrigger('b');
        expect(e.lastShotDied, shot == 2);
      }
      expect(b.alive, isFalse);
      e.proceedAfterShot();
      expect(e.phase, Phase.gameOver);
      expect(e.winnerId, 'a');
    });

    test('la vue ne révèle ni les mains adverses ni les balles', () {
      final e = twoPlayers()..startGame();
      final view = e.viewFor('a');
      final hand = (view['hand'] as List).cast<Map>();
      expect(hand.map((c) => c['id']).toSet(),
          e.seat('a').hand.map((c) => c.id).toSet());
      expect(view.toString().toLowerCase().contains('chamber'), isFalse,
          reason: 'la position des balles reste secrète');
      final parsed = GameView.fromJson(view);
      expect(parsed.players.length, 2);
      expect(parsed.player('b')!.cardCount, 5);
    });
  });

  group('variantes', () {
    test('nombre de jokers réglable', () {
      for (final j in [0, 2, 4]) {
        final e = twoPlayers()
          ..updateSettings(GameSettings(jokers: j))
          ..startGame();
        expect(e.composition[Rank.joker], j);
      }
      expect(() => twoPlayers().updateSettings(const GameSettings(jokers: 3)),
          throwsA(isA<GameError>()));
    });

    test('deux balles par barillet', () {
      final e = twoPlayers()
        ..updateSettings(const GameSettings(bullets: 2))
        ..startGame();
      for (final s in e.seats) {
        expect(s.bulletChambers.length, 2);
      }
      expect(e.settings.risk(0), closeTo(2 / 6, 1e-9));
      expect(e.settings.risk(4), 1);
    });

    test('barillet re-tourné : risque constant et mort aléatoire', () {
      const s = GameSettings(respin: true);
      expect(s.risk(0), s.risk(5));
      var deaths = 0;
      for (var seed = 0; seed < 600; seed++) {
        final e = twoPlayers(seed: seed)
          ..updateSettings(s)
          ..startGame();
        e.phase = Phase.roulette;
        e.shooterId = 'a';
        e.lastShotDied = null;
        e.pullTrigger('a');
        if (e.lastShotDied!) deaths++;
      }
      expect(deaths, inInclusiveRange(60, 140), reason: '≈ 1 sur 6');
    });

    test('personnages : attribués sans doublon puis modifiables au salon', () {
      final e = GameEngine(random: Random(1));
      for (var i = 0; i < 5; i++) {
        e.addPlayer('p$i', 'P$i');
      }
      expect(e.seats.map((s) => s.skin).toSet().length, 5);
      e.setSkin('p0', 3);
      expect(e.seat('p0').skin, 3);
      expect((e.viewFor('p1')['players'] as List).first['skin'], 3);
      e.startGame();
      expect(() => e.setSkin('p0', 1), throwsA(isA<GameError>()));
    });
  });

  test('2000 parties complètes entre bots se terminent avec un gagnant', () {
    final rng = Random(42);
    for (var g = 0; g < 2000; g++) {
      final n = 2 + rng.nextInt(5);
      final e = GameEngine(random: Random(g));
      final brains = <String, BotBrain>{};
      for (var i = 0; i < n; i++) {
        e.addPlayer('p$i', 'P$i', isBot: true);
        brains['p$i'] = BotBrain(random: Random(g * 10 + i));
      }
      e.startGame();
      var steps = 0;
      while (e.phase != Phase.gameOver) {
        expect(++steps, lessThan(5000), reason: 'partie bloquée');
        switch (e.phase) {
          case Phase.playing:
            final cur = e.currentSeat!;
            expect(cur.alive, isTrue);
            final action = brains[cur.id]!.decide(GameView.fromJson(e.viewFor(cur.id)));
            switch (action) {
              case BotPlay(:final cardIds):
                e.playCards(cur.id, cardIds);
              case BotCall():
                e.callLiar(cur.id);
              default:
                fail('aucune action pour le joueur courant');
            }
          case Phase.reveal:
            e.proceedToRoulette();
          case Phase.roulette:
            if (e.lastShotDied == null) {
              final a = brains[e.shooterId]!.decide(GameView.fromJson(e.viewFor(e.shooterId!)));
              expect(a, isA<BotShoot>());
              e.pullTrigger(e.shooterId!);
            } else {
              e.proceedAfterShot();
            }
          default:
            fail('phase inattendue ${e.phase}');
        }
      }
      expect(e.aliveSeats.length, 1);
      expect(e.winnerId, e.aliveSeats.single.id);
    }
  });
}
