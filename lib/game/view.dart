/// Vue d'une partie telle que reçue par un joueur (hôte compris).
library;

import 'cards.dart';
import 'engine.dart' show GameSettings, Phase;

class PlayerView {
  final String id;
  final String name;
  final bool isBot;
  final bool connected;
  final bool alive;
  final int cardCount;
  final int shots;
  final int skin;

  PlayerView.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String,
        name = j['name'] as String,
        isBot = j['bot'] as bool,
        connected = j['conn'] as bool,
        alive = j['alive'] as bool,
        cardCount = j['cards'] as int,
        shots = j['shots'] as int,
        skin = (j['skin'] as num?)?.toInt() ?? 0;
}

class RevealView {
  final String accuserId;
  final String accusedId;
  final List<GameCard> cards;
  final bool wasLie;
  String get loserId => wasLie ? accusedId : accuserId;

  RevealView.fromJson(Map<String, dynamic> j)
      : accuserId = j['accuser'] as String,
        accusedId = j['accused'] as String,
        cards = [
          for (final c in j['cards'] as List)
            GameCard.fromJson((c as Map).cast<String, dynamic>()),
        ],
        wasLie = j['lie'] as bool;
}

class GameView {
  /// JSON d'origine (transmis tel quel à la scène 3D).
  final Map<String, dynamic> raw;
  final int version;
  final Phase phase;
  final int round;
  final Rank? tableRank;
  final String? currentId;
  final String myId;
  final List<PlayerView> players;
  final List<GameCard> hand;
  final bool mustCall;
  final String? lastPlayBy;
  final int lastPlayCount;
  final int pile;
  final RevealView? reveal;
  final String? shooterId;
  final bool? shotDied;
  final String? winnerId;
  final Map<Rank, int> deck;
  final List<String> log;
  final GameSettings settings;

  /// Temps restant pour l'action en cours (minuteur de tour), à réception.
  final int? turnLeftMs;
  final DateTime receivedAt;

  GameView.fromJson(Map<String, dynamic> j)
      : raw = j,
        version = j['v'] as int,
        phase = Phase.values[j['phase'] as int],
        round = j['round'] as int,
        tableRank = j['table'] == null ? null : Rank.values[j['table'] as int],
        currentId = j['current'] as String?,
        myId = j['me'] as String,
        players = [
          for (final p in j['players'] as List)
            PlayerView.fromJson((p as Map).cast<String, dynamic>()),
        ],
        hand = [
          for (final c in j['hand'] as List)
            GameCard.fromJson((c as Map).cast<String, dynamic>()),
        ],
        mustCall = j['mustCall'] as bool,
        lastPlayBy = (j['lastPlay'] as Map?)?['by'] as String?,
        lastPlayCount = ((j['lastPlay'] as Map?)?['count'] as int?) ?? 0,
        pile = j['pile'] as int,
        reveal = j['reveal'] == null
            ? null
            : RevealView.fromJson((j['reveal'] as Map).cast<String, dynamic>()),
        shooterId = j['shooter'] as String?,
        shotDied = j['shot'] as bool?,
        winnerId = j['winner'] as String?,
        deck = {
          for (final e in (j['deck'] as Map).entries)
            Rank.values[int.parse(e.key as String)]: e.value as int,
        },
        log = [for (final l in j['log'] as List) l as String],
        settings = GameSettings.fromJson(
            ((j['settings'] as Map?) ?? const {}).cast<String, dynamic>()),
        turnLeftMs = (j['turnLeft'] as num?)?.toInt(),
        receivedAt = DateTime.now();

  /// Secondes restantes au minuteur, ou null s'il n'y en a pas.
  int? get secondsLeft {
    final ms = turnLeftMs;
    if (ms == null) return null;
    final left = ms - DateTime.now().difference(receivedAt).inMilliseconds;
    return (left / 1000).ceil().clamp(0, 999);
  }

  /// Risque de mourir au prochain tir pour ce joueur.
  double riskFor(PlayerView p) => settings.risk(p.shots);

  PlayerView? player(String? id) {
    if (id == null) return null;
    for (final p in players) {
      if (p.id == id) return p;
    }
    return null;
  }

  PlayerView? get me => player(myId);
  bool get isMyTurn => phase == Phase.playing && currentId == myId;
  bool get canCall =>
      isMyTurn && lastPlayBy != null && lastPlayBy != myId;
  bool get canPlay => isMyTurn && !mustCall && hand.isNotEmpty;
  bool get iMustShoot =>
      phase == Phase.roulette && shooterId == myId && shotDied == null;

  String nameOf(String? id) => player(id)?.name ?? '?';
}
