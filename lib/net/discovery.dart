/// Découverte automatique des parties sur le réseau local (UDP broadcast).
library;

import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'protocol.dart';

class HostAnnouncement {
  final String name;
  final String address;
  final int port;
  final int players;
  final int maxPlayers;
  final bool open;
  DateTime lastSeen;

  HostAnnouncement({
    required this.name,
    required this.address,
    required this.port,
    required this.players,
    required this.maxPlayers,
    required this.open,
    required this.lastSeen,
  });

  String get key => '$address:$port';
}

/// Côté hôte : annonce la partie toutes les secondes.
class DiscoveryBroadcaster {
  RawDatagramSocket? _socket;
  Timer? _timer;
  Map<String, dynamic> info = {};

  Future<void> start() async {
    try {
      _socket = await RawDatagramSocket.bind(InternetAddress.anyIPv4, 0);
      _socket!.broadcastEnabled = true;
    } catch (_) {
      return; // pas de découverte possible : la saisie d'IP reste dispo
    }
    _timer = Timer.periodic(const Duration(seconds: 1), (_) => _send());
    _send();
  }

  Future<void> _send() async {
    final s = _socket;
    if (s == null) return;
    final data = utf8.encode(jsonEncode({'m': discoveryMagic, ...info}));
    for (final target in await broadcastTargets()) {
      try {
        s.send(data, target, discoveryPort);
      } catch (_) {}
    }
  }

  void stop() {
    _timer?.cancel();
    _socket?.close();
    _socket = null;
  }
}

/// Côté invité : écoute les annonces et maintient la liste des parties.
class DiscoveryListener {
  RawDatagramSocket? _socket;
  Timer? _pruneTimer;
  final _hosts = <String, HostAnnouncement>{};
  final _controller = StreamController<List<HostAnnouncement>>.broadcast();

  Stream<List<HostAnnouncement>> get hosts => _controller.stream;
  List<HostAnnouncement> get current => _hosts.values.toList();

  Future<bool> start() async {
    try {
      _socket = await RawDatagramSocket.bind(
        InternetAddress.anyIPv4,
        discoveryPort,
        reuseAddress: true,
      );
    } catch (_) {
      return false;
    }
    _socket!.listen((event) {
      if (event != RawSocketEvent.read) return;
      final dg = _socket?.receive();
      if (dg == null) return;
      try {
        final m = jsonDecode(utf8.decode(dg.data));
        if (m is! Map || m['m'] != discoveryMagic) return;
        final a = HostAnnouncement(
          name: '${m['name']}',
          address: dg.address.address,
          port: (m['port'] as num?)?.toInt() ?? gamePort,
          players: (m['players'] as num?)?.toInt() ?? 0,
          maxPlayers: (m['max'] as num?)?.toInt() ?? 6,
          open: m['open'] == true,
          lastSeen: DateTime.now(),
        );
        _hosts[a.key] = a;
        _emit();
      } catch (_) {}
    });
    _pruneTimer = Timer.periodic(const Duration(seconds: 2), (_) {
      final before = _hosts.length;
      _hosts.removeWhere((_, h) =>
          DateTime.now().difference(h.lastSeen) > const Duration(seconds: 4));
      if (_hosts.length != before) _emit();
    });
    return true;
  }

  void _emit() {
    if (!_controller.isClosed) _controller.add(current);
  }

  void stop() {
    _pruneTimer?.cancel();
    _socket?.close();
    _socket = null;
    _controller.close();
  }
}
