# MVP Polish

## PvP Map Combat Polish

Cel: podnieść jakość walki na mapie tak, żeby była bardziej emocjonująca, dawała efekt "wow" i budowała nawyk wracania do gry.

## Diagnoza obecnego stanu

Obecna walka działa poprawnie jako system, ale jest zbyt płaska emocjonalnie:

- atak to głównie pojedynczy rzut `attackRoll vs defenseRoll`
- preview pokazuje win rate, ale słabo tłumaczy dramaturgię starcia
- wynik walki jest binarny i mało widowiskowy
- boty działają sensownie, ale ich zachowanie nie jest jeszcze wystarczająco "czytelne jako osobowość"
- mapa wygląda dobrze funkcjonalnie, ale jeszcze nie opowiada historii konfliktu

Efekt:

- jest decyzja, ale mało napięcia
- jest progres, ale mało satysfakcji z pojedynczego zwycięstwa
- jest mapa, ale mało powodów, żeby wracać kilka razy dziennie poza samym odnowieniem charge'y

## Pakiet 1 - Combat Feel Pack

Cel: każde kliknięcie `Attack` ma dawać emocję i czytelny payoff.

Propozycje:

- dodać krótki battle reveal zamiast samego toastu:
  - `Your power`
  - `Enemy power`
  - `Stability`
  - `Strategy`
  - final roll / rezultat
- w preview poza % dodać jakościowe etykiety:
  - `Favored`
  - `Even`
  - `Desperate`
  - `Clean Punish`
- rozróżnić typy wyników:
  - `clean win`
  - `close win`
  - `failed push`
  - `crushed`
- dodać animowany feedback na mapie po walce:
  - puls sektora
  - flash koloru ownera
  - przebieg linii frontu
  - efekt "pękania" stability po nieudanej obronie
- świeżo przejęty sektor powinien być wizualnie "gorący":
  - puls
  - podkreślona niestabilność
  - czytelny timer niestabilności

Dlaczego:

- dziś walka dzieje się matematycznie, ale nie widowiskowo
- ten pakiet mocno poprawia odczucie bez konieczności przebudowy core systemu

## Pakiet 2 - Decision Depth Pack

Cel: atak ma być ciekawą decyzją, a nie tylko kliknięciem najlepszego hexa.

Propozycje:

- mocniej odróżnić strategie:
  - `Aggressive`: mocniejsze ciśnienie na frontline, ale gorsze utrzymanie po wygranej
  - `Stable`: najbardziej przewidywalny baseline
  - `Risky`: wyższy ceiling, ale wyraźnie boleśniejsza porażka
- dodać partial outcomes:
  - porażka nie zawsze jest tylko "nic się nie stało"
  - bliska porażka może obniżać stability albo nakładać `pressure`
- dodać `front pressure`:
  - sektor wielokrotnie atakowany w krótkim czasie staje się bardziej podatny
  - pressure działa tylko na realnych frontach
- premiować walkę z klastra połączonego z `Home`
  - dobrze połączony front może dawać lekki bonus do ataku lub utrzymania sektora
- karać zbyt rozciągniętą ekspansję
  - długie cienkie fronty są dobre do rajdu, ale gorsze do utrzymania

Dlaczego:

- obecnie strategie są zbyt podobne
- wynik walki jest zbyt binarny
- gracz powinien czuć, że prowadzi kampanię, a nie tylko klika atak

## Pakiet 3 - Return Loop Pack

Cel: dać graczowi konkretne powody do wracania do gry kilka razy dziennie.

Propozycje:

- alerty mapy:
  - `Attack charges full`
  - `Front collapsing`
  - `Enemy captured nearby`
  - `Island cluster detected`
- tworzyć krótkie "okna okazji":
  - sektor z premią do przejęcia
  - chwilowo osłabiony flank botów
  - lokalna niestabilność zwiększająca sens powrotu
- dodać małe cele sesyjne:
  - `Capture 2 frontline sectors`
  - `Reconnect your isolated cluster`
  - `Break into enemy ring`
- premiować aktywną grę na froncie, nie tylko bierne posiadanie mapy
- mocniej zintegrować `Overclock` z mapą:
  - krótki "hero moment"
  - wejście na mapę, aktywacja, szybkie przebicie frontu

Dlaczego:

- samo odnawianie charge'y to za mało, żeby budować mocny nawyk powrotu
- gracz powinien mieć poczucie, że mapa "żyje", kiedy go nie ma

## Pakiet 4 - Map Drama Pack

Cel: mapa ma opowiadać historię konfliktu i różnicować sytuacje taktyczne.

Propozycje:

- wprowadzić sektory specjalne:
  - `Power Relay`
  - `Forge`
  - `Uplink`
  - `Shield Node`
- specjalne sektory powinny dawać nie tylko bonus ekonomiczny, ale też sens mapowy:
  - szybszy regen ataków
  - mocniejsza obrona klastra
  - łatwiejsze reconnect
  - bonus do `Overclock`
- dodać frontline objectives:
  - przebicie przez konkretny sektor otwiera nową linię nacisku
- mocniej wyeksponować osobowości botów:
  - agresywny bot zostawia spalone fronty
  - ekspansywny zalewa neutralne obszary
  - defensywny buduje bastiony
- eventy mapy:
  - `Supply Drop`
  - `Signal Jam`
  - `Instability Storm`
  - `Reactor Surge`

Dlaczego:

- sama geometria mapy nie wystarczy
- gracz powinien pamiętać konkretne momenty i miejsca na mapie

## Największy efekt przy najmniejszym koszcie

Najlepsza kolejność wdrażania:

1. `Combat Feel Pack`
2. lekka część `Decision Depth Pack`
3. lekka część `Return Loop Pack`

Największy ROI da:

- lepszy reveal wyniku walki
- wyraźniejsze różnice strategii
- partial outcomes
- front pressure
- alerty mapy i okna okazji

## Rekomendacja praktyczna

Najpierw warto zrobić mało nowych zasad, ale bardzo mocno podanych:

- lepszy feedback po walce
- czytelniejsze ryzyko i nagroda
- powody do częstszego wracania

Dopiero potem dokładać głębsze systemy:

- sektory specjalne
- eventy mapy
- bardziej złożoną dramaturgię frontów

## Ryzyka

- zbyt dużo RNG zabije poczucie kontroli
- zbyt dużo dodatkowych zasad zabije czytelność MVP
- zbyt mocny snowball nagród zrobi mapę jednostronną
- eventy bez mocnego feedbacku będą wyglądały jak losowy chaos

## Wniosek

Obecna walka ma dobry fundament systemowy, ale potrzebuje:

- większej dramaturgii
- mocniejszego feedbacku
- ciekawszych decyzji
- silniejszej pętli powrotu

Najważniejsze jest to, żeby każda walka dawała:

- emocję tu i teraz
- poczucie wpływu
- i powód, by wrócić później na mapę
