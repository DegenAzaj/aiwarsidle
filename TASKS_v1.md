Poniżej masz **Jira-ready backlog** pod MVP z docelowym async PvP (na końcu z prawdziwym backend snapshot, nie tylko boty).

Struktura:
**Epic → Story → Task**

Możesz to wkleić 1:1 do Jira.

---

# EPIC 1 — Core Idle System

---

## STORY 1.1 — Game State & Persistence

**TASK 1.1.1**
Implement `GameState` model

* waluty
* generatory
* prestige
* liga

**TASK 1.1.2**
Implement `SaveDataV1` schema

**TASK 1.1.3**
Implement `SaveService`

* autosave co 30s
* save on pause/quit
* load with version check

**TASK 1.1.4**
Unit test: Save → Load → stan identyczny

---

## STORY 1.2 — Production System

**TASK 1.2.1**
Implement `ProductionService`

* tick co 1s
* obliczanie production/s

**TASK 1.2.2**
Implement offline progression

* różnica czasu
* cap 12h

**TASK 1.2.3**
UI binding produkcji do HUD

---

## STORY 1.3 — Generator Upgrades

**TASK 1.3.1**
Implement `UpgradeService`

* koszt wykładniczy
* wzrost produkcji

**TASK 1.3.2**
Dodaj 5 generatorów w BalanceConfig

**TASK 1.3.3**
UI panel generatorów (upgrade x1/x10/max)

**TASK 1.3.4**
Analytics event: generator_upgrade

---

## STORY 1.4 — Prestige

**TASK 1.4.1**
Implement `PrestigeService`

**TASK 1.4.2**
Reset generatorów

**TASK 1.4.3**
Permanent multiplier upgrade (max 10 poziomów)

**TASK 1.4.4**
UI modal prestige

**TASK 1.4.5**
Analytics event: prestige

---

# EPIC 2 — UI & Navigation

---

## STORY 2.1 — Main Hub

**TASK 2.1.1**
Implement `UIRouter`

**TASK 2.1.2**
HubPanel (HUD, waluty, production/s, prestige bar)

**TASK 2.1.3**
Overclock mechanic (active boost)

---

## STORY 2.2 — PvP Screen UI

**TASK 2.2.1**
PvP panel layout

**TASK 2.2.2**
Wyświetlanie mocy gracza

**TASK 2.2.3**
Przycisk “Find Opponent”

---

## STORY 2.3 — Shop & Subscription UI

**TASK 2.3.1**
ShopPanel layout

**TASK 2.3.2**
Subscription UI

**TASK 2.3.3**
Rewarded Ad button integration

---

# EPIC 3 — PvP (Local MVP Phase)

Najpierw wersja lokalna (bot snapshots), potem backend.

---

## STORY 3.1 — Snapshot System

**TASK 3.1.1**
Implement `PvpSnapshot` model

**TASK 3.1.2**
Implement `SnapshotService`

---

## STORY 3.2 — Matchmaking (Local)

**TASK 3.2.1**
Implement `MatchmakingService`

* zakres 80–120% mocy

**TASK 3.2.2**
Generate bot snapshot if no backend

---

## STORY 3.3 — Battle Simulation

**TASK 3.3.1**
Implement `BattleSimService`

**TASK 3.3.2**
Implement wynik (win/lose)

**TASK 3.3.3**
Apply reward & league points

**TASK 3.3.4**
Analytics: pvp_attack, pvp_result

---

## STORY 3.4 — League System

**TASK 3.4.1**
Implement `LeagueService`

**TASK 3.4.2**
Define league thresholds

**TASK 3.4.3**
League promotion/demotion logic

**TASK 3.4.4**
Season reset (date-based)

---

# EPIC 4 — Monetization

---

## STORY 4.1 — Rewarded Ads

**TASK 4.1.1**
Integrate Ad SDK

**TASK 4.1.2**
Implement x2 offline reward

**TASK 4.1.3**
Implement +1 PvP attack reward

**TASK 4.1.4**
Analytics: ad_watched

---

## STORY 4.2 — Subscription

**TASK 4.2.1**
Integrate store subscription

**TASK 4.2.2**
Apply 2x production if active

**TASK 4.2.3**
Disable ads if active

**TASK 4.2.4**
Analytics: subscription_started

---

# EPIC 5 — Analytics

---

## STORY 5.1 — Core Tracking

**TASK 5.1.1**
Implement `AnalyticsService` wrapper

**TASK 5.1.2**
Track session_start

**TASK 5.1.3**
Track offline_claim

**TASK 5.1.4**
Track generator_upgrade

**TASK 5.1.5**
Track prestige

**TASK 5.1.6**
Track pvp_attack

**TASK 5.1.7**
Track pvp_result

---

# EPIC 6 — Backend Async PvP (Docelowy Cel)

To robisz po walidacji lokalnej wersji.

---

## STORY 6.1 — Backend Snapshot API

**TASK 6.1.1**
Design snapshot API schema

**TASK 6.1.2**
Implement endpoint: upload snapshot

**TASK 6.1.3**
Implement endpoint: fetch opponent snapshot

---

## STORY 6.2 — Server-side Battle Resolution

**TASK 6.2.1**
Move battle simulation na backend

**TASK 6.2.2**
Implement validation anti-cheat

**TASK 6.2.3**
Return signed battle result

---

## STORY 6.3 — Leaderboard Backend

**TASK 6.3.1**
Store season points server-side

**TASK 6.3.2**
Implement top100 endpoint

**TASK 6.3.3**
Season reset job

---

# EPIC 7 — Polish & Stability

---

**TASK 7.1**
Performance test (low-end device)

**TASK 7.2**
Memory profiling

**TASK 7.3**
Bugfix pass

**TASK 7.4**
Soft launch build prep

---

# Realistyczna kolejność sprintów

Sprint 1–2
Core idle + save

Sprint 3
Prestige + UI

Sprint 4
Local PvP + league

Sprint 5
Monetization + analytics

Sprint 6+
Backend async PvP

---

