---
id: ca.dettagli
titolo: Dettagli costruttivi di travi e pilastri
libreria: GPCChecker.Concrete
classi:
  - GPC.Checkers.Concrete.Detailing.MemberDetailingCalculator
  - GPC.Checkers.Concrete.Detailing.MemberDetailingInput
  - GPC.Checkers.Concrete.Detailing.MemberDetailingOptions
  - GPC.Checkers.Concrete.Detailing.PlateDetailingData
  - GPC.Checkers.Concrete.Detailing.PlateDetailingRules
  - GPC.Checkers.Concrete.Detailing.DetailingProfile
  - GPC.Checkers.Concrete.Detailing.MemberDetailingResult
  - GPC.Checkers.Concrete.Detailing.DetailingCheck
  - GPC.Checkers.Concrete.Detailing.MemberDetailingKind
  - GPC.Checkers.Concrete.Detailing.DetailingProfiles
  - GPC.Checkers.Concrete.Cracking.CrackSectionGeometry
versione: 0.34.0.0
norme:
  - ntc2018-4.1.6.1.1
  - ntc2018-4.1.6.1.2
  - ntc2018-4.1.6.1.3
  - circ2019-C4.1.6.1.1
  - en1992-1-1-4.4.1
  - en1992-1-1-8.2
  - en1992-1-1-9.2.1.1
  - en1992-1-1-9.2.1.2
  - en1992-1-1-9.2.1.4
  - en1992-1-1-9.2.2
  - en1992-1-1-9.5.2
  - en1992-1-1-9.5.3
  - na-uni-2012-4.4.1
  - na-uni-2012-8.2
  - na-uni-2012-9.2.2
  - na-uni-2012-9.5.2
  - na-uni-2012-9.5.3
  - na-dk-2024-9
  - cnr-dt200-r1-2013
stato: bozza
---

# Dettagli costruttivi di travi e pilastri

## 1. Scopo

Il metodo controlla le regole di dettaglio di una sezione di trave o di pilastro in c.a. ordinario. I controlli
sono:

- interferro minimo;
- copriferro nominale e margine di copriferro di ogni barra;
- armatura longitudinale minima e massima;
- diametro, area minima, passo e interasse trasversale delle braccia delle staffe;
- trattenimento delle barre compresse.

Ogni regola produce un controllo con chiave stabile, valore effettivo e limite con unità, ed esito: soddisfatto, non
soddisfatto oppure in sospeso. Un controllo è in sospeso quando mancano dati o conferme, oppure quando la regola
nazionale non è implementata. L'esito complessivo è non soddisfatto se almeno un controllo non è soddisfatto, in
sospeso se nessun controllo fallisce ma almeno uno è in sospeso, soddisfatto negli altri casi.

## 2. Campo di applicazione

| Caso | Stato | Comportamento del programma |
| --- | --- | --- |
| Trave (sezione 1D), flessione in entrambi i versi | supportato | le due metà della sezione sono verificate come zone tese potenziali |
| Pilastro (sezione 1D) | supportato | NEd è la compressione massima di progetto, fornita dal chiamante |
| Sezioni rettangolari, a T, circolari, cave, poligonali | supportato | geometria esplicita: contorno, fori e barre ordinarie della sezione |
| Strisce di soletta piena (EN 9.3.1) e pareti (EN 9.6), profilo NTC 2018 (0.0.18.0) | supportato | striscia rettangolare; interasse misurato lungo gli strati (6.5) |
| Fondazioni, mensole tozze, nodi, zone di diffusione | non supportato | fuori dal metodo |
| Sezioni precompresse, sezioni composte acciaio-calcestruzzo | non supportato | la geometria considera solo le barre ordinarie; il verificatore del modello restituisce «non supportato» |
| Regole per zone sismiche (NTC cap. 7, EN 1998-1) | non supportato | valgono solo le regole del capitolo 4 NTC e delle sezioni 8-9 EN |
| Ancoraggio delle barre inferiori agli appoggi e regola della traslazione | conferma | controllo in sospeso finché il chiamante non conferma le zone di estremità |
| Trattenimento delle barre compresse (travi) | calcolato | NTC ed Eurocodice: passo ≤ 15 Ømin (`CompressionBarRestraint`); DS anche la conferma `BarsHeldByLinks` della 9.5.3(6), che il DK NA 9.2.1.2(3) estende alle travi (riquadri D-1 e D-5) |
| Solette e pareti con profili diversi da NTC 2018 e CNR-DT 200 | non supportato | `NotSupportedException`; soletta che non è una striscia rettangolare senza fori: `ArgumentException` |
| Quota minima di staffe nell'armatura a taglio (NTC 50%, EN β3 = 0,5) | non supportato | non verificata: richiede la ripartizione fra staffe e sagomati |
| Staffe inclinate | non supportato | α = 90° in tutte le espressioni |
| Una barra in ogni spigolo dei pilastri poligonali (EN 9.5.2(4)) | non supportato | è verificato solo il numero minimo di 4 barre nei pilastri circolari |
| Disposizione delle barre non riconosciuta per l'interasse automatico | in sospeso | controllo `LongitudinalSpacing` in sospeso |
| Percentuale di barre giuntate nella sezione di sovrapposizione, famiglia Eurocodice (EN 8.7.2(4)) | calcolato con i dati, altrimenti in sospeso | `LapPercentage` con la disposizione delle sovrapposizioni (`LapArrangementData`); senza, in sospeso (riquadro D-8) |
| Barre di diametro maggiore di 32 mm, famiglia Eurocodice (EN 8.8) | in sospeso; calcolato in parte | ancoraggio e controllo della fessurazione come conferme; sovrapposizione soddisfatta con dimensione minima ≥ 1 m, altrimenti in sospeso (riquadro D-8) |
| NTC 2018; CNR-DT 200 R1/2013 (membratura in c.a. secondo NTC) | supportato | profilo NTC |
| EN 1992-1-1, valori raccomandati | supportato | profilo EN |
| UNI EN 1992-1-1 con DM 31/07/2012 | supportato | valori nazionali di 9.2.2(8), 9.5.2(1)-(2), 9.5.3(3) |
| DS/EN 1992-1-1 con DK NA | supportato in parte | capitolo 9 invariato salvo le scelte nazionali; travi: ρw,min = 0,063 √fck/fyk (9.2.2(5)); As,min (9.2.1.1(1)) in sospeso come regola non implementata (riquadro D-5) |
| DIN EN, NS-EN, Model Code 2010, CNR-DT 204 | non supportato | `NotSupportedException` con il motivo |
| CS-TR34 | non applicabile | motivo restituito |
| ACI 318, AASHTO | non supportato | implementazione futura |

## 3. Riferimenti normativi

| Formula o grandezza | Norma | Edizione e appendice | Paragrafo | Eq. o tabella | Riscontro |
| --- | --- | --- | --- | --- | --- |
| As,min travi = 0,26 fctm/fyk bt d ≥ 0,0013 bt d; As,max = 0,04 Ac per l'armatura tesa o compressa fuori dalle sovrapposizioni; staffe ≥ 1,5 b mm²/m con almeno 3 staffe/m e passo ≤ 0,8 d; barre compresse trattenute con passo ≤ 15Ø; armatura inferiore ancorata agli appoggi di estremità con la regola della traslazione | NTC | 2018 | 4.1.6.1.1 | [4.1.45] | testo |
| Pilastri: Ø ≥ 12 mm, interasse ≤ 300 mm, As,min = 0,10 NEd/fyd ≥ 0,003 Ac; passo delle staffe ≤ 12 Ømin e ≤ 250 mm; Østaffa ≥ 6 mm e ≥ Ømax/4; As,max = 0,04 Ac fuori dalle sovrapposizioni | NTC | 2018 | 4.1.6.1.2 | [4.1.46] | testo |
| Copriferro e interferro commisurati ad ambiente, aggregato e aderenza; rinvio a UNI EN 1992-1-1 | NTC | 2018 | 4.1.6.1.3 | — | testo |
| Ancoraggio inferiore agli appoggi di estremità (secondo capoverso di §4.1.6.1.1) anche per le travi senza armatura a taglio | Circolare | 2019 | C4.1.6.1.1 | — | testo |
| Copriferro di durabilità (pagina [ca.durabilita-copriferri](ca.durabilita-copriferri.md)) | Circolare | 2019 | C4.1.6.1.3 | Tab. C4.1.IV | testo |
| cnom = cmin + Δcdev; cmin = max(cmin,b; cmin,dur; 10 mm); cmin,b = Ø (+5 mm con dg > 32 mm); superfici irregolari (+5 mm), abrasione (k1, k2, k3), getti contro terreno (40 e 75 mm) | EN 1992-1-1 | 2004 + AC:2010 | 4.4.1.1-4.4.1.3 | (4.1), (4.2), prospetto 4.2 | testo |
| Interferro ≥ max(k1 Ø; dg + k2; 20 mm), k1 = 1 e k2 = 5 mm | EN 1992-1-1 | 2004 + AC:2010 | 8.2(2) | — | testo |
| As,min e As,max (0,04 Ac) delle travi | EN 1992-1-1 | 2004 + AC:2010 | 9.2.1.1(1), (3) | (9.1N) | testo |
| Barre compresse trattenute con passo ≤ 15Ø | EN 1992-1-1 | 2004 + AC:2010 | 9.2.1.2(3) | — | testo (UNI EN 1992-1-1:2015, riscontrato il 1/11/2026, voce R14) |
| Ancoraggio dell'armatura inferiore agli appoggi di estremità | EN 1992-1-1 | 2004 + AC:2010 | 9.2.1.4 | — | testo |
| ρw,min = 0,08 √fck/fyk; sl,max = 0,75 d (1 + cot α); st,max = 0,75 d ≤ 600 mm | EN 1992-1-1 | 2004 + AC:2010 | 9.2.2(5), (6), (8) | (9.4), (9.5N), (9.6N), (9.8N) | testo |
| Pilastri: Ømin = 8 mm; As,min = max(0,10 NEd/fyd; 0,002 Ac); As,max = 0,04 Ac (0,08 Ac nelle sovrapposizioni); almeno 4 barre nei pilastri circolari | EN 1992-1-1 | 2004 + AC:2010 | 9.5.2(1)-(4) | (9.12N) | testo |
| Østaffa ≥ max(6 mm; Ømax/4); scl,tmax = min(20 Ømin; b; 400 mm), ridotto di 0,6 vicino a travi e solette e nelle sovrapposizioni con Ømax > 14 mm; barre d'angolo trattenute; nessuna barra compressa a più di 150 mm da una barra trattenuta | EN 1992-1-1 | 2004 + AC:2010 | 9.5.3(1), (3), (4), (6) | — | testo |
| k1 = 1, k2 = 5 mm; Δcdev = 10 mm; k1/k2/k3 = 5/10/15 mm; getti contro terreno 40/75 mm | UNI EN 1992-1-1 | DM 31/07/2012 | 8.2(2), 4.4.1.2(13), 4.4.1.3(2)-(4) | — | testo |
| st,max = 0,75 d ≤ 300 mm; Ømin = 12 mm; As,min = max(0,10 NEd/fyd; 0,003 Ac); As,max 0,04 Ac (0,08 Ac nelle sovrapposizioni); scl,tmax = min(12 Ømin; b; 250 mm) | UNI EN 1992-1-1 | DM 31/07/2012 | 9.2.2(8), 9.5.2(1)-(3), 9.5.3(3) | — | testo |
| Capitolo 9 invariato salvo 9.2.1.1(1) e 9.2.2(5) (scelte nazionali) e 9.2.1.2(3) (vale anche 9.5.3(6)) | DS/EN 1992-1-1 | DK NA:2024 | capitolo 9 | (9.5N NA) | testo (riquadro D-5) |
| Barre tese giuntate nella stessa sezione: 100% con le barre in un solo strato, 50% con più strati; barre compresse e di ripartizione tutte nella stessa sezione | EN 1992-1-1 | 2004 + AC:2010 | 8.7.2(4) | — | testo (UNI EN 1992-1-1:2015, letto il 1/11/2026) |
| Barre con Ø > φlarge: fessurazione con armatura di pelle o con il calcolo; ancoraggio con dispositivi meccanici o rettilineo con staffe di confinamento; niente sovrapposizioni salvo dimensione minima 1,0 m o tensione ≤ 80% di quella di progetto; armatura trasversale aggiuntiva ΣAsh ≥ 0,25 As n1, ΣAsv ≥ 0,25 As n2, passo ≤ 5Ø; armatura di pelle ≥ 0,01 Act,ext e 0,02 Act,ext | EN 1992-1-1 | 2004 + AC:2010 | 8.8(1)-(8) | (8.12), (8.13) | testo (UNI EN 1992-1-1:2015, letto il 1/11/2026) |
| φlarge = 32 mm | UNI EN 1992-1-1 | DM 31/07/2012 | 8.8(1) | — | testo |
| φlarge | DS/EN 1992-1-1 | DK NA:2024 | 8.8(1) | — | da riscontrare (usato il valore raccomandato, dichiarato nel controllo) |
| Membratura in c.a. secondo NTC | CNR-DT 200 | R1/2013 | — | — | da riscontrare |

## 4. Ipotesi

- La sezione è descritta da contorno, fori e barre ordinarie con posizione, diametro e area. Le barre da
  precompressione sono escluse.
- Le larghezze sono dati espliciti: bt della zona tesa, superiore o inferiore; bw dell'anima per le staffe. Il metodo
  non le ricava dalla forma.
- Trave: la sezione è divisa a metà della sua altezza. Le barre della metà superiore formano l'armatura della zona
  superiore e quelle della metà inferiore l'armatura della zona inferiore. Entrambe le zone sono considerate
  potenzialmente tese (inversione del momento); l'altezza utile d di ciascuna è misurata dal lembo opposto al
  baricentro delle sue barre.
- Pilastro: NEd è il modulo della massima compressione di progetto dello stato verificato (il verificatore del modello
  fa una verifica per asta e stato).
- Il copriferro di durabilità cmin,dur viene dal progetto di durabilità (pagina
  [ca.durabilita-copriferri](ca.durabilita-copriferri.md)) insieme alle maggiorazioni per superficie irregolare e
  abrasione e al minimo dei getti contro terreno. Senza cmin,dur `NominalCover` è in sospeso e `BarCoverMargin` non è
  prodotto.
- Le staffe sono a 90°, con diametro, passo e numero di braccia dati. Le braccia sono equidistanti sull'anima.
- Le conferme sono dati del chiamante: barre compresse trattenute; zone di estremità (ancoraggio agli appoggi e
  regola della traslazione; per i pilastri Eurocodice, passo ridotto vicino a travi e solette).

## 5. Notazione, unità e convenzioni

Unità: mm, mm², MPa, N. Diversamente dalla convenzione generale della libreria (compressione negativa), NEd è il
modulo della compressione (positivo).

| Simbolo | Significato | Unità | Nel codice |
| --- | --- | --- | --- |
| Ac | area del calcestruzzo | mm² | `ConcreteArea` |
| fck, fctm, fyk, fyd | resistenze dei materiali | MPa | `Fck`, `Fctm`, `Fyk`, `Fyd` |
| bt,sup, bt,inf | larghezza della zona tesa superiore e inferiore | mm | `TopWidth`, `BottomWidth` |
| bw (b nella NTC) | larghezza dell'anima per le staffe | mm | `WebWidth` |
| b | dimensione minore del pilastro (ingombro del contorno) | mm | `CrackSectionGeometry.Width`, `Height` |
| NEd | modulo della massima compressione di progetto | N | `Compression` |
| Øw, s, n | diametro, passo e numero di braccia delle staffe | mm, mm, — | `LinkDiameter`, `LinkSpacing`, `LinkLegs` |
| dg | dimensione massima dell'aggregato | mm | `Aggregate` |
| cnom | copriferro nominale (alle staffe; alle barre se non ci sono staffe) | mm | `NominalCover` |
| cmin,dur | copriferro minimo di durabilità | mm | `MinimumDurabilityCover` |
| Δcdev | tolleranza di esecuzione | mm | `CoverDeviation` |
| Δcadd | maggiorazioni di cmin (superficie irregolare, abrasione) | mm | `CoverAddition` |
| cground | copriferro nominale minimo dei getti contro terreno (40 o 75 mm; 0 = cassero) | mm | `GroundCover` |
| Ømin, Ømax | diametro minimo e massimo delle barre longitudinali | mm | calcolati |
| As | area dell'armatura longitudinale (totale o della zona) | mm² | calcolata |
| d | altezza utile della zona | mm | calcolata |
| ai,j | distanza netta fra le barre i e j | mm | calcolata |
| ci | copriferro geometrico della barra i (distanza netta da contorno e fori) | mm | `CrackSectionGeometry.BarCover` |

Le chiavi dei controlli delle travi che dipendono dalla zona hanno il suffisso della faccia: `:Bottom` (zona inferiore)
e `:Top` (zona superiore).

## 6. Formulazione

### 6.1 Controlli comuni

Interferro minimo, su tutte le coppie di barre (EN 8.2(2); NTC §4.1.6.1.3), chiave `ClearSpacing`:

```math
a_{min} = \min_{i<j}\left[\sqrt{(x_i-x_j)^2+(y_i-y_j)^2} - \frac{\varnothing_i+\varnothing_j}{2}\right] \ \ge\ \max\left(20\ \text{mm};\ \varnothing_{max};\ d_g + 5\ \text{mm}\right) \qquad \text{(D.1)}
```

Copriferro richiesto per un diametro di aderenza Øb (EN (4.1)-(4.2), 4.4.1.2(3), (11), (13), 4.4.1.3(1), (4)):

```math
c_{req}(\varnothing_b) = \max\left\{\max\left[10\ \text{mm};\ c_{min,dur};\ \varnothing_b + 5\ \text{mm}\cdot[d_g>32\ \text{mm}]\right] + \Delta c_{add} + \Delta c_{dev};\ c_{ground}\right\} \qquad \text{(D.2)}
```

Il minimo dei getti contro terreno cground vale per tutte le facce della sezione, sia in cnom sia per ogni barra (a
favore di sicurezza sulle facce non gettate contro terreno). Verifiche del copriferro: nominale con Øb = Øw (o Ømax
senza staffe), chiave `NominalCover`; margine di ogni barra con il suo diametro, chiave `BarCoverMargin`:

```math
c_{nom} \ge c_{req}(\varnothing_w), \qquad \Delta c = \min_i\left[c_i - c_{req}(\varnothing_i)\right] \ge 0 \qquad \text{(D.3)}
```

### 6.2 Travi

Zone e altezze utili (ymid a metà dell'ingombro verticale; ȳ baricentro delle barre della zona):

```math
d_{inf} = y_{max} - \bar y_{inf}, \qquad d_{sup} = \bar y_{sup} - y_{min} \qquad \text{(D.4)}
```

Armatura minima e massima di ciascuna zona (NTC [4.1.45]; EN (9.1N) e 9.2.1.1(3)), chiavi `MinimumTension:<faccia>` e
`MaximumTension:<faccia>`. La massima non è controllata nelle zone di sovrapposizione:

```math
A_{s,zona} \ge \rho_{min}\,b_t\,d, \quad \rho_{min} = \max\left(0{,}26\,\frac{f_{ctm}}{f_{yk}};\ 0{,}0013\right); \qquad A_{s,zona} \le 0{,}04\,A_c \qquad \text{(D.5)}
```

Se una metà della sezione non contiene barre, il limite minimo è ρmin per la larghezza e l'altezza d'ingombro della
sezione e il valore effettivo è zero.

Staffe con il profilo NTC (§4.1.6.1.1), chiavi `MinimumLinks:<faccia>`, `LinkSpacing:<faccia>` e
`CompressionBarRestraint`:

```math
\frac{A_{st}}{s} = n\,\frac{\pi\,\varnothing_w^2}{4}\,\frac{1000}{s} \ge 1{,}5\,b\ \ [\text{mm}^2/\text{m}]; \qquad s \le \min\left(\frac{1000}{3};\ 0{,}8\,d\right); \qquad s \le 15\,\varnothing_{min} \qquad \text{(D.6)}
```

Staffe con la famiglia Eurocodice (EN 9.2.2(5), (6), (8) con α = 90°), chiavi `MinimumLinks:<faccia>`,
`LinkSpacing:<faccia>` e `LinkLegSpacing:<faccia>`:

```math
\frac{A_{sw}}{s} \ge \rho_{w,min}\,b_w\cdot 1000, \quad \rho_{w,min} = \frac{0{,}08\sqrt{f_{ck}}}{f_{yk}}; \qquad s \le 0{,}75\,d; \qquad s_t = \frac{b_w - 2\,c_{nom} - \varnothing_w}{n-1} \le \min\left(0{,}75\,d;\ s_{t,lim}\right) \qquad \text{(D.7)}
```

con st,lim = 600 mm (EN e DS) oppure 300 mm (DM 31/07/2012). Con meno di due braccia st è in sospeso. Con DS
ρw,min = 0,063 √fck/fyk (DK NA 9.2.2(5), (9.5N NA)); il controllo di ρmin (D.5) resta in sospeso come regola
nazionale non implementata (riquadro D-5).

> **Scostamento corretto — D-1 (R14) Trattenimento delle barre compresse nelle travi (Eurocodice)**
>
> - Norma: EN 9.2.1.2(3) richiede un passo delle staffe ≤ 15Ø per le barre compresse considerate nella resistenza
>   (anche NTC §4.1.6.1.1).
> - Programma fino a Concrete 0.27: nella famiglia Eurocodice il limite non era calcolato e il controllo chiedeva una
>   conferma (`BarsHeldByLinks`); il profilo NTC lo calcola, con Ømin di tutte le barre della sezione.
> - Correzione (ANTHEA 1.16.0): `CompressionBarRestraint` anche per EN, UNI e DS, passo ≤ 15 Ømin con Ømin di tutte le
>   barre della sezione come per NTC (a favore di sicurezza quando le barre più piccole non sono considerate nella
>   resistenza). La conferma `BarsHeldByLinks` resta solo per DS, sulla regola 9.5.3(6) che il DK NA 9.2.1.2(3) estende
>   alle travi (riquadro D-5).
> - Stato: corretto (voce R14 del registro delle differenze).

### 6.3 Pilastri

Profilo NTC (§4.1.6.1.2), chiavi `LongitudinalDiameter`, `LongitudinalSpacing`, `MinimumLongitudinal`,
`MaximumLongitudinal`, `LinkDiameter` e `LinkSpacing`:

```math
\varnothing_{min} \ge 12\ \text{mm}; \quad i_{max} \le 300\ \text{mm}; \quad A_s \ge \max\left(0{,}10\,\frac{N_{Ed}}{f_{yd}};\ 0{,}003\,A_c\right); \quad A_s \le 0{,}04\,A_c; \quad \varnothing_w \ge \max\left(6;\ \frac{\varnothing_{max}}{4}\right); \quad s \le \min\left(250;\ 12\,\varnothing_{min}\right) \qquad \text{(D.8)}
```

L'interasse imax è la massima distanza fra barre adiacenti allineate sulla stessa retta orizzontale o verticale,
purché il punto medio cada nel calcestruzzo. Nelle sezioni circolari con un solo anello è l'arco fra barre adiacenti.
Con l'opzione `MemberDetailingOptions.ColumnSpacingAlongPerimeter` (F5.15, R12, 26/10/2026) imax si misura lungo il
perimetro delle barre: è il massimo tratto fra barre consecutive sul contorno convesso dei centri delle barre, se ogni
barra vi giace. Se qualche barra è interna (sezioni a T, cave, barre interne) resta la misura per allineamenti; le
sezioni circolari restano invariate.

> **Scostamento dichiarato — D-2 (R12) Interasse delle barre dei pilastri misurato attraverso il nucleo**
>
> - Norma: NTC 2018 §4.1.6.1.2 limita a 300 mm l'interasse delle barre, disposte lungo il perimetro.
> - Programma: misura la distanza fra barre adiacenti di ogni allineamento orizzontale o verticale, anche attraverso
>   il nucleo; in un pilastro con barre d'angolo e barre intermedie sulle facce, le due barre intermedie opposte sono
>   allineate e la loro distanza (circa il doppio dell'interasse sulla faccia) entra nel massimo.
> - Effetto: a favore di sicurezza, ma con esiti non soddisfatti indebiti. Pilastro 600 × 600 con 8Ø20: interasse
>   sulle facce 249 mm, valore calcolato 498 mm, controllo non soddisfatto. Pilastro 400 × 400 dell'esempio: 149 mm
>   sulle facce, 298 mm calcolati, ancora entro 300 mm.
> - Programma, caso opposto: se le barre alle estremità di una faccia non sono allineate, la faccia non si misura.
>   Pilastro 300 × 500 con 3 barre sotto (x = −100, 0, 100) e 2 sopra (x = ±90): risultano 180 mm, mentre la faccia
>   laterale senza barre misura 400,12 mm, oltre 300 mm. In questo caso l'effetto è a sfavore di sicurezza.
> - Stato: correzione con l'opzione `ColumnSpacingAlongPerimeter`, misura lungo il perimetro, decisa dall'utente il 25/10/2026 (F5.15);
>   predefinito invariato, la regola di 0.0.17.0, per il contratto `detailing-contract-0.0.17.csv` e le catture
>   congelate (voce R12 del registro delle differenze).

Famiglia Eurocodice (EN 9.5.2, 9.5.3; DM 31/07/2012), chiavi `LongitudinalDiameter`, `MinimumLongitudinal`,
`MaximumLongitudinal`, `BarCount`, `LinkDiameter` e `LinkSpacing`:

```math
\varnothing_{min} \ge \varnothing_{lim}; \quad A_s \ge \max\left(0{,}10\,\frac{N_{Ed}}{f_{yd}};\ k_A\,A_c\right); \quad A_s \le 0{,}04\,A_c; \quad n_{barre} \ge 4\ (\text{circolari}); \quad \varnothing_w \ge \max\left(6;\ \frac{\varnothing_{max}}{4}\right); \quad s \le \beta\,s_{cl,tmax} \qquad \text{(D.9)}
```

| Grandezza | EN e DS | UNI (DM 31/07/2012) |
| --- | --- | --- |
| Ølim | 8 mm | 12 mm |
| kA | 0,002 | 0,003 |
| scl,tmax | min(20 Ømin; b; 400 mm) | min(12 Ømin; b; 250 mm) |
| β | 0,6 nelle zone di sovrapposizione (con `LapLinkReductionAboveFourteen` solo se Ømax > 14 mm), altrimenti 1 | come EN |

> **Scostamento dichiarato — D-3 (R13) Riduzione del passo delle staffe nelle sovrapposizioni dei pilastri**
>
> - Norma: EN 9.5.3(4)(ii) riduce il passo a 0,6 scl,tmax vicino alle giunzioni solo se il diametro massimo delle
>   barre longitudinali supera 14 mm.
> - Programma: applica la riduzione in ogni zona di sovrapposizione.
> - Effetto: a favore di sicurezza, solo con Ømax ≤ 14 mm. Con 8Ø14, staffe a passo 200 mm e pilastro 400 × 400 il
>   limite scende da 280 a 168 mm e il controllo risulta non soddisfatto.
> - Stato: corretto con l'opzione `MemberDetailingOptions.LapLinkReductionAboveFourteen` (riduzione solo con Ømax > 14 mm), decisa
>   dall'utente il 25/10/2026 (F5.15); predefinito invariato, la regola di 0.0.17.0, per il contratto (voce R13 del registro
>   delle differenze).

Per tutti i profili, nelle zone di sovrapposizione dei pilastri (EN 9.5.2(3)) e delle pareti (riferimento «EC2 §§9.5.2 /
9.6.2»), chiave `MaximumAtLap`, sull'armatura della sezione data, che deve contenere entrambe le barre di ogni giunzione
(riquadro D-6):

```math
A_s \le 0{,}08\,A_c \qquad \text{(D.10)}
```

> **Scostamento dichiarato — D-4 (R13) Armatura massima nelle sovrapposizioni con NTC**
>
> - Norma: NTC 2018 §4.1.6.1.2 fissa As,max = 0,04 Ac solo fuori dalle sovrapposizioni e non dà un limite nelle
>   giunzioni.
> - Programma: applica anche al profilo NTC il limite di 0,08 Ac di EN 9.5.2(3).
> - Effetto: regola aggiuntiva, più restrittiva della NTC; riguarda solo pilastri con più dell'8% di armatura nella
>   sezione di giunzione.
> - Stato: dichiarato (voce R13 del registro delle differenze).

> **Scostamento dichiarato — D-6 Armatura nella sezione di giunzione**
>
> - Norma: EN 9.5.2(3), nota: As,max = 0,04 Ac fuori dalle sovrapposizioni, 0,08 Ac nelle sovrapposizioni, dove le
>   barre giuntate sono contate due volte.
> - Programma: confronta con 0,08 Ac l'armatura della sezione così come il chiamante la descrive e nella zona di
>   sovrapposizione non controlla 0,04 Ac. Il verificatore del modello passa la sezione corrente dell'asta, che di norma
>   non raddoppia le barre giuntate.
> - Effetto: a sfavore di sicurezza se la sezione data non contiene le barre sovrapposte: con il 100% di barre giuntate
>   il limite effettivo raddoppia (per esempio As = 0,06 Ac accettato nella giunzione).
> - Stato: dichiarato; la descrizione della sezione di giunzione è del chiamante.

### 6.4 Conferme ed esito

Controlli in sospeso per conferma mancante:

- `BarsHeldByLinks`: NTC con staffe (ogni membratura); pilastri della famiglia Eurocodice, EN 9.5.3(6); travi DS, DK NA
  9.2.1.2(3) che estende alle travi la 9.5.3(6);
- `EndSupportAnchorage`: travi, NTC §4.1.6.1.1 (anche per le travi senza armatura a taglio, Circolare C4.1.6.1.1)
  ed EN 9.2.1.4;
- `LinkSpacingNearBeams`: pilastri Eurocodice, EN 9.5.3(4)(i);
- `LapPercentage` (senza la disposizione delle sovrapposizioni), `LargeBarAnchorage`, `LargeBarCrackControl` e, con
  dimensione minima sotto 1 m o sezione cava, `LargeBarLap`: famiglia Eurocodice, EN 8.7.2(4) e 8.8 (6.6).

L'esito complessivo è:

```math
\text{Passed} = \begin{cases} \text{false} & \exists\ \text{controllo non soddisfatto} \\ \text{null} & \text{altrimenti, se}\ \exists\ \text{controllo in sospeso} \\ \text{true} & \text{altrimenti} \end{cases} \qquad \text{(D.11)}
```

> **Scostamento dichiarato in parte — D-5 (R20) DS: valori nazionali delle travi**
>
> - Norma: DK NA:2024 9.2.2(5) fissa ρw,min = 0,063 √fck/fyk (9.5N NA); la scelta nazionale di 9.2.1.1(1) prescrive
>   nelle anime alte un'armatura distribuita sulle facce con il rapporto di 9.2.2(5); 9.2.1.2(3) estende alle travi la
>   regola 9.5.3(6).
> - Programma: dalla 1.16.0 di ANTHEA (Concrete 0.28) `MinimumLinks:<faccia>` applica ρw,min della (9.5N NA); prima era
>   in sospeso con `NotImplemented` = true. `MinimumTension:<faccia>` resta in sospeso con `NotImplemented` = true (la
>   regola nazionale delle anime alte non è implementata); il trattenimento delle barre compresse è una conferma.
> - Effetto: l'esito complessivo DS delle travi resta in sospeso per As,min, salvo quando un controllo calcolato non è
>   soddisfatto (per esempio staffe sotto ρw,min): allora è «non soddisfatto». Nessun esito errato.
> - Stato: ρw,min corretto (voce R20 del registro delle differenze, decisione dell'utente del 28/10/2026); As,min e
>   9.5.3(6) dichiarati. Il DK NA:2024 non è nella raccolta dell'ufficio (1/11/2026): 0,063 √fck/fyk non è stato
>   riscontrato di nuovo sull'edizione ufficiale; il valore raccomandato EN (9.5N) 0,08 √fck/fyk sì (9.2.2(5)).

### 6.5 Solette e pareti: interasse delle barre

Nelle strisce di soletta piena (chiave `MainSpacing`, EN 9.3.1.1(3): s ≤ min(3h; 400 mm), nelle zone critiche
s ≤ min(2h; 250 mm)) e nelle pareti (`WallVerticalSpacing`, EN 9.6.2(3): s ≤ min(3t; 400 mm)) l'interasse è la
massima distanza fra barre adiacenti dello stesso strato, cioè delle file parallele alla striscia: righe di uguale y
nella soletta (h lungo y) e nella parete con lo spessore lungo y (b ≥ h), colonne di uguale x nella parete ruotata
(b < h). La distanza fra lo strato superiore e quello inferiore, attraverso lo spessore, non entra mai (analisi dei bug
del 15/10/2026, DET-1): prima due barre allineate dei due strati valevano come adiacenti e l'interasse risultava
max(interasse nello strato; h − 2c − Ø), con esiti non soddisfatti indebiti oltre h ≈ 480 mm (striscia 1000 × 500,
c = 30, 6Ø16 per faccia: 424 mm invece di 184,8 mm). L'opzione con nome
`MemberDetailingOptions.LegacyPlateSpacingAcrossLayers` riproduce la misura precedente; la cattura congelata
`detailing-plate-legacy.csv` è stata rigenerata con la misura nuova e il test la confronta anche con l'opzione, che
restituisce i valori di ANTHEA. La regola dei pilastri (riquadro D-2) non cambia.

### 6.6 Sovrapposizioni e barre di grande diametro (famiglia Eurocodice, dalla 0.34.0.0)

Nei profili EN, UNI e DS, dopo tutti gli altri controlli (che conservano l'ordine), le regole di EN 8.7.2(4) e 8.8
(decisione dell'utente del 1/11/2026). Nella sezione di sovrapposizione (`LapZone`), con la percentuale ρl di barre
tese giuntate e il numero nl di strati delle barre giuntate dati dal chiamante (`LapArrangementData`):

```math
\rho_l \le \begin{cases} 100\% & n_l = 1 \\ 50\% & n_l \ge 2 \end{cases} \qquad \text{(D.12)}
```

Con barre di diametro maggiore di φlarge = 32 mm, in ogni sezione `LargeBarAnchorage` (8.8(3), (5)-(7)) e
`LargeBarCrackControl` (8.8(2), (8)) come conferme; nella sezione di sovrapposizione, con bmin il lato minore del
rettangolo d'ingombro (per le travi anche bw):

```math
\text{LargeBarLap: soddisfatto se}\ b_{min} \ge 1000\ \text{mm e la sezione non ha fori; altrimenti in sospeso} \qquad \text{(D.13)}
```

> **Scostamento dichiarato — D-8 Regole di EN 8.7.2(4) e 8.8 in sospeso senza i dati**
>
> - Norma: EN 8.7.2(4) ammette il 100% di barre tese giuntate nella stessa sezione solo con le barre in un solo
>   strato, altrimenti il 50%; EN 8.8 chiede per le barre oltre φlarge (32 mm, valore raccomandato e dell'appendice
>   italiana) ancoraggio meccanico o con staffe di confinamento, armatura trasversale aggiuntiva (8.12)-(8.13),
>   controllo della fessurazione con armatura di pelle o con il calcolo, e nessuna sovrapposizione salvo sezioni con
>   dimensione minima di 1,0 m o tensione delle barre non maggiore dell'80% di quella di progetto.
> - Programma: la sezione non dà la disposizione delle sovrapposizioni, gli strati giuntati, le zone di ancoraggio né
>   la tensione delle barre giuntate; senza `MemberDetailingOptions.Laps` `LapPercentage` è in sospeso; le regole di
>   8.8 sono conferme in sospeso, salvo `LargeBarLap` soddisfatto con bmin ≥ 1 m. Con DS φlarge è il valore
>   raccomandato, dichiarato nella spiegazione (DK NA non riscontrato). Il profilo NTC e le solette e pareti non
>   cambiano.
> - Effetto: nessun esito inventato; un caso della famiglia Eurocodice nella zona di sovrapposizione o con barre oltre
>   32 mm non è più «soddisfatto» senza le conferme (nel contratto dei dettagli 2 casi passano da soddisfatto a in
>   sospeso).
> - Stato: dichiarato (voce CA-METODI-02 del registro delle differenze).

## 7. Coefficienti e valori predefiniti

| Simbolo | Valore | Fonte | Modificabile | Dove nel codice |
| --- | --- | --- | --- | --- |
| interferro | max(20 mm; Ømax; dg + 5 mm) | EN 8.2(2) (k1 = 1, k2 = 5 mm); DM 31/07/2012 | no | `MemberDetailingCalculator.Calculate` |
| minimo assoluto del copriferro | 10 mm | EN (4.2) | no | `Calculate` |
| maggiorazione di cmin,b | 5 mm per dg > 32 mm | EN prospetto 4.2 | no | `Calculate` |
| ρmin | max(0,26 fctm/fyk; 0,0013) | NTC [4.1.45]; EN (9.1N) | no | `Beam` |
| As,max | 0,04 Ac (0,08 Ac nelle sovrapposizioni dei pilastri) | NTC §4.1.6.1.1-2; EN 9.2.1.1(3), 9.5.2(3) | no | `Beam`, `Column`, `Calculate` |
| staffe minime NTC | 1,5 b mm²/m; 3 staffe/m; 0,8 d; 15Ø | NTC §4.1.6.1.1 | no | `Beam` |
| ρw,min | 0,08 √fck/fyk | EN (9.5N) | no | `Beam` |
| ρw,min | 0,063 √fck/fyk | DS (9.5N NA) | no | `Beam` |
| sl,max | 0,75 d | EN (9.6N), α = 90° | no | `Beam` |
| st,max | min(0,75 d; 600 mm), UNI min(0,75 d; 300 mm) | EN (9.8N); DM 31/07/2012 | no | `Beam` |
| pilastri NTC | Ø ≥ 12; 300 mm; 0,10 NEd/fyd; 0,003 Ac; 6 mm; Ømax/4; 12 Ømin; 250 mm | NTC §4.1.6.1.2 | no | `Column` |
| pilastri EN | Ø ≥ 8; 0,002 Ac; 20 Ømin; b; 400 mm; 4 barre (circolari) | EN 9.5.2, 9.5.3 | no | `Column` |
| pilastri UNI | Ø ≥ 12; 0,003 Ac; 12 Ømin; b; 250 mm | DM 31/07/2012 | no | `Column` |
| riduzione del passo | 0,6 | EN 9.5.3(4) (riquadro D-3) | solo la condizione Ømax > 14 mm (`LapLinkReductionAboveFourteen`) | `Column` |
| cmin,dur, Δcdev, Δcadd, cground | dati | progetto di durabilità; EN 4.4.1 | sì | `MemberDetailingInput` |
| percentuale giuntata ammessa | 100% in un solo strato, 50% con più strati | EN 8.7.2(4) | dati ρl e nl (`LapArrangementData`) | `LapsAndLargeBars` |
| φlarge | 32 mm | EN 8.8(1); DM 31/07/2012 | no (`LargeBarDiameter`) | `MemberDetailingCalculator` |
| dimensione minima per sovrapporre barre grandi | 1000 mm | EN 8.8(4) | no (`LargeBarLapMinimumDimension`) | `MemberDetailingCalculator` |

## 8. Implementazione

Percorsi relativi alla radice del repository Checker (namespace `GPC.Checkers.Concrete.Detailing`, salvo indicazione).

- `MemberDetailingInput` (`GPCChecker.Concrete/Detailing/MemberDetailingCalculator.cs:97-167`), con le opzioni
  `MemberDetailingOptions` (righe 42-91), i dati di soletta e parete `PlateDetailingData` (righe 20-27) e la disposizione
  delle sovrapposizioni `LapArrangementData` (righe 34-39); `DetailingCheck` (righe 170-184), `MemberDetailingResult`
  (righe 186-194, esito aggregato (D.11) alla riga 192).
- `MemberDetailingCalculator.Calculate` (righe 215-275):
  1. controlla i dati: valori non finiti o negativi danno `ArgumentException` (righe 217-231, con la disposizione delle
     sovrapposizioni alle righe 229-231; costruttore righe 157-161);
  2. calcola Ømin, Ømax e As (riga 243) e l'interferro su tutte le coppie, (D.1) (righe 244-248);
  3. calcola il copriferro richiesto, (D.2) (riga 252), e i controlli (D.3) (righe 249-258); senza cmin,dur
     `NominalCover` è in sospeso (riga 259);
  4. smista a `Column`, `Beam`, `Slab` o `Wall` (righe 262-268);
  5. aggiunge le conferme NTC sulle barre trattenute (riga 269), il massimo nelle sovrapposizioni di pilastri e
     pareti, (D.10) (righe 270-272), e per la famiglia Eurocodice le regole di 6.6 (riga 273).
- `MemberDetailingCalculator.Column` (righe 347-382):
  - profilo NTC: righe 352-365, (D.8), con l'interasse da `CrackSectionGeometry.MaximumSpacing` (righe 355-359) o,
    con l'opzione, lungo il perimetro (`MemberDetailingCalculator.Perimeter.cs:44-96`);
  - famiglia Eurocodice: righe 366-378, (D.9), con la riduzione 0,6 nelle sovrapposizioni alle righe 374-376;
  - conferme: righe 379-381.
- `MemberDetailingCalculator.LapsAndLargeBars` (righe 391-417): (D.12) (righe 394-400), conferme di 8.8 (righe
  401-407) e (D.13) (righe 408-416); costanti `LargeBarDiameter` e `LargeBarLapMinimumDimension` (righe 210-213).
- `MemberDetailingCalculator.Beam` (righe 423-471):
  - divisione in zone (righe 427, 434), ρmin (riga 428), Ast/s (riga 429);
  - zona senza barre (righe 435-440), d (riga 442);
  - armatura minima e massima, (D.5) (righe 443-445);
  - staffe NTC, (D.6) (righe 447-451); staffe Eurocodice, (D.7) (righe 452-463);
  - trattenimento delle barre compresse (righe 468-469) e ancoraggio di estremità (riga 470).
- `MemberDetailingCalculator.National` (righe 419-420): controlli DS non implementati (`NotImplemented` = true).
- Solette e pareti: `IsRectangularStrip` (righe 278-293), `Slab` (righe 302-320), `Wall` (righe 327-345); limiti in
  `PlateDetailingRules` (`PlateDetailingRules.cs`, interferro alla riga 11) e in `LongitudinalReinforcementLimits`
  (`LongitudinalReinforcementLimits.cs`: 0,04 Ac alla riga 9, ρmin alla riga 11). `CalculateColumnOnPerimeter`
  (`MemberDetailingCalculator.Perimeter.cs:16-38`) misura l'interasse dei pilastri NTC lungo un percorso chiuso dato
  dal chiamante.
- `DetailingProfiles` (`GPCChecker.Concrete/Detailing/DetailingProfiles.cs`): risoluzione della norma per tipo esatto
  (righe 17-29, 45-52) e motivi (righe 31-43).
- `GPC.Checkers.Concrete.Cracking.CrackSectionGeometry` (`GPCChecker.Concrete/Cracking/CrackSectionGeometry.cs`):
  `From`, geometria di una sezione del modello (righe 74-87); `BarCover`, copriferro geometrico con segno rispetto a
  contorno e fori (righe 164-176); `MaximumSpacing`, interasse automatico per allineamenti o anelli (righe 273 e
  282-324).
- Uso nel verificatore del modello (repository Checker, `ModelChecker/Adapters/Concrete/ConcreteSectionVerifier.Detailing.cs`):
  una attività per asta e stato; NEd è la compressione massima dei campioni dello stato (riga 48); cmin,dur viene dal
  progetto di durabilità, prendendo il maggiore fra il valore calcolato e quello eventualmente assegnato (righe 73-84);
  i controlli in sospeso danno «dati insufficienti», le regole non implementate «non supportato» (righe 95-121).

## 9. Limiti e casi non supportati

- Solette e pareti: solo profilo NTC 2018, striscia rettangolare; l'interasse è misurato lungo gli strati (6.5).
- Le larghezze bt e bw, la dimensione dell'aggregato e il copriferro nominale sono dati: il metodo non li ricava
  dalla forma.
- La divisione a metà altezza tratta le barre di parete come armatura della zona in cui cadono. Ne cambiano l'area e
  il baricentro della zona, quindi d. Nelle sezioni con barre distribuite sull'altezza conviene verificare il
  risultato.
- L'interferro usa Ømax di tutta la sezione per ogni coppia di barre (a favore di sicurezza). Non tiene conto che le
  barre sovrapposte possono toccarsi (EN 8.2(4)).
- Senza l'opzione `ColumnSpacingAlongPerimeter` l'interasse dei pilastri NTC si misura anche attraverso il nucleo, e le
  facce con le barre d'estremità non allineate non si misurano (riquadro D-2). Con l'opzione le sezioni con barre
  interne al contorno delle barre conservano la misura per allineamenti.
- Il passo delle staffe nelle sovrapposizioni dei pilastri Eurocodice è ridotto anche con Ømax ≤ 14 mm
  (riquadro D-3).
- Pilastri della famiglia Eurocodice: non sono controllati il campo di 9.5.1 (h ≤ 4b), le tre staffe nella lunghezza
  di sovrapposizione di 9.5.3(4)(ii) e, per la «dimensione minore» del pilastro di 9.5.3(3), il rettangolo d'ingombro
  sostituisce la sezione (sovrastima nelle sezioni a L o ruotate).
- Solette con il profilo NTC: interasse massimo con i valori raccomandati EN 9.3.1.1(3), min(3h; 400 mm) per
  l'armatura principale e min(3,5h; 450 mm) per la secondaria; l'appendice italiana di UNI EN 1992-1-1 (DM 31/07/2012)
  dà 2h ≤ 350 mm e 3h ≤ 400 mm (2h ≤ 250 mm nelle zone di carichi concentrati o di momento massimo; 9.3.1.1(3), GU
  27/3/2013 p. 87), più restrittivi. Il rinvio dell'NTC 2018 per le solette non è stato riscontrato (testo non nella
  raccolta dell'ufficio): domanda aperta.
- La quota minima di staffe nell'armatura a taglio, la presenza di una barra in ogni spigolo dei pilastri
  poligonali, le staffe inclinate e le regole sismiche non sono verificate.
- Con DS, As,min delle travi resta in sospeso (riquadro D-5).
- Famiglia Eurocodice: la percentuale di barre giuntate (EN 8.7.2(4)) richiede la disposizione delle sovrapposizioni
  dal chiamante; le regole di EN 8.8 sono conferme, salvo la sovrapposizione delle barre grandi con dimensione minima
  ≥ 1 m (riquadro D-8). La distanza longitudinale fra sovrapposizioni adiacenti (0,3 l0) e l'interferro fra
  sovrapposizioni adiacenti (2Ø o 20 mm) di EN 8.7.2(3) non sono controllati.
- Testo dei riferimenti (R14), corretto nella 1.16.0 di ANTHEA (Concrete 0.28): nel profilo NTC il riferimento di
  `MinimumTension:<faccia>` delle travi cita EC2 §9.2.1.1 (prima §9.3.1.1, delle solette, che resta per le solette), i
  riferimenti NTC non hanno più lo spazio prima del numero del sottoparagrafo («§4.1.6.1.3», prima «§4.1.6.1 .3») e
  `MaximumAtLap` dei pilastri cita «EC2 §9.5.2(3)» (prima «EC2 §§9.5.2»). Il valore calcolato non cambia.

## 10. Esempio numerico verificato

Dati comuni:

- C25/30: fck = 25 MPa, fctm = 0,30 · 25^(2/3) = 2,5650 MPa;
- B450C: fyk = 450 MPa, fyd = 391,30 MPa;
- dg = 20 mm, cnom = 35 mm alle staffe, cmin,dur = 25 mm, Δcdev = 10 mm;
- staffe Ø8 a 2 braccia; nessuna sovrapposizione; conferme date.

**Trave 300 × 500 mm.** Contorno da (−150; −250) a (150; 250). Barre inferiori 3Ø20 in y = −197 mm (x = −97, 0, 97);
barre superiori 2Ø14 in y = 200 mm (x = ±100). Staffe a passo 200 mm; bt = bw = 300 mm.

1. Copriferro geometrico delle barre: 250 − 197 − 10 = 43 mm (Ø20) e 250 − 200 − 7 = 43 mm (Ø14).
2. Interferro: 97 − 20 = 77 mm ≥ max(20; 20; 25) = 25 mm.
3. Copriferro richiesto alle staffe: max(10; 25; 8) + 10 = 35 mm, uguale a cnom, soddisfatto. Margine minimo delle
   barre: 43 − (max(10; 25; 20) + 10) = 8 mm ≥ 0.
4. Armatura longitudinale:
   - ρmin = max(0,26 · 2,5650/450; 0,0013) = 0,0014820;
   - zona inferiore: d = 250 + 197 = 447 mm, As,min = 0,0014820 · 300 · 447 = 198,73 mm², As = 942,48 mm²;
   - zona superiore: d = 200 + 250 = 450 mm, As,min = 200,07 mm², As = 307,88 mm²;
   - As,max = 0,04 · 150000 = 6000 mm².
5. Staffe: Ast/s = 2 · 50,27 · 1000/200 = 502,65 mm²/m.
   - NTC: minimo 1,5 · 300 = 450 mm²/m; passo ≤ min(333,33; 357,6) = 333,33 mm; 15 · 14 = 210 mm ≥ 200 mm. Tutto
     soddisfatto.
   - EN: ρw,min bw · 1000 = 0,08 · 5/450 · 300 · 1000 = 266,67 mm²/m; sl,max = 0,75 · 447 = 335,25 mm (inferiore) e
     337,5 mm (superiore); st = (300 − 70 − 8)/1 = 222 mm ≤ min(335,25; 600) = 335,25 mm.
   - UNI: st,max = min(335,25; 300) = 300 mm.
   - DS: ρw,min bw · 1000 = 0,063 · 5/450 · 300 · 1000 = 210,00 mm²/m ≤ 502,65 mm²/m, soddisfatto; ρmin in sospeso,
     quindi esito complessivo in sospeso.

**Pilastro 400 × 400 mm.** 8Ø16 negli angoli e a metà dei lati, in ±149 mm. NEd = 2000 kN; staffe a passo 250 mm.

1. As = 8 · 201,06 = 1608,50 mm²; Ac = 160000 mm².
2. NTC:
   - As,min = max(0,10 · 2 000 000/391,30; 0,003 · 160000) = max(511,11; 480) = 511,11 mm²;
   - passo ≤ min(250; 12 · 16) = 192 mm < 250 mm: non soddisfatto;
   - interasse calcolato 298 mm (barre intermedie opposte, attraverso il nucleo) ≤ 300 mm.
3. EN: As,min = max(511,11; 320) = 511,11 mm²; passo ≤ min(20 · 16; 400; 400) = 320 mm ≥ 250 mm: soddisfatto. In
   zona di sovrapposizione (Ømax = 16 > 14 mm): 0,6 · 320 = 192 mm, non soddisfatto; As ≤ 0,08 Ac = 12800 mm².
4. UNI: As,min = 511,11 mm²; passo ≤ min(192; 400; 250) = 192 mm: non soddisfatto.

Valori della libreria (GPCChecker.Concrete 0.0.17.0, eseguita; i valori restano quelli della 0.33.0.0, verifica sul
codice del 1/11/2026, e `pagine_metodi.py` di ANTHEA ricontrolla il profilo NTC):

| Controllo | A mano | Libreria | Scarto |
| --- | --- | --- | --- |
| Trave, `ClearSpacing` | 77 ≥ 25 | 77; 25 | 0 |
| Trave, `NominalCover` | 35 ≥ 35 | 35; 35, soddisfatto | 0 |
| Trave, `BarCoverMargin` | 8 | 8 | 0 |
| Trave, `MinimumTension:Bottom` [mm²] | 942,48 ≥ 198,733 | 942,4777960769379; 198,7334045227657 | < 10⁻¹² |
| Trave, `MinimumTension:Top` [mm²] | 307,88 ≥ 200,067 | 307,8760800517997; 200,06718576117353 | < 10⁻¹² |
| Trave NTC, `MinimumLinks:Bottom` [mm²/m] | 502,65 ≥ 450 | 502,65482457436684; 450 | < 10⁻¹² |
| Trave NTC, `LinkSpacing:Bottom`; `CompressionBarRestraint` [mm] | 333,33; 210 | 333,3333333333333; 210 | < 10⁻¹² |
| Trave EN, `MinimumLinks:Bottom` [mm²/m] | 266,667 | 266,6666666666667 | < 10⁻¹² |
| Trave EN, `LinkSpacing:Bottom`; `LinkLegSpacing:Bottom` [mm] | 335,25; 222 ≤ 335,25 | 335,25; 222 ≤ 335,25 | 0 |
| Trave UNI, `LinkLegSpacing:Bottom` [mm] | 222 ≤ 300 | 222 ≤ 300 | 0 |
| Trave DS, esito complessivo | in sospeso (regole nazionali) | null; `NotImplemented` = true | — |
| Pilastro NTC, `MinimumLongitudinal` [mm²] | 511,111 | 511,1111111111111 | < 10⁻¹² |
| Pilastro NTC, `LinkSpacing` [mm] | 250 > 192, non soddisfatto | 250; 192; false | 0 |
| Pilastro NTC, `LongitudinalSpacing` [mm] | 298 (attraverso il nucleo) | 298 | 0 |
| Pilastro EN, `LinkSpacing` [mm] | 250 ≤ 320, soddisfatto | 250; 320; true | 0 |
| Pilastro EN in sovrapposizione, `LinkSpacing`; `MaximumAtLap` | 192; 12800 | 192; 12800 | 0 |
| Pilastro UNI, `LinkSpacing` [mm] | 192, non soddisfatto | 192; false | 0 |
| Pilastro 600 × 600 con 8Ø20 (riquadro D-2), `LongitudinalSpacing` | 249 sulle facce | 498, non soddisfatto | vedi riquadro |
| Pilastro 400 × 400 con 8Ø14 in sovrapposizione (riquadro D-3), `LinkSpacing` | 280 (EN 9.5.3(4)(ii)) | 168 | vedi riquadro |

## 11. Validazione

- **Casi congelati del motore precedente** (`detailing-legacy.csv` del progetto di test): 144 travi e pilastri NTC
  sulle sezioni di `detailing-sections.xml` (rettangolari, a T, circolare, cava e un pilastro con barre Ø10), con NEd,
  staffe, aggregato, cmin,dur, Δcdev, sovrapposizione e conferme variabili. Sono confrontati 1385 controlli (chiave,
  valore effettivo, limite ed esito) con tolleranza relativa 10⁻⁹.
- **Test** di `DetailingMigrationTests`:
  - `LegacyDetailingIsReproduced`: i casi congelati;
  - `EurocodeAndAnnexValuesOfBeamsAndColumns`: valori a mano EN, UNI e DS su pilastro R400x400 e trave R300x500, con
    la riduzione 0,6, il passo UNI di 192 mm e le regole DS in sospeso;
  - `ColumnSpacingAlongThePerimeterAndLapLinksAboveFourteenMillimetres` (F5.15, R12 e R13), senza e con le due opzioni:
    - pilastro 600 × 600 con 8 barre: 498 e 249 mm;
    - pilastro 300 × 500 con le barre solo sopra e sotto: 180 e 400,12 mm, non soddisfatto con l'opzione;
    - staffe nelle sovrapposizioni con 8Ø14: 168 e 280 mm;
    - staffe nelle sovrapposizioni con 8Ø16: 192 mm in entrambi i casi;
  - `CoverAdditionsAndGroundCoverAreApplied`: maggiorazioni di cmin e copriferro contro terreno 40 e 75 mm;
  - `ProfilesAreResolvedByExactTypeWithoutFallback`;
  - `SlabAndWallSpacingIsMeasuredAlongTheLayers` (DET-1): striscia 1000 × h, c = 30, 6Ø16 per faccia, interasse a mano
    (1000 − 2 · 38)/5 = 184,8 mm per soletta h = 500 (≤ 400), soletta h = 400 in zona critica (≤ 250), parete
    1000 × 500 e parete ruotata (≤ 400); con l'opzione legacy 424, 324, 424 e 424 mm, non soddisfatti;
  - `LegacyPlateDetailingIsReproduced`: cattura congelata delle solette e pareti con le opzioni predefinite
    dell'interasse; con l'approvazione dell'utente le 60 righe in cui ANTHEA misurava l'interasse anche fra gli strati
    sono state rigenerate (S1000x200D 102 → 69,38, W250x1200N 164 → 159,14, W1000x200D 92 → 68,62 mm), con gli
    stessi esiti; con l'opzione legacy gli altri controlli sono identici e tornano i 60 valori precedenti.
- **Test** `EurocodeLapAndLargeBarTests` (0.34.0.0, attesi dal testo di EN 8.7.2(4) e 8.8): `LapPercentage` in
  sospeso senza dati, 100% in un solo strato soddisfatto, 60% in due strati non soddisfatto (limite 50%), 50% in due
  strati soddisfatto, per EN, UNI e DS; nessun controllo nuovo fuori dalla sovrapposizione con Ø ≤ 32 mm né con NTC;
  Ø36 in una trave 400 × 600: ancoraggio e fessurazione in sospeso, sovrapposizione in sospeso (400 mm); pilastro
  1200 × 1200 con Ø40: sovrapposizione soddisfatta (1200 ≥ 1000 mm); trave 1200 × 1200 con anima di 300 mm: in sospeso;
  dati non validi rifiutati.
- **Contratto dei dettagli** (`detailing-contract-0.0.17.csv`): dalla 0.34.0.0 i 66 casi della famiglia Eurocodice
  nella zona di sovrapposizione hanno `LapPercentage` in sospeso in coda, con gli altri controlli invariati e 2 esiti
  complessivi da soddisfatto a in sospeso, verificati riga per riga da
  `tools/attesi/classifica_contratto_dettagli_20261101.py`.
- Nel repository Model, `MemberDetailingTest` verifica il collegamento come attività d'asta (trave NTC soddisfatta,
  pilastro NTC non soddisfatto sul passo delle staffe, EN, DS, dati mancanti, conferme).
- **Esempio di questa pagina**: eseguito con la libreria 0.0.17.0 e ricalcolato in modo indipendente, scarto
  inferiore a 10⁻¹².
- **Benchmark indipendenti pubblicati**: nessuno, per ora.

## 12. Bibliografia

- DM 17 gennaio 2018, *Aggiornamento delle «Norme tecniche per le costruzioni»*, §4.1.6.1.
- Circolare 21 gennaio 2019 n. 7 C.S.LL.PP., §C4.1.6.1.1 e §C4.1.6.1.3.
- EN 1992-1-1:2004 + AC:2010, *Eurocode 2: Design of concrete structures — Part 1-1: General rules and rules for
  buildings*, §4.4.1, §8.2, §9.2, §9.5.
- UNI EN 1992-1-1, appendice nazionale: DM 31 luglio 2012.
- DS/EN 1992-1-1 DK NA:2024, capitolo 9.
- CNR-DT 200 R1/2013, *Istruzioni per la progettazione, l'esecuzione ed il controllo di interventi di
  consolidamento statico mediante l'utilizzo di compositi fibrorinforzati*.
