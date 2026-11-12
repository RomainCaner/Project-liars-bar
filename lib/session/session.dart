/// Abstraction commune hôte / invité consommée par l'interface.
library;

import 'dart:async';
import 'dart:math';

import 'package:flutter/foundation.dart';

import '../game/view.dart';

enum ConnectionStatus { connecting, connected, reconnecting, closed }

abstract class GameSession extends ChangeNotifier {
  GameView? get view;
  bool get isHost;
  ConnectionStatus get status;

  /// Raison de la fermeture (affichée si la connexion est perdue).
  String? get closedReason;

  final _toasts = StreamController<String>.broadcast();
  Stream<String> get toasts => _toasts.stream;
  @protected
  void toast(String msg) {
    if (!_toasts.isClosed) _toasts.add(msg);
  }

  void play(List<int> cardIds);
  void callLiar();
  void shoot();
  Future<void> close();

  bool _disposed = false;

  @override
  void notifyListeners() {
    if (!_disposed) super.notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _toasts.close();
    super.dispose();
  }
}

/// Identifiant stable pendant la vie de l'app : permet de se reconnecter
/// à sa place après une coupure Wi-Fi.
final String clientId = () {
  final r = Random.secure();
  return List.generate(12, (_) => r.nextInt(36).toRadixString(36)).join();
}();
