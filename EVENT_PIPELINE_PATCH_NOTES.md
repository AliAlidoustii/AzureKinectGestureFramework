# AKGF Event Pipeline Patch

This patch fixes a case where candidates appeared in the debug UI but no final result/API/Console output fired.

Cause: AkgfGestureCoordinator and AkgfGestureSystemApi could mark themselves as subscribed even when their target recognizer/coordinator references were still null during OnEnable. Later, candidates were visible through polling, but event subscriptions were missing.

Fix: event subscriptions now track static/sequence and single/multi links separately, and missing links are reconnected during LateUpdate.
