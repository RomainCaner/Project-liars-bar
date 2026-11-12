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

File get _settingsFile => File('${Directory.systemTemp.path}/menteur_3d.txt');

/// Table en 3D (par défaut) ou en 2D (téléphones peu puissants).
bool use3d = true;

Future<void> loadSettings() async {
  try {
    use3d = (await _settingsFile.readAsString()).trim() != '0';
  } catch (_) {}
}

Future<void> saveSettings() async {
  try {
    await _settingsFile.writeAsString(use3d ? '1' : '0');
  } catch (_) {}
}
