/// Mémorise le pseudo sans dépendance externe.
library;

import 'dart:io';

File get _file => File('${Directory.systemTemp.path}/menteur_pseudo.txt');

Future<String> loadPseudo() async {
  try {
    return (await _file.readAsString()).trim();
  } catch (_) {
    return '';
  }
}

Future<void> savePseudo(String name) async {
  try {
    await _file.writeAsString(name);
  } catch (_) {}
}
