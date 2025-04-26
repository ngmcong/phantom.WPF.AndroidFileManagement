import 'package:flutter/material.dart';

class BusyIndicator extends StatefulWidget {
  const BusyIndicator({super.key});

  @override
  State<BusyIndicator> createState() => BusyIndicatorState();
}

class BusyIndicatorState extends State<BusyIndicator> {
  String busyText = 'Processing...';
  void updateText(String newText) {
    setState(() {
      busyText = newText;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        const CircularProgressIndicator(),
        const SizedBox(height: 16),
        Text(busyText), // Now within the StatefulBuilder
      ],
    );
  }
}