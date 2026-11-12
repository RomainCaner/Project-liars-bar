/// Scène 3D (three.js dans une WebView), pilotée par l'état de la partie.
library;

import 'dart:convert';

import 'package:flutter/services.dart';
import 'package:material_ui/material_ui.dart';
import 'package:webview_flutter/webview_flutter.dart';

import '../../game/view.dart';

/// La 3D n'est possible que là où une WebView existe (Android, iOS).
bool get scene3dSupported => WebViewPlatform.instance != null;

String? _htmlCache;

class Table3DScene extends StatefulWidget {
  final GameView view;
  final Set<int> selected;
  final bool canSelect;

  /// Hauteur (en dp) des panneaux Flutter posés par-dessus la scène.
  final double insetTop;
  final double insetBottom;
  final ValueChanged<int> onCardTap;
  final VoidCallback onFailure;

  const Table3DScene({
    super.key,
    required this.view,
    required this.selected,
    required this.canSelect,
    required this.insetTop,
    required this.insetBottom,
    required this.onCardTap,
    required this.onFailure,
  });

  @override
  State<Table3DScene> createState() => _Table3DSceneState();
}

class _Table3DSceneState extends State<Table3DScene> {
  late final WebViewController _controller;
  bool _ready = false;
  String? _lastPushed;

  @override
  void initState() {
    super.initState();
    _controller = WebViewController()
      ..setJavaScriptMode(JavaScriptMode.unrestricted)
      ..setBackgroundColor(const Color(0xFF0D0806))
      ..addJavaScriptChannel('Bridge', onMessageReceived: _onMessage)
      ..setNavigationDelegate(NavigationDelegate(
        onPageFinished: (_) => _markReady(),
        onWebResourceError: (e) {
          if (e.isForMainFrame ?? true) widget.onFailure();
        },
      ));
    _load();
  }

  Future<void> _load() async {
    try {
      _htmlCache ??= await rootBundle.loadString('assets/table3d.html');
      await _controller.loadHtmlString(_htmlCache!);
    } catch (_) {
      widget.onFailure();
    }
  }

  void _onMessage(JavaScriptMessage msg) {
    final m = jsonDecode(msg.message);
    if (m is! Map) return;
    switch (m['t']) {
      case 'ready':
        _markReady();
      case 'tap':
        if (m['id'] is int) widget.onCardTap(m['id'] as int);
    }
  }

  void _markReady() {
    if (_ready) return;
    _ready = true;
    _push();
  }

  void _push() {
    if (!_ready) return;
    final state = {
      ...widget.view.raw,
      'selected': widget.selected.toList(),
      'canSelect': widget.canSelect,
      'insets': {'top': widget.insetTop, 'bottom': widget.insetBottom},
    };
    final json = jsonEncode(state);
    if (json == _lastPushed) return;
    _lastPushed = json;
    _controller.runJavaScript('window.table3d && window.table3d.update($json)').catchError((_) {});
  }

  @override
  void didUpdateWidget(Table3DScene old) {
    super.didUpdateWidget(old);
    _push();
  }

  @override
  Widget build(BuildContext context) => WebViewWidget(controller: _controller);
}
