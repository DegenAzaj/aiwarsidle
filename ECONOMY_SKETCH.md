# ECONOMY_SKETCH.md

> Uwaga: w aktualnym MVP ekonomia sektorów ma wspierać mapę PvE na hex gridzie `radius = 4` (~61 sektorów), z 6 stronami konfliktu i resetem mapy co ok. 72h. Historyczne liczby w szkicu poniżej należy interpretować pod te nowsze założenia.

Dobra. Projektujemy ekonomię tak, żeby:

* early game był szybki i satysfakcjonujący
* D1 dawał 1–2 prestiże
* D3–D7 zaczynała się walka o mapę
* snowball był kontrolowany
* było miejsce na monetizację

Robimy wersję **MVP-safe, ale skalowalną**.

---

# 🧱 1️⃣ Waluty

## Soft Currency

➡ Compute Power (CP)

Służy do:

* upgrade generatorów
* prestige threshold

---

## Hard Currency (premium)

➡ Credits

Służy do:

* dodatkowe ataki
* boost produkcji (czasowy)
* reroll artefaktów (później)

---

## Punkty sezonowe

➡ Season Points (SP)

Służą wyłącznie do:

* rankingu
* nagród sezonowych

Nie wpływają na ekonomię produkcji.

---

# ⚙ 2️⃣ Generatory

5 generatorów.

## Koszt

```
cost = baseCost × (1.15 ^ level)
```

Dlaczego 1.15?

* szybki early
* wyraźne spowolnienie po ~100 levelach

---

## Produkcja

```
production = baseOutput × level × globalMultiplier
```

Milestone co 25 poziomów:
×2 output generatora

---

# 🎯 3️⃣ Tempo progresu (docelowe)

### Dzień 1

* 1 prestige w 20–30 min
* 2–3 prestiże w 24h aktywnego gracza

### Dzień 3

* 5–7 prestiży
* 6–10 sektorów w peak momencie

### Dzień 7

* 10–15 prestiży
* realny maintenance pressure

---

# 🔁 4️⃣ Prestige

Warunek:

```
TotalEarnedCP >= Threshold
```

Threshold rośnie:

```
threshold = base × (1.6 ^ prestigeCount)
```

Nagroda:

```
Shards += 1
```

W MVP: `Shards` ma cap do 10 (1 linia permanent upgrade).

Global multiplier:

```
1 + (Shards × 0.05)
```

Czyli:
Każdy shard = +5%

---

# 🗺 5️⃣ Ekonomia sektorów

Każdy sektor daje:

* +4% production
* +2% PvP power

Ale działa diminishing return.

---

## Diminishing model

Jeśli masz N sektorów:

```
effectiveBonus = baseBonus × (1 / (1 + 0.1 × (N-1)))
```

Czyli:

* 1 sektor → 100%
* 5 sektorów → ~71%
* 10 sektorów → ~53%
* 20 sektorów → ~33%

Naturalny hamulec.

---

# 🧊 6️⃣ Maintenance

Powyżej 5 sektorów:

```
maintenancePenalty = (N - 5) × 1%
```

Czyli:

* 10 sektorów → -5% produkcji
* 15 sektorów → -10%

To zapobiega runaway snowball.

---

# ⚔ 7️⃣ PvP ekonomia

## Ataki

5 ataków dziennie  
1 co 2h regen  
Max 5

Premium:

* +1 za reklamę
* +X za Credits

---

## Nagrody

Wygrana:

* +10 SP
* +CP bonus (np. 2% dziennej produkcji)

Obrona:

* +5 SP

Utrata:

* -5 SP

Porażka w ataku:

* -2 SP

Underdog bonus:

* +3–5 SP jeśli atakujesz silniejszego

---

# 📆 8️⃣ Mapa PvP: 7 dni (w ramach ligi: 30 dni)

* Mapa resetuje się co 7 dni (ownership + stability).
* Punkty sezonowe (SP) z PvP sumują się w sezonie ligi trwającym 30 dni.

Za każdy sektor co 24h:

* +2 SP

To premiuje utrzymanie,
ale aktywność nadal ważniejsza.

---

# 💰 9️⃣ Punkty monetizacji

Naturalne miejsca:

* gdy maintenance zaczyna boleć
* gdy stabilność sektora niska
* gdy kończy się energia ataku
* gdy jesteś blisko Top 10

Boost:

* +25% produkcji przez 8h

Cena: testować

---

# 📊 10️⃣ Kontrola inflacji

Musisz monitorować:

* średni CP per gracz
* średnią ilość sektorów
* średni SP per dzień
* różnicę między Top 1 a Top 10

Jeśli Top 1 ma >2× punktów Top 5
→ balans jest zły.

---

# 🎯 Finalna struktura ekonomii

Short loop:  
Generator → upgrade → milestone

Mid loop:  
Prestige → multiplier → szybciej zdobywasz sektory

Long loop:  
Sektory → SP → liga → sezon

Meta:  
Shards + artefakty (po soft launchu)

---

# 🧠 Dlaczego to stabilne

* produkcja rośnie wykładniczo
* bonusy sektorów maleją
* maintenance hamuje dominację
* SP nie zależy tylko od wielkości

Masz:  
kontrolowany wzrost  
kontrolowaną rywalizację  
skalowalność pod whales

---
