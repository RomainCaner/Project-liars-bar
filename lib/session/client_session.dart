/// Session invitée : se connecte à l'hôte par WebSocket.
library;

import 'dart:async';
import 'dart:io';

import '../game/view.dart';
import '../net/protocol.dart';
import 'session.dart';

class ClientSession extends GameSession {
  final String host;
  final int port;
  final String name;

  /// Identifiant de reconnexion (un par appareil en pratique).
  final String cid;

  WebSocket? _ws;
  GameView? _view;
  ConnectionStatus _status = ConnectionStatus.connecting;
  String? _closedReason;
  bool _byeReceived = false;
  int _reconnectAttempts = 0;

  ClientSession({
    required this.host,
    required this.port,
    required this.name,
    String? cid,
  }) : cid = cid ?? clientId;

  @override
  GameView? get view => _view;
  @override
  bool get isHost => false;
  @override
  ConnectionStatus get status => _status;
  @override
  String? get closedReason => _closedReason;

  /// Première connexion : lève une exception si l'hôte est injoignable.
  Future<void> connect() async {
    await _open();
  }

  Future<void> _open() async {
    final ws = await WebSocket.connect('ws://$host:$port/ws')
        .timeout(const Duration(seconds: 5));
    ws.pingInterval = const Duration(seconds: 4);
    _ws = ws;
    _status = ConnectionStatus.connected;
    _reconnectAttempts = 0;
    ws.listen(
      _onMessage,
      onDone: _onDone,
      onError: (_) => _onDone(),
      cancelOnError: true,
    );
    _send({'t': 'hello', 'name': name, 'cid': cid});
    notifyListeners();
  }

  void _send(Map<String, dynamic> m) {
    try {
      _ws?.add(encodeMsg(m));
    } catch (_) {}
  }

  void _onMessage(dynamic raw) {
    final m = decodeMsg(raw);
    if (m == null) return;
    switch (m['t']) {
      case 'state':
        final v = GameView.fromJson((m['state'] as Map).cast<String, dynamic>());
        if (_view == null || v.version >= _view!.version || v.version == 0) {
          _view = v;
          notifyListeners();
        }
      case 'error':
        toast('${m['msg']}');
      case 'bye':
        _byeReceived = true;
        _closedReason = '${m['msg']}';
    }
  }

  void _onDone() {
    if (_status == ConnectionStatus.closed) return;
    _ws = null;
    if (_byeReceived || _reconnectAttempts >= 6) {
      _status = ConnectionStatus.closed;
      _closedReason ??= 'Connexion perdue avec l\'hôte';
      notifyListeners();
      return;
    }
    _status = ConnectionStatus.reconnecting;
    notifyListeners();
    _reconnectAttempts++;
    Timer(const Duration(seconds: 2), () async {
      if (_status == ConnectionStatus.closed) return;
      try {
        await _open();
      } catch (_) {
        _onDone();
      }
    });
  }

  @override
  void play(List<int> cardIds) => _send({'t': 'play', 'cards': cardIds});
  @override
  void callLiar() => _send({'t': 'call'});
  @override
  void shoot() => _send({'t': 'shoot'});
  @override
  void chooseSkin(int skin) => _send({'t': 'skin', 'skin': skin});

  @override
  Future<void> close() async {
    _status = ConnectionStatus.closed;
    await _ws?.close();
    _ws = null;
  }
}
