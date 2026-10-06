---
id: ca.durabilita-copriferri
titolo: Durabilità, classi di esposizione e copriferri
libreria: GPCChecker.Concrete
classi:
  - GPC.Checkers.Concrete.Durability.ExposureClasses
  - GPC.Checkers.Concrete.Durability.ExposureClass
  - GPC.Checkers.Concrete.Durability.StrengthRequirement
  - GPC.Checkers.Concrete.Durability.CoverRequirements
  - GPC.Checkers.Concrete.Durability.CoverInput
  - GPC.Checkers.Concrete.Durability.CoverResult
  - GPC.Checkers.Concrete.Durability.CoverLine
  - GPC.Checkers.Concrete.Durability.DurabilityProfiles
  - GPC.Checkers.Concrete.Durability.DurabilityProfile
versione: 0.0.15.0
norme:
  - ntc2018-4.1.2.2.4.2
  - ntc2018-4.1.6.1.3
  - ntc2018-11.2.11
  - circ2019-c4.1.6.1.3
  - en1992-1-1-4.4.1
  - en1992-1-1-e.1
  - uni-en1992-1-1-na2012-e.1
  - ds-en1992-1-1-na2024-4.4.1
  - ds-en1992-1-1-na2024-e.1
  - uni-en206-1-2006-f.1
  - uni11104-2016
  - uni11104-2004-p4
  - cnr-dt200-r1-2013
stato: bozza
---

# Durabilità, classi di esposizione e copriferri

## Scopo

Il metodo raccoglie le regole di durabilità del calcestruzzo armato ordinario:

- il catalogo delle 18 classi di esposizione di EN 206, con i requisiti di composizione di EN 206 e di
  UNI 11104, il gruppo ambientale NTC e le classi indicative di resistenza dell'appendice E di EN 1992-1-1;
- la classe minima di resistenza di una combinazione di esposizioni, secondo la norma di progetto;
- il copriferro minimo di durabilità cmin,dur, il copriferro minimo cmin e il copriferro nominale cnom.

I risultati alimentano i controlli del copriferro della pagina `ca.dettagli` e il controllo della classe minima
di resistenza della sezione.

## Campo di applicazione

| Caso | Stato | Comportamento del programma |
| --- | --- | --- |
| Classi X0, XC1-XC4, XD1-XD3, XS1-XS3, XF1-XF4, XA1-XA3 e loro combinazioni | supportato | Governa la classe più severa della combinazione; X0 non si combina (`ArgumentException`). |
| Armatura ordinaria | supportato | Prospetti dei copriferri per barre. |
| Armatura da precompressione (EN prospetto 4.5N, colonne «cavi» della Tab. C4.1.IV) | non supportato | Nessuna tabella per i cavi. |
| Vita nominale 50 o 100 anni | supportato | Altri valori rifiutati (`ArgumentException`). DS: solo 50 anni (`NotSupportedException`). |
| fck fra 12 e 90 MPa | supportato | Fuori campo: `ArgumentException`. |
| Superficie irregolare, abrasione XM1-XM3, getti contro terreno | supportato | +5 mm; k1/k2/k3 = 5/10/15 mm; cnom ≥ 40 o 75 mm. |
| Acciaio inossidabile, protezioni aggiuntive, elemento di sicurezza aggiuntivo (EN 4.4.1.2(6)-(8)) | non supportato | Δcdur,γ = Δcdur,st = Δcdur,add = 0 (valori raccomandati). |
| Riduzione della soglia di resistenza con aria inglobata oltre il 4 % (EN prospetto 4.3N, nota 2) | non supportato | Soglia non ridotta, a favore di sicurezza. |
| Classi XF e XA da sole con la famiglia Eurocodice | rifiutato | `ArgumentException`: non definiscono cmin,dur (EN 4.4.1.2(12)). |
| Resistenza al fuoco (EN 1992-1-2) | non supportato | Fuori dal metodo. |
| NTC 2018; CNR-DT 200 R1/2013 | supportato | Tab. C4.1.IV della Circolare; classe minima secondo UNI 11104. |
| EN 1992-1-1 (raccomandati); UNI EN 1992-1-1 con DM 31/07/2012 | supportato | Prospetti 4.3N e 4.4N; classe minima indicativa dell'appendice E. |
| DS/EN 1992-1-1 con DK NA | supportato | Tabel 4.4N NA senza classi strutturali; Tabel E.1(2). |
| DIN EN, NS-EN, Model Code 2010, CNR-DT 204 | non supportato | `NotSupportedException` con il motivo. |
| ACI 318, AASHTO | non supportato | Implementazione futura. |
| UNI 11104:2025 | non implementata | Il catalogo usa i valori descritti in Formulazione (tabelle A e B). |
| Composizione della miscela | non supportato | Il metodo restituisce i limiti di composizione, non una miscela. |

## Riferimenti normativi

| Norma | Edizione e appendice | Punto | Contenuto usato |
| --- | --- | --- | --- |
| NTC 2018 | DM 17/01/2018 | §4.1.2.2.4.2, Tab. 4.1.III | Condizioni ambientali: ordinarie (X0, XC1, XC2, XC3, XF1); aggressive (XC4, XD1, XS1, XA1, XA2, XF2, XF3); molto aggressive (XD2, XD3, XS2, XS3, XA3, XF4). |
| NTC 2018 | DM 17/01/2018 | §4.1.6.1.3 | Copriferro in funzione dell'aggressività dell'ambiente e delle tolleranze di posa; riferimento a UNI EN 1992-1-1. |
| NTC 2018 | DM 17/01/2018 | §11.2.11 | Caratteristiche del calcestruzzo secondo le Linee Guida del C.S.LL.PP., con riferimento a UNI EN 206 e UNI 11104. |
| Circolare 2019 | n. 7 del 21/01/2019 | §C4.1.6.1.3, Tab. C4.1.IV | Copriferri minimi per ambiente, elemento (piastra o altro) e classe (C ≥ C0; Cmin ≤ C < C0). Valori da aumentare di 10 mm per 100 anni e di 5 mm sotto Cmin; riducibili di 5 mm con controllo di qualità; tolleranza di posa fino a 10 mm. Cmin riferita alla pertinente classe di esposizione. |
| EN 1992-1-1 | 2004 + A1:2014, valori raccomandati | §4.4.1.1-4.4.1.3, eq. (4.1)-(4.2), prospetti 4.2, 4.3N, 4.4N | cnom = cmin + Δcdev; cmin = max(cmin,b; cmin,dur; 10 mm); classi strutturali S1-S6 (S4 di base); maggiorazioni 4.4.1.2(11), (13); getti contro terreno 4.4.1.3(4). |
| EN 1992-1-1 | 2004 + A1:2014 | Appendice E, prospetto E.1N (informativa) | Classi indicative di resistenza per esposizione; XF4 assente. |
| UNI EN 1992-1-1 | appendice nazionale DM 31/07/2012 | §4.4.1; prospetto E.1N | Valori raccomandati di 4.4.1; prospetto E.1N nazionale (XC1 C25/30, XF2 C30/37). |
| DS/EN 1992-1-1 | DK NA:2024 | 4.4.1.2(5), Tabel 4.4N NA; 4.4.1.3(1)P; Tabel E.1(2) | cmin,dur per esposizione senza classi strutturali; Δcdev ≥ 5 mm; resistenza minima per gruppi. |
| UNI EN 206-1 | 2006 | prospetto F.1 (informativo) | a/c massimo, classe minima, cemento minimo, aria minima per esposizione. |
| UNI 11104 | edizione 2016 secondo la fonte usata (vedi tabella A) | prospetto dei valori limite di composizione | a/c, classe minima, cemento, aria per XF2-XF4: valori implementati. |
| UNI 11104 | 2004 | prospetto 4 | Classe minima C28/35 per XC3, XD1, XF4 e XA1 (tabella B). Testo non consultato, valori da fonti secondarie. |
| UNI 11104 | 2025 (in vigore dal 24/07/2025, sostituisce l'edizione 2016) | — | Non implementata; testo non consultato. |
| CNR-DT 200 R1/2013 | 2013 | — | Membratura in c.a. secondo NTC. |

## Ipotesi

- Le classi di esposizione sono assegnate dal progettista. Una combinazione agisce insieme: governa il valore
  più severo (resistenza massima, a/c minimo, cemento massimo, copriferro massimo).
- Copriferri per armature ordinarie, misurati sull'armatura più esterna (staffe comprese).
- cmin,b è il diametro della barra o della staffa considerata, aumentato di 5 mm se l'aggregato supera 32 mm
  (EN prospetto 4.2). Il diametro equivalente dei fasci è a carico del chiamante.
- Le maggiorazioni per superficie irregolare e abrasione si sommano a cmin. Il minimo dei getti contro terreno
  vale per cnom. Le stesse regole di EN 1992-1-1 valgono nel profilo NTC, che a esse rimanda (NTC §4.1.6.1.3).
- Profilo NTC: la classe minima Cmin della Tab. C4.1.IV è un dato, cioè la classe pertinente all'esposizione.
  Se manca, il metodo usa il valore di tabella dell'ambiente (vedi Scostamento dichiarato).
- Profili Eurocodice: la classe strutturale di partenza è S4 per 50 anni; le modifiche del prospetto 4.3N sono
  dati del chiamante.
- I valori di composizione sono requisiti minimi per la prescrizione del calcestruzzo; non costituiscono una
  miscela.

## Notazione, unità e convenzioni

Unità: mm, MPa. Le classi di resistenza sono rappresentate da fck in MPa (C30/37 → 30). Nel testo si usa la
virgola decimale.

| Simbolo | Significato | Unità | Proprietà nel codice |
| --- | --- | --- | --- |
| X | insieme delle classi di esposizione | — | `CoverInput.Exposures` |
| fck | resistenza caratteristica cilindrica del calcestruzzo | MPa | `CoverInput.Fck` |
| VN | vita nominale (50 o 100 anni) | anni | `DesignLife` |
| Øb | diametro per l'aderenza (barra o staffa) | mm | `BarDiameter` |
| dg | dimensione massima dell'aggregato | mm | `Aggregate` |
| Δcdev | tolleranza di esecuzione | mm | `Deviation` |
| Δcrug | maggiorazione per superficie irregolare (0 o 5 mm) | mm | `RoughSurface` |
| Δcabr | maggiorazione per abrasione (0, 5, 10, 15 mm) | mm | `Abrasion` |
| cground | minimo per getti contro terreno (0, 40, 75 mm) | mm | `Ground` |
| g | gruppo ambientale NTC (0 ordinario, 1 aggressivo, 2 molto aggressivo) | — | `CoverResult.NtcEnvironment` |
| Cmin, C0 | classi della Tab. C4.1.IV (come fck) | MPa | `NtcCmin`, `NtcC0`; dato `PertinentCmin` |
| S | classe strutturale EN (1-6) | — | `CoverLine.StructuralClass` |
| cmin,b | copriferro minimo per l'aderenza | mm | `CoverResult.Bond` |
| cmin,dur | copriferro minimo di durabilità | mm | `CoverResult.Durability` |
| cmin | copriferro minimo | mm | `CoverResult.Minimum` |
| cnom | copriferro nominale | mm | `CoverResult.Nominal` |

## Formulazione

### Catalogo delle esposizioni

Valori implementati per classe. Colonne:

- EN 206: UNI EN 206-1:2006, prospetto F.1, con a/c massimo, classe minima, cemento minimo in kg/m³ e aria
  minima in %;
- g: gruppo NTC della Tab. 4.1.III;
- col. 4.4N: colonna del prospetto 4.4N di EN 1992-1-1 (— = nessuna);
- UNI 11104 A: valori implementati, tabella A, con a/c, classe e cemento;
- E.1N: classe indicativa di EN 1992-1-1 e del DM 31/07/2012;
- DK: DK NA Tabel E.1(2).

| Classe | EN 206 | g | col. 4.4N | UNI 11104 A | E.1N EN / DM 2012 | DK |
| --- | --- | --- | --- | --- | --- | --- |
| X0 | — / C12/15 / — / — | 0 | X0 | — / C12/15 / — | C12/15 / C12/15 | 12 |
| XC1 | 0,65 / C20/25 / 260 / — | 0 | XC1 | 0,60 / C25/30 / 300 | C20/25 / C25/30 | 12 |
| XC2 | 0,60 / C25/30 / 280 / — | 0 | XC2-XC3 | 0,60 / C25/30 / 300 | C25/30 / C25/30 | 30 |
| XC3 | 0,55 / C30/37 / 280 / — | 0 | XC2-XC3 | 0,55 / C30/37 / 320 | C25/30 / C25/30 | 30 |
| XC4 | 0,50 / C30/37 / 300 / — | 1 | XC4 | 0,50 / C32/40 / 340 | C30/37 / C30/37 | 30 |
| XD1 | 0,55 / C30/37 / 300 / — | 1 | XD1-XS1 | 0,55 / C30/37 / 320 | C30/37 / C30/37 | 35 |
| XD2 | 0,55 / C30/37 / 300 / — | 2 | XD2-XS2 | 0,50 / C32/40 / 340 | C30/37 / C30/37 | 40 |
| XD3 | 0,45 / C35/45 / 320 / — | 2 | XD3-XS3 | 0,45 / C35/45 / 360 | C35/45 / C35/45 | 40 |
| XS1 | 0,50 / C30/37 / 300 / — | 1 | XD1-XS1 | 0,50 / C32/40 / 340 | C30/37 / C30/37 | 35 |
| XS2 | 0,45 / C35/45 / 320 / — | 2 | XD2-XS2 | 0,45 / C35/45 / 360 | C35/45 / C35/45 | 35 |
| XS3 | 0,45 / C35/45 / 340 / — | 2 | XD3-XS3 | 0,45 / C35/45 / 360 | C35/45 / C35/45 | 40 |
| XF1 | 0,55 / C30/37 / 300 / — | 0 | — | 0,50 / C32/40 / 320 | C30/37 / C30/37 | 30 |
| XF2 | 0,55 / C25/30 / 300 / 4,0 | 1 | — | 0,50 / C25/30 / 340 | C25/30 / C30/37 | 35 |
| XF3 | 0,50 / C30/37 / 320 / 4,0 | 1 | — | 0,50 / C25/30 / 340 | C30/37 / C30/37 | 35 |
| XF4 | 0,45 / C30/37 / 340 / 4,0 | 2 | — | 0,45 / C30/37 / 360 | — / — | 40 |
| XA1 | 0,55 / C30/37 / 300 / — | 1 | — | 0,55 / C30/37 / 320 | C30/37 / C30/37 | 30 |
| XA2 | 0,50 / C30/37 / 320 / — | 1 | — | 0,50 / C32/40 / 340 | C30/37 / C30/37 | 35 |
| XA3 | 0,45 / C35/45 / 360 / — | 2 | — | 0,45 / C35/45 / 360 | C35/45 / C35/45 | 40 |

UNI 11104, aria inglobata per XF2-XF4: 4 % con dg > 20 mm; 5 % con 12 mm ≤ dg ≤ 16 mm; nessun valore negli
altri casi.

### Classe minima di resistenza secondo UNI 11104: due riferimenti

Per quattro classi le fonti danno due valori di classe minima.

| Classe | Tabella A, implementata | Tabella B |
| --- | --- | --- |
| XC3 | C30/37 | C28/35 |
| XD1 | C30/37 | C28/35 |
| XF4 | C30/37 | C28/35 |
| XA1 | C30/37 | C28/35 |
| altre 14 classi | come nel catalogo | uguali alla tabella A |

- **Tabella A** (implementata). È la tabella dei valori limite della UNI 11104 riprodotta nella documentazione
  ATECAP del 2020, che indica come edizione in vigore quella del 2016. Coincide con EN 206-1 prospetto F.1 per
  le quattro classi. Il testo UNI non è stato consultato.
- **Tabella B.** È il prospetto 4 della UNI 11104:2004, con C28/35 per le quattro classi, secondo fonti
  secondarie. Il testo non è stato consultato.

Per a/c e cemento le fonti non mostrano differenze fra le due tabelle.

> **Scostamento dichiarato (e) — classe minima UNI 11104 per XC3, XD1, XF4 e XA1.** Il codice usa C30/37
> (tabella A). La tabella B dà C28/35. La decisione dell'utente è di mantenere entrambi i riferimenti e di
> lasciare all'utente la scelta; la scelta sarà un'opzione del metodo e oggi non c'è. Effetto della tabella A
> rispetto alla B:
>
> - la classe minima cambia in 46 delle 520 combinazioni di esposizione considerate (al più una classe per
>   famiglia, da una a tre classi, più X0);
> - l'esito del controllo cambia solo per i calcestruzzi con 28 ≤ fck < 30 MPa, cioè C28/35, che con la tabella
>   A risultano insufficienti;
> - nel profilo NTC, quando la classe UNI 11104 è usata come Cmin pertinente, il copriferro cresce di 5 mm per
>   gli stessi calcestruzzi (184 casi su 17 646 di una griglia di combinazioni, resistenze e tipi di elemento).
>
> Esempio numerico 1 qui sotto.

### Copriferro minimo e nominale, comune a tutti i profili

EN eq. (4.1)-(4.2), prospetto 4.2, 4.4.1.2(11), (13), 4.4.1.3(4):

```math
c_{min,b} = \varnothing_b + 5\ \text{mm}\cdot[d_g > 32\ \text{mm}],\qquad
c_{min} = \max\left(10\ \text{mm};\ c_{min,b};\ c_{min,dur}\right) + \Delta c_{rug} + \Delta c_{abr},\qquad
c_{nom} = \max\left(c_{min} + \Delta c_{dev};\ c_{ground}\right)
\tag{1}
```

### Profilo NTC 2018 e CNR-DT 200

Gruppo ambientale della combinazione (Tab. 4.1.III) e classi della Tab. C4.1.IV. Il default di Cmin vale solo
se il chiamante non assegna la classe pertinente:

```math
g = \max_{X} g(X),\qquad C_0 = 35 + 5\,g,\qquad C_{min} = C_{min,pertinente}\ \ \text{oppure}\ \ 25 + 5\,g,\qquad 12 \le C_{min} \le C_0
\tag{2}
```

Valore della Tab. C4.1.IV e copriferro di durabilità (Circolare §C4.1.6.1.3):

```math
c_{tab} = c_{base} + 10\,g + 5\,[f_{ck} < C_0],\quad c_{base} = 15\ \text{mm (piastre)},\ 20\ \text{mm (altri elementi)}
\tag{3}
```

```math
c_{min,dur} = c_{tab} + 10\,[V_N = 100] + 5\,[f_{ck} < C_{min}] - 5\,[\text{controllo di qualità}]
\tag{4}
```

Con fck < Cmin il valore della colonna Cmin ≤ C < C0 aumenta di altri 5 mm: complessivamente +10 mm rispetto a
C ≥ C0.

> **Scostamento dichiarato — Cmin di default nel profilo NTC.** La Circolare riferisce Cmin alla pertinente
> classe di esposizione. Senza un valore assegnato, il codice usa la classe di tabella dell'ambiente: C25/30,
> C30/37 o C35/45. Per alcune esposizioni questa classe è inferiore a quella pertinente; per esempio XC3 ha
> C25/30 in tabella e C30/37 nella tabella A. Effetto: con 25 ≤ fck < 30 e XC3 il copriferro di durabilità
> risulta 5 mm più basso di quello calcolato con la classe pertinente. Il verificatore del modello passa sempre
> la classe UNI 11104 come Cmin pertinente, quindi il default riguarda solo le chiamate dirette.

### Profili EN 1992-1-1 e UNI (DM 31/07/2012)

Classe strutturale di ogni esposizione con colonna nel prospetto 4.4N (EN prospetto 4.3N):

```math
S(X) = \max\left[1;\ 4 + 2\,[V_N = 100] - [\text{riduzione} \wedge f_{ck} \ge f_{soglia}(X)] - [\text{soletta}] - [\text{controllo di qualità}]\right]
\tag{5}
```

| Esposizione | fsoglia |
| --- | --- |
| X0, XC1 | 30 MPa (C30/37) |
| XC2, XC3 | 35 MPa (C35/45) |
| XC4, XD1, XD2, XS1 | 40 MPa (C40/50) |
| XD3, XS2, XS3 | 45 MPa (C45/55) |

Copriferro di durabilità (EN prospetto 4.4N; le esposizioni XF e XA non hanno colonna):

```math
c_{min,dur} = \max_{X\ \text{con colonna}}\ T_{4.4N}\left[S(X),\ \text{colonna}(X)\right]
\tag{6}
```

| S | X0 | XC1 | XC2/XC3 | XC4 | XD1/XS1 | XD2/XS2 | XD3/XS3 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| S1 | 10 | 10 | 10 | 15 | 20 | 25 | 30 |
| S2 | 10 | 10 | 15 | 20 | 25 | 30 | 35 |
| S3 | 10 | 10 | 20 | 25 | 30 | 35 | 40 |
| S4 | 10 | 15 | 25 | 30 | 35 | 40 | 45 |
| S5 | 15 | 20 | 30 | 35 | 40 | 45 | 50 |
| S6 | 20 | 25 | 35 | 40 | 45 | 50 | 55 |

### Profilo DS (DK NA)

Tabel 4.4N NA, senza classi strutturali, solo per 50 anni, con Δcdev ≥ 5 mm:

```math
c_{min,dur} = \max_{X\ \text{con colonna}} c_{DK}(X),\quad
c_{DK} = \begin{cases}
10 & \text{X0, XC1}\\
20 & \text{XC2, XC3, XC4}\\
30 & \text{XD1, XS1, XS2}\\
40 & \text{XD2, XD3, XS3}
\end{cases}
\tag{7}
```

### Classe minima di resistenza della combinazione

```math
f_{ck,min}(X) =
\begin{cases}
\max_X f_{UNI\,11104}(X) & \text{NTC, CNR-DT 200 (NTC §11.2.11)}\\
\max_{X \ne XF4} f_{E.1N}(X) & \text{EN, UNI (appendice E informativa; XF4 non definita, segnalata)}\\
\max_X f_{DK}(X) & \text{DS (DK NA Tabel E.1(2))}
\end{cases}
\tag{8}
```

Requisiti di composizione UNI 11104 della combinazione: a/c = min_X, cemento = max_X, aria come sopra. EN 206
F.1 è disponibile per classe e come massimo della resistenza minima.

## Coefficienti e valori predefiniti

| Simbolo | Valore | Fonte | Modificabile | Dove sta nel codice |
| --- | --- | --- | --- | --- |
| catalogo delle classi (18) | vedi tabella | EN 206 F.1; NTC Tab. 4.1.III; UNI 11104 (tabella A); EN E.1N; DM 2012 E.1N; DK E.1(2) | no | `ExposureClasses.All` |
| classe UNI 11104 di XC3, XD1, XF4, XA1 | C30/37 | tabella A (scostamento (e)) | no (opzione da implementare) | `ExposureClasses.All` |
| minimo assoluto | 10 mm | EN (4.2) | no | `CoverRequirements.Calculate` |
| maggiorazione di cmin,b | 5 mm per dg > 32 mm | EN prospetto 4.2 | no | `Calculate` |
| superficie irregolare | 5 mm | EN 4.4.1.2(11) | sì/no (`RoughSurface`) | `CoverInput` |
| abrasione k1/k2/k3 | 5/10/15 mm | EN 4.4.1.2(13) (raccomandati) | scelta fra i valori | `CoverInput` |
| getti contro terreno k1/k2 | 40/75 mm | EN 4.4.1.3(4) (raccomandati) | scelta fra i valori | `CoverInput` |
| Δcdev | dato (EN 10 mm raccomandato; NTC ≤ 10 mm; DS ≥ 5 mm) | EN 4.4.1.3(1)P; Circolare; DK NA | sì | `CoverInput` |
| Δcdur,γ, Δcdur,st, Δcdur,add | 0 | EN 4.4.1.2(6)-(8) (raccomandati) | no | implicito |
| NTC: cbase, passo per gruppo, C0, Cmin di default | 15/20 mm; 10 mm; 35 + 5g; 25 + 5g | Circolare Tab. C4.1.IV | Cmin sì (`PertinentCmin`) | `Calculate` |
| NTC: 100 anni, sotto Cmin, controllo di qualità | +10, +5, −5 mm | Circolare §C4.1.6.1.3 | tramite i dati | `Calculate` |
| EN: classe di base, modifiche, soglie | S4; +2, −1, −1, −1; tabella delle soglie | EN prospetto 4.3N | tramite i dati | `CoverRequirements.StructuralClass` |
| EN: prospetto 4.4N | vedi tabella | EN prospetto 4.4N | no | `CoverRequirements.Table44N` |
| DS: Tabel 4.4N NA | 10/20/30/40 mm | DK NA:2024 | no | `Calculate` |
| aria UNI 11104 | 4 % (dg > 20), 5 % (12-16 mm) | UNI 11104 | no | `ExposureClasses.Uni11104Air` |

## Implementazione

Classi e file (namespace `GPC.Checkers.Concrete.Durability`):

- `Durability/ExposureClasses.cs`:
  - `ExposureClass` (16-42) e `StrengthRequirement` (45-51);
  - catalogo `All` (56-76); le classi di tabella A sono alle righe 61, 63, 72 e 73;
  - `Get` (78) e `Resolve` (81-87), che controlla X0 e la combinazione vuota;
  - `Uni11104MinimumStrength` (90), `En206MinimumStrength` (93), `MinimumStrength` (100-119; riferimento
    testuale alla riga 107), `Uni11104Mix` (122-126), `Uni11104Air` (129-135).
- `Durability/CoverRequirements.cs`:
  - `DurabilityProfiles` (16-58): risoluzione per tipo esatto (18-30), motivi (32-37), riferimenti (46-57);
  - `CoverInput` (65-90), `CoverLine` (93-99), `CoverResult` (102-119);
  - prospetto 4.4N (131-132), `StructuralClass` (135-139);
  - `Calculate` (141-181):
    1. risolve le esposizioni e controlla i dati (144-148);
    2. calcola cmin,b (149);
    3. applica il ramo NTC, eq. 2-4 (152-162), il ramo DS, eq. 7 (163-171), oppure il ramo EN/UNI, eq. 5-6
       (172-177);
    4. calcola cmin e cnom, eq. 1 (178-179).
- Uso nel verificatore del modello (repository Model, `ModelChecker/ConcreteSectionVerifier.Detailing.cs`,
  123-167):
  - Cmin pertinente = valore assegnato oppure classe UNI 11104 della combinazione (133);
  - copriferro calcolato con soletta e piastra sempre false (137-139), a favore di sicurezza;
  - controllo della classe minima di resistenza come metrica (157-163); le classi non definite (XF4 con EN e
    UNI) sono annotate (164-165).

## Limiti e casi non supportati

- Nessuna tabella per armature da precompressione.
- DS: solo 50 anni. DIN, NS, Model Code 2010 e CNR-DT 204 non supportati.
- La riduzione della soglia di resistenza con aria inglobata (EN prospetto 4.3N, nota 2) e le riduzioni per
  acciaio inossidabile o protezioni aggiuntive non sono applicate.
- UNI 11104: due tabelle di classe minima (scostamento (e)); oggi è implementata solo la tabella A. L'edizione
  2025 non è implementata.
- Il testo di riferimento restituito per il profilo NTC cita «UNI 11104 prospetto 5». La numerazione del
  prospetto dipende dall'edizione: 4 nell'edizione 2004, 5 nella documentazione usata per la tabella A, 6
  nell'edizione 2025 secondo una fonte secondaria.
- Aria inglobata UNI 11104: nessun valore per 16 mm < dg ≤ 20 mm e per dg < 12 mm, dove la norma indica solo
  un aumento rispetto al 4 %.
- Nel profilo NTC, senza Cmin pertinente si usa la classe di tabella dell'ambiente (Scostamento dichiarato).
- I valori di EN 206 sono quelli di UNI EN 206-1:2006. Il prospetto F.1 dell'edizione EN 206:2013+A2:2021 non è
  stato riletto.

## Esempio numerico verificato

**Esempio 1 — NTC 2018, trave in XC3, C28/35, 50 anni.** Staffe Ø8, dg = 20 mm, Δcdev = 10 mm, elemento non a
piastra, senza controllo di qualità.

1. g = 0 (XC3 ordinaria), C0 = 35 MPa; c_tab = 20 + 0 + 5 = 25 mm, perché 28 < 35.
2. Tabella A, Cmin = 30: 28 < 30, quindi cmin,dur = 25 + 5 = 30 mm. cmin = max(10; 8; 30) = 30 mm;
   cnom = 40 mm.
3. Tabella B, Cmin = 28: nessuna maggiorazione, cmin,dur = 25 mm, cnom = 35 mm.
4. Senza Cmin pertinente (default 25): cmin,dur = 25 mm, cnom = 35 mm.
5. Classe minima: tabella A C30/37, quindi C28/35 insufficiente; tabella B C28/35, sufficiente. EN 206 F.1:
   C30/37, a/c 0,55, cemento 280 kg/m³. UNI 11104 (tabella A): a/c 0,55, cemento 320 kg/m³.

**Esempio 2 — XC4 + XD1, C40/50, staffe Ø12, dg = 20 mm, Δcdev = 10 mm.**

1. EN, 50 anni, con riduzione per resistenza:
   - XC4: fck = 40 ≥ 40, S = 4 − 1 = 3, quindi 25 mm;
   - XD1: fck = 40 ≥ 40, S3, quindi 30 mm;
   - cmin,dur = 30 mm, cmin = max(10; 12; 30) = 30 mm, cnom = 40 mm.
2. EN senza riduzione: S4, 30 e 35 mm, cnom = 45 mm.
3. EN, 100 anni con riduzione: S5, 35 e 40 mm, cnom = 50 mm.
4. NTC:
   - g = 1 (XC4 e XD1 aggressive), C0 = 40, Cmin = max(32; 30) = 32 (UNI 11104);
   - c_tab = 20 + 10 + 0 = 30 mm, perché 40 ≥ C0;
   - fck ≥ Cmin, quindi cmin,dur = 30 mm e cnom = 40 mm.
5. DS: XC4 20 mm, XD1 30 mm; cmin,dur = 30 mm, cnom = 40 mm.
6. Classe minima:
   - NTC: C32/40 (UNI 11104 XC4);
   - EN: C30/37 (E.1N);
   - DS: 35 MPa (XD1);
   - EN 206 F.1: C30/37.

Valori della libreria (GPCChecker.Concrete 0.0.15.0, eseguita):

| Caso | A mano: cmin,dur / cmin / cnom [mm] | Libreria | Scarto |
| --- | --- | --- | --- |
| 1, Cmin = 30 (tabella A) | 30 / 30 / 40 | 30 / 30 / 40 (g 0, C0 35, tab. 25, +5) | 0 |
| 1, Cmin = 28 (tabella B) | 25 / 25 / 35 | 25 / 25 / 35 | 0 |
| 1, Cmin di default | 25 / 25 / 35 | 25 / 25 / 35 (Cmin 25) | 0 |
| 1, classe minima NTC | C30/37 | 30 (riferimento «UNI 11104 prospetto 5 (NTC 2018 §11.2.11)») | 0 |
| 2, EN con riduzione | 30 / 30 / 40; S3, S3 | 30 / 30 / 40; XC4 S3 25, XD1 S3 30 | 0 |
| 2, EN senza riduzione | 35 / 35 / 45 | 35 / 35 / 45; S4 | 0 |
| 2, EN 100 anni | 40 / 40 / 50 | 40 / 40 / 50; S5 | 0 |
| 2, NTC | 30 / 30 / 40 | 30 / 30 / 40 (g 1, Cmin 32, C0 40, tab. 30) | 0 |
| 2, DS | 30 / 30 / 40 | 30 / 30 / 40 | 0 |
| 2, classi minime NTC / EN / DS / EN 206 | 32 / 30 / 35 / 30 | 32 / 30 / 35 / 30 | 0 |

## Validazione

- Casi congelati del motore precedente (`durability-legacy.csv` del progetto di test).
  Griglia: 24 combinazioni di esposizione × 7 resistenze × 6 insiemi di opzioni.
  - 588 copriferri EC2 e 1932 NTC: cmin,b, cmin,dur, cmin, cnom, righe EC2 con classe strutturale, dettagli NTC;
  - 505 rifiuti;
  - 23 requisiti UNI 11104: classe minima, a/c, cemento, aria con dg di 8, 16 e 32 mm.

  Ripetuti sulle DLL della versione 0.0.15.0: 0 differenze.
- Test:
  - `DurabilityMigrationTests` (3 metodi): casi congelati; valori di tabella EN, DS e NTC; risoluzione per tipo
    esatto;
  - `DurabilityEdgeCaseTests` (9 metodi): soglie del prospetto 4.3N, limiti S1-S6, combinazioni, maggiorazioni,
    limiti di Cmin e C0, DS senza classi strutturali, dati non validi, classe minima per profilo, composizione e
    aria;
  - `DurabilityExamplesTests` (5 esempi calcolati a mano: NTC aggressivo, EN con classe strutturale, UNI contro
    terreno, DS marino con abrasione, NTC piastra a 100 anni).
- Riscontro sui testi in questa revisione:
  - UNI EN 206-1:2006 prospetto F.1: le 18 classi coincidono con il catalogo;
  - EN 1992-1-1:2004 §4.4.1 e prospetti 4.3N, 4.4N, E.1N;
  - NTC 2018 Tab. 4.1.III e §11.2.11;
  - Circolare 2019 §C4.1.6.1.3 e Tab. C4.1.IV.

  UNI 11104: tabella A verificata sulla documentazione ATECAP del 2020; tabella B e edizione 2025 solo da fonti
  secondarie. DM 31/07/2012 (prospetto E.1N) e DK NA:2024 non sono stati riletti in questa revisione.

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
6. UNI EN 206-1:2006, *Calcestruzzo — Parte 1: Specificazione, prestazione, produzione e conformità*.
7. UNI 11104:2004 e UNI 11104:2016, *Calcestruzzo — Specificazione, prestazione, produzione e conformità —
   Istruzioni complementari per l'applicazione della EN 206*. Edizione corrente: UNI 11104:2025.
8. ATECAP, *La corretta prescrizione del calcestruzzo — Documentazione di riferimento*, 2020 (prospetto 5,
   «Norma UNI 11104: valori limite per la composizione e le proprietà del calcestruzzo»).
9. Consiglio Superiore dei Lavori Pubblici, Servizio Tecnico Centrale, *Linee guida sul calcestruzzo
   strutturale*.
10. CNR-DT 200 R1/2013, *Istruzioni per la progettazione, l'esecuzione ed il controllo di interventi di
    consolidamento statico mediante l'utilizzo di compositi fibrorinforzati*.
