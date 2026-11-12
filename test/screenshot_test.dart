// Génère les captures d'écran de docs/ (dans test/shots/, non commité) :
//   flutter test --tags screenshots --run-skipped --update-goldens
@Tags(['screenshots'])
library;

import 'dart:io';
import 'dart:math';

import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:material_ui/material_ui.dart';
import 'package:menteur/game/cards.dart';
import 'package:menteur/game/engine.dart';
import 'package:menteur/main.dart';
import 'package:menteur/session/host_session.dart';
import 'package:menteur/ui/screens/table_screen.dart';
import 'package:menteur/ui/theme.dart';

Future<void> loadFonts() async {
  // flutter_tester vit dans bin/cache/artifacts/engine/<plateforme>/.
  final dir = '${File(Platform.resolvedExecutable).parent.parent.parent.path}/material_fonts';
  final roboto = FontLoader('Roboto');
  for (final f in ['Regular', 'Medium', 'Bold', 'Black']) {
    roboto.addFont(File('$dir/Roboto-$f.ttf').readAsBytes().then((b) => ByteData.view(b.buffer)));
  }
  await roboto.load();
  final icons = FontLoader('MaterialIcons')
    ..addFont(File('$dir/MaterialIcons-Regular.otf').readAsBytes().then((b) => ByteData.view(b.buffer)));
  await icons.load();
}

void main() {
  setUpAll(loadFonts);

  Future<void> phone(WidgetTester t) async {
    t.view.physicalSize = const Size(1080, 2280);
    t.view.devicePixelRatio = 2.75;
  }

  testWidgets('home', (t) async {
    await phone(t);
    await t.pumpWidget(const MenteurApp());
    await t.pump(const Duration(seconds: 1));
    await expectLater(find.byType(MaterialApp), matchesGoldenFile('shots/1_home.png'));
  });

  testWidgets('table', (t) async {
    await phone(t);
    late HostSession s;
    await t.runAsync(() async {
      s = HostSession(hostName: 'Romain', online: false, random: Random(5));
    });
    s.addBot();
    s.addBot();
    s.addBot();
    await t.pumpWidget(MaterialApp(theme: buildTheme(), home: TableScreen(session: s)));
    await t.pump(const Duration(milliseconds: 500));
    // Les images d'assets se décodent hors du temps simulé.
    await t.runAsync(() async {
      final ctx = t.element(find.byType(TableScreen));
      for (var i = 0; i < 5; i++) {
        await precacheImage(AssetImage('assets/portraits/$i.png'), ctx);
      }
    });
    await t.pump(const Duration(milliseconds: 100));
    await expectLater(find.byType(MaterialApp), matchesGoldenFile('shots/2_lobby.png'));

    final e = s.engine;
    e.startGame();
    e.currentIndex = 0;
    e.tableRank = Rank.queen;
    e.seat('host').hand = const [
      GameCard(1, Rank.king), GameCard(2, Rank.queen), GameCard(3, Rank.queen),
      GameCard(4, Rank.ace), GameCard(5, Rank.joker),
    ];
    e.lastPlay = PlayRecord(e.seats.last.id, const [GameCard(9, Rank.ace), GameCard(10, Rank.ace)]);
    e.pileCount = 4;
    e.seats[1].shotsFired = 2;
    e.seats[2].hand = e.seats[2].hand.take(2).toList();
    // Empêche les bots de jouer pendant la capture.
    e.seats.last.hand = e.seats.last.hand.take(3).toList();
    s.forceRefresh();
    await t.pump(const Duration(milliseconds: 300));
    await t.tap(find.text('DAME').at(1));
    await t.pump(const Duration(milliseconds: 300));
    await expectLater(find.byType(MaterialApp), matchesGoldenFile('shots/3_my_turn.png'));

    e.callLiar('host');
    s.forceRefresh();
    for (var i = 0; i < 50; i++) {
      await t.pump(const Duration(milliseconds: 50));
    }
    await expectLater(find.byType(MaterialApp), matchesGoldenFile('shots/4_reveal.png'));

    e.proceedToRoulette();
    s.forceRefresh();
    await t.pump(const Duration(milliseconds: 300));
    await expectLater(find.byType(MaterialApp), matchesGoldenFile('shots/5_roulette.png'));

    final shooter = e.seat(e.shooterId!);
    shooter.bulletChambers = {shooter.shotsFired};
    e.pullTrigger(shooter.id);
    s.forceRefresh();
    for (var i = 0; i < 25; i++) {
      await t.pump(const Duration(milliseconds: 50));
    }
    await expectLater(find.byType(MaterialApp), matchesGoldenFile('shots/6_bang.png'));

    for (final x in e.seats.skip(1)) {
      x.alive = false;
    }
    e.phase = Phase.gameOver;
    e.winnerId = 'host';
    s.forceRefresh();
    await t.pump(const Duration(milliseconds: 300));
    await expectLater(find.byType(MaterialApp), matchesGoldenFile('shots/7_win.png'));
    await t.pumpWidget(const SizedBox());
    await t.pump(const Duration(seconds: 5));
  });
}
