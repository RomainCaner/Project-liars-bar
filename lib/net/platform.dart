/// Petits services natifs Android (voir MainActivity.kt).
library;

import 'package:flutter/services.dart';

const _channel = MethodChannel('menteur/net');

/// Android filtre les paquets broadcast en Wi-Fi sans « multicast lock ».
Future<void> acquireMulticastLock() async {
  try {
    await _channel.invokeMethod('acquireMulticastLock');
  } catch (_) {/* pas sur Android / non dispo : on ignore */}
}

Future<void> releaseMulticastLock() async {
  try {
    await _channel.invokeMethod('releaseMulticastLock');
  } catch (_) {}
}
