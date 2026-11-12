import 'package:material_ui/material_ui.dart';

import '../../session/host_session.dart';
import '../prefs.dart';
import '../theme.dart';
import '../widgets/playing_card.dart';
import '../../game/cards.dart';
import 'join_screen.dart';
import 'rules_screen.dart';
import 'table_screen.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  final _name = TextEditingController();
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    loadPseudo().then((n) {
      if (mounted && _name.text.isEmpty) _name.text = n;
    });
  }

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  String? _pseudo() {
    final n = _name.text.trim();
    if (n.isEmpty) {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Choisis d\'abord un pseudo')));
      return null;
    }
    savePseudo(n);
    return n.length > 16 ? n.substring(0, 16) : n;
  }

  Future<void> _host({required bool online}) async {
    final n = _pseudo();
    if (n == null || _busy) return;
    setState(() => _busy = true);
    final session = HostSession(hostName: n, online: online);
    try {
      await session.startNetwork();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('Impossible d\'ouvrir le serveur : $e')));
      }
    }
    if (!online) {
      session.addBot();
      session.addBot();
    }
    if (!mounted) return;
    setState(() => _busy = false);
    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => TableScreen(session: session)),
    );
  }

  void _join() {
    final n = _pseudo();
    if (n == null) return;
    Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => JoinScreen(pseudo: n)),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: FeltBackground(
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 420),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const SizedBox(height: 12),
                    SizedBox(
                      height: 110,
                      child: Stack(
                        alignment: Alignment.center,
                        children: [
                          for (final (i, r) in [Rank.king, Rank.joker, Rank.queen].indexed)
                            Transform.translate(
                              offset: Offset((i - 1) * 44.0, i == 1 ? -6 : 4),
                              child: Transform.rotate(
                                angle: (i - 1) * 0.22,
                                child: PlayingCardView(rank: r, width: 66),
                              ),
                            ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 18),
                    const Text(
                      'MENTEUR !',
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        fontSize: 44,
                        fontWeight: FontWeight.w900,
                        color: Palette.gold,
                        letterSpacing: 3,
                        shadows: [Shadow(color: Colors.black, blurRadius: 12)],
                      ),
                    ),
                    const Text(
                      'Bluff, cartes et barillet',
                      textAlign: TextAlign.center,
                      style: TextStyle(color: Palette.muted, fontSize: 15),
                    ),
                    const SizedBox(height: 32),
                    TextField(
                      controller: _name,
                      maxLength: 16,
                      textCapitalization: TextCapitalization.words,
                      decoration: const InputDecoration(
                        labelText: 'Ton pseudo',
                        prefixIcon: Icon(Icons.person),
                        counterText: '',
                      ),
                    ),
                    const SizedBox(height: 20),
                    FilledButton.icon(
                      onPressed: _busy ? null : () => _host(online: true),
                      icon: const Icon(Icons.wifi_tethering),
                      label: const Text('CRÉER UNE PARTIE'),
                    ),
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed: _busy ? null : _join,
                      icon: const Icon(Icons.login),
                      label: const Text('REJOINDRE UNE PARTIE'),
                    ),
                    const SizedBox(height: 12),
                    OutlinedButton.icon(
                      onPressed: _busy ? null : () => _host(online: false),
                      icon: const Icon(Icons.smart_toy),
                      label: const Text('Entraînement contre des bots'),
                    ),
                    const SizedBox(height: 8),
                    TextButton.icon(
                      onPressed: () => Navigator.of(context).push(
                        MaterialPageRoute(builder: (_) => const RulesScreen()),
                      ),
                      icon: const Icon(Icons.menu_book, color: Palette.muted),
                      label: const Text('Règles du jeu',
                          style: TextStyle(color: Palette.muted)),
                    ),
                    const SizedBox(height: 16),
                    const Text(
                      'Tous les téléphones doivent être sur le même Wi-Fi '
                      '(ou sur le partage de connexion de l\'hôte).',
                      textAlign: TextAlign.center,
                      style: TextStyle(color: Colors.white38, fontSize: 12),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
