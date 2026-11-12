/// Cartes et composition du paquet.
library;

enum Rank { king, queen, ace, joker }

extension RankLabels on Rank {
  String get label => switch (this) {
        Rank.king => 'Roi',
        Rank.queen => 'Dame',
        Rank.ace => 'As',
        Rank.joker => 'Joker',
      };

  String get plural => switch (this) {
        Rank.king => 'Rois',
        Rank.queen => 'Dames',
        Rank.ace => 'As',
        Rank.joker => 'Jokers',
      };

  String get short => switch (this) {
        Rank.king => 'R',
        Rank.queen => 'D',
        Rank.ace => 'A',
        Rank.joker => 'J',
      };
}

/// Les rangs qui peuvent être tirés comme « carte du tour ».
const tableRanks = [Rank.king, Rank.queen, Rank.ace];

class GameCard {
  final int id;
  final Rank rank;

  const GameCard(this.id, this.rank);

  /// Une carte « dit la vérité » si c'est la carte du tour ou un joker.
  bool matches(Rank tableRank) => rank == tableRank || rank == Rank.joker;

  Map<String, dynamic> toJson() => {'id': id, 'r': rank.index};

  factory GameCard.fromJson(Map<String, dynamic> j) =>
      GameCard(j['id'] as int, Rank.values[j['r'] as int]);

  @override
  bool operator ==(Object other) =>
      other is GameCard && other.id == id && other.rank == rank;

  @override
  int get hashCode => Object.hash(id, rank);

  @override
  String toString() => '${rank.short}#$id';
}

const cardsPerHand = 5;
const maxCardsPerPlay = 3;
const minPlayers = 2;
const maxPlayers = 6;
const revolverChambers = 6;

/// Composition du paquet selon le nombre de joueurs.
///
/// Jusqu'à 4 joueurs : 6 Rois, 6 Dames, 6 As, 2 Jokers (20 cartes).
/// Au-delà, on ajoute des cartes pour que chacun en reçoive 5.
Map<Rank, int> deckComposition(int playerCount) {
  final jokers = playerCount <= 4 ? 2 : 3;
  final needed = playerCount * cardsPerHand - jokers;
  var perRank = (needed / 3).ceil();
  if (perRank < 6) perRank = 6;
  return {
    Rank.king: perRank,
    Rank.queen: perRank,
    Rank.ace: perRank,
    Rank.joker: jokers,
  };
}

List<GameCard> buildDeck(Map<Rank, int> composition) {
  final deck = <GameCard>[];
  var id = 0;
  for (final rank in Rank.values) {
    for (var i = 0; i < (composition[rank] ?? 0); i++) {
      deck.add(GameCard(id++, rank));
    }
  }
  return deck;
}
