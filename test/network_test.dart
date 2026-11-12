import 'dart:async';
import 'dart:math';

import 'package:flutter_test/flutter_test.dart';
import 'package:menteur/game/bot.dart';
import 'package:menteur/game/engine.dart';
import 'package:menteur/session/client_session.dart';
import 'package:menteur/session/host_session.dart';
import 'package:menteur/session/session.dart';

/// Fait jouer une session (hôte ou invité) automatiquement avec l'IA.
void autoplay(GameSession s, int seed) {
  final brain = BotBrain(random: Random(seed));
  var lastVersion = -1;
  s.addListener(() {
    final v = s.view;
    if (v == null || v.version == lastVersion) return;
    lastVersion = v.version;
    final action = brain.decide(v);
    switch (action) {
      case BotPlay(:final cardIds):
        s.play(cardIds);
      case BotCall():
        s.callLiar();
      case BotShoot():
        s.shoot();
      case null:
        break;
    }
  });
}

Future<void> waitFor(bool Function() cond, {Duration timeout = const Duration(seconds: 20), String Function()? label}) async {
  final end = DateTime.now().add(timeout);
  while (!cond()) {
    if (DateTime.now().isAfter(end)) throw TimeoutException('condition non atteinte (${label?.call()})');
    await Future<void>.delayed(const Duration(milliseconds: 10));
  }
}

void main() {
  test('partie complète en réseau local : hôte + 2 invités + 1 bot', () async {
    final host = HostSession(
      hostName: 'Hôte',
      timings: HostTimings.instant,
      random: Random(7),
    );
    await host.startNetwork();
    final port = host.port!;

    final c1 = ClientSession(host: '127.0.0.1', port: port, name: 'Léa', cid: 'c1');
    final c2 = ClientSession(host: '127.0.0.1', port: port, name: 'Léa', cid: 'c2');
    await c1.connect();
    await c2.connect();
    await waitFor(() => host.engine.seats.length == 3, label: () => 'seats=${host.engine.seats.length}');
    expect(host.engine.seats.map((s) => s.name), contains('Léa 2'),
        reason: 'pseudos dédoublonnés');

    host.addBot();
    await waitFor(() => c1.view?.players.length == 4 && c2.view?.players.length == 4, label: () => 'c1=${c1.view?.players.length} c2=${c2.view?.players.length}');

    autoplay(host, 1);
    autoplay(c1, 2);
    autoplay(c2, 3);
    host.startGame();

    await waitFor(() => host.engine.phase == Phase.gameOver,
        timeout: const Duration(seconds: 60),
        label: () => 'phase=${host.engine.phase} cur=${host.engine.currentSeat?.name} '
            'shooter=${host.engine.shooterId} shot=${host.engine.lastShotDied} '
            'c1=${c1.view?.version} host=${host.engine.version}');
    await waitFor(() => c1.view?.phase == Phase.gameOver && c2.view?.phase == Phase.gameOver, label: () => 'c1=${c1.view?.phase} c2=${c2.view?.phase} ${c1.status} ${c2.status}');
    expect(c1.view!.winnerId, host.engine.winnerId);
    expect(c2.view!.winnerId, host.engine.winnerId);

    await c1.close();
    await c2.close();
    await host.close();
  });

  test('un invité qui se déconnecte en partie est remplacé par un bot', () async {
    final host = HostSession(hostName: 'Hôte', timings: HostTimings.instant, random: Random(9));
    await host.startNetwork();
    final c1 = ClientSession(host: '127.0.0.1', port: host.port!, name: 'Tom');
    await c1.connect();
    await waitFor(() => host.engine.seats.length == 2);
    autoplay(host, 4);
    host.startGame();
    await c1.close();
    await waitFor(() => host.engine.phase == Phase.gameOver,
        timeout: const Duration(seconds: 30));
    expect(host.engine.seats.firstWhere((s) => s.name == 'Tom').connected, isFalse);
    await host.close();
  });

  test('minuteur : l\'IA joue à la place d\'un joueur trop lent', () async {
    final host = HostSession(
      hostName: 'Hôte',
      online: false,
      timings: HostTimings.instant,
      random: Random(3),
    );
    host.addBot();
    host.updateSettings(const GameSettings(turnSeconds: 20)); // 20 ms en test
    host.startGame();
    // Personne ne joue pour l'hôte : la partie doit quand même se terminer.
    await waitFor(() => host.engine.phase == Phase.gameOver,
        timeout: const Duration(seconds: 30));
    await host.close();
  });

  test('un invité choisit son personnage dans le salon', () async {
    final host = HostSession(hostName: 'Hôte', random: Random(2));
    await host.startNetwork();
    final c = ClientSession(host: '127.0.0.1', port: host.port!, name: 'Zoé', cid: 'z');
    await c.connect();
    await waitFor(() => host.engine.seats.length == 2);
    c.chooseSkin(4);
    await waitFor(() => host.engine.seat('z').skin == 4);
    await waitFor(() => c.view?.player('z')?.skin == 4);
    await c.close();
    await host.close();
  });

  test('une partie déjà lancée refuse les nouveaux venus', () async {
    final host = HostSession(hostName: 'Hôte', random: Random(1));
    await host.startNetwork();
    host.addBot();
    host.startGame();
    final late = ClientSession(host: '127.0.0.1', port: host.port!, name: 'Retard');
    await late.connect();
    await waitFor(() => late.status == ConnectionStatus.closed);
    expect(late.closedReason, contains('déjà commencé'));
    await host.close();
  });
}
