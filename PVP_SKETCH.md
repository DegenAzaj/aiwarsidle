# PVP_SKETCH.md

Dobra. Projektujemy to tak, żeby:

* dominacja była możliwa
* ale nie nieodwracalna
* PvP było częste
* ale nie spamowalne
* punkty sezonowe premiowały aktywność, nie tylko rozmiar imperium

---

# 🧊 SYSTEM ANTI-SNOWBALL

Cel:  
Duże imperium ma przewagę, ale rośnie mu koszt utrzymania i podatność.

---

## 1️⃣ Diminishing Return z sektorów

Każdy sektor daje bonus produkcji.

Model:

* 1–3 sektory → 100% bonusu
* 4–6 → 80%
* 7–10 → 60%
* 11–15 → 40%
* 16+ → 25%

Przykład:  
Jeśli sektor daje +5%:

* przy 2 sektorach → +5%
* przy 12 sektorach → realnie +2%

Efekt:

* wczesna ekspansja opłacalna
* masowa dominacja mniej efektywna

---

## 2️⃣ Maintenance Cost (miękki hamulec)

Każdy sektor powyżej 5:

* -1% global production efficiency

Czyli:

* Masz 10 sektorów → -5%
* Masz 20 sektorów → -15%

To sprawia, że:

* zbyt duże imperium spowalnia
* mniejsze frakcje mogą dogonić

---

## 3️⃣ Stability scaling

Im więcej sektorów ma gracz:

* tym wolniej rośnie stabilność nowych sektorów
* tym szybciej spada przy ataku

Czyli:

* Małe imperium → trudniej ruszyć
* Duże imperium → bardziej kruche na obrzeżach

To tworzy naturalny balans.

---

# ⚔ SYSTEM PvP

---

## 1️⃣ Zasady ataku

Możesz atakować tylko:

* sektor sąsiedni do swojego
* sektor neutralny
* sektor wroga

Nie możesz:

* teleportować się po mapie
* atakować przez 3 pola

---

## 2️⃣ Liczba ataków

Bazowo:

* 5 ataków dziennie
* regeneracja 1 co 2h
* max 5

Dodatkowo:

* 1 atak za reklamę
* dokupienie dodatkowych ataków za walutę premium (Credits) — bez nielimitowanej energii w MVP

To daje monetizację bez paywall.

---

## 3️⃣ Rozstrzygnięcie walki

Siła ataku:

TwojaPvpPower  
× StrategyMultiplier  
× Random(0.95–1.05)

Siła obrony:

OwnerPvpPower  
× StabilityMultiplier  
× Random(0.95–1.05)

Jeśli wygrasz:

* przejmujesz sektor
* stability resetuje się do niskiego poziomu
* dostajesz punkty sezonowe

Jeśli przegrasz:

* sektor zyskuje +stability
* mały reward consolation

---

## 4️⃣ Strategie ataku

Agresywna  
+15% ataku  
-20% stability startowej po przejęciu

Stabilna  
brak bonusów

Ryzykowna  
+25% ataku  
-40% stability + większa strata przy porażce

To dodaje decyzję bez komplikowania systemu.

---

# 🏆 SYSTEM PUNKTÓW SEZONOWYCH

Dominacja ≠ liczba sektorów.  
Dominacja = punkty.

---

## Punkty za:

Przejęcie sektora  
+10

Obrona sektora  
+5

Atak na silniejszego (underdog bonus):

* dodatkowe 3–5

Seria wygranych (3 z rzędu)  
+10 bonus

---

## Punkty ujemne:

Utrata sektora  
-5

Porażka w ataku  
-2

---

## Dodatkowe punkty pasywne

Za każdy sektor co 24h:  
+2 punkty

To premiuje utrzymanie, ale nie zabija aktywności.

---

# 📆 Mapa PvP: reset co 7 dni (liga: 30 dni)

Liga (sezon) trwa 30 dni, ale sama mapa PvP resetuje się co 7 dni, żeby utrzymać dynamikę i dać częste “końcówki”.

Na koniec:

Ranking wg punktów sezonowych, nie wg ilości sektorów.

Reset:

* mapa: ownership + stability (co 7 dni)
* punkty sezonowe (SP): reset dopiero na koniec sezonu ligi (30 dni)
* ligi: reset dopiero na koniec sezonu ligi (30 dni)

---

# 🧠 Dlaczego to działa

Duży gracz:

* ma dużo sektorów
* ale malejące bonusy
* wyższy maintenance
* bardziej podatne obrzeża

Mały gracz:

* ma szybki wzrost
* underdog bonus
* łatwiejszą stabilność

To utrzymuje mapę dynamiczną.

---

# 🚨 Najważniejsze parametry do testów

* ilość ataków dziennie
* próg diminishing return
* maintenance %
* stability growth rate
* punkty za obronę

Te 5 zmiennych zdecyduje czy meta żyje.

---

# 🧪 Uproszczona symulacja (szkic)

Zróbmy uproszczoną symulację 50 graczy, 7 dni, mapa 12×12 (144 hexy).

Założenia:

* 50 graczy (każdy ma 1 HOME niezniszczalny)
* 94 sektory neutralne na start
* 5 ataków dziennie / gracz
* Średni winrate przy równym power = 50%
* 20% graczy to „top performers” (lepszy power / aktywność)
* Diminishing + maintenance jak ustaliliśmy
* Punkty:
  * +10 za przejęcie
  * +5 za obronę
  * -5 za utratę
  * +2/dzień za sektor

---

## 📅 Dzień 1

Wszyscy atakują neutralne sektory.

Średnio:

* Każdy wygrywa ~3 z 5 ataków.
* 150 udanych przejęć (część na tych samych polach).

Na koniec dnia:

* Średni gracz ma 3–4 sektory.
* Top 20% mają 5–6.

Mapa w 70% zajęta.

---

## 📅 Dzień 2

Zaczynają się konflikty między graczami.

Top gracze:

* atakują słabszych sąsiadów
* zdobywają 2–3 sektory netto

Średni gracze:

* balansują (1 zdobyty, 1 stracony)

Na koniec:

* Top gracze: 8–10 sektorów
* Średni: 4–5
* Kilku pechowców: 2–3

Diminishing zaczyna działać.

---

## 📅 Dzień 3

Pierwszy efekt snowball.

Top 3–5 graczy:

* mają 12–14 sektorów
* zaczyna ich boleć maintenance
* ich nowe sektory mają niską stabilność

Mniejsi gracze:

* zaczynają odzyskiwać obrzeża dużych

Mapa dynamiczna.

---

## 📅 Dzień 4–5

Meta się stabilizuje.

Duzi gracze:

* nie rosną już szybko (maintenance + malejące bonusy)
* są atakowani z 2–3 stron

Średni gracze:

* 5–8 sektorów
* zdobywają dużo punktów za obronę

Punkty sezonowe zaczynają się rozjeżdżać.

---

## 📅 Dzień 6

Top 10 wygląda mniej więcej tak:

* Gracz A: 13 sektorów
* Gracz B: 11
* Gracz C: 9
* Gracz D–J: 6–8

Ale punktowo Gracz C może być wyżej niż B, jeśli:

* częściej wygrywał walki
* miał serię zwycięstw
* zdobywał trudne cele (underdog bonus)

To kluczowe: dominacja ≠ liczba sektorów.

---

## 📅 Dzień 7 (końcówka sezonu)

Ostatnie 24h:

* więcej ataków
* duzi gracze tracą peryferia
* małe imperia robią underdog push

Efekt końcowy (typowy):

🥇 1 miejsce: gracz z 9–12 sektorami, ale bardzo aktywny.  
🥈 2–5 miejsce: gracze z 7–10 sektorami.

Whale z 18 sektorami? Niekoniecznie wygra, bo:

* diminishing return
* maintenance
* łatwiej go farmić punktowo

---

# 📊 Co pokazuje symulacja

Przy dobrze ustawionym:

* maintenance
* diminishing
* punktach za obronę

Nie wygrywa największy.  
Wygrywa najbardziej aktywny i efektywny.

To jest zdrowa meta.

---

# ⚠️ Co by zabiło balans

1. Brak maintenance.
2. Punkty tylko za ilość sektorów.
3. Brak limitu ataków.
4. Za wysoka stabilność u dużych graczy.

Wtedy: Top 1 po dniu 3 jest nietykalny.

---

# Wniosek

Przy 50 graczach i 7 dniach:

* mapa jest dynamiczna,
* nie ma trwałej dominacji,
* top 5 walczy do końca,
* sezon ma sens.

To jest dużo bezpieczniejsze jako reset mapy niż trzymanie tej samej mapy przez 30 dni.

---
