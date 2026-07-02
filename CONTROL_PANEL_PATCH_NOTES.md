# AKGF Control Panel Patch

This patch adds one central editor window:

`Tools > Azure Kinect Gesture Framework > AKGF Control Panel`

Use it to control the most important runtime/debug settings from one place:

- Tracking mode
- Skeleton/Kinect source assignment
- Static pose recognizer settings
- Sequence recognizer settings
- Coordinator/result-output settings
- Gesture settings database defaults
- Per-gesture threshold/hold/cooldown/group/phase settings
- Gesture groups
- Recording hotkeys and durations
- Debug overlays
- MultiUser settings
- Runtime candidate/final-output status

For the current debugging issue where candidates appear but no result is printed, open the panel and click:

`SingleUser Debug Mode`

This sets permissive thresholds, zero cooldowns, enabled output phases, active Default group, and reconnects the coordinator/API.
