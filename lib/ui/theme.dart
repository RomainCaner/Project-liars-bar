import 'package:material_ui/material_ui.dart';

class Palette {
  static const wood = Color(0xFF1A110B);
  static const woodLight = Color(0xFF2B1C12);
  static const felt = Color(0xFF184634);
  static const feltDark = Color(0xFF0F2E22);
  static const gold = Color(0xFFD9AE52);
  static const cream = Color(0xFFF4EAD3);
  static const blood = Color(0xFFC0392B);
  static const ink = Color(0xFF241A14);
  static const muted = Color(0xFFA89A86);
  static const truth = Color(0xFF4CAF7A);
}

ThemeData buildTheme() {
  final base = ThemeData(
    brightness: Brightness.dark,
    useMaterial3: true,
    fontFamily: 'Roboto',
    colorScheme: ColorScheme.fromSeed(
      seedColor: Palette.gold,
      brightness: Brightness.dark,
      primary: Palette.gold,
      secondary: Palette.blood,
      surface: Palette.woodLight,
    ),
    scaffoldBackgroundColor: Palette.wood,
  );
  return base.copyWith(
    appBarTheme: const AppBarTheme(
      backgroundColor: Colors.transparent,
      elevation: 0,
      centerTitle: true,
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: Palette.gold,
        foregroundColor: Palette.ink,
        minimumSize: const Size(0, 52),
        textStyle: const TextStyle(fontFamily: 'Roboto', 
            fontWeight: FontWeight.w800, fontSize: 16, letterSpacing: 0.5),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: Palette.cream,
        minimumSize: const Size(0, 52),
        side: const BorderSide(color: Palette.gold, width: 1.2),
        textStyle: const TextStyle(fontFamily: 'Roboto', fontWeight: FontWeight.w700, fontSize: 15),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      ),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Palette.woodLight,
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: BorderSide.none,
      ),
    ),
    snackBarTheme: const SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
      backgroundColor: Palette.woodLight,
      contentTextStyle: TextStyle(color: Palette.cream),
    ),
  );
}

/// Fond « table de bar » : feutre vert avec vignettage.
class FeltBackground extends StatelessWidget {
  final Widget child;
  const FeltBackground({super.key, required this.child});

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: const BoxDecoration(
        gradient: RadialGradient(
          center: Alignment(0, -0.1),
          radius: 1.1,
          colors: [Palette.felt, Palette.feltDark, Palette.wood],
          stops: [0, 0.65, 1],
        ),
      ),
      child: child,
    );
  }
}
