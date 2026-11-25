# Phase 10 campaign — 160 levels, nine Worlds

Seed `0x504C4F5031300000`, quality search v2, expanded plan v1, solver v1. Parallel search 83 s, total 89 s on 10 threads, 65948 candidate searches.

Strict verification: **passed** (0 errors, 0 warnings). Sequential re-search of sampled slots identical: **True**.

| World | Levels | Authored | Generated | Phase 8 classic | Avg optimal | Avg challenge | Greedy solves | Uses Frost | Breeze | Lava (hazard) | Loops |
|---|---|---|---|---|---|---|---|---|---|---|---|
| cozy-nest | 1–6 | 5 | 0 | 1 | 3.8 | 8.3 | 4 | 0 | 0 | 0 | 0 |
| frosted-pond | 7–20 | 2 | 12 | 0 | 7.9 | 24.9 | 4 | 14 | 0 | 0 | 0 |
| breezy-meadow | 21–34 | 2 | 12 | 0 | 8.1 | 28.0 | 3 | 4 | 14 | 0 | 0 |
| ember-hollow | 35–48 | 2 | 12 | 0 | 9.2 | 33.8 | 2 | 7 | 4 | 14 | 0 |
| prism-observatory | 49–63 | 1 | 14 | 0 | 8.7 | 34.9 | 1 | 1 | 0 | 7 | 15 |
| dewdrop-garden | 64–85 | 0 | 19 | 3 | 10.6 | 40.6 | 0 | 10 | 9 | 10 | 9 |
| rainy-window | 86–108 | 0 | 20 | 3 | 11.6 | 47.5 | 0 | 11 | 10 | 10 | 10 |
| twilight-attic | 109–133 | 0 | 22 | 3 | 13.3 | 53.7 | 0 | 12 | 13 | 15 | 16 |
| moonlit-clouds | 134–160 | 0 | 24 | 3 | 14.6 | 61.1 | 0 | 18 | 13 | 19 | 19 |

## Per level

| # | World | Kind | Board | Plops | Tiles | Opt | Limit | Challenge | Category | Greedy | Hazards | Mech steps | Slides/Pushes/Teleports | Id |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | cozy-nest | Authored | 4×4 | 1 | – | 1 | – | 0.0 | Tutorial | solves | 0 | 0 | 0/0/0 | `PLOP-A77A222A745E` |
| 2 | cozy-nest | Authored | 4×4 | 1 | – | 4 | – | 1.0 | Tutorial | solves | 0 | 0 | 0/0/0 | `PLOP-18792159677A` |
| 3 | cozy-nest | Authored | 4×4 | 2 | – | 2 | – | 0.0 | Tutorial | solves | 0 | 0 | 0/0/0 | `PLOP-CC559DBE0F62` |
| 4 | cozy-nest | Authored | 4×4 | 2 | – | 4 | – | 1.3 | Tutorial | solves | 0 | 0 | 0/0/0 | `PLOP-7713C5833A0F` |
| 5 | cozy-nest | Authored | 3×3 | 2 | – | 4 | – | 23.0 | Normal | fails | 0 | 0 | 0/0/0 | `PLOP-EBC5AB002FB6` |
| 6 | cozy-nest | Classic | 5×5 | 2 | – | 8 | – | 24.5 | Normal | fails | 0 | 0 | 0/0/0 | `PLOP-D43737ED2FB2` |
| 7 | frosted-pond | Authored | 5×3 | 1 | F1 | 1 | – | 0.3 | Tutorial | solves | 0 | 1 | 1/0/0 | `PLOP-BEC9D2C4D6A1` |
| 8 | frosted-pond | Authored | 5×3 | 1 | F1 | 2 | – | 1.8 | Tutorial | solves | 0 | 1 | 1/0/0 | `PLOP-5D1CACB958FF` |
| 9 | frosted-pond | Generated | 5×5 | 1 | F1 | 8 | – | 22.6 | Normal | fails | 0 | 1 | 1/0/0 | `PLOP-9B860AF9CFF8` |
| 10 | frosted-pond | Generated | 5×5 | 1 | F4 | 8 | – | 21.8 | Normal | fails | 0 | 2 | 2/0/0 | `PLOP-B18D408409FE` |
| 11 | frosted-pond | Generated | 5×5 | 2 | F1 | 8 | – | 13.0 | Easy | solves | 0 | 2 | 2/0/0 | `PLOP-AED829F3C1AA` |
| 12 | frosted-pond | Generated | 5×5 | 2 | F2 | 10 | – | 29.8 | Normal | fails | 0 | 2 | 2/0/0 | `PLOP-8C4398D61D57` |
| 13 | frosted-pond | Generated | 5×6 | 2 | F1 | 8 | – | 27.7 | Normal | fails | 0 | 3 | 3/0/0 | `PLOP-DA9CE2F869B7` |
| 14 | frosted-pond | Generated | 5×6 | 2 | F1 | 8 | – | 25.5 | Normal | fails | 0 | 2 | 2/0/0 | `PLOP-2B342E7F7246` |
| 15 | frosted-pond | Generated | 5×6 | 2 | F1 | 9 | – | 32.7 | Hard | fails | 0 | 2 | 2/0/0 | `PLOP-4C5D58B3BF69` |
| 16 | frosted-pond | Generated | 5×6 | 2 | F2 | 10 | – | 17.6 | Easy | solves | 0 | 3 | 3/0/0 | `PLOP-B139C83E1D0C` |
| 17 | frosted-pond | Generated | 5×6 | 2 | F2 | 9 | – | 37.6 | Hard | fails | 0 | 2 | 2/0/0 | `PLOP-0296098C6005` |
| 18 | frosted-pond | Generated | 5×6 | 2 | F1 | 7 | – | 34.6 | Hard | fails | 0 | 3 | 3/0/0 | `PLOP-0389B44CD9AD` |
| 19 | frosted-pond | Generated | 5×6 | 2 | F1 | 11 | – | 43.2 | Expert | fails | 0 | 2 | 2/0/0 | `PLOP-EC8D7A6CB346` |
| 20 | frosted-pond | Generated | 5×6 | 2 | F2 | 11 | – | 40.7 | Hard | fails | 0 | 3 | 3/0/0 | `PLOP-AD35D365A625` |
| 21 | breezy-meadow | Authored | 4×4 | 1 | F1 | 1 | – | 0.0 | Tutorial | solves | 0 | 1 | 0/1/0 | `PLOP-BFE509C99549` |
| 22 | breezy-meadow | Authored | 4×4 | 1 | F1 | 4 | – | 3.1 | Tutorial | solves | 0 | 1 | 0/1/0 | `PLOP-DB647C4B71BC` |
| 23 | breezy-meadow | Generated | 5×6 | 1 | F2 | 7 | – | 22.1 | Normal | fails | 0 | 2 | 0/2/0 | `PLOP-AF42DCA4898A` |
| 24 | breezy-meadow | Generated | 5×6 | 1 | F2 | 9 | – | 24.5 | Normal | fails | 0 | 2 | 0/2/0 | `PLOP-B44028491C92` |
| 25 | breezy-meadow | Generated | 5×6 | 2 | F2 | 9 | – | 18.8 | Normal | solves | 0 | 4 | 0/4/0 | `PLOP-28E26B8028EA` |
| 26 | breezy-meadow | Generated | 5×6 | 2 | F2 | 11 | – | 34.7 | Hard | fails | 0 | 3 | 0/3/0 | `PLOP-FEF85E74AFD4` |
| 27 | breezy-meadow | Generated | 5×6 | 2 | F1F1 | 10 | – | 28.5 | Normal | fails | 0 | 3 | 2/1/0 | `PLOP-F798469B317B` |
| 28 | breezy-meadow | Generated | 5×6 | 2 | F1F1 | 6 | – | 28.1 | Normal | fails | 0 | 2 | 0/2/0 | `PLOP-2ADCAC5E7EAA` |
| 29 | breezy-meadow | Generated | 5×6 | 2 | F1F1 | 9 | – | 43.5 | Expert | fails | 0 | 3 | 2/1/0 | `PLOP-5DD5B1209927` |
| 30 | breezy-meadow | Generated | 5×6 | 2 | F1 | 8 | 10 | 32.5 | Hard | fails | 0 | 2 | 0/2/0 | `PLOP-5D234C07C788` |
| 31 | breezy-meadow | Generated | 5×6 | 2 | F1F1 | 11 | – | 40.0 | Hard | fails | 0 | 2 | 1/1/0 | `PLOP-ECD6CEC6557D` |
| 32 | breezy-meadow | Generated | 5×6 | 2 | F2 | 11 | – | 36.8 | Hard | fails | 0 | 3 | 0/3/0 | `PLOP-EBE51D77263D` |
| 33 | breezy-meadow | Generated | 5×6 | 2 | F1F4 | 10 | – | 40.1 | Hard | fails | 0 | 3 | 3/2/0 | `PLOP-1896D77003B1` |
| 34 | breezy-meadow | Generated | 5×6 | 2 | F1 | 7 | – | 39.0 | Hard | fails | 0 | 2 | 0/2/0 | `PLOP-ACDD73419E28` |
| 35 | ember-hollow | Authored | 3×2 | 1 | L1 | 4 | – | 7.2 | Tutorial | solves | 2 | 0 | 0/0/0 | `PLOP-91370B97F25A` |
| 36 | ember-hollow | Authored | 5×3 | 1 | F1L1 | 4 | – | 2.9 | Tutorial | solves | 1 | 0 | 0/0/0 | `PLOP-2B1635018ED3` |
| 37 | ember-hollow | Generated | 5×6 | 1 | L2F1 | 12 | – | 35.1 | Hard | fails | 2 | 1 | 1/0/0 | `PLOP-6A8A384BDDA1` |
| 38 | ember-hollow | Generated | 6×6 | 1 | F2L2 | 8 | – | 31.5 | Hard | fails | 2 | 2 | 2/0/0 | `PLOP-D3B856259B1F` |
| 39 | ember-hollow | Generated | 5×6 | 2 | L1 | 9 | – | 27.2 | Normal | fails | 2 | 0 | 0/0/0 | `PLOP-0B669D7E07FC` |
| 40 | ember-hollow | Generated | 6×6 | 2 | L3 | 12 | – | 38.6 | Hard | fails | 5 | 0 | 0/0/0 | `PLOP-C1870F28473D` |
| 41 | ember-hollow | Generated | 5×6 | 2 | F1L1F1 | 11 | – | 41.8 | Hard | fails | 4 | 1 | 0/1/0 | `PLOP-E7292723F395` |
| 42 | ember-hollow | Generated | 6×6 | 2 | F1L2 | 10 | – | 44.1 | Expert | fails | 3 | 2 | 2/0/0 | `PLOP-0CADC73BA9EB` |
| 43 | ember-hollow | Generated | 5×6 | 2 | L2F1 | 7 | – | 45.0 | Expert | fails | 4 | 1 | 0/1/0 | `PLOP-3E4B060A7117` |
| 44 | ember-hollow | Generated | 6×6 | 2 | L2F1 | 10 | – | 39.0 | Hard | fails | 4 | 1 | 0/1/0 | `PLOP-5519059C8460` |
| 45 | ember-hollow | Generated | 5×6 | 2 | L1F1 | 11 | – | 36.6 | Hard | fails | 1 | 1 | 1/0/0 | `PLOP-B51F16E712E4` |
| 46 | ember-hollow | Generated | 6×6 | 2 | F1L1 | 11 | – | 42.7 | Expert | fails | 5 | 1 | 1/0/0 | `PLOP-B3D884619756` |
| 47 | ember-hollow | Generated | 5×6 | 2 | L1F1 | 10 | – | 37.6 | Hard | fails | 1 | 1 | 1/0/0 | `PLOP-1AA4FB8C4653` |
| 48 | ember-hollow | Generated | 6×6 | 2 | F2L1F1 | 10 | – | 43.7 | Expert | fails | 6 | 2 | 1/2/0 | `PLOP-9959C829F5E9` |
| 49 | prism-observatory | Authored | 5×2 | 1 | P2 | 2 | – | 1.4 | Tutorial | solves | 0 | 1 | 0/0/1 | `PLOP-20A31232BC6A` |
| 50 | prism-observatory | Generated | 5×6 | 1 | P2 | 7 | – | 23.7 | Normal | fails | 0 | 2 | 0/0/2 | `PLOP-17B46BC540FE` |
| 51 | prism-observatory | Generated | 5×6 | 1 | P2 | 7 | – | 22.9 | Normal | fails | 0 | 1 | 0/0/1 | `PLOP-E279F42B310D` |
| 52 | prism-observatory | Generated | 5×6 | 2 | P2 | 10 | – | 32.6 | Hard | fails | 0 | 1 | 0/0/1 | `PLOP-2685FD6FDED6` |
| 53 | prism-observatory | Generated | 5×6 | 2 | P2 | 12 | – | 29.6 | Normal | fails | 0 | 1 | 0/0/1 | `PLOP-0376EC2E6B55` |
| 54 | prism-observatory | Generated | 5×6 | 2 | P2 | 9 | – | 40.9 | Hard | fails | 0 | 2 | 0/0/2 | `PLOP-C789C2B54EB9` |
| 55 | prism-observatory | Generated | 5×6 | 2 | P2 | 7 | – | 43.7 | Expert | fails | 0 | 2 | 0/0/2 | `PLOP-7D2FFBC15AD8` |
| 56 | prism-observatory | Generated | 5×6 | 2 | P2L1 | 10 | – | 43.0 | Expert | fails | 3 | 2 | 0/0/2 | `PLOP-D6CE4196BE90` |
| 57 | prism-observatory | Generated | 5×6 | 2 | F2P2L1 | 6 | – | 41.2 | Hard | fails | 1 | 3 | 2/0/1 | `PLOP-C0E8BC52A7BB` |
| 58 | prism-observatory | Generated | 5×6 | 2 | P2L1 | 9 | – | 31.9 | Hard | fails | 1 | 1 | 0/0/1 | `PLOP-9965ACEAA646` |
| 59 | prism-observatory | Generated | 5×6 | 2 | P2L3 | 10 | – | 41.7 | Hard | fails | 4 | 1 | 0/0/1 | `PLOP-5562641C9715` |
| 60 | prism-observatory | Generated | 5×6 | 2 | P2L3 | 9 | 11 | 38.5 | Hard | fails | 3 | 1 | 0/0/1 | `PLOP-73B6E6045C4A` |
| 61 | prism-observatory | Generated | 5×6 | 2 | P2L1 | 10 | – | 41.1 | Hard | fails | 1 | 3 | 0/0/3 | `PLOP-3460F04FAFD4` |
| 62 | prism-observatory | Generated | 5×6 | 2 | P2L1 | 12 | – | 46.9 | Expert | fails | 7 | 3 | 0/0/3 | `PLOP-743FBFD4C255` |
| 63 | prism-observatory | Generated | 5×6 | 2 | P2 | 10 | – | 44.4 | Expert | fails | 0 | 1 | 0/0/1 | `PLOP-6E7D6F384DA4` |
| 64 | dewdrop-garden | Generated | 6×6 | 2 | F1F2 | 12 | – | 38.4 | Hard | fails | 0 | 4 | 3/1/0 | `PLOP-13A21C093CCB` |
| 65 | dewdrop-garden | Generated | 5×6 | 2 | L1F1 | 12 | – | 44.0 | Expert | fails | 5 | 2 | 0/2/0 | `PLOP-94BE213F5D42` |
| 66 | dewdrop-garden | Generated | 6×6 | 2 | F1P2 | 12 | – | 43.9 | Expert | fails | 0 | 2 | 1/0/1 | `PLOP-F46E082174C7` |
| 67 | dewdrop-garden | Generated | 5×6 | 3 | F2L2 | 11 | – | 45.3 | Expert | fails | 4 | 2 | 2/0/0 | `PLOP-6390A799C91C` |
| 68 | dewdrop-garden | Generated | 6×6 | 2 | F1P2 | 8 | – | 26.5 | Normal | fails | 0 | 2 | 0/1/1 | `PLOP-AA4A0DBF4411` |
| 69 | dewdrop-garden | Generated | 5×6 | 2 | P2L1 | 11 | – | 39.5 | Hard | fails | 2 | 2 | 0/0/2 | `PLOP-964B2362E057` |
| 70 | dewdrop-garden | Classic | 6×6 | 2 | – | 11 | – | 32.4 | Hard | fails | 0 | 0 | 0/0/0 | `PLOP-B7CAD97CCD8D` |
| 71 | dewdrop-garden | Generated | 5×6 | 2 | F1L1 | 9 | – | 36.6 | Hard | fails | 1 | 2 | 0/2/0 | `PLOP-3999CB7B0F22` |
| 72 | dewdrop-garden | Generated | 6×6 | 3 | P2F1 | 11 | – | 53.6 | Expert | fails | 0 | 3 | 1/0/2 | `PLOP-18A1B76EB9B4` |
| 73 | dewdrop-garden | Generated | 5×6 | 2 | F2L1 | 8 | – | 33.9 | Hard | fails | 2 | 2 | 2/0/0 | `PLOP-E851959BCD96` |
| 74 | dewdrop-garden | Generated | 6×6 | 2 | F1P2 | 13 | – | 38.7 | Hard | fails | 0 | 2 | 0/1/1 | `PLOP-3B42FA596EEB` |
| 75 | dewdrop-garden | Generated | 5×6 | 2 | P2L2 | 12 | – | 40.5 | Hard | fails | 2 | 2 | 0/0/2 | `PLOP-1FD346B8F187` |
| 76 | dewdrop-garden | Generated | 6×6 | 2 | F2F1 | 7 | – | 40.7 | Hard | fails | 0 | 4 | 3/1/0 | `PLOP-CBCDA2F502D4` |
| 77 | dewdrop-garden | Classic | 5×6 | 3 | – | 12 | – | 34.8 | Hard | fails | 0 | 0 | 0/0/0 | `PLOP-647C80A8683A` |
| 78 | dewdrop-garden | Generated | 6×6 | 2 | F1P2 | 11 | – | 33.8 | Hard | fails | 0 | 2 | 1/0/2 | `PLOP-17D9DBBC55DC` |
| 79 | dewdrop-garden | Generated | 5×6 | 2 | F2L1 | 10 | – | 45.7 | Expert | fails | 4 | 2 | 2/0/0 | `PLOP-E032394B0CB8` |
| 80 | dewdrop-garden | Generated | 6×6 | 2 | F2P2 | 9 | – | 48.5 | Expert | fails | 0 | 4 | 0/2/2 | `PLOP-C98EC115AA7D` |
| 81 | dewdrop-garden | Generated | 5×6 | 2 | P2L1 | 10 | – | 42.0 | Expert | fails | 2 | 3 | 0/0/3 | `PLOP-36EF90D21E51` |
| 82 | dewdrop-garden | Generated | 6×6 | 3 | F1F1 | 11 | – | 50.0 | Expert | fails | 0 | 4 | 2/2/0 | `PLOP-4972F13B54B1` |
| 83 | dewdrop-garden | Generated | 5×6 | 2 | F1L1 | 12 | – | 34.5 | Hard | fails | 1 | 2 | 0/2/0 | `PLOP-A70940FFCD42` |
| 84 | dewdrop-garden | Classic | 5×6 | 3 | – | 11 | – | 39.7 | Hard | fails | 0 | 0 | 0/0/0 | `PLOP-57BA207976D1` |
| 85 | dewdrop-garden | Generated | 5×6 | 2 | L2F1 | 10 | – | 49.9 | Expert | fails | 6 | 2 | 2/0/0 | `PLOP-F4D2C2A1539B` |
| 86 | rainy-window | Generated | 6×6 | 3 | F2P2 | 9 | – | 42.2 | Expert | fails | 0 | 4 | 3/0/1 | `PLOP-E4F794FC1215` |
| 87 | rainy-window | Generated | 6×6 | 3 | L1F1 | 8 | – | 38.6 | Hard | fails | 1 | 2 | 2/0/0 | `PLOP-DDD2184282EA` |
| 88 | rainy-window | Generated | 6×6 | 3 | F1P2 | 13 | – | 51.0 | Expert | fails | 0 | 6 | 0/2/4 | `PLOP-21FCD3A32CC0` |
| 89 | rainy-window | Generated | 6×6 | 3 | P2L1 | 11 | – | 50.7 | Expert | fails | 3 | 2 | 0/0/2 | `PLOP-0212ECE72E54` |
| 90 | rainy-window | Generated | 6×6 | 2 | F1F1 | 14 | 16 | 37.2 | Hard | fails | 0 | 4 | 2/2/0 | `PLOP-D998A603EC7E` |
| 91 | rainy-window | Generated | 6×6 | 3 | F2L1 | 10 | – | 53.0 | Expert | fails | 1 | 3 | 0/3/0 | `PLOP-AC6234F10955` |
| 92 | rainy-window | Classic | 5×6 | 3 | – | 10 | – | 38.8 | Hard | fails | 0 | 0 | 0/0/0 | `PLOP-A001565C8CFB` |
| 93 | rainy-window | Generated | 6×6 | 3 | F1L1 | 14 | – | 53.5 | Expert | fails | 1 | 2 | 2/0/0 | `PLOP-814816F6D535` |
| 94 | rainy-window | Generated | 6×6 | 3 | F2P2F2 | 13 | – | 55.7 | Expert | fails | 0 | 6 | 3/2/3 | `PLOP-B19BFA1C6E79` |
| 95 | rainy-window | Generated | 6×6 | 2 | P2L2 | 13 | – | 49.5 | Expert | fails | 6 | 4 | 0/0/4 | `PLOP-8529B5382073` |
| 96 | rainy-window | Generated | 6×6 | 3 | F2F2 | 10 | – | 53.0 | Expert | fails | 0 | 6 | 2/8/0 | `PLOP-E653C8FC4B34` |
| 97 | rainy-window | Generated | 6×6 | 3 | F1L3 | 10 | – | 51.2 | Expert | fails | 6 | 2 | 0/2/0 | `PLOP-67CD5A0C32EC` |
| 98 | rainy-window | Generated | 6×6 | 3 | P2F1 | 8 | – | 43.8 | Expert | fails | 0 | 4 | 3/0/1 | `PLOP-FCA90CEE6D43` |
| 99 | rainy-window | Classic | 5×6 | 3 | – | 13 | – | 41.5 | Hard | fails | 0 | 0 | 0/0/0 | `PLOP-736168F83DEB` |
| 100 | rainy-window | Generated | 6×6 | 2 | F1P2 | 10 | – | 38.0 | Hard | fails | 0 | 5 | 0/3/2 | `PLOP-1EE27743BA3A` |
| 101 | rainy-window | Generated | 6×6 | 3 | P2L1 | 11 | – | 52.3 | Expert | fails | 2 | 3 | 0/0/3 | `PLOP-2FEF8DAD4008` |
| 102 | rainy-window | Generated | 6×6 | 3 | F3F1 | 14 | – | 48.4 | Expert | fails | 0 | 7 | 4/4/0 | `PLOP-29427CAD7981` |
| 103 | rainy-window | Generated | 6×6 | 3 | F1L1 | 13 | – | 51.2 | Expert | fails | 4 | 3 | 0/3/0 | `PLOP-A00E3DB3B540` |
| 104 | rainy-window | Generated | 6×6 | 3 | F2P2 | 14 | – | 51.6 | Expert | fails | 0 | 5 | 3/0/2 | `PLOP-D5FD39305C84` |
| 105 | rainy-window | Generated | 6×6 | 2 | F1L1 | 13 | – | 44.6 | Expert | fails | 2 | 2 | 2/0/0 | `PLOP-4665C817B825` |
| 106 | rainy-window | Classic | 6×6 | 3 | – | 13 | – | 45.0 | Expert | fails | 0 | 0 | 0/0/0 | `PLOP-0B07DFCFED5E` |
| 107 | rainy-window | Generated | 6×6 | 3 | P2L2 | 10 | – | 49.4 | Expert | fails | 6 | 2 | 0/0/2 | `PLOP-9B4189ED1FB8` |
| 108 | rainy-window | Generated | 6×6 | 3 | F1F1 | 13 | – | 52.4 | Expert | fails | 0 | 4 | 2/2/0 | `PLOP-EB1F032E6EBD` |
| 109 | twilight-attic | Generated | 6×6 | 3 | F2F1L2 | 10 | – | 41.6 | Hard | fails | 1 | 3 | 2/1/0 | `PLOP-BD7EE705A8FF` |
| 110 | twilight-attic | Generated | 6×7 | 3 | F2P2L1 | 14 | – | 52.9 | Expert | fails | 5 | 3 | 2/0/1 | `PLOP-06B25E3868AF` |
| 111 | twilight-attic | Generated | 6×6 | 3 | F1P2 | 11 | – | 44.1 | Expert | fails | 0 | 5 | 0/2/3 | `PLOP-B64961B98230` |
| 112 | twilight-attic | Generated | 6×7 | 3 | F2L1P2 | 13 | – | 52.4 | Expert | fails | 2 | 4 | 0/4/1 | `PLOP-A89C73E85482` |
| 113 | twilight-attic | Generated | 6×6 | 2 | F1L1F1 | 15 | – | 46.9 | Expert | fails | 1 | 3 | 2/1/0 | `PLOP-AE014CF994B6` |
| 114 | twilight-attic | Generated | 6×7 | 3 | L1P2 | 10 | – | 49.8 | Expert | fails | 6 | 3 | 0/0/3 | `PLOP-C0C999A4B464` |
| 115 | twilight-attic | Classic | 6×6 | 3 | – | 14 | – | 44.1 | Expert | fails | 0 | 0 | 0/0/0 | `PLOP-7FFE8C644CFC` |
| 116 | twilight-attic | Generated | 6×7 | 3 | F1P2L1 | 14 | – | 54.5 | Expert | fails | 1 | 4 | 0/0/4 | `PLOP-9ADF43DCA04E` |
| 117 | twilight-attic | Generated | 6×6 | 3 | F2L1 | 14 | – | 54.3 | Expert | fails | 2 | 5 | 0/6/0 | `PLOP-62AF711045E5` |
| 118 | twilight-attic | Generated | 6×7 | 2 | F1L3P2 | 9 | – | 45.3 | Expert | fails | 4 | 3 | 3/0/1 | `PLOP-394E75C0242F` |
| 119 | twilight-attic | Generated | 6×6 | 3 | F1P2F1 | 12 | – | 49.6 | Expert | fails | 0 | 7 | 3/1/6 | `PLOP-A3DFC45764A3` |
| 120 | twilight-attic | Generated | 6×7 | 3 | L1F1P2 | 15 | 17 | 64.4 | Expert | fails | 5 | 3 | 0/2/1 | `PLOP-16B2E104622A` |
| 121 | twilight-attic | Generated | 6×6 | 3 | F2F1 | 14 | – | 57.9 | Expert | fails | 0 | 5 | 7/1/0 | `PLOP-0C7FF6F1FEF2` |
| 122 | twilight-attic | Classic | 6×6 | 3 | – | 13 | – | 47.6 | Expert | fails | 0 | 0 | 0/0/0 | `PLOP-BF13C4799C9E` |
| 123 | twilight-attic | Generated | 6×6 | 2 | F1P2 | 10 | – | 43.9 | Expert | fails | 0 | 4 | 0/1/3 | `PLOP-046B27BD56F9` |
| 124 | twilight-attic | Generated | 6×7 | 3 | L1P2 | 14 | – | 56.1 | Expert | fails | 1 | 3 | 0/0/3 | `PLOP-64163E94A1E8` |
| 125 | twilight-attic | Generated | 6×6 | 3 | F1F1 | 15 | – | 52.1 | Expert | fails | 0 | 4 | 1/3/0 | `PLOP-407F36B4628A` |
| 126 | twilight-attic | Generated | 6×7 | 3 | F2L1P2 | 15 | – | 67.9 | Expert | fails | 3 | 8 | 5/0/4 | `PLOP-4D2E50FCF6C5` |
| 127 | twilight-attic | Generated | 6×6 | 3 | F3F1P2 | 14 | – | 72.2 | Expert | fails | 0 | 7 | 9/2/2 | `PLOP-A560DDAB84A4` |
| 128 | twilight-attic | Generated | 6×7 | 2 | P2L3 | 15 | – | 59.3 | Expert | fails | 4 | 5 | 0/0/5 | `PLOP-D5FB3344EDBC` |
| 129 | twilight-attic | Classic | 6×6 | 4 | – | 15 | – | 50.2 | Expert | fails | 0 | 0 | 0/0/0 | `PLOP-8D91428CF2F2` |
| 130 | twilight-attic | Generated | 6×7 | 3 | F2P2L1 | 13 | – | 55.3 | Expert | fails | 1 | 3 | 1/0/2 | `PLOP-70FA7813BC5C` |
| 131 | twilight-attic | Generated | 6×6 | 3 | F1P2 | 14 | – | 58.1 | Expert | fails | 0 | 6 | 2/0/4 | `PLOP-E5F6A0502416` |
| 132 | twilight-attic | Generated | 6×7 | 3 | F1P2L3 | 15 | – | 61.7 | Expert | fails | 4 | 6 | 0/2/4 | `PLOP-E926475FBCD1` |
| 133 | twilight-attic | Generated | 6×6 | 2 | F1F1L2 | 14 | – | 60.4 | Expert | fails | 2 | 3 | 1/2/0 | `PLOP-95D8957D80A3` |
| 134 | moonlit-clouds | Generated | 6×7 | 3 | F4F1L2 | 14 | – | 56.3 | Expert | fails | 3 | 6 | 6/1/0 | `PLOP-99CFD91947B4` |
| 135 | moonlit-clouds | Generated | 6×7 | 3 | P2L1F1 | 15 | – | 66.1 | Expert | fails | 2 | 7 | 1/0/6 | `PLOP-AA6751122374` |
| 136 | moonlit-clouds | Generated | 6×7 | 3 | F2P2 | 14 | – | 60.2 | Expert | fails | 0 | 5 | 0/2/3 | `PLOP-68984ACF232F` |
| 137 | moonlit-clouds | Generated | 6×7 | 3 | F2P2L1F1 | 15 | – | 70.5 | Expert | fails | 3 | 8 | 7/1/2 | `PLOP-EB86D1BC21A2` |
| 138 | moonlit-clouds | Generated | 6×7 | 2 | F1L1F1 | 12 | – | 48.9 | Expert | fails | 1 | 7 | 2/5/0 | `PLOP-92D99B27109D` |
| 139 | moonlit-clouds | Generated | 6×7 | 3 | F1P2L2 | 17 | – | 64.4 | Expert | fails | 3 | 4 | 3/0/1 | `PLOP-72E94913FE80` |
| 140 | moonlit-clouds | Classic | 6×6 | 3 | – | 13 | – | 49.8 | Expert | fails | 0 | 0 | 0/0/0 | `PLOP-E2DB81948141` |
| 141 | moonlit-clouds | Generated | 6×7 | 3 | F1L2P2 | 16 | – | 56.0 | Expert | fails | 1 | 3 | 0/2/1 | `PLOP-32DE02C58981` |
| 142 | moonlit-clouds | Generated | 6×7 | 3 | F1P2L1 | 16 | – | 72.5 | Expert | fails | 10 | 6 | 3/0/4 | `PLOP-F2DA6ACE1B8E` |
| 143 | moonlit-clouds | Generated | 6×7 | 2 | P2F1 | 12 | – | 55.8 | Expert | fails | 0 | 3 | 2/0/1 | `PLOP-B9246178721F` |
| 144 | moonlit-clouds | Generated | 6×7 | 3 | F1P2F1 | 11 | – | 54.4 | Expert | fails | 0 | 3 | 1/1/1 | `PLOP-962EB8EB6902` |
| 145 | moonlit-clouds | Generated | 6×7 | 3 | F2P2L2 | 14 | – | 61.9 | Expert | fails | 5 | 5 | 2/0/4 | `PLOP-5C5F9B25524F` |
| 146 | moonlit-clouds | Generated | 6×7 | 3 | L1F2F1 | 16 | – | 65.7 | Expert | fails | 9 | 6 | 3/3/0 | `PLOP-04B4F5ABEF24` |
| 147 | moonlit-clouds | Classic | 6×6 | 3 | – | 16 | – | 53.3 | Expert | fails | 0 | 0 | 0/0/0 | `PLOP-B4B64342D8B6` |
| 148 | moonlit-clouds | Generated | 6×7 | 2 | F1P2 | 17 | – | 50.8 | Expert | fails | 0 | 3 | 0/1/3 | `PLOP-2182E66ED74B` |
| 149 | moonlit-clouds | Generated | 6×7 | 3 | F1L2 | 15 | – | 67.0 | Expert | fails | 2 | 3 | 0/3/0 | `PLOP-0A414BDB2869` |
| 150 | moonlit-clouds | Generated | 6×7 | 3 | F1P2L1 | 16 | 18 | 67.6 | Expert | fails | 4 | 6 | 2/0/4 | `PLOP-ED84FC61F254` |
| 151 | moonlit-clouds | Generated | 6×7 | 3 | F1P2L1 | 11 | – | 59.0 | Expert | fails | 4 | 6 | 1/0/6 | `PLOP-8B5FBDBAE1F1` |
| 152 | moonlit-clouds | Generated | 6×7 | 3 | F1P2L1 | 17 | – | 65.2 | Expert | fails | 4 | 7 | 3/0/4 | `PLOP-FDBBEDFA9E67` |
| 153 | moonlit-clouds | Generated | 6×7 | 2 | L1F1P2 | 15 | – | 64.4 | Expert | fails | 2 | 6 | 0/4/2 | `PLOP-3D4C3A954320` |
| 154 | moonlit-clouds | Classic | 6×6 | 3 | – | 15 | – | 56.0 | Expert | fails | 0 | 0 | 0/0/0 | `PLOP-50E7451D7893` |
| 155 | moonlit-clouds | Generated | 6×7 | 3 | F2P2L1F1 | 15 | – | 59.6 | Expert | fails | 2 | 4 | 2/1/1 | `PLOP-9FC87D2D2762` |
| 156 | moonlit-clouds | Generated | 6×7 | 3 | F1F1P2 | 17 | – | 63.9 | Expert | fails | 0 | 5 | 0/1/5 | `PLOP-FE5D49E6147E` |
| 157 | moonlit-clouds | Generated | 6×7 | 3 | F2P2L1F1 | 15 | – | 77.2 | Expert | fails | 3 | 11 | 2/8/4 | `PLOP-D0787DFCC52A` |
| 158 | moonlit-clouds | Generated | 6×7 | 2 | L1F2 | 14 | – | 56.2 | Expert | fails | 2 | 3 | 3/0/0 | `PLOP-B6FDF78D68E2` |
| 159 | moonlit-clouds | Generated | 6×7 | 3 | F1P2L1 | 13 | – | 60.2 | Expert | fails | 3 | 5 | 3/0/2 | `PLOP-1F8FDAAEBE0C` |
| 160 | moonlit-clouds | Generated | 6×7 | 3 | F2P2L2 | 14 | – | 65.8 | Expert | fails | 2 | 5 | 2/0/4 | `PLOP-F27345FAF133` |
