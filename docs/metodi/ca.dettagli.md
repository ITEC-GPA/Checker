---
id: ca.dettagli
titolo: Dettagli costruttivi di travi e pilastri
libreria: GPCChecker.Concrete
classi:
  - GPC.Checkers.Concrete.Detailing.MemberDetailingCalculator
  - GPC.Checkers.Concrete.Detailing.MemberDetailingInput
  - GPC.Checkers.Concrete.Detailing.MemberDetailingResult
  - GPC.Checkers.Concrete.Detailing.DetailingCheck
  - GPC.Checkers.Concrete.Detailing.MemberDetailingKind
  - GPC.Checkers.Concrete.Detailing.DetailingProfiles
  - GPC.Checkers.Concrete.Cracking.CrackSectionGeometry
versione: 0.0.15.0
norme:
  - ntc2018-4.1.6.1.1
  - ntc2018-4.1.6.1.2
  - ntc2018-4.1.6.1.3
  - circ2019-c4.1.6.1.1
  - en1992-1-1-4.4.1
  - en1992-1-1-8.2
  - en1992-1-1-9.2.1.1
  - en1992-1-1-9.2.1.2
  - en1992-1-1-9.2.1.4
  - en1992-1-1-9.2.2
  - en1992-1-1-9.5.2
  - en1992-1-1-9.5.3
  - uni-en1992-1-1-na2012-9.2.2
  - uni-en1992-1-1-na2012-9.5.2
  - uni-en1992-1-1-na2012-9.5.3
  - ds-en1992-1-1-na2024-9
  - cnr-dt200-r1-2013
stato: bozza
---

# Dettagli costruttivi di travi e pilastri

## Scopo

Il metodo controlla le regole di dettaglio di una sezione di trave o di pilastro in c.a. ordinario. I controlli
sono:

- interferro minimo;
- copriferro nominale e margine di copriferro di ogni barra;
- armatura longitudinale minima e massima;
- diametro, area minima, passo e interasse trasversale delle braccia delle staffe;
- trattenimento delle barre compresse.

Ogni regola produce un controllo con:

- chiave stabile;
- valore effettivo e limite, con unità;
- esito: soddisfatto, non soddisfatto oppure in sospeso.

Un controllo è in sospeso quando mancano dati o conferme, oppure quando la regola nazionale non è implementata.
L'esito complessivo è:

- non soddisfatto se almeno un controllo non è soddisfatto;
- in sospeso se nessun controllo fallisce ma almeno uno è in sospeso;
- soddisfatto negli altri casi.

## Campo di applicazione

| Caso | Stato | Comportamento del programma |
| --- | --- | --- |
| Trave (sezione 1D), flessione in entrambi i versi | supportato | Le due metà della sezione sono verificate come zone tese potenziali. |
| Pilastro (sezione 1D) | supportato | NEd è la compressione massima di progetto, fornita dal chiamante. |
| Sezioni rettangolari, a T, circolari, cave, poligonali | supportato | Geometria esplicita: contorno, fori e barre ordinarie della sezione. |
| Solette e piastre (EN 9.3), pareti (EN 9.6) | non supportato | Nessun controllo prodotto dalla libreria. Le regole sono presenti solo nel motore precedente dell'applicazione (vedi Implementazione). |
| Fondazioni, mensole tozze, nodi, zone di diffusione | non supportato | Fuori dal metodo. |
| Sezioni precompresse, sezioni composte acciaio-calcestruzzo | non supportato | La geometria considera solo le barre ordinarie; il verificatore del modello restituisce «non supportato». |
| Regole per zone sismiche (NTC cap. 7, EN 1998-1) | non supportato | Valgono solo le regole del capitolo 4 NTC e delle sezioni 8-9 EN. |
| Ancoraggio delle barre inferiori agli appoggi e regola della traslazione | conferma | Controllo in sospeso finché il chiamante non conferma le zone di estremità. |
| Trattenimento delle barre compresse (travi) | NTC: calcolato; Eurocodice: conferma | NTC: passo ≤ 15Ø. Famiglia Eurocodice: controllo in sospeso senza conferma (vedi Scostamento dichiarato). |
| Quota minima di staffe nell'armatura a taglio (NTC 50 %, EN β3 = 0,5) | non supportato | Non verificata: richiede la ripartizione fra staffe e sagomati. |
| Staffe inclinate | non supportato | α = 90° in tutte le espressioni. |
| Una barra in ogni spigolo dei pilastri poligonali (EN 9.5.2(4)) | non supportato | Solo il numero minimo di 4 barre nei pilastri circolari è verificato. |
| Disposizione delle barre non riconosciuta per l'interasse automatico | in sospeso | Controllo `LongitudinalSpacing` in sospeso. |
| NTC 2018; CNR-DT 200 R1/2013 (membratura in c.a. secondo NTC) | supportato | Profilo NTC. |
| EN 1992-1-1, valori raccomandati | supportato | Profilo EN. |
| UNI EN 1992-1-1 con DM 31/07/2012 | supportato | Valori nazionali di 9.2.2(8), 9.5.2(1)-(2), 9.5.3(3). |
| DS/EN 1992-1-1 con DK NA | supportato in parte | Capitolo 9 invariato. Travi: As,min (9.2.1.1(1)) e ρw,min (9.2.2(5)) in sospeso come regole non implementate. |
| DIN EN, NS-EN, Model Code 2010, CNR-DT 204 | non supportato | `NotSupportedException` con il motivo. |
| CS-TR34 | non applicabile | Motivo restituito. |
| ACI 318, AASHTO | non supportato | Implementazione futura. |

## Riferimenti normativi

| Norma | Edizione e appendice | Punto | Contenuto usato |
| --- | --- | --- | --- |
| NTC 2018 | DM 17/01/2018 | §4.1.6.1.1, eq. [4.1.45] | As,min travi = 0,26 fctm/fyk bt d ≥ 0,0013 bt d. As,max = 0,04 Ac per l'armatura tesa o compressa, fuori dalle sovrapposizioni. Staffe ≥ 1,5 b mm²/m con almeno 3 staffe/m e passo ≤ 0,8 d. Barre compresse trattenute con passo ≤ 15Φ. Armatura inferiore ancorata agli appoggi di estremità. |
| NTC 2018 | DM 17/01/2018 | §4.1.6.1.2, eq. [4.1.46] | Pilastri: Ø ≥ 12 mm, interasse ≤ 300 mm, As,min = 0,10 NEd/fyd ≥ 0,003 Ac. Passo delle staffe ≤ 12 Ømin e ≤ 250 mm. Østaffa ≥ 6 mm e ≥ Ømax/4. As,max = 0,04 Ac fuori dalle sovrapposizioni. |
| NTC 2018 | DM 17/01/2018 | §4.1.6.1.3 | Copriferro e interferro commisurati ad ambiente, aggregato e aderenza; riferimento a UNI EN 1992-1-1. |
| Circolare 2019 | n. 7 del 21/01/2019 | §C4.1.6.1.1; §C4.1.6.1.3, Tab. C4.1.IV | Prescrizione delle staffe anche per travi senza armatura a taglio. Copriferro di durabilità (pagina `ca.durabilita-copriferri`). |
| EN 1992-1-1 | 2004 + A1:2014 | §4.4.1.1-4.4.1.3, eq. (4.1)-(4.2) | cnom = cmin + Δcdev; cmin = max(cmin,b; cmin,dur; 10 mm). cmin,b = Ø (+5 mm con dg > 32 mm). Superfici irregolari (+5 mm), abrasione (k1, k2, k3), getti contro terreno (40 e 75 mm). |
| EN 1992-1-1 | 2004 + A1:2014 | §8.2(2) | Interferro ≥ max(k1 Ø; dg + k2; 20 mm), k1 = 1 e k2 = 5 mm raccomandati. |
| EN 1992-1-1 | 2004 + A1:2014 | §9.2.1.1(1), (3), eq. (9.1N) | As,min e As,max (0,04 Ac) delle travi. |
| EN 1992-1-1 | 2004 + A1:2014 | §9.2.1.2(3) | Barre compresse trattenute con passo ≤ 15Ø. |
| EN 1992-1-1 | 2004 + A1:2014 | §9.2.1.4 | Ancoraggio dell'armatura inferiore agli appoggi di estremità. |
| EN 1992-1-1 | 2004 + A1:2014 | §9.2.2(5), (6), (8), eq. (9.4), (9.5N), (9.6N), (9.8N) | ρw,min = 0,08 √fck/fyk; sl,max = 0,75 d (1 + cot α); st,max = 0,75 d ≤ 600 mm. |
| EN 1992-1-1 | 2004 + A1:2014 | §9.5.2(1)-(4), eq. (9.12N) | Pilastri: Ømin = 8 mm; As,min = max(0,10 NEd/fyd; 0,002 Ac); As,max = 0,04 Ac (0,08 Ac nelle sovrapposizioni); almeno 4 barre nei pilastri circolari. |
| EN 1992-1-1 | 2004 + A1:2014 | §9.5.3(1), (3), (4), (6) | Østaffa ≥ max(6 mm; Ømax/4). scl,tmax = min(20 Ømin; b; 400 mm), ridotto di 0,6 vicino a travi e solette e nelle sovrapposizioni con Ømax > 14 mm. Barre d'angolo trattenute; nessuna barra compressa a più di 150 mm da una barra trattenuta. |
| UNI EN 1992-1-1 | appendice nazionale DM 31/07/2012 | 9.2.2(8), 9.5.2(1)-(2), 9.5.3(3) | st,max ≤ 300 mm; Ømin = 12 mm; As,min = max(0,10 NEd/fyd; 0,003 Ac); scl,tmax = min(12 Ømin; b; 250 mm). |
| DS/EN 1992-1-1 | DK NA:2024 | capitolo 9 | Invariato rispetto ai valori raccomandati, tranne 9.2.1.1(1) e 9.2.2(5) (valori nazionali non implementati). |
| CNR-DT 200 R1/2013 | 2013 | — | Membratura in c.a. secondo NTC. |

## Ipotesi

- La sezione è descritta da contorno, fori e barre ordinarie con posizione, diametro e area. Le barre da
  precompressione sono escluse.
- Le larghezze sono dati espliciti:
  - bt della zona tesa, superiore o inferiore;
  - bw dell'anima per le staffe.

  Il metodo non le ricava dalla forma.
- Trave: la sezione è divisa a metà della sua altezza. Le barre della metà superiore formano l'armatura della
  zona superiore e quelle della metà inferiore l'armatura della zona inferiore. Entrambe le zone sono
  considerate potenzialmente tese (inversione del momento); l'altezza utile d di ciascuna è misurata dal lembo
  opposto al baricentro delle sue barre.
- Pilastro: NEd è il modulo della massima compressione di progetto fra gli stati verificati.
- Il copriferro di durabilità cmin,dur viene dal progetto di durabilità (pagina `ca.durabilita-copriferri`)
  insieme alle maggiorazioni per superficie irregolare e abrasione e al minimo dei getti contro terreno. Senza
  cmin,dur i controlli del copriferro restano in sospeso.
- Le staffe sono a 90°, con diametro, passo e numero di braccia dati. Le braccia sono equidistanti sull'anima.
- Le conferme sono dati del chiamante:
  - barre compresse trattenute;
  - zone di estremità (ancoraggio agli appoggi e regola della traslazione; per i pilastri Eurocodice, passo
    ridotto vicino a travi e solette).

## Notazione, unità e convenzioni

Unità: mm, mm², MPa, N. Diversamente dalla convenzione generale della libreria (compressione negativa), NEd è
il modulo della compressione (positivo). Nel testo si usa la virgola decimale.

| Simbolo | Significato | Unità | Proprietà nel codice |
| --- | --- | --- | --- |
| Ac | area del calcestruzzo | mm² | `ConcreteArea` |
| fck, fctm, fyk, fyd | resistenze dei materiali | MPa | `Fck`, `Fctm`, `Fyk`, `Fyd` |
| bt,sup, bt,inf | larghezza della zona tesa superiore e inferiore | mm | `TopWidth`, `BottomWidth` |
| bw (b nella NTC) | larghezza dell'anima per le staffe | mm | `WebWidth` |
| b | dimensione minore del pilastro (ingombro del contorno) | mm | `CrackSectionGeometry.Width/Height` |
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
| a_i,j | distanza netta fra le barre i e j | mm | calcolata |
| c_i | copriferro geometrico della barra i (distanza netta da contorno e fori) | mm | `CrackSectionGeometry.BarCover` |

## Formulazione

### Controlli comuni

Interferro minimo, su tutte le coppie di barre (EN 8.2(2); NTC §4.1.6.1.3), chiave `ClearSpacing`:

```math
a_{min} = \min_{i<j}\left[\sqrt{(x_i-x_j)^2+(y_i-y_j)^2} - \frac{\varnothing_i+\varnothing_j}{2}\right]
\ \ge\ \max\left(20\ \text{mm};\ \varnothing_{max};\ d_g + 5\ \text{mm}\right)
\tag{1}
```

Copriferro richiesto per un diametro di aderenza Øb (EN eq. (4.1)-(4.2), 4.4.1.2(3), (11), (13), 4.4.1.3(1),
(4)):

```math
c_{req}(\varnothing_b) = \max\left\{\max\left[10\ \text{mm};\ c_{min,dur};\ \varnothing_b + 5\ \text{mm}\cdot[d_g>32\ \text{mm}]\right] + \Delta c_{add} + \Delta c_{dev};\ c_{ground}\right\}
\tag{2}
```

Verifiche del copriferro: nominale con Øb = Øw (o Ømax senza staffe), chiave `NominalCover`; margine di ogni
barra con il suo diametro, chiave `BarCoverMargin`:

```math
c_{nom} \ge c_{req}(\varnothing_w),\qquad
\Delta c = \min_i\left[c_i - c_{req}(\varnothing_i)\right] \ge 0
\tag{3}
```

### Travi

Zone e altezze utili (y_mid a metà dell'ingombro verticale; ȳ baricentro delle barre della zona):

```math
d_{inf} = y_{max} - \bar y_{inf},\qquad d_{sup} = \bar y_{sup} - y_{min}
\tag{4}
```

Armatura minima e massima di ciascuna zona (NTC [4.1.45]; EN (9.1N) e 9.2.1.1(3)), chiavi
`MinimumTension:Bottom|Top` e `MaximumTension:Bottom|Top`. La massima non è controllata nelle zone di
sovrapposizione:

```math
A_{s,zona} \ge \rho_{min}\,b_t\,d,\quad \rho_{min} = \max\left(0{,}26\,\frac{f_{ctm}}{f_{yk}};\ 0{,}0013\right);
\qquad A_{s,zona} \le 0{,}04\,A_c
\tag{5}
```

Se una metà della sezione non contiene barre, il limite minimo è ρmin per la larghezza e l'altezza
d'ingombro della sezione e il valore effettivo è zero.

Staffe con il profilo NTC (§4.1.6.1.1), chiavi `MinimumLinks`, `LinkSpacing` e `CompressionBarRestraint`:

```math
\frac{A_{st}}{s} = n\,\frac{\pi\,\varnothing_w^2}{4}\,\frac{1000}{s} \ge 1{,}5\,b\ \ [\text{mm}^2/\text{m}];\qquad
s \le \min\left(\frac{1000}{3};\ 0{,}8\,d\right);\qquad
s \le 15\,\varnothing_{min}
\tag{6}
```

Staffe con la famiglia Eurocodice (EN 9.2.2(5), (6), (8) con α = 90°), chiavi `MinimumLinks`, `LinkSpacing` e
`LinkLegSpacing`:

```math
\frac{A_{sw}}{s} \ge \rho_{w,min}\,b_w\cdot 1000,\quad \rho_{w,min} = \frac{0{,}08\sqrt{f_{ck}}}{f_{yk}};\qquad
s \le 0{,}75\,d;\qquad
s_t = \frac{b_w - 2\,c_{nom} - \varnothing_w}{n-1} \le \min\left(0{,}75\,d;\ s_{t,lim}\right)
\tag{7}
```

con s_t,lim = 600 mm (EN e DS) oppure 300 mm (DM 31/07/2012). Con meno di due braccia s_t è in sospeso. Con DS
i controlli di ρmin (eq. 5) e di ρw,min (eq. 7) sono in sospeso come regole nazionali non implementate.

> **Scostamento dichiarato — trattenimento delle barre compresse nelle travi (Eurocodice).** EN 9.2.1.2(3)
> richiede un passo delle staffe ≤ 15Ø per le barre compresse considerate nella resistenza. Nella famiglia
> Eurocodice il codice non calcola il limite e chiede una conferma (`BarsHeldByLinks`). Effetto: senza conferma
> il controllo è in sospeso; con la conferma il limite di 15Ø non è verificato. Il profilo NTC lo calcola, con
> Ømin di tutte le barre della sezione.

### Pilastri

Profilo NTC (§4.1.6.1.2), chiavi `LongitudinalDiameter`, `LongitudinalSpacing`, `MinimumLongitudinal`,
`MaximumLongitudinal`, `LinkDiameter` e `LinkSpacing`:

```math
\varnothing_{min} \ge 12\ \text{mm};\quad i_{max} \le 300\ \text{mm};\quad
A_s \ge \max\left(0{,}10\,\frac{N_{Ed}}{f_{yd}};\ 0{,}003\,A_c\right);\quad A_s \le 0{,}04\,A_c;\quad
\varnothing_w \ge \max\left(6;\ \frac{\varnothing_{max}}{4}\right);\quad s \le \min\left(250;\ 12\,\varnothing_{min}\right)
\tag{8}
```

L'interasse i_max è la massima distanza fra barre adiacenti allineate sulla stessa retta orizzontale o
verticale, purché il punto medio cada nel calcestruzzo. Nelle sezioni circolari con un solo anello è l'arco
fra barre adiacenti.

> **Scostamento dichiarato — interasse delle barre dei pilastri misurato attraverso il nucleo.** NTC
> §4.1.6.1.2 limita l'interasse delle barre lungo il perimetro. Il codice misura la distanza fra barre
> adiacenti di ogni allineamento orizzontale o verticale, anche attraverso il nucleo. In un pilastro con barre
> d'angolo e barre intermedie sulle facce, le due barre intermedie opposte sono allineate e la loro distanza
> (circa il doppio dell'interasse sulla faccia) entra nel massimo. Effetto: a favore di sicurezza, ma con
> possibili esiti non soddisfatti indebiti. Pilastro 600 × 600 con 8Ø20: interasse sulle facce 249 mm,
> valore calcolato 498 mm, controllo non soddisfatto. Pilastro 400 × 400 dell'esempio: 149 mm sulle facce,
> 298 mm calcolati, ancora entro 300 mm.

Famiglia Eurocodice (EN 9.5.2, 9.5.3; DM 31/07/2012), chiavi `LongitudinalDiameter`, `MinimumLongitudinal`,
`MaximumLongitudinal`, `BarCount`, `LinkDiameter` e `LinkSpacing`:

```math
\varnothing_{min} \ge \varnothing_{lim};\quad
A_s \ge \max\left(0{,}10\,\frac{N_{Ed}}{f_{yd}};\ k_A\,A_c\right);\quad
A_s \le 0{,}04\,A_c;\quad
n_{barre} \ge 4\ (\text{circolari});\quad
\varnothing_w \ge \max\left(6;\ \frac{\varnothing_{max}}{4}\right);\quad
s \le \beta\,s_{cl,tmax}
\tag{9}
```

| Grandezza | EN e DS | UNI (DM 31/07/2012) |
| --- | --- | --- |
| Ø_lim | 8 mm | 12 mm |
| k_A | 0,002 | 0,003 |
| s_cl,tmax | min(20 Ømin; b; 400 mm) | min(12 Ømin; b; 250 mm) |
| β | 0,6 nelle zone di sovrapposizione, altrimenti 1 | come EN |

> **Scostamento dichiarato — riduzione del passo delle staffe nelle sovrapposizioni dei pilastri.** EN
> 9.5.3(4)(ii) riduce il passo a 0,6 scl,tmax vicino alle giunzioni solo se il diametro massimo delle barre
> longitudinali supera 14 mm. Il codice applica la riduzione in ogni zona di sovrapposizione. Effetto: a favore
> di sicurezza, solo con Ømax ≤ 14 mm. Con 8Ø14, staffe a passo 200 mm e pilastro 400 × 400 il limite scende
> da 280 a 168 mm e il controllo risulta non soddisfatto.

Per tutti i profili, nelle zone di sovrapposizione dei pilastri (EN 9.5.2(3)), chiave `MaximumAtLap`:

```math
A_s \le 0{,}08\,A_c
\tag{10}
```

> **Scostamento dichiarato — armatura massima nelle sovrapposizioni con NTC.** NTC §4.1.6.1.2 fissa
> As,max = 0,04 Ac solo fuori dalle sovrapposizioni e non dà un limite nelle giunzioni. Il codice applica anche
> al profilo NTC il limite di 0,08 Ac di EN 9.5.2(3). Effetto: regola aggiuntiva, più restrittiva della NTC;
> riguarda solo pilastri con più dell'8 % di armatura nella sezione di giunzione.

### Conferme ed esito

Controlli in sospeso per conferma mancante:

- `BarsHeldByLinks`: NTC con staffe; EN 9.2.1.2 e 9.5.3(6);
- `EndSupportAnchorage`: travi, NTC §4.1.6.1.1 ed EN 9.2.1.4;
- `LinkSpacingNearBeams`: pilastri Eurocodice, EN 9.5.3(4)(i).

L'esito complessivo è:

```math
\text{Passed} =
\begin{cases}
\text{false} & \exists\ \text{controllo non soddisfatto}\\
\text{null} & \text{altrimenti, se}\ \exists\ \text{controllo in sospeso}\\
\text{true} & \text{altrimenti}
\end{cases}
\tag{11}
```

## Coefficienti e valori predefiniti

| Simbolo | Valore | Fonte | Modificabile | Dove sta nel codice |
| --- | --- | --- | --- | --- |
| interferro | max(20 mm; Ømax; dg + 5 mm) | EN 8.2(2) (k1 = 1, k2 = 5 mm) | no | `MemberDetailingCalculator.Calculate` |
| minimo assoluto del copriferro | 10 mm | EN (4.2) | no | `Calculate` |
| maggiorazione di cmin,b | 5 mm per dg > 32 mm | EN prospetto 4.2 | no | `Calculate` |
| ρmin | max(0,26 fctm/fyk; 0,0013) | NTC [4.1.45]; EN (9.1N) | no | `Beam` |
| As,max | 0,04 Ac (0,08 Ac nelle sovrapposizioni dei pilastri) | NTC §4.1.6.1.1-2; EN 9.2.1.1(3), 9.5.2(3) | no | `Beam`, `Column`, `Calculate` |
| staffe minime NTC | 1,5 b mm²/m; 3 staffe/m; 0,8 d; 15Ø | NTC §4.1.6.1.1 | no | `Beam` |
| ρw,min | 0,08 √fck/fyk | EN (9.5N) | no | `Beam` |
| sl,max | 0,75 d | EN (9.6N), α = 90° | no | `Beam` |
| st,max | min(0,75 d; 600 mm), UNI min(0,75 d; 300 mm) | EN (9.8N); DM 31/07/2012 | no | `Beam` |
| pilastri NTC | Ø ≥ 12; 300 mm; 0,10 NEd/fyd; 0,003 Ac; 6 mm; Ømax/4; 12 Ømin; 250 mm | NTC §4.1.6.1.2 | no | `Column` |
| pilastri EN | Ø ≥ 8; 0,002 Ac; 20 Ømin; b; 400 mm; 4 barre (circolari) | EN 9.5.2, 9.5.3 | no | `Column` |
| pilastri UNI | Ø ≥ 12; 0,003 Ac; 12 Ømin; b; 250 mm | DM 31/07/2012 | no | `Column` |
| riduzione del passo | 0,6 | EN 9.5.3(4) | no | `Column` |
| cmin,dur, Δcdev, Δcadd, cground | dati | progetto di durabilità; EN 4.4.1 | sì | `MemberDetailingInput` |

## Implementazione

Classi e file (namespace `GPC.Checkers.Concrete.Detailing`, salvo indicazione):

- `MemberDetailingInput` (`Detailing/MemberDetailingCalculator.cs:14-64`), `DetailingCheck` (67-81),
  `MemberDetailingResult` (83-91, esito aggregato eq. 11 alla riga 89).
- `MemberDetailingCalculator.Calculate` (103-142):
  1. controlla i dati: valori non finiti o negativi danno `ArgumentException` (106-109);
  2. calcola Ømin, Ømax e As (119) e l'interferro su tutte le coppie, eq. 1 (120-124);
  3. calcola il copriferro richiesto, eq. 2 (128), e i controlli eq. 3 (125-134); senza cmin,dur il controllo è
     in sospeso (135);
  4. smista a `Column` o `Beam` (137-138);
  5. aggiunge le conferme NTC sulle barre trattenute (139) e il massimo nelle sovrapposizioni dei pilastri,
     eq. 10 (140).
- `MemberDetailingCalculator.Column` (144-174):
  - profilo NTC: righe 149-159, eq. 8, con l'interasse da `CrackSectionGeometry.MaximumSpacing` (152-154);
  - famiglia Eurocodice: righe 161-173, eq. 9, con la riduzione 0,6 nelle sovrapposizioni alla riga 169;
  - conferme: righe 171-173.
- `MemberDetailingCalculator.Beam` (179-222):
  - divisione in zone (183, 190), ρmin (184), Ast/s (185);
  - zona senza barre (191-196), d (198);
  - armatura minima e massima, eq. 5 (199-201);
  - staffe NTC, eq. 6 (202-206); staffe Eurocodice, eq. 7 (207-217);
  - trattenimento delle barre compresse (219-220) e ancoraggio di estremità (221).
- `MemberDetailingCalculator.National` (176-177): controlli DS non implementati (`NotImplemented` = true).
- `DetailingProfiles` (`Detailing/DetailingProfiles.cs`): risoluzione della norma per tipo esatto (17-29,
  45-52) e motivi (31-43).
- `GPC.Checkers.Concrete.Cracking.CrackSectionGeometry` (`Cracking/CrackSectionGeometry.cs`):
  - `From`: geometria di una sezione del modello (63-76);
  - `BarCover`: copriferro geometrico con segno rispetto a contorno e fori (107-119);
  - `MaximumSpacing`: interasse automatico per allineamenti o anelli (124-164).
- Uso nel verificatore del modello (repository Model, `ModelChecker/ConcreteSectionVerifier.Detailing.cs`). Una
  attività per asta e stato. NEd è la compressione massima dei campioni (43). cmin,dur viene dal progetto di
  durabilità, prendendo il maggiore fra il valore calcolato e quello eventualmente assegnato (66-77). I controlli
  in sospeso danno «dati insufficienti», le regole non implementate «non supportato» (88-114).
- Le regole di solette e pareti esistono solo nel motore precedente dell'applicazione
  (`ANTHEA/X.Calculations/ConcreteDetailing.cs:88-135`):
  - soletta (109-118):
    - interasse dell'armatura principale min(2h; 250) in zona critica, min(3h; 400) altrove;
    - armatura secondaria ≥ 20 % della principale;
    - interasse della secondaria min(3h; 400) o min(3,5h; 450);
    - conferme di bordi, appoggi e punzonamento;
  - parete (125-135):
    - As,v fra 0,002 e 0,04 Ac;
    - interasse verticale ≤ min(3t; 400);
    - armatura orizzontale ≥ max(0,25 As,v; 0,001 Ac) per metro;
    - interasse orizzontale ≤ 400 mm;
  - 0,08 Ac nelle giunzioni di pilastri e pareti (137-138).

  Nella libreria queste regole mancano.

## Limiti e casi non supportati

- Solette e pareti non sono verificate dalla libreria.
- Le larghezze bt e bw, la dimensione dell'aggregato e il copriferro nominale sono dati: il metodo non li ricava
  dalla forma.
- La divisione a metà altezza tratta le barre di parete come armatura della zona in cui cadono. Ne cambiano
  l'area e il baricentro della zona, quindi d. Nelle sezioni con barre distribuite sull'altezza conviene
  verificare il risultato.
- L'interferro usa Ømax di tutta la sezione per ogni coppia di barre (a favore di sicurezza). Non tiene conto
  che le barre sovrapposte possono toccarsi (EN 8.2(4)).
- L'interasse dei pilastri è misurato anche attraverso il nucleo (Scostamento dichiarato).
- Il passo delle staffe nelle sovrapposizioni dei pilastri Eurocodice è ridotto anche con Ømax ≤ 14 mm
  (Scostamento dichiarato).
- Il trattenimento delle barre compresse nelle travi Eurocodice è una conferma, non un calcolo (Scostamento
  dichiarato).
- La quota minima di staffe nell'armatura a taglio, la presenza di una barra in ogni spigolo dei pilastri
  poligonali, le staffe inclinate e le regole sismiche non sono verificate.
- Con DS, As,min e ρw,min delle travi restano in sospeso.
- Testo dei riferimenti: nel profilo NTC il riferimento di `MinimumTension` cita EC2 §9.3.1.1 (solette) invece
  di §9.2.1.1, e i riferimenti NTC hanno uno spazio prima del numero del sottoparagrafo (per esempio
  «§4.1.6.1 .3»). Il valore calcolato non cambia.

## Esempio numerico verificato

Dati comuni:

- C25/30: fck = 25 MPa, fctm = 0,30 · 25^(2/3) = 2,5650 MPa;
- B450C: fyk = 450 MPa, fyd = 391,30 MPa;
- dg = 20 mm, cnom = 35 mm alle staffe, cmin,dur = 25 mm, Δcdev = 10 mm;
- staffe Ø8 a 2 braccia; nessuna sovrapposizione; conferme date.

**Trave 300 × 500 mm.** Contorno da (−150; −250) a (150; 250). Barre inferiori 3Ø20 in y = −197 mm (x = −97,
0, 97); barre superiori 2Ø14 in y = 200 mm (x = ±100). Staffe a passo 200 mm; bt = bw = 300 mm.

Calcolo a mano:

1. Copriferro geometrico delle barre: 250 − 197 − 10 = 43 mm (Ø20) e 250 − 200 − 7 = 43 mm (Ø14).
2. Interferro: 97 − 20 = 77 mm ≥ max(20; 20; 25) = 25 mm.
3. Copriferro richiesto alle staffe: max(10; 25; 8) + 10 = 35 mm, uguale a cnom, soddisfatto. Margine minimo
   delle barre: 43 − (max(10; 25; 20) + 10) = 8 mm ≥ 0.
4. Armatura longitudinale:
   - ρmin = max(0,26 · 2,5650/450; 0,0013) = 0,0014820;
   - zona inferiore: d = 250 + 197 = 447 mm, As,min = 0,0014820 · 300 · 447 = 198,73 mm², As = 942,48 mm²;
   - zona superiore: d = 200 + 250 = 450 mm, As,min = 200,07 mm², As = 307,88 mm²;
   - As,max = 0,04 · 150000 = 6000 mm².
5. Staffe: Ast/s = 2 · 50,27 · 1000/200 = 502,65 mm²/m.
   - NTC: minimo 1,5 · 300 = 450 mm²/m; passo ≤ min(333,33; 357,6) = 333,33 mm; 15 · 14 = 210 mm ≥ 200 mm.
     Tutto soddisfatto.
   - EN: ρw,min bw · 1000 = 0,08 · 5/450 · 300 · 1000 = 266,67 mm²/m; sl,max = 0,75 · 447 = 335,25 mm
     (inferiore) e 337,5 mm (superiore); st = (300 − 70 − 8)/1 = 222 mm ≤ min(335,25; 600) = 335,25 mm.
   - UNI: st,max = min(335,25; 300) = 300 mm.
   - DS: ρmin e ρw,min in sospeso, quindi esito complessivo in sospeso.

**Pilastro 400 × 400 mm.** 8Ø16 negli angoli e a metà dei lati, in ±149 mm. NEd = 2000 kN; staffe a passo
250 mm.

1. As = 8 · 201,06 = 1608,50 mm²; Ac = 160000 mm².
2. NTC:
   - As,min = max(0,10 · 2 000 000/391,30; 0,003 · 160000) = max(511,11; 480) = 511,11 mm²;
   - passo ≤ min(250; 12 · 16) = 192 mm < 250 mm: non soddisfatto;
   - interasse calcolato 298 mm (barre intermedie opposte, attraverso il nucleo) ≤ 300 mm.
3. EN: As,min = max(511,11; 320) = 511,11 mm²; passo ≤ min(20 · 16; 400; 400) = 320 mm ≥ 250 mm:
   soddisfatto. In zona di sovrapposizione (Ømax = 16 > 14 mm): 0,6 · 320 = 192 mm, non soddisfatto;
   As ≤ 0,08 Ac = 12800 mm².
4. UNI: As,min = 511,11 mm²; passo ≤ min(192; 400; 250) = 192 mm: non soddisfatto.

Valori della libreria (GPCChecker.Concrete 0.0.15.0, eseguita):

| Controllo | A mano | Libreria | Scarto |
| --- | --- | --- | --- |
| Trave, `ClearSpacing` | 77 ≥ 25 | 77; 25 | 0 |
| Trave, `NominalCover` | 35 ≥ 35 | 35; 35, soddisfatto | 0 |
| Trave, `BarCoverMargin` | 8 | 8 | 0 |
| Trave, `MinimumTension:Bottom` [mm²] | 942,48 ≥ 198,733 | 942,4777960769379; 198,7334045227657 | < 1e-12 |
| Trave, `MinimumTension:Top` [mm²] | 307,88 ≥ 200,067 | 307,8760800517997; 200,06718576117353 | < 1e-12 |
| Trave NTC, `MinimumLinks` [mm²/m] | 502,65 ≥ 450 | 502,65482457436684; 450 | < 1e-12 |
| Trave NTC, `LinkSpacing`; `CompressionBarRestraint` [mm] | 333,33; 210 | 333,3333333333333; 210 | < 1e-12 |
| Trave EN, `MinimumLinks` [mm²/m] | 266,667 | 266,6666666666667 | < 1e-12 |
| Trave EN, `LinkSpacing:Bottom`; `LinkLegSpacing:Bottom` [mm] | 335,25; 222 ≤ 335,25 | 335,25; 222 ≤ 335,25 | 0 |
| Trave UNI, `LinkLegSpacing:Bottom` [mm] | 222 ≤ 300 | 222 ≤ 300 | 0 |
| Trave DS, esito complessivo | in sospeso (regole nazionali) | null; `NotImplemented` = true | — |
| Pilastro NTC, `MinimumLongitudinal` [mm²] | 511,111 | 511,1111111111111 | < 1e-12 |
| Pilastro NTC, `LinkSpacing` [mm] | 250 > 192, non soddisfatto | 250; 192; false | 0 |
| Pilastro NTC, `LongitudinalSpacing` [mm] | 298 (attraverso il nucleo) | 298 | 0 |
| Pilastro EN, `LinkSpacing` [mm] | 250 ≤ 320, soddisfatto | 250; 320; true | 0 |
| Pilastro EN in sovrapposizione, `LinkSpacing`; `MaximumAtLap` | 192; 12800 | 192; 12800 | 0 |
| Pilastro UNI, `LinkSpacing` [mm] | 192, non soddisfatto | 192; false | 0 |
| Pilastro 600 × 600 con 8Ø20 (scostamento), `LongitudinalSpacing` | 249 sulle facce | 498, non soddisfatto | vedi scostamento |
| Pilastro 400 × 400 con 8Ø14 in sovrapposizione (scostamento), `LinkSpacing` | 280 (EN 9.5.3(4)(ii)) | 168 | vedi scostamento |

## Validazione

- Casi congelati del motore precedente (`GPCChecker.Test.Concrete/Fixtures/detailing-legacy.csv`): 144 travi e
  pilastri NTC sulle sezioni di `detailing-sections.xml`, cioè rettangolari, a T, circolare, cava e un pilastro
  con barre Ø10. Variano:
  - NEd, staffe e aggregato;
  - cmin,dur e Δcdev;
  - sovrapposizione e conferme.

  Sono confrontati 1385 controlli: chiave, valore effettivo, limite ed esito, con tolleranza relativa 1e-9.
  Ripetuti sulle DLL della versione 0.0.15.0: 0 differenze.
- Test di `DetailingMigrationTests`:
  - `LegacyDetailingIsReproduced`: i casi congelati;
  - `EurocodeAndAnnexValuesOfBeamsAndColumns`: valori a mano EN, UNI e DS su pilastro R400x400 e trave
    R300x500, con la riduzione 0,6, il passo UNI di 192 mm e le regole DS in sospeso;
  - `CoverAdditionsAndGroundCoverAreApplied`: maggiorazioni di cmin e copriferro contro terreno 40 e 75 mm;
  - `ProfilesAreResolvedByExactTypeWithoutFallback`.
- Nel repository Model, `MemberDetailingTest` verifica il collegamento come attività d'asta (trave NTC
  soddisfatta, pilastro NTC non soddisfatto sul passo delle staffe, EN, DS, dati mancanti, conferme).
- Riscontro sui testi in questa revisione:
  - NTC 2018 §§4.1.6.1.1-4.1.6.1.3 (Gazzetta Ufficiale): tutte le regole del profilo NTC sono presenti nel
    testo, compreso il passo ≤ 15Φ per le barre compresse;
  - Circolare 2019 §C4.1.6.1;
  - EN 1992-1-1:2004 §§4.4.1, 8.2, 9.2.1, 9.2.2, 9.5.2 e 9.5.3.

  DM 31/07/2012 e DK NA:2024 non sono stati riletti in questa revisione.

## Bibliografia

1. D.M. 17 gennaio 2018, *Aggiornamento delle «Norme tecniche per le costruzioni»*, G.U. n. 42 del 20/02/2018,
   S.O. n. 8.
2. Circolare 21 gennaio 2019, n. 7 C.S.LL.PP., *Istruzioni per l'applicazione dell'«Aggiornamento delle Norme
   tecniche per le costruzioni»*.
3. EN 1992-1-1:2004 + AC:2010 + A1:2014, *Eurocode 2: Design of concrete structures — Part 1-1: General rules
   and rules for buildings*.
4. UNI EN 1992-1-1:2015 e D.M. 31 luglio 2012, *Approvazione delle Appendici nazionali recanti i parametri
   tecnici per l'applicazione degli Eurocodici*.
5. DS/EN 1992-1-1 DK NA:2024, *National Annex to Eurocode 2: Design of concrete structures — Part 1-1*.
6. CNR-DT 200 R1/2013, *Istruzioni per la progettazione, l'esecuzione ed il controllo di interventi di
   consolidamento statico mediante l'utilizzo di compositi fibrorinforzati*.
