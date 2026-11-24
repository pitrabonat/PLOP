# Production campaign generation — Phase 8 (quality search)

Seed `0x504C4F5021`, generator v3, quality search v1, campaign plan v1, solver v1, .NET 10.0.15, 10 worker threads.

| Metric | Value |
|---|---|
| Campaign levels | 100 (10 authored tutorials + 90 quality-searched) |
| Board evaluations (exhaustive analyses) | 15053 |
| Slot searches that needed a retry (failed/duplicate) | 1 |
| Daily pool | 60 puzzles |
| Wall time | 76.6 s parallel slot search, 80.2 s total |
| Strict campaign verification (structure, canonical IDs, duplicates, shortest replay, optimalMoves, Undo/Restart) | PASS |
| Daily pool verification + optimalMoves | PASS |
| Sequential re-search of slots 11, 48, 100 | identical |

Per-level player-model metrics: see `campaign_audit_phase8.md`.
