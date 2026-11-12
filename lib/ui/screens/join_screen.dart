import 'dart:async';

import 'package:material_ui/material_ui.dart';

import '../../net/discovery.dart';
import '../../net/platform.dart';
import '../../net/protocol.dart';
import '../../session/client_session.dart';
import '../theme.dart';
import 'table_screen.dart';

class JoinScreen extends StatefulWidget {
  final String pseudo;
  const JoinScreen({super.key, required this.pseudo});

  @override
  State<JoinScreen> createState() => _JoinScreenState();
}

class _JoinScreenState extends State<JoinScreen> {
  final _ip = TextEditingController();
  DiscoveryListener? _listener;
  StreamSubscription<List<HostAnnouncement>>? _sub;
  List<HostAnnouncement> _hosts = [];
  bool _listening = false;
  bool _connecting = false;

  @override
  void initState() {
    super.initState();
    _startDiscovery();
  }

  Future<void> _startDiscovery() async {
    await acquireMulticastLock();
    final l = DiscoveryListener();
    final ok = await l.start();
    if (!mounted) {
      l.stop();
      return;
    }
    _listener = l;
    _sub = l.hosts.listen((h) => setState(() => _hosts = h));
    setState(() => _listening = ok);
  }

  @override
  void dispose() {
    _sub?.cancel();
    _listener?.stop();
    releaseMulticastLock();
    _ip.dispose();
    super.dispose();
  }

  Future<void> _connect(String host, int port) async {
    if (_connecting) return;
    setState(() => _connecting = true);
    final session = ClientSession(host: host, port: port, name: widget.pseudo);
    try {
      await session.connect();
    } catch (_) {
      if (mounted) {
        setState(() => _connecting = false);
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: Text('Impossible de joindre $host:$port. Vérifie que vous êtes '
              'sur le même réseau.'),
        ));
      }
      return;
    }
    if (!mounted) return;
    setState(() => _connecting = false);
    await Navigator.of(context).pushReplacement(
      MaterialPageRoute(builder: (_) => TableScreen(session: session)),
    );
  }

  void _connectManual() {
    var text = _ip.text.trim();
    if (text.isEmpty) return;
    var port = gamePort;
    if (text.contains(':')) {
      final parts = text.split(':');
      text = parts.first;
      port = int.tryParse(parts.last) ?? gamePort;
    }
    _connect(text, port);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Rejoindre une partie')),
      body: FeltBackground(
        child: ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Row(
              children: [
                const Text('Parties à proximité',
                    style: TextStyle(
                        color: Palette.gold, fontWeight: FontWeight.w800, fontSize: 17)),
                const Spacer(),
                if (_listening)
                  const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2)),
              ],
            ),
            const SizedBox(height: 10),
            if (_hosts.isEmpty)
              Container(
                padding: const EdgeInsets.all(18),
                decoration: BoxDecoration(
                  color: Colors.black26,
                  borderRadius: BorderRadius.circular(14),
                ),
                child: Text(
                  _listening
                      ? 'Recherche en cours…\nDemande à l\'hôte de créer la partie '
                          'et d\'être sur le même Wi-Fi que toi.'
                      : 'Recherche automatique indisponible : utilise l\'adresse IP.',
                  style: const TextStyle(color: Palette.muted, height: 1.4),
                ),
              ),
            for (final h in _hosts)
              Card(
                color: Colors.black38,
                margin: const EdgeInsets.only(bottom: 10),
                child: ListTile(
                  leading: const Icon(Icons.table_bar, color: Palette.gold),
                  title: Text('Table de ${h.name}',
                      style: const TextStyle(fontWeight: FontWeight.w700)),
                  subtitle: Text(
                    '${h.players}/${h.maxPlayers} joueurs · ${h.address}'
                    '${h.open ? '' : ' · en cours'}',
                  ),
                  trailing: h.open
                      ? const Icon(Icons.chevron_right)
                      : const Icon(Icons.lock, color: Palette.muted),
                  onTap: _connecting ? null : () => _connect(h.address, h.port),
                ),
              ),
            const SizedBox(height: 28),
            const Text('Ou par adresse IP',
                style: TextStyle(
                    color: Palette.gold, fontWeight: FontWeight.w800, fontSize: 17)),
            const SizedBox(height: 10),
            TextField(
              controller: _ip,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              decoration: const InputDecoration(
                hintText: 'ex : 192.168.1.23',
                prefixIcon: Icon(Icons.lan),
              ),
              onSubmitted: (_) => _connectManual(),
            ),
            const SizedBox(height: 12),
            FilledButton(
              onPressed: _connecting ? null : _connectManual,
              child: _connecting
                  ? const SizedBox(
                      width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2.5))
                  : const Text('SE CONNECTER'),
            ),
          ],
        ),
      ),
    );
  }
}
