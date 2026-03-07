OK. Konkretnie. Bez nadmiaru feature’ów.

Idle zostaje silnikiem.
Mapa staje się sceną.
PvP to walka o sektory.
Liga to meta-ranking sezonowy.

---

# 1️⃣ MAPA — jak ma wyglądać w MVP

Nie robisz Google Maps.
Nie robisz 3D globusa.

## MVP wersja:

* 1 ekran
* heksagonalna siatka (`hex grid`) w kształcie dużego hexa
* `radius = 4` → ok. 61 sektorów
* 6 stron konfliktu: gracz + 5 botów
* każdy uczestnik startuje w jednym narożniku mapy
* większość sektorów jest neutralna na starcie

Każdy sektor:

* ma nazwę
* ma bonus (np. +5% global production)
* ma właściciela (kolor)
* ma wskaźnik “stability”
* może być `Home Sector` (nietykalny, nie do przejęcia)

Klikasz sektor → panel z info:

* Właściciel
* Bonus
* Twoja szansa wygranej
* Wybór strategii
* Atak

---

# 2️⃣ Jak działa PvP na mapie

### Każdy sektor ma:

* Base Value (bonus)
* Stability (0–100)
* Owner Snapshot (power właściciela)

---

## Atak sektora

Krok 1: Klikasz sektor
Krok 2: Wybierasz strategię
Krok 3: Symulacja

Możesz atakować tylko sąsiedni hex.
To naturalnie tworzy fronty na granicach terytoriów.

### Siła ataku:

TwojaPvpPower × StrategyMultiplier × Random

### Siła obrony:

OwnerPvpPower × StabilityMultiplier × Random

Jeśli wygrasz:

* przejmujesz sektor
* stability resetuje się nisko (łatwy do odbicia)
* dostajesz nagrodę

Jeśli przegrasz:

* stability rośnie
* cooldown ataku

---

# 3️⃣ Stability — bardzo ważny mechanizm

To zapobiega flipowaniu sektorów co minutę.

Każdy świeżo przejęty sektor:

* startuje z niską stabilnością
* ma okno niestabilności
* rośnie z czasem

Im wyższa stabilność:

* tym trudniej go przejąć

To daje:

* sens utrzymywaniu sektorów
* defensywną wartość mocy

---

# 4️⃣ System lig (nie mieszaj z mapą)

Liga ≠ ilość sektorów.

Liga zależy od:

* PvP points
* liczby wygranych
* aktywności sezonowej

5 lig:

* Bronze
* Silver
* Gold
* Diamond
* Apex

---

## Jak zdobywasz punkty ligi

Za:

* przejęcie sektora
* udaną obronę
* wygraną walkę

Tracisz punkty za:

* utratę sektora
* porażkę w ataku

Liga daje:

* lepsze nagrody sezonowe
* meta-progres poza pojedynczą potyczką mapy

---

# 5️⃣ Cykle: Mapa PvP (~72h) + Liga (30 dni)

Mapa PvP:

* resetuje się **co ok. 72h** (ownership + stability)
* daje “krótką wojnę” i powód do częstych powrotów

Liga (sezon):

* trwa **30 dni**
* punkty ligi/Season Points zbierasz przez cały sezon (w tym przez kolejne 72h reset-y mapy)

Na koniec sezonu ligi:

* punkty ligi reset
* top 100 dostaje nagrody

To daje:

* czysty start
* nową rywalizację
* FOMO

---

# 6️⃣ Dlaczego to działa lepiej niż zwykłe PvP

Stare PvP:

> kliknij → wygrałeś → +10 punktów

Nowe PvP:

> Przejąłeś Berlin
> Ktoś atakuje Twój Singapur
> Straciłeś Nowy Jork

Masz:

* wizualną dominację
* realną stratę
* motywację do powrotu

---

# 7️⃣ Co absolutnie NIE dodawać w MVP

* guildie
* sojusze
* czat
* real-time
* 100+ sektorów
* dynamiczne eventy per sektor

Mapa musi być:

* prosta
* czytelna
* szybka

---

# 8️⃣ Idealny flow 60 sekund

1. Odbierasz offline
2. Upgrade
3. Widzisz, że bot przejął graniczny sektor
4. Atakujesz inny
5. Zdobywasz region
6. Wychodzisz

Masz historię. Nie tylko liczby.

---

# 9️⃣ Największe ryzyko

Jeśli:

* bonus sektorów jest za mały → nikogo to nie obchodzi
* bonus za duży → snowball efekt (1 gracz dominuje)

Balans sektorów jest kluczowy.

---
