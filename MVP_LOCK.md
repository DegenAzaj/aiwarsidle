# MVP_LOCK.md

Ten plik jest **źródłem prawdy** dla założeń MVP v1.
Jeśli inne `.md` są niespójne, **wygrywa `MVP_LOCK.md`**.

---

# 1) Pitch / USP

Idle economy + async PvP na mapie sektorów.
Gracz buduje “moc obliczeniową”, a następnie walczy o terytoria, które realnie zmieniają produkcję.

---

# 2) Pętle i rytm gry

## Core loop (60–90s)

1. Wejście → odbiór offline produkcji (cap 12h)
2. Upgrade generatorów
3. **Overclock** (aktywny boost, “moment decyzji”)
4. Mapa → podgląd sektorów
5. Wydanie 1–5 ataków PvP (zależnie od charge’y)
6. Cel na następną sesję: kolejny sektor / próg prestiżu

## Cykle czasowe

### Mapa PvP: 7 dni

* Reset **co 7 dni**:
  * ownership sektorów wraca do neutral/bot
  * stability reset
* Cel: częste “końcówki”, dynamika, brak zastania mapy.

### Liga (sezon): 30 dni

* Sezon ligi trwa **30 dni**.
* Punkty ligi/Season Points (SP) zbierasz przez cały sezon (przez kolejne tygodniowe reset-y mapy).
* Koniec sezonu ligi:
  * reset SP i lig
  * nagrody dla top graczy (np. top 100)

---

# 3) Ataki PvP (limity, regen, ad, premium)

## Zasada

Ataki są **charge’ami**:

* `MaxAttacks = 5`
* Regeneracja: `+1` co `2h` do capu 5

## Rewarded ad

* `+1 atak / dzień` (wymaga trackowania “claim” per dzień)

## Premium

* Możliwość dokupienia dodatkowych ataków za walutę premium (Credits).
* W MVP: brak “nielimitowanej energii” i brak skomplikowanych refill timerów.

---

# 4) Zasady mapy (sąsiedztwo / brak teleportu)

* **Brak teleportu**: atak jest możliwy tylko na sektor sąsiedni.
* Sąsiedztwo jest zdefiniowane w `MapConfig` (graf połączeń sektorów).
* Walidacja w domenie: `AttackSector()` odrzuca atak na niesąsiedni sektor.

---

# 5) Walka, stability, strategie

## Snapshot (źródło mocy)

Snapshot zawiera minimum:

* `PvpPower` (stat do walki; sub ×2 nie działa bezpośrednio)
* liga + SP (do UI/telemetrii i balansowania)

## Stability (anty-flip)

* Po przejęciu sektor startuje z niską stabilnością.
* Stabilność rośnie w czasie (clamp 0..100).
* Stability wpływa na obronę sektora.

## Strategie (3 opcje)

* **Aggressive**: większa szansa wygranej, ale niższa stabilność startowa po przejęciu
* **Stable**: neutralny wariant
* **Risky**: największy upside, największy downside

## Rozstrzygnięcie (MVP)

* `AttackRoll = AttackerPvpPower × StrategyMultiplier × Random(0.95–1.05)`
* `DefenseRoll = DefenderPvpPower × StabilityMultiplier(Stability) × Random(0.95–1.05)`
* `AttackRoll > DefenseRoll` → przejęcie sektora

---

# 6) Anti-snowball (MUST)

MVP musi mieć co najmniej:

* cap lub diminishing returns na łączny bonus z sektorów
* maintenance penalty po przekroczeniu progu sektorów

Cel: dominacja jest możliwa, ale nie “nieodwracalna”.

---

# 7) Overclock (zamiast tap boost)

Overclock to aktywny przycisk decyzji (nie spam).
Parametry MVP (konfiguracja, guardrails):

* czas trwania: 10s
* cooldown/regen: 1 ładunek co 90s
* max ładunki: 2
* efekty (MVP): ×3 produkcja, +20% PvP attack power, +10% szybszy wzrost stability świeżo zdobytego sektora

---

# 8) MVP monetization

* Rewarded ads:
  * x2 offline claim
  * +1 atak/dzień
* Subskrypcja (1 plan):
  * +100% produkcji
  * brak reklam przerywających (interstitial); rewarded ads zostają (opcjonalne)

---

# 9) Telemetria (MUST w MVP)

Minimalny zestaw eventów:

* session_start
* offline_claim (amount)
* generator_upgrade (id, level)
* prestige (count)
* overclock_activate
* map_open
* sector_view (sectorId)
* sector_attack (sectorId, strategy)
* sector_result (sectorId, win)
* ad_watched (placement)
* subscription_started

---

# 10) Out of scope (MVP)

* real-time PvP
* czat / gildie / sojusze
* tarcze i rozbudowane systemy obrony
* artefakty + rerolle (po MVP)
* battle pass (po MVP)
* dynamiczne oferty / VIP 49+
* server-side resolution (po MVP; dopiero po danych z MVP)
