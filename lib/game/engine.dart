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

/// Réglages choisis par l'hôte dans le salon.
class GameSettings {
  /// Jokers dans le paquet (0, 2 ou 4).
  final int jokers;

  /// Balles dans chaque barillet (1 ou 2).
  final int bullets;

  /// Le barillet est re-tourné avant chaque tir : risque constant.
  final bool respin;

  /// Temps de réflexion par tour, en secondes (0 = illimité).
  final int turnSeconds;

  const GameSettings({
    this.jokers = 2,
    this.bullets = 1,
    this.respin = false,
    this.turnSeconds = 0,
  });

  GameSettings copyWith({int? jokers, int? bullets, bool? respin, int? turnSeconds}) =>
      GameSettings(
        jokers: jokers ?? this.jokers,
        bullets: bullets ?? this.bullets,
        respin: respin ?? this.respin,
        turnSeconds: turnSeconds ?? this.turnSeconds,
      );

  Map<String, dynamic> toJson() =>
      {'jokers': jokers, 'bullets': bullets, 'respin': respin, 'turn': turnSeconds};

  factory GameSettings.fromJson(Map<String, dynamic> j) => GameSettings(
        jokers: (j['jokers'] as num?)?.toInt() ?? 2,
        bullets: (j['bullets'] as num?)?.toInt() ?? 1,
        respin: j['respin'] == true,
        turnSeconds: (j['turn'] as num?)?.toInt() ?? 0,
      );

  /// Probabilité de mourir au prochain tir après [shots] tirs ratés.
  double risk(int shots) {
    if (respin) return bullets / revolverChambers;
    final left = revolverChambers - shots;
    return left <= 0 ? 1 : (bullets / left).clamp(0, 1).toDouble();
  }
}

class Seat {
  final String id;
  String name;
  bool isBot;
  bool connected;
  bool alive = true;
  List<GameCard> hand = [];

  /// Chambres (0..5) qui contiennent une balle.
  Set<int> bulletChambers = {0};

  /// Personnage 3D choisi (0..skinCount-1).
  int skin;

  /// Nombre de fois où le joueur a appuyé sur la détente.
  int shotsFired = 0;

  Seat({
    required this.id,
    required this.name,
    this.isBot = false,
    this.connected = true,
    this.skin = 0,
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
  GameSettings settings = const GameSettings();

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

  void addPlayer(String id, String name, {bool isBot = false, int? skin}) {
    if (phase != Phase.lobby) throw GameError('La partie a déjà commencé');
    if (seats.length >= maxPlayers) throw GameError('La table est pleine');
    if (seatOrNull(id) != null) throw GameError('Joueur déjà présent');
    seats.add(Seat(id: id, name: name, isBot: isBot, skin: skin ?? _freeSkin(random: isBot)));
    _log('$name rejoint la table');
    _touch();
  }

  int _freeSkin({bool random = false}) {
    final used = seats.map((s) => s.skin).toSet();
    final free = [for (var i = 0; i < skinCount; i++) if (!used.contains(i)) i];
    if (free.isEmpty) return seats.length % skinCount;
    return random ? free[_rng.nextInt(free.length)] : free.first;
  }

  /// Choix du personnage (dans le salon uniquement).
  void setSkin(String id, int skin) {
    if (phase != Phase.lobby) throw GameError('La partie a déjà commencé');
    if (skin < 0 || skin >= skinCount) throw GameError('Personnage inconnu');
    seat(id).skin = skin;
    _touch();
  }

  void updateSettings(GameSettings s) {
    if (phase != Phase.lobby && phase != Phase.gameOver) {
      throw GameError('Partie en cours');
    }
    if (![0, 2, 4].contains(s.jokers) || s.bullets < 1 || s.bullets > 2 || s.turnSeconds < 0) {
      throw GameError('Réglage invalide');
    }
    settings = s;
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
    composition = deckComposition(seats.length, jokers: settings.jokers);
    for (final s in seats) {
      s.alive = true;
      s.hand = [];
      s.shotsFired = 0;
      final chambers = List.generate(revolverChambers, (i) => i)..shuffle(_rng);
      s.bulletChambers = chambers.take(settings.bullets).toSet();
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
    final died = settings.respin
        ? _rng.nextInt(revolverChambers) < settings.bullets
        : s.bulletChambers.contains(s.shotsFired);
    s.shotsFired++;
    lastShotDied = died;
    if (died) {
      s.alive = false;
      _log('PAN ! ${s.name} est éliminé.');
    } else {
      _log(settings.respin
          ? 'Clic… ${s.name} survit.'
          : 'Clic… ${s.name} survit (${s.shotsFired}/$revolverChambers).');
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
            'skin': s.skin,
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
      'settings': settings.toJson(),
      'log': log.length > 8 ? log.sublist(log.length - 8) : log,
    };
  }
}
