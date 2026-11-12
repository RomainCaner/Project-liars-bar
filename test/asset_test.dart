import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

void main() {
  test('la scène 3D compilée est embarquée et autonome', () {
    final f = File('assets/table3d.html');
    expect(f.existsSync(), isTrue, reason: 'lancer npm run build dans web3d/');
    final html = f.readAsStringSync();
    expect(html, contains('table3d'));
    expect(html, isNot(contains('<script src=')), reason: 'aucune dépendance réseau');
    expect(File('pubspec.yaml').readAsStringSync(), contains('assets/table3d.html'));
  });
}
