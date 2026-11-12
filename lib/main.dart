import 'package:material_ui/material_ui.dart';
import 'package:flutter/services.dart';

import 'ui/screens/home_screen.dart';
import 'ui/theme.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
  runApp(const MenteurApp());
}

class MenteurApp extends StatelessWidget {
  const MenteurApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Menteur !',
      debugShowCheckedModeBanner: false,
      theme: buildTheme(),
      home: const HomeScreen(),
    );
  }
}
