/// Constantes et utilitaires du protocole réseau local.
///
/// Messages JSON échangés par WebSocket :
///   client → hôte : hello {name, cid} · play {cards} · call · shoot
///   hôte → client : state {state} · error {msg} · bye {msg}
/// Découverte : l'hôte diffuse un datagramme UDP chaque seconde.
library;

import 'dart:convert';
import 'dart:io';

const gamePort = 4040;
const discoveryPort = 4041;
const discoveryMagic = 'MENTEUR/1';

String encodeMsg(Map<String, dynamic> m) => jsonEncode(m);

Map<String, dynamic>? decodeMsg(dynamic raw) {
  if (raw is! String) return null;
  try {
    final v = jsonDecode(raw);
    return v is Map<String, dynamic> ? v : null;
  } catch (_) {
    return null;
  }
}

bool _isPrivate(InternetAddress a) {
  final b = a.rawAddress;
  if (b.length != 4) return false;
  return b[0] == 10 ||
      (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
      (b[0] == 192 && b[1] == 168);
}

/// Adresses IPv4 locales utiles (Wi-Fi / point d'accès), privées en premier.
Future<List<InternetAddress>> localAddresses() async {
  try {
    final ifaces = await NetworkInterface.list(
      type: InternetAddressType.IPv4,
      includeLoopback: false,
    );
    final all = [
      for (final i in ifaces)
        for (final a in i.addresses)
          if (!a.isLinkLocal) a,
    ];
    all.sort((a, b) => (_isPrivate(a) ? 0 : 1).compareTo(_isPrivate(b) ? 0 : 1));
    return all;
  } catch (_) {
    return [];
  }
}

/// Adresses de broadcast à viser : global + dirigé (/24) pour chaque réseau,
/// car certains Android ignorent 255.255.255.255.
Future<List<InternetAddress>> broadcastTargets() async {
  final targets = <String>{'255.255.255.255'};
  for (final a in await localAddresses()) {
    final b = a.rawAddress;
    targets.add('${b[0]}.${b[1]}.${b[2]}.255');
  }
  return targets.map(InternetAddress.new).toList();
}
