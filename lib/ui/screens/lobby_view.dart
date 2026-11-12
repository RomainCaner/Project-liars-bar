import 'package:material_ui/material_ui.dart';

import '../../game/cards.dart';
import '../../game/view.dart';
import '../../net/protocol.dart';
import '../../session/host_session.dart';
import '../../session/session.dart';
import '../theme.dart';

class LobbyView extends StatefulWidget {
  final GameSession session;
  final GameView view;
  final VoidCallback onLeave;

  const LobbyView({
    super.key,
    required this.session,
    required this.view,
    required this.onLeave,
  });

  @override
  State<LobbyView> createState() => _LobbyViewState();
}

class _LobbyViewState extends State<LobbyView> {
  List<String> _ips = [];

  HostSession? get host =>
      widget.session is HostSession ? widget.session as HostSession : null;

  @override
  void initState() {
    super.initState();
    if (host?.online ?? false) {
      localAddresses().then((a) {
        if (mounted) setState(() => _ips = a.map((e) => e.address).toList());
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final v = widget.view;
    final h = host;
    final port = h?.port;
    final portSuffix = port == null || port == gamePort ? '' : ':$port';
    return Column(
      children: [
        Row(
          children: [
            IconButton(onPressed: widget.onLeave, icon: const Icon(Icons.arrow_back)),
            const Expanded(
              child: Text('Le salon',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800)),
            ),
            const SizedBox(width: 48),
          ],
        ),
        Expanded(
          child: ListView(
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
            children: [
              if (h != null && h.online)
                Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: Colors.black38,
                    borderRadius: BorderRadius.circular(16),
                    border: Border.all(color: Palette.gold.withAlpha(120)),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Tes amis te trouvent dans « Rejoindre ».',
                          style: TextStyle(fontWeight: FontWeight.w700)),
                      const SizedBox(height: 6),
                      const Text('Sinon, ils peuvent taper cette adresse :',
                          style: TextStyle(color: Palette.muted)),
                      const SizedBox(height: 6),
                      if (_ips.isEmpty)
                        const Text('Aucun réseau détecté — active le Wi-Fi ou '
                            'le partage de connexion.',
                            style: TextStyle(color: Palette.blood))
                      else
                        for (final ip in _ips)
                          SelectableText('$ip$portSuffix',
                              style: const TextStyle(
                                  fontSize: 24,
                                  fontWeight: FontWeight.w900,
                                  color: Palette.gold,
                                  letterSpacing: 1)),
                    ],
                  ),
                )
              else if (h != null)
                const Text('Mode entraînement : ajoute ou retire des bots puis lance.',
                    style: TextStyle(color: Palette.muted)),
              const SizedBox(height: 20),
              Text('Joueurs (${v.players.length}/$maxPlayers)',
                  style: const TextStyle(
                      color: Palette.gold, fontWeight: FontWeight.w800, fontSize: 16)),
              const SizedBox(height: 8),
              for (final p in v.players)
                Card(
                  color: Colors.black26,
                  margin: const EdgeInsets.only(bottom: 8),
                  child: ListTile(
                    leading: Icon(
                      p.id == HostSession.hostId
                          ? Icons.star
                          : p.isBot
                              ? Icons.smart_toy
                              : Icons.person,
                      color: p.id == HostSession.hostId ? Palette.gold : Palette.muted,
                    ),
                    title: Text(p.id == v.myId ? '${p.name} (toi)' : p.name,
                        style: const TextStyle(fontWeight: FontWeight.w700)),
                    subtitle: Text(p.id == HostSession.hostId
                        ? 'Hôte'
                        : p.isBot
                            ? 'Bot'
                            : 'Joueur'),
                    trailing: h != null && p.id != HostSession.hostId
                        ? IconButton(
                            icon: const Icon(Icons.close),
                            tooltip: 'Retirer',
                            onPressed: () => h.kick(p.id),
                          )
                        : null,
                  ),
                ),
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 8, 20, 20),
          child: h == null
              ? const Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2)),
                    SizedBox(width: 12),
                    Text('En attente que l\'hôte lance la partie…'),
                  ],
                )
              : Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    OutlinedButton.icon(
                      onPressed: v.players.length < maxPlayers ? h.addBot : null,
                      icon: const Icon(Icons.smart_toy),
                      label: const Text('Ajouter un bot'),
                    ),
                    const SizedBox(height: 10),
                    FilledButton(
                      onPressed: v.players.length >= minPlayers ? h.startGame : null,
                      child: Text(v.players.length >= minPlayers
                          ? 'LANCER LA PARTIE'
                          : 'IL FAUT AU MOINS $minPlayers JOUEURS'),
                    ),
                  ],
                ),
        ),
      ],
    );
  }
}
