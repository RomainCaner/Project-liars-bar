/// Moteur de jeu autoritaire (tourne uniquement chez l'hôte).
///
/// Il ne dépend ni de Flutter ni du réseau : il est entièrement testable.
library;

import 'dart:math';

import 'cards.dart';

enum Phase { lobby, playing, reveal, roulette, gameOver }

class GameError implements Exception {
  final String message;
  GameError(this.message);
  @override
  String toString() => message;
}

class Seat {
  final String id;
  String name;
  bool isBot;
  bool connected;
  bool alive = true;
  List<GameCard> hand = [];

  /// Chambre (0..5) qui contient la balle.
  int bulletChamber = 0;

  /// Nombre de fois où le joueur a appuyé sur la détente.
  int shotsFired = 0;

  Seat({
    required this.id,
    required this.name,
    this.isBot = false,
    this.connected = true,
  });
}

class PlayRecord {
  final String playerId;
  final List<GameCard> cards;
  PlayRecord(this.playerId, this.cards);
}

class RevealInfo {
  final String accuserId;
  final String accusedId;
  final List<GameCard> cards;
  final bool wasLie;
  String get loserId => wasLie ? accusedId : accuserId;

  RevealInfo({
    required this.accuserId,
    required this.accusedId,
    required this.cards,
    required this.wasLie,
  });
}

class GameEngine {
  final Random _rng;
  final List<Seat> seats = [];

  Phase phase = Phase.lobby;
  Rank? tableRank;
  int round = 0;
  int currentIndex = 0;
  PlayRecord? lastPlay;
  int pileCount = 0;
  RevealInfo? reveal;
  String? shooterId;

  /// null tant que le tireur n'a pas appuyé, puis true (mort) / false (clic).
  bool? lastShotDied;
  String? winnerId;
  Map<Rank, int> composition = deckComposition(2);

  /// Incrémenté à chaque modification : permet aux clients d'ignorer les
  /// états obsolètes et à l'UI de détecter les nouveaux événements.
  int version = 0;
  final List<String> log = [];

  GameEngine({Random? random}) : _rng = random ?? Random.secure();

  // ---------------------------------------------------------------- helpers

  Seat seat(String id) => seats.firstWhere(
        (s) => s.id == id,
        orElse: () => throw GameError('Joueur inconnu'),
      );

  Seat? seatOrNull(String id) {
    for (final s in seats) {
      if (s.id == id) return s;
    }
    return null;
  }

  Seat? get currentSeat =>
      phase == Phase.playing && seats.isNotEmpty ? seats[currentIndex] : null;

  List<Seat> get aliveSeats => seats.where((s) => s.alive).toList();

  /// Le joueur n'a pas d'autre choix que d'accuser : tous les autres ont
  /// vidé leur main, poser des cartes ferait revenir le tour vers lui.
  bool mustCall(Seat s) {
    if (lastPlay == null) return false;
    if (s.hand.isEmpty) return true;
    return !seats.any((o) => o != s && o.alive && o.hand.isNotEmpty);
  }

  bool canPlay(Seat s) => s.hand.isNotEmpty && !mustCall(s);

  bool canCall(Seat s) => lastPlay != null && lastPlay!.playerId != s.id;

  void _log(String line) {
    log.add(line);
    if (log.length > 40) log.removeAt(0);
  }

  void _touch() => version++;

  // ------------------------------------------------------------------ lobby

  void addPlayer(String id, String name, {bool isBot = false}) {
    if (phase != Phase.lobby) throw GameError('La partie a déjà commencé');
    if (seats.length >= maxPlayers) throw GameError('La table est pleine');
    if (seatOrNull(id) != null) throw GameError('Joueur déjà présent');
    seats.add(Seat(id: id, name: name, isBot: isBot));
    _log('$name rejoint la table');
    _touch();
  }

  void removePlayer(String id) {
    if (phase != Phase.lobby) throw GameError('La partie a déjà commencé');
    final s = seatOrNull(id);
    if (s == null) return;
    seats.remove(s);
    _log('${s.name} quitte la table');
    _touch();
  }

  /// Un joueur déconnecté en pleine partie est remplacé par un bot
  /// (et reprend la main s'il se reconnecte).
  void setConnected(String id, bool connected) {
    final s = seatOrNull(id);
    if (s == null) return;
    s.connected = connected;
    _log(connected ? '${s.name} est revenu' : '${s.name} s\'est déconnecté');
    _touch();
  }

  // ------------------------------------------------------------------- game

  void startGame() {
    if (phase != Phase.lobby && phase != Phase.gameOver) {
      throw GameError('Partie déjà en cours');
    }
    if (seats.length < minPlayers) {
      throw GameError('Il faut au moins $minPlayers joueurs');
    }
    composition = deckComposition(seats.length);
    for (final s in seats) {
      s.alive = true;
      s.hand = [];
      s.shotsFired = 0;
      s.bulletChamber = _rng.nextInt(revolverChambers);
    }
    round = 0;
    winnerId = null;
    log.clear();
    _log('La partie commence !');
    _startRound(_rng.nextInt(seats.length));
  }

  /// Revient au salon (après une partie) pour en relancer une.
  void backToLobby() {
    phase = Phase.lobby;
    tableRank = null;
    lastPlay = null;
    reveal = null;
    shooterId = null;
    lastShotDied = null;
    winnerId = null;
    pileCount = 0;
    for (final s in seats) {
      s.alive = true;
      s.hand = [];
      s.shotsFired = 0;
    }
    // Les joueurs partis pendant la partie quittent la table.
    seats.removeWhere((s) => !s.isBot && !s.connected);
    _touch();
  }

  int _nextAliveIndex(int from, {bool includeSelf = false}) {
    final n = seats.length;
    for (var k = includeSelf ? 0 : 1; k <= n; k++) {
      final i = (from + k) % n;
      if (seats[i].alive) return i;
    }
    return from;
  }

  void _startRound(int startIndex) {
    round++;
    tableRank = tableRanks[_rng.nextInt(tableRanks.length)];
    final deck = buildDeck(composition)..shuffle(_rng);
    for (final s in seats) {
      s.hand = [];
      if (!s.alive) continue;
      for (var i = 0; i < cardsPerHand && deck.isNotEmpty; i++) {
        s.hand.add(deck.removeLast());
      }
      s.hand.sort((a, b) => a.rank.index.compareTo(b.rank.index));
    }
    lastPlay = null;
    pileCount = 0;
    reveal = null;
    shooterId = null;
    lastShotDied = null;
    currentIndex = _nextAliveIndex(startIndex, includeSelf: true);
    phase = Phase.playing;
    _log('Manche $round — carte du tour : ${tableRank!.label}');
    _touch();
  }

  Seat _requireCurrent(String playerId) {
    if (phase != Phase.playing) throw GameError('Ce n\'est pas le moment');
    final cur = seats[currentIndex];
    if (cur.id != playerId) throw GameError('Ce n\'est pas ton tour');
    return cur;
  }

  void playCards(String playerId, List<int> cardIds) {
    final s = _requireCurrent(playerId);
    if (mustCall(s)) {
      throw GameError('Tu es le dernier à avoir des cartes : tu dois accuser');
    }
    final ids = cardIds.toSet();
    if (ids.isEmpty || ids.length > maxCardsPerPlay) {
      throw GameError('Pose entre 1 et $maxCardsPerPlay cartes');
    }
    final played = s.hand.where((c) => ids.contains(c.id)).toList();
    if (played.length != ids.length) throw GameError('Carte invalide');
    s.hand.removeWhere((c) => ids.contains(c.id));
    lastPlay = PlayRecord(s.id, played);
    pileCount += played.length;
    final word = played.length > 1 ? tableRank!.plural : tableRank!.label;
    _log('${s.name} pose ${played.length} carte${played.length > 1 ? 's' : ''}'
        ' (« ${played.length} $word »)');
    _advanceTurn();
    _touch();
  }

  void _advanceTurn() {
    final n = seats.length;
    for (var k = 1; k <= n; k++) {
      final i = (currentIndex + k) % n;
      final s = seats[i];
      if (s.alive && s.hand.isNotEmpty && s.id != lastPlay?.playerId) {
        currentIndex = i;
        return;
      }
    }
    // Personne d'autre n'a de cartes : le joueur vivant suivant doit accuser.
    currentIndex = _nextAliveIndex(currentIndex);
  }

  void callLiar(String playerId) {
    final s = _requireCurrent(playerId);
    final play = lastPlay;
    if (play == null) throw GameError('Rien à contester');
    if (play.playerId == s.id) throw GameError('Tu ne peux pas t\'accuser');
    final lie = play.cards.any((c) => !c.matches(tableRank!));
    reveal = RevealInfo(
      accuserId: s.id,
      accusedId: play.playerId,
      cards: play.cards,
      wasLie: lie,
    );
    phase = Phase.reveal;
    final accused = seat(play.playerId);
    _log('${s.name} crie MENTEUR sur ${accused.name} !');
    _log(lie
        ? '${accused.name} mentait (${play.cards.map((c) => c.rank.label).join(', ')})'
        : '${accused.name} disait la vérité !');
    _touch();
  }

  /// Après l'affichage des cartes révélées : le perdant prend le revolver.
  void proceedToRoulette() {
    if (phase != Phase.reveal) throw GameError('Ce n\'est pas le moment');
    shooterId = reveal!.loserId;
    lastShotDied = null;
    phase = Phase.roulette;
    _log('${seat(shooterId!).name} prend le revolver…');
    _touch();
  }

  void pullTrigger(String playerId) {
    if (phase != Phase.roulette) throw GameError('Ce n\'est pas le moment');
    if (shooterId != playerId) throw GameError('Ce n\'est pas à toi de tirer');
    if (lastShotDied != null) throw GameError('Déjà tiré');
    final s = seat(playerId);
    final died = s.shotsFired == s.bulletChamber;
    s.shotsFired++;
    lastShotDied = died;
    if (died) {
      s.alive = false;
      _log('PAN ! ${s.name} est éliminé.');
    } else {
      _log('Clic… ${s.name} survit (${s.shotsFired}/$revolverChambers).');
    }
    _touch();
  }

  /// Après l'animation du tir : nouvelle manche ou fin de partie.
  void proceedAfterShot() {
    if (phase != Phase.roulette || lastShotDied == null) {
      throw GameError('Ce n\'est pas le moment');
    }
    final alive = aliveSeats;
    if (alive.length <= 1) {
      phase = Phase.gameOver;
      winnerId = alive.isEmpty ? null : alive.first.id;
      if (alive.isNotEmpty) _log('${alive.first.name} remporte la partie !');
      _touch();
      return;
    }
    final shooterIdx = seats.indexWhere((s) => s.id == shooterId);
    _startRound(shooterIdx);
  }

  // ------------------------------------------------------------ projection

  /// État vu par un joueur donné : on ne divulgue ni les mains adverses,
  /// ni la position des balles.
  Map<String, dynamic> viewFor(String playerId) {
    final me = seatOrNull(playerId);
    final cur = currentSeat;
    return {
      'v': version,
      'phase': phase.index,
      'round': round,
      'table': tableRank?.index,
      'current': cur?.id,
      'me': playerId,
      'players': [
        for (final s in seats)
          {
            'id': s.id,
            'name': s.name,
            'bot': s.isBot,
            'conn': s.connected,
            'alive': s.alive,
            'cards': s.hand.length,
            'shots': s.shotsFired,
          },
      ],
      'hand': me == null ? [] : me.hand.map((c) => c.toJson()).toList(),
      'mustCall': me != null && cur == me && mustCall(me),
      'lastPlay': lastPlay == null
          ? null
          : {'by': lastPlay!.playerId, 'count': lastPlay!.cards.length},
      'pile': pileCount,
      'reveal': reveal == null
          ? null
          : {
              'accuser': reveal!.accuserId,
              'accused': reveal!.accusedId,
              'cards': reveal!.cards.map((c) => c.toJson()).toList(),
              'lie': reveal!.wasLie,
            },
      'shooter': shooterId,
      'shot': lastShotDied,
      'winner': winnerId,
      'deck': {for (final e in composition.entries) '${e.key.index}': e.value},
      'log': log.length > 8 ? log.sublist(log.length - 8) : log,
    };
  }
}
