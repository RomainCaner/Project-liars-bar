/// Session hôte : fait tourner le moteur, les bots et le serveur WebSocket.
library;

import 'dart:async';
import 'dart:io';
import 'dart:math';

import 'package:flutter/foundation.dart' show visibleForTesting;

import '../game/bot.dart';
import '../game/cards.dart';
import '../game/engine.dart';
import '../game/view.dart';
import '../net/discovery.dart';
import '../net/protocol.dart';
import 'session.dart';

/// Délais des animations côté hôte (les invités suivent le rythme de l'hôte).
class HostTimings {
  final Duration reveal;
  final Duration afterShot;
  final Duration botMin;
  final Duration botMax;
  final Duration botShoot;

  /// Durée d'une « seconde » du minuteur de tour (raccourcie dans les tests).
  final Duration turnUnit;

  const HostTimings({
    this.reveal = const Duration(milliseconds: 3200),
    this.afterShot = const Duration(milliseconds: 3800),
    this.botMin = const Duration(milliseconds: 1100),
    this.botMax = const Duration(milliseconds: 2600),
    this.botShoot = const Duration(milliseconds: 2200),
    this.turnUnit = const Duration(seconds: 1),
  });

  static const instant = HostTimings(
    reveal: Duration.zero,
    afterShot: Duration.zero,
    botMin: Duration.zero,
    botMax: Duration.zero,
    botShoot: Duration.zero,
    turnUnit: Duration(milliseconds: 1),
  );
}

const _botNames = [
  'Gaston', 'Lulu', 'Bébert', 'Mimi', 'Le Borgne', 'Rosalie', 'Dédé', 'Josette',
];

class HostSession extends GameSession {
  static const hostId = 'host';

  final GameEngine engine;
  final bool online;
  final HostTimings timings;
  final Random _rng;
  final _brains = <String, BotBrain>{};
  final _clients = <WebSocket, String?>{};

  HttpServer? _server;
  DiscoveryBroadcaster? _broadcaster;
  Timer? _timer;
  int _scheduledFor = -1;
  int _botCounter = 0;
  GameView? _view;
  ConnectionStatus _status = ConnectionStatus.connected;
  String? _closedReason;

  /// Port réellement ouvert (null hors ligne).
  int? get port => _server?.port;

  HostSession({
    required String hostName,
    this.online = true,
    this.timings = const HostTimings(),
    Random? random,
  })  : _rng = random ?? Random(),
        engine = GameEngine(random: random) {
    engine.addPlayer(hostId, hostName);
    _refresh();
  }

  @override
  GameView? get view => _view;
  @override
  bool get isHost => true;
  @override
  ConnectionStatus get status => _status;
  @override
  String? get closedReason => _closedReason;

  // ---------------------------------------------------------------- réseau

  Future<void> startNetwork() async {
    if (!online) return;
    try {
      _server = await HttpServer.bind(InternetAddress.anyIPv4, gamePort);
    } on SocketException {
      _server = await HttpServer.bind(InternetAddress.anyIPv4, 0);
    }
    _server!.listen(_onRequest, onError: (_) {});
    _broadcaster = DiscoveryBroadcaster();
    _updateAnnouncement();
    await _broadcaster!.start();
  }

  Future<void> _onRequest(HttpRequest req) async {
    if (!WebSocketTransformer.isUpgradeRequest(req)) {
      req.response
        ..statusCode = HttpStatus.ok
        ..write('Serveur de jeu Menteur')
        ..close();
      return;
    }
    final ws = await WebSocketTransformer.upgrade(req);
    ws.pingInterval = const Duration(seconds: 4);
    _clients[ws] = null;
    ws.listen(
      (raw) => _onClientMessage(ws, decodeMsg(raw)),
      onDone: () => _onClientGone(ws),
      onError: (_) => _onClientGone(ws),
      cancelOnError: true,
    );
  }

  void _send(WebSocket ws, Map<String, dynamic> m) {
    try {
      ws.add(encodeMsg(m));
    } catch (_) {}
  }

  void _onClientMessage(WebSocket ws, Map<String, dynamic>? m) {
    if (m == null) return;
    final pid = _clients[ws];
    try {
      switch (m['t']) {
        case 'hello':
          _onHello(ws, m);
        case 'play':
          if (pid == null) return;
          engine.playCards(pid, [for (final c in m['cards'] as List) c as int]);
          _changed();
        case 'call':
          if (pid == null) return;
          engine.callLiar(pid);
          _changed();
        case 'shoot':
          if (pid == null) return;
          engine.pullTrigger(pid);
          _changed();
        case 'skin':
          if (pid == null) return;
          engine.setSkin(pid, (m['skin'] as num).toInt());
          _changed();
      }
    } on GameError catch (e) {
      _send(ws, {'t': 'error', 'msg': e.message});
    } catch (_) {
      _send(ws, {'t': 'error', 'msg': 'Action invalide'});
    }
  }

  void _onHello(WebSocket ws, Map<String, dynamic> m) {
    final cid = '${m['cid']}';
    var name = '${m['name']}'.trim();
    if (name.isEmpty) name = 'Joueur';
    if (name.length > 16) name = name.substring(0, 16);

    final existing = engine.seatOrNull(cid);
    if (existing != null) {
      // Reconnexion : on ferme l'ancienne socket éventuelle.
      for (final e in _clients.entries.toList()) {
        if (e.value == cid && e.key != ws) {
          _clients[e.key] = null;
          e.key.close();
        }
      }
      _clients[ws] = cid;
      if (!existing.connected) engine.setConnected(cid, true);
      toast('${existing.name} est de retour');
      _changed();
      return;
    }
    if (engine.phase != Phase.lobby) {
      _send(ws, {'t': 'bye', 'msg': 'La partie a déjà commencé'});
      ws.close();
      return;
    }
    if (engine.seats.length >= maxPlayers) {
      _send(ws, {'t': 'bye', 'msg': 'La table est pleine'});
      ws.close();
      return;
    }
    engine.addPlayer(cid, _uniqueName(name));
    _clients[ws] = cid;
    toast('${engine.seat(cid).name} a rejoint la table');
    _changed();
  }

  String _uniqueName(String base) {
    final taken = engine.seats.map((s) => s.name.toLowerCase()).toSet();
    if (!taken.contains(base.toLowerCase())) return base;
    for (var i = 2;; i++) {
      final n = '$base $i';
      if (!taken.contains(n.toLowerCase())) return n;
    }
  }

  void _onClientGone(WebSocket ws) {
    if (!_clients.containsKey(ws)) return;
    final pid = _clients.remove(ws);
    if (pid == null) return;
    if (_clients.containsValue(pid)) return; // déjà reconnecté ailleurs
    final s = engine.seatOrNull(pid);
    if (s == null) return;
    if (engine.phase == Phase.lobby) {
      engine.removePlayer(pid);
      toast('${s.name} a quitté la table');
    } else {
      engine.setConnected(pid, false);
      toast('${s.name} s\'est déconnecté — un bot prend sa place');
    }
    _changed();
  }

  void _updateAnnouncement() {
    _broadcaster?.info = {
      'name': engine.seat(hostId).name,
      'port': _server?.port ?? gamePort,
      'players': engine.seats.length,
      'max': maxPlayers,
      'open': engine.phase == Phase.lobby && engine.seats.length < maxPlayers,
    };
  }

  // ----------------------------------------------------------------- salon

  void addBot() {
    final used = engine.seats.map((s) => s.name).toSet();
    final free = _botNames.where((n) => !used.contains(n)).toList();
    final name = free.isEmpty ? 'Bot ${_botCounter + 1}' : free[_rng.nextInt(free.length)];
    final id = 'bot-${_botCounter++}';
    try {
      engine.addPlayer(id, name, isBot: true);
      _brains[id] = BotBrain(random: _rng);
      _changed();
    } on GameError catch (e) {
      toast(e.message);
    }
  }

  void kick(String playerId) {
    if (playerId == hostId) return;
    for (final e in _clients.entries.toList()) {
      if (e.value == playerId) {
        _clients[e.key] = null;
        _send(e.key, {'t': 'bye', 'msg': 'Tu as été retiré de la table'});
        e.key.close();
      }
    }
    try {
      engine.removePlayer(playerId);
      _brains.remove(playerId);
      _changed();
    } on GameError catch (e) {
      toast(e.message);
    }
  }

  void updateSettings(GameSettings s) {
    try {
      engine.updateSettings(s);
      _changed();
    } on GameError catch (e) {
      toast(e.message);
    }
  }

  @override
  void chooseSkin(int skin) => _act(() => engine.setSkin(hostId, skin));

  void startGame() {
    try {
      engine.startGame();
      _changed();
    } on GameError catch (e) {
      toast(e.message);
    }
  }

  void backToLobby() {
    engine.backToLobby();
    _brains.removeWhere((id, _) => engine.seatOrNull(id) == null);
    _changed();
  }

  // --------------------------------------------------------------- actions

  void _act(void Function() f) {
    try {
      f();
      _changed();
    } on GameError catch (e) {
      toast(e.message);
    }
  }

  @override
  void play(List<int> cardIds) => _act(() => engine.playCards(hostId, cardIds));
  @override
  void callLiar() => _act(() => engine.callLiar(hostId));
  @override
  void shoot() => _act(() => engine.pullTrigger(hostId));

  // ------------------------------------------------------------ diffusion

  /// Fin du temps imparti pour l'action en cours (minuteur de tour).
  DateTime? _deadline;

  Map<String, dynamic> _viewFor(String id) {
    final v = engine.viewFor(id);
    final d = _deadline;
    if (d != null) v['turnLeft'] = d.difference(DateTime.now()).inMilliseconds.clamp(0, 1 << 30);
    return v;
  }

  void _refresh() {
    _view = GameView.fromJson(_viewFor(hostId));
  }

  void _changed() {
    _schedule();
    _refresh();
    for (final e in _clients.entries) {
      if (e.value != null) {
        _send(e.key, {'t': 'state', 'state': _viewFor(e.value!)});
      }
    }
    _updateAnnouncement();
    notifyListeners();
  }

  /// Pour les tests : rediffuse l'état après une modification directe du moteur.
  @visibleForTesting
  void forceRefresh() => _changed();

  bool _automated(Seat s) => s.isBot || !s.connected;

  BotBrain _brainFor(String id) =>
      _brains.putIfAbsent(id, () => BotBrain(random: _rng, boldness: 0.3));

  /// Programme la prochaine action automatique (bot, transition de phase).
  void _schedule() {
    if (_scheduledFor == engine.version) return;
    _timer?.cancel();
    _deadline = null;
    _scheduledFor = engine.version;
    final v = engine.version;
    void later(Duration d, void Function() f) {
      _timer = Timer(d, () {
        if (engine.version != v || _status == ConnectionStatus.closed) return;
        try {
          f();
        } on GameError {
          return;
        }
        _changed();
      });
    }

    final secs = engine.settings.turnSeconds;
    final turnLimit = secs > 0 ? timings.turnUnit * secs : null;
    switch (engine.phase) {
      case Phase.reveal:
        later(timings.reveal, engine.proceedToRoulette);
      case Phase.roulette:
        if (engine.lastShotDied != null) {
          later(timings.afterShot, engine.proceedAfterShot);
        } else {
          final shooter = engine.seat(engine.shooterId!);
          if (_automated(shooter)) {
            later(timings.botShoot, () => engine.pullTrigger(shooter.id));
          } else if (turnLimit != null) {
            _deadline = DateTime.now().add(turnLimit);
            later(turnLimit, () => engine.pullTrigger(shooter.id));
          }
        }
      case Phase.playing:
        final cur = engine.currentSeat!;
        if (!_automated(cur)) {
          // Minuteur : passé le délai, l'IA joue à la place du joueur.
          if (turnLimit != null) {
            _deadline = DateTime.now().add(turnLimit);
            later(turnLimit, () {
              toast('Temps écoulé pour ${cur.name}');
              _autoAct(cur);
            });
          }
          return;
        }
        final spread = timings.botMax - timings.botMin;
        final delay = timings.botMin +
            Duration(
                milliseconds: spread.inMilliseconds <= 0
                    ? 0
                    : _rng.nextInt(spread.inMilliseconds));
        later(delay, () => _autoAct(cur));
      case Phase.lobby:
      case Phase.gameOver:
        break;
    }
  }

  void _autoAct(Seat seat) {
    final view = GameView.fromJson(engine.viewFor(seat.id));
    final action = _brainFor(seat.id).decide(view);
    switch (action) {
      case BotPlay(:final cardIds):
        engine.playCards(seat.id, cardIds);
      case BotCall():
        engine.callLiar(seat.id);
      case BotShoot():
        engine.pullTrigger(seat.id);
      case null:
        break;
    }
  }

  @override
  Future<void> close() async {
    _status = ConnectionStatus.closed;
    _timer?.cancel();
    _broadcaster?.stop();
    for (final ws in _clients.keys.toList()) {
      _send(ws, {'t': 'bye', 'msg': 'L\'hôte a fermé la partie'});
      await ws.close();
    }
    _clients.clear();
    await _server?.close(force: true);
    _server = null;
  }
}
