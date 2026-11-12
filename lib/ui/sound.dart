/// Effets sonores et ambiance (sons synthétisés : voir tool/make_sounds.py).
library;

import 'dart:async';
import 'dart:io';

import 'package:audioplayers/audioplayers.dart';

enum Sfx { card, play, deal, slam, spin, click, bang, turn, truth, lie, win, lose }

class Sound {
  Sound._();

  static bool _on = true;
  static bool get on => _on;

  static final bool _available = !Platform.environment.containsKey('FLUTTER_TEST');
  static final Map<Sfx, AudioPlayer> _players = {};
  static AudioPlayer? _ambience;
  static bool _ambienceWanted = false;
  static Future<void>? _init;

  static Future<void> _ensure() => _init ??= _load();

  static Future<void> _load() async {
    if (!_available) return;
    try {
      for (final s in Sfx.values) {
        final p = AudioPlayer();
        await p.setPlayerMode(PlayerMode.lowLatency);
        await p.setReleaseMode(ReleaseMode.stop);
        await p.setSource(AssetSource('sounds/${s.name}.wav'));
        _players[s] = p;
      }
    } catch (_) {
      // Pas de son (plateforme non gérée) : le jeu continue en silence.
    }
  }

  static Future<void> play(Sfx s, {double volume = 1}) async {
    if (!_on || !_available) return;
    try {
      await _ensure();
      final p = _players[s];
      if (p == null) return;
      await p.stop();
      await p.setVolume(volume);
      await p.resume();
    } catch (_) {}
  }

  /// Joue un son après un délai (synchronisé avec une animation).
  static void later(Duration d, Sfx s, {double volume = 1}) {
    Timer(d, () => play(s, volume: volume));
  }

  static Future<void> startAmbience() async {
    _ambienceWanted = true;
    if (!_on || !_available) return;
    try {
      final p = _ambience ??= AudioPlayer();
      await p.setReleaseMode(ReleaseMode.loop);
      await p.setVolume(0.35);
      await p.play(AssetSource('sounds/ambience.wav'));
    } catch (_) {}
  }

  static Future<void> stopAmbience() async {
    _ambienceWanted = false;
    try {
      await _ambience?.stop();
    } catch (_) {}
  }

  static Future<void> setOn(bool value) async {
    _on = value;
    if (!value) {
      try {
        await _ambience?.stop();
      } catch (_) {}
    } else if (_ambienceWanted) {
      await startAmbience();
    }
  }
}
