---
id: ca.fessurazione
titolo: Fessurazione di sezione in calcestruzzo armato, requisiti, ampiezza delle fessure, aree efficaci e interassi
libreria: GPCChecker.Concrete
classi:
  - GPC.Checkers.Concrete.Cracking.SectionCrackCheck
  - GPC.Checkers.Concrete.Cracking.SectionCrackInput
  - GPC.Checkers.Concrete.Cracking.SectionCrackResult
  - GPC.Checkers.Concrete.Cracking.CrackRegion
  - GPC.Checkers.Concrete.Cracking.CrackOutcome
  - GPC.Checkers.Concrete.Cracking.CrackVerdict
  - GPC.Checkers.Concrete.Cracking.CrackRequirements
  - GPC.Checkers.Concrete.Cracking.CrackRequirement
  - GPC.Checkers.Concrete.Cracking.CrackCriterion
  - GPC.Checkers.Concrete.Cracking.CrackWidthCalculator
  - GPC.Checkers.Concrete.Cracking.CrackWidthInput
  - GPC.Checkers.Concrete.Cracking.CrackSectionGeometry
  - GPC.Checkers.Concrete.Cracking.CrackBar
  - GPC.Checkers.Concrete.Cracking.CrackBarLayout
  - GPC.Checkers.Concrete.Cracking.CrackProfiles
  - GPC.Checkers.Concrete.Cracking.CrackProfile
versione: 0.0.15.0
norme:
  - ntc2018-4.1.2.2.4
  - circ2019-C4.1.2.2.4
  - en1992-1-1-7.3.1
  - en1992-1-1-7.3.2
  - en1992-1-1-7.3.4
  - na-uni-2012-7.3.1
  - na-uni-2012-7.3.4
  - na-din-7.3.1
  - na-din-7.3.2
  - na-din-7.3.4
  - na-dk-2024-7.3.1
  - na-dk-2024-7.3.2
  - na-dk-2024-7.3.4
  - na-ns-7.3.1
  - mc2010-7.6.4
  - cnr-dt200-r1-2013
stato: bozza
---

# Fessurazione di sezione in calcestruzzo armato

## 1. Scopo

Il metodo verifica la fessurazione di una sezione di calcestruzzo armato ordinario per uno stato di esercizio.
Comprende:

- il **requisito** della norma per la combinazione e la classe di esposizione: nessuna verifica, decompressione,
  formazione delle fessure o apertura delle fessure con il limite wlim;
- la **decompressione** e la **formazione delle fessure** sulla sezione non fessurata;
- l'**ampiezza delle fessure** wk su ogni regione tesa efficace: zona tesa delle sezioni parzializzate, facce delle
  sezioni interamente tese, superfici interne delle sezioni cave; con l'area efficace Ac,eff, l'armatura efficace e
  l'interasse delle barre.

Restituisce il criterio e il limite, wk e il rapporto wk/wlim della regione governante (o la tensione della sezione
non fessurata), l'esito, tutte le regioni esaminate e la traccia dei valori intermedi.

## 2. Campo di applicazione

| Caso | Stato | Comportamento del programma |
| --- | --- | --- |
| NTC 2018, UNI, EN 1992-1-1, DIN, DS, NS, Model Code 2010 | supportato | requisito e ampiezza della norma |
| CNR-DT 200 R1/2013 | supportato senza dati FRP | requisito e ampiezza di NTC 2018 |
| CNR-DT 204/2006 | non supportato | errore (`NotSupportedException`): fessurazione dei calcestruzzi fibrorinforzati non implementata |
| CS-TR34 | non applicabile | errore (`NotSupportedException`) con il motivo |
| ACI 318, AASHTO; classi derivate da quelle elencate | non supportato | errore (`NotSupportedException`) |
| Combinazione per cui la norma non richiede la verifica | non richiesta | esito `NotRequired` con la combinazione richiesta |
| Classe di esposizione mancante dove serve | dati insufficienti | esito incompleto (`MissingExposure`) |
| wlim di progetto mancante dove la norma non dà un valore (Model Code 2010; classi fuori tabella) | dati insufficienti | esito incompleto (`MissingDesignLimit`) |
| Decompressione o formazione senza la tensione della sezione non fessurata | dati non validi | errore (`ArgumentException`) |
| Apertura delle fessure con analisi non lineare o con cls teso | non valutabile | esito incompleto (`RequiresLinearCrackedAnalysis`) |
| Sezione con trefoli (c.a.p.) | non supportato | esito incompleto (`PrestressNotSupported`) |
| Sezione interamente compressa | supportato | wk = 0 |
| Asse neutro nel copriferro del lembo teso armato | supportato | wk = 0 (riquadro F-3) |
| Lembo teso senza armatura | non valutabile | esito incompleto (`NoTensileReinforcement`) |
| Barre tese tutte fuori da Ac,eff | supportato | limite superiore con sr,max da (h − x) (riquadro F-4) |
| Interasse automatico non determinato (meno di due barre efficaci, barre non allineate, anelli non confermati) | dati insufficienti | esito incompleto (`SpacingUndetermined`); l'interasse si assegna |
| Piano di deformazione senza gradiente con sezione parzializzata | non valutabile | esito incompleto (`NeutralAxisUndetermined`) |
| Area efficace nulla | non valutabile | esito incompleto (`NoEffectiveArea`) |
| Sezione cava con foro rettangolare allineato agli assi o anello circolare | supportato | superfici interne verificate a parte |
| Altri fori con superficie interna tesa | non supportato | esito incompleto (`InnerSurfaceNotSupported`) |
| Superficie interna tesa senza armatura | non valutabile | esito incompleto (`InnerSurfaceUnreinforced`) |
| Barre lisce con Model Code 2010 o DIN | non supportato | errore (`ArgumentException`) |

## 3. Riferimenti normativi

| Formula o grandezza | Norma | Edizione e appendice | Paragrafo | Eq. o tabella | Riscontro |
| --- | --- | --- | --- | --- | --- |
| Gruppi di esposizione | NTC | 2018 | 4.1.2.2.4 | Tab. 4.1.III | testo NTC 2008 |
| Criteri per combinazione, ambiente e sensibilità delle armature; w1, w2, w3 = 0,2, 0,3, 0,4 mm | NTC | 2018 | 4.1.2.2.4 | Tab. 4.1.IV | testo NTC 2008 |
| Decompressione; formazione delle fessure con σt = fctm/1,2 | NTC | 2018 | 4.1.2.2.4 | — | testo NTC 2008 |
| wd = 1,7 wm | NTC | 2018 | 4.1.2.2.4 | — | testo NTC 2008 |
| εsm, Δsm (F.12); barre distanziate (riquadro F-1) | Circolare | 2019 | C4.1.2.2.4 | n. da riscontrare | in verifica |
| Requisiti con le condizioni ambientali NTC | UNI EN 1992-1-1 | DM 31/07/2012 | 7.3.1(5) | — | testo |
| k3 = 3,4, k4 = 0,425 | UNI EN 1992-1-1 | DM 31/07/2012 | 7.3.4(3) | — | testo |
| wmax per classe di esposizione | EN 1992-1-1 | 2004 + AC:2010 | 7.3.1(5) | Prospetto 7.1N | da riscontrare (in particolare XD3) |
| hc,ef, Ac,eff | EN 1992-1-1 | 2004 + AC:2010 | 7.3.2(3) | Fig. 7.1 | da riscontrare |
| wk, εsm − εcm, sr,max, Øeq, k2, barre distanziate | EN 1992-1-1 | 2004 + AC:2010 | 7.3.4(1)-(3) | (7.8), (7.9), (7.11), (7.12), (7.13), (7.14) | da riscontrare |
| Prospetto 7.1DE (X0, XC1 0,4; altre 0,3) | DIN EN 1992-1-1 | NA | 7.3.1(5) | Tab. 7.1DE | fonte secondaria |
| hc,ef con coefficiente funzione di h/(h − d) e (h − x)/3 se ≥ c + 20 mm | DIN EN 1992-1-1 | NA | 7.3.2(3) | Bild 7.1DE | fonte secondaria (riquadro F-7) |
| sr,max = Ø/(3,6 ρ) ≤ σs Ø/(3,6 fct,eff); kt = 0,4 | DIN EN 1992-1-1 | NA | 7.3.4(2), 7.3.4(3) | (7.11) modificata | fonte secondaria |
| Tabel 7.1 NA (0,2 / 0,3 / 0,4 mm) | DS/EN 1992-1-1 | DK NA:2024 | 7.3.1(5) | Tabel 7.1 NA | testo |
| Ac,eff con baricentro sull'armatura; sistema fine e grossolano | DS/EN 1992-1-1 | DK NA:2024 | 7.3.2(1)P, 7.3.4(1) | Fig. 7.100 NA | testo |
| k3 = 3,4 (25/c)^(2/3) | DS/EN 1992-1-1 | DK NA:2024 | 7.3.4(3) | — | testo |
| Tabella NA.7.1N (X0 0,4; XC, XD, XS 0,3 kc; frequente per XD3, XS3) | NS-EN 1992-1-1 | NA | 7.3.1(5) | Tab. NA.7.1N | fonte secondaria (riquadro F-5) |
| Ampiezza delle fessure, ls,max, τbms, βTS | fib MC2010 | 2013 | 7.6.4 | n. da riscontrare | da riscontrare (riquadro F-6) |

## 4. Ipotesi

- Lo stato tensionale fessurato viene dal solutore sezionale con analisi lineare senza resistenza a trazione del
  calcestruzzo, per la combinazione indicata: piano di deformazione nel piano della sezione e tensioni delle barre
  ordinarie (compressione negativa). Decompressione e formazione usano invece la tensione massima di trazione della
  sezione non fessurata omogeneizzata con il cls teso, fornita dal chiamante.
- Si considerano solo le barre ordinarie; i trefoli rendono la sezione "precompressa" e la verifica non è eseguita.
- La direzione di verifica di una sezione parzializzata è quella del gradiente del piano di deformazione; la zona
  tesa efficace è la parte della sezione oltre la quota Qmax − hc,eff lungo quel gradiente.
- Nella sezione interamente tesa le facce opposte (±x, ±y, oppure fasce radiali nei cerchi) sono verificate in modo
  indipendente e le loro aree efficaci non si sommano mai.
- σs è la tensione massima delle barre efficaci (a favore di sicurezza con più strati).
- Il copriferro c è misurato alla superficie delle barre longitudinali (copriferro nominale più diametro della
  staffa), dato dal chiamante per la zona tesa delle sezioni parzializzate, oppure calcolato dalla geometria per le
  facce e le superfici interne; può essere assegnato.
- fct,eff = fctm del calcestruzzo, dato dal chiamante.
- Durata del carico (breve o lunga) e aderenza delle barre (migliorata o liscia) sono dati del chiamante.

## 5. Notazione, unità e convenzioni

| Simbolo | Significato | Unità | Nel codice |
| --- | --- | --- | --- |
| ε(x, y) | deformazione del piano, compressione negativa | — | `SectionCrackInput.StrainPlane` |
| ∇ε, q | gradiente del piano e suo versore | 1/mm, — | `ChiX`, `ChiY` |
| Q(P) | coordinata di un punto lungo q | mm | — |
| h | altezza della sezione lungo q | mm | traccia `h` |
| h − x | profondità della zona tesa: εmax/\|∇ε\| | mm | `TensileDepth` |
| h − d | distanza dal lembo teso al baricentro delle barre tese | mm | traccia `h − d` |
| hc,eff | altezza dell'area tesa efficace | mm | traccia `hc,eff` |
| Ac,eff, As,eff | area efficace di calcestruzzo e armatura in essa | mm² | `EffectiveArea`, `EffectiveSteel` |
| ρ (ρp,eff) | As,eff/Ac,eff | — | `CrackWidthInput.Rho` |
| Ø, Øeq | diametro, diametro equivalente ΣØ²/ΣØ | mm | `Diameter` |
| c | copriferro alla superficie delle barre longitudinali | mm | `NominalCover`, `CoverOverride`, `Cover` |
| s | interasse massimo delle barre efficaci | mm | `BarSpacing`, `SpacingOverride` |
| σs | tensione della barra efficace più tesa | MPa | `SteelStress` |
| Es, Ecm, αe | moduli di acciaio e calcestruzzo, αe = Es/Ecm | MPa, MPa, — | `Es`, `Ecm` |
| fctm (fct,eff) | resistenza media a trazione del calcestruzzo | MPa | `Fctm`, `Fct` |
| kt | coefficiente della durata del carico | — | traccia |
| k1, k2, k3, k4 | coefficienti di aderenza, di distribuzione delle deformazioni, del copriferro e del diametro | — | traccia |
| εsm − εcm | differenza delle deformazioni medie di acciaio e calcestruzzo | — | traccia |
| sr,max, Δsm | distanza massima (Eurocodice) e media (NTC) tra le fessure | mm | traccia |
| wk (wd) | ampiezza di calcolo delle fessure | mm | `Width` |
| wlim | limite di ampiezza | mm | `Limit` |
| σct,max, σct,lim | tensione massima della sezione non fessurata e suo limite (trazione positiva) | MPa | `UncrackedMaximumStress`, `StressLimit` |

## 6. Formulazione

### 6.1 Requisito

**NTC 2018, UNI EN 1992-1-1 e CNR-DT 200.** Combinazione caratteristica: non richiesta. Frequente e quasi permanente
secondo la Tab. 4.1.IV, con i gruppi della Tab. 4.1.III (ordinarie: X0, XC1, XC2, XC3, XF1; aggressive: XC4, XD1,
XS1, XA1, XA2, XF2, XF3; molto aggressive: XD2, XD3, XS2, XS3, XA3, XF4) e w1 = 0,2, w2 = 0,3, w3 = 0,4 mm:

| Ambiente | Combinazione | Armature sensibili | Armature poco sensibili |
| --- | --- | --- | --- |
| Ordinario | frequente | wk ≤ w2 | wk ≤ w3 |
| Ordinario | quasi permanente | wk ≤ w1 | wk ≤ w2 |
| Aggressivo | frequente | wk ≤ w1 | wk ≤ w2 |
| Aggressivo | quasi permanente | decompressione | wk ≤ w1 |
| Molto aggressivo | frequente | formazione delle fessure | wk ≤ w1 |
| Molto aggressivo | quasi permanente | decompressione | wk ≤ w1 |

Il wlim di progetto non è usato con questi profili.

**Famiglia Eurocodice e Model Code 2010.** Verifica richiesta solo nella combinazione quasi permanente (NS: nella
frequente per XD3 e XS3); un wlim di progetto, se assegnato, sostituisce la tabella. Senza wlim di progetto:

| Profilo | wlim = 0,4 mm | wlim = 0,3 mm | wlim = 0,2 mm | Altre classi |
| --- | --- | --- | --- | --- |
| EN 1992-1-1, DIN | X0, XC1 | XC2, XC3, XC4, XD1, XD2, XD3, XS1, XS2, XS3 | — | wlim di progetto richiesto |
| DS | XC2, XC3, XC4 | XD1, XS1, XS2 | XD2, XD3, XS3 | wlim di progetto richiesto (anche X0, XC1) |
| NS | X0 | XC1-XC4, XD1-XD3, XS1-XS3 | — | wlim di progetto richiesto |
| Model Code 2010 | — | — | — | wlim di progetto sempre richiesto |

### 6.2 Decompressione e formazione delle fessure

```math
\sigma_{ct,max} \le \sigma_{ct,lim}, \qquad \sigma_{ct,lim} = \begin{cases} 0 & \text{decompressione} \\ f_{ctm}/1{,}2 & \text{formazione delle fessure} \end{cases} \qquad \text{(F.1)}
```

con σct,max la tensione massima (trazione positiva) della sezione non fessurata omogeneizzata con il cls teso
(NTC 2018 §4.1.2.2.4).

### 6.3 Classificazione dello stato

Con le deformazioni ai vertici del contorno e dei fori: se εmax ≤ 10⁻¹² la sezione è interamente compressa e
wk = 0; se εmin ≥ 0 è interamente tesa (6.8); altrimenti è parzializzata (6.4).

### 6.4 Sezione parzializzata: zona tesa efficace

```math
q = \frac{\nabla\varepsilon}{|\nabla\varepsilon|}, \qquad Q(P) = q \cdot P, \qquad h = Q_{max} - Q_{min}, \qquad h - x = \frac{\varepsilon_{max}}{|\nabla\varepsilon|} \qquad \text{(F.2)}
```

```math
h - d = Q_{max} - \frac{\sum_{i \in T} Q_i A_i}{\sum_{i \in T} A_i}, \qquad T = \{\text{barre con}\ \varepsilon_i > 0\} \qquad \text{(F.3)}
```

```math
h_{c,eff} = \min\left[2{,}5\,(h - d);\; \frac{h - x}{3};\; \frac{h}{2}\right] \qquad \text{(F.4)}
```

(EN 1992-1-1 7.3.2(3), Fig. 7.1; profili NTC, UNI, EN, NS, Model Code 2010, CNR-DT 200.) Profilo DIN:

```math
h_{c,eff} = \min\left[\beta\,(h - d);\; \frac{h}{2}\right],\quad \beta = \min\left\{5;\; \max\left[2{,}5;\; 2 + 0{,}1\,\frac{h}{h - d}\right]\right\};\quad \text{e}\ \min\left(h_{c,eff};\; \frac{h - x}{3}\right)\ \text{se}\ \frac{h - x}{3} \ge c + 20\ \text{mm} \qquad \text{(F.5)}
```

Profilo DS: hc,eff è l'altezza della fascia tesa, a partire dal lembo, il cui baricentro coincide con quello delle
barre tese (DK NA Fig. 7.100 NA), cercata in [0; min(h; h − x)].

```math
A_{c,eff} = \left|\{P \in \text{sezione}: Q(P) \ge Q_{max} - h_{c,eff}\}\right| - \text{fori}, \qquad A_{s,eff} = \sum_{i \in T,\ Q_i \ge Q_{max} - h_{c,eff}} A_i \qquad \text{(F.6)}
```

```math
\rho = \frac{A_{s,eff}}{A_{c,eff}}, \qquad \varnothing_{eq} = \frac{\sum \varnothing_i^2}{\sum \varnothing_i}, \qquad \sigma_s = \max_i \sigma_{s,i}\ \ \text{(barre efficaci)} \qquad \text{(F.7)}
```

Ac,eff si ottiene ritagliando il poligono del contorno e quelli dei fori con il semipiano Q ≥ Qmax − hc,eff.

### 6.5 Ampiezza delle fessure, famiglia Eurocodice

```math
\varepsilon_{sm} - \varepsilon_{cm} = \max\left\{\frac{\sigma_s - k_t\, \dfrac{f_{ct,eff}}{\rho}\,(1 + \alpha_e\, \rho)}{E_s};\; \beta_{min}\, \frac{\sigma_s}{E_s}\right\}, \qquad \alpha_e = \frac{E_s}{E_{cm}} \qquad \text{(F.8)}
```

```math
s_{r,max} = \begin{cases} k_3\, c + k_1\, k_2\, k_4\, \dfrac{\varnothing_{eq}}{\rho} & s \le 5\,(c + \varnothing_{eq}/2) \\ 1{,}3\,(h - x) & s > 5\,(c + \varnothing_{eq}/2) \end{cases} \qquad \text{(F.9)}
```

```math
w_k = s_{r,max}\, (\varepsilon_{sm} - \varepsilon_{cm}) \qquad \text{(F.10)}
```

(EN 1992-1-1 (7.8), (7.9), (7.11), (7.14).) Coefficienti: kt = 0,6 per carichi di breve durata, 0,4 di lunga durata;
βmin = 0,6; k1 = 0,8 per barre ad aderenza migliorata, 1,6 per barre lisce; k3 = 3,4; k4 = 0,425. Varianti:

- **DS**: k3 = 3,4 (25/c)^(2/3) (DK NA 7.3.4(3)), per c > 0.
- **DIN**: kt = 0,4 sempre; sr,max = min[Ø/(3,6 ρ) oppure 1,3 (h − x) per barre distanziate; σs Ø/(3,6 fct,eff)]
  (DIN NA 7.3.4(3)); solo barre ad aderenza migliorata.
- **Model Code 2010**: sr = 2 [c + Ø/(4 (τbm/fctm) ρ)] con τbm/fctm = 1,8 per carichi di breve durata e 1,35 di lunga
  durata; βmin = 1 − kt; solo barre ad aderenza migliorata; la regola delle barre distanziate non si applica.

### 6.6 Ampiezza delle fessure, NTC 2018 (Circolare 2019)

Con εsm − εcm da (F.8) (βmin = 0,6) e i coefficienti kt, k1 della famiglia Eurocodice:

```math
\Delta_{sm,v} = \frac{3{,}4\, c + k_1\, k_2\, 0{,}425\, \varnothing_{eq}/\rho}{1{,}7}, \qquad \Delta_{sm} = \begin{cases} \Delta_{sm,v} & s \le 5\,(c + \varnothing_{eq}/2) \\ \max\left[\Delta_{sm,v};\; 0{,}75\,(h - x)\right] & s > 5\,(c + \varnothing_{eq}/2) \end{cases} \qquad \text{(F.11)}
```

```math
w_d = \max\left[0;\; 1{,}7\, \Delta_{sm}\, (\varepsilon_{sm} - \varepsilon_{cm})\right] \qquad \text{(F.12)}
```

Con barre vicine (F.12) coincide con (F.10).

> **Scostamento dichiarato — F-1 (D7-a) NTC: barre distanziate**
>
> - Norma: per barre con interasse maggiore di 5 (c + Ø/2), EN 1992-1-1 (7.14) sostituisce sr,max con 1,3 (h − x);
>   nella forma NTC con wd = 1,7 Δsm (εsm − εcm) la stessa regola dà Δsm = 1,3 (h − x)/1,7 = 0,765 (h − x), cioè
>   wd = 1,3 (h − x) (εsm − εcm). Il testo della Circolare 2019 per questo caso è in verifica.
> - Programma: Δsm = max[Δsm,v; 0,75 (h − x)] (F.11), cioè wd = max[sr,max; 1,275 (h − x)] (εsm − εcm). Lo stesso
>   coefficiente 1,7 · 0,75 = 1,275 vale nel limite superiore senza barre aderenti (F.15).
> - Le due componenti:
>   - coefficiente 1,275 invece di 1,3: dove governa il ramo distante wd è minore dell'1,96% (a sfavore);
>   - massimo invece della sostituzione: dove il ramo vicino supera quello distante il programma tiene il ramo
>     vicino, maggiore (a favore).
> - Effetto: nell'esempio C3 wd = 0,3671 mm invece di 0,3743 mm (−1,9%). Su una griglia di 222 912 stati di flessione
>   e tensoflessione (C30/37, σs da 200 a 320 MPa, travi, solette, sezioni a T, semplice armatura) il solo
>   coefficiente 1,3 cambia l'esito in 337 stati (da soddisfatto a non soddisfatto); la regola completa con la
>   sostituzione cambia wd tra −67% e +1,96% e l'esito in 4581 stati (337 peggiorano, 4244 migliorano). Nei casi
>   congelati del motore precedente l'esito non cambia.
> - Stato: in verifica (ricerca normativa separata); decisione dell'utente: "correggi se è sbagliato".

### 6.7 Coefficiente k2

```math
k_2 = \begin{cases} 0{,}5 & \text{sezione parzializzata, famiglia Eurocodice e Model Code 2010} \\ 0{,}5\ \text{se almeno una barra è compressa},\ 1{,}0\ \text{altrimenti} & \text{sezione parzializzata, NTC e CNR-DT 200} \\ \min\left\{1;\; \max\left[0{,}5;\; \dfrac{\varepsilon_{min} + \varepsilon_{max}}{2\,\varepsilon_{max}}\right]\right\} & \text{sezione interamente tesa (tutti)} \end{cases} \qquad \text{(F.13)}
```

con εmin, εmax le deformazioni estreme del contorno (EN 1992-1-1 (7.13)). Una barra con tensione nulla non è
compressa.

> **Scostamento dichiarato — F-2 (D7-b) NTC: k2 con il criterio della barra compressa**
>
> - Norma: k2 = 0,5 per la flessione, 1,0 per la trazione pura, (ε1 + ε2)/(2 ε1) per la tensoflessione (EN 1992-1-1
>   7.3.4(3), (7.13), richiamata dalla Circolare): una sezione con l'asse neutro interno è inflessa.
> - Programma, profili NTC e CNR-DT 200: k2 = 0,5 solo se almeno una barra è compressa; una trave inflessa senza
>   barre compresse (semplice armatura) ha k2 = 1,0. I profili Eurocodice usano 0,5 per ogni sezione parzializzata.
> - Effetto: a favore di sicurezza. Nell'esempio C2 (trave con sole barre tese) wd = 0,3683 mm invece di 0,2538 mm
>   (+45%) e la verifica passa da soddisfatta a non soddisfatta. Sulla griglia di 222 912 stati, nei 37 128 stati
>   parzializzati con k2 = 1, wd con k2 = 0,5 è minore fino al 48% (mediana tra 33% e 40% secondo la famiglia) e
>   l'esito cambia in 9282 stati.
> - Stato: da discutere (decisione dell'utente).

### 6.8 Casi particolari della sezione parzializzata

**Asse neutro nel copriferro.** Se nessuna barra è tesa ma ci sono barre a distanza minore di h/2 dal lembo teso,
l'asse neutro cade tra il lembo e le barre: σs ≤ 0 e

```math
w_k = 0 \qquad \text{(F.14)}
```

Senza barre entro h/2 dal lembo teso la zona tesa non è armata e la verifica non ha esito.

**Barre tese fuori da Ac,eff.** Se le barre tese sono tutte oltre hc,eff (asse neutro vicino alle barre), il metodo
usa il limite superiore di EN 1992-1-1 (7.14) con ρ → 0 in (F.8):

```math
w_k = s_r\, \beta_{min}\, \frac{\sigma_s}{E_s}, \qquad s_r = \begin{cases} 1{,}3\,(h - x) & \text{famiglia Eurocodice, Model Code 2010} \\ 1{,}7 \cdot 0{,}75\,(h - x) & \text{NTC, CNR-DT 200} \\ \min\left[1{,}3\,(h - x);\; \sigma_s \varnothing_{eq}/(3{,}6\, f_{ct,eff})\right] & \text{DIN} \end{cases} \qquad \text{(F.15)}
```

con σs e Øeq delle barre tese.

> **Scostamento dichiarato — F-3 Asse neutro nel copriferro: wk = 0**
>
> - Norma: le formule (7.8)-(7.9) non trattano esplicitamente il caso di barre tutte compresse con una zona tesa di
>   calcestruzzo nel copriferro.
> - Programma: wk = 0, come limite di (7.9) per σs → 0.
> - Effetto: nessuna fessura di calcolo all'altezza delle barre; la fessura del solo copriferro non è valutata.
> - Stato: dichiarato.

> **Scostamento dichiarato — F-4 Barre tese fuori da Ac,eff**
>
> - Norma: EN 1992-1-1 7.3.4(3) usa sr,max = 1,3 (h − x) come limite superiore quando l'interasse è grande o quando
>   nella zona tesa non c'è armatura aderente.
> - Programma: applica il limite anche quando le barre tese esistono ma stanno tutte oltre hc,eff, con
>   εsm − εcm = βmin σs/Es e σs delle barre tese.
> - Effetto: un risultato invece di nessun esito; il valore è un limite superiore.
> - Stato: dichiarato.

### 6.9 Sezione interamente tesa

Per ogni faccia, con versore q uguale a ±x e ±y (rettangoli e poligoni) oppure radiale verso ogni barra e lungo il
gradiente (cerchi):

- barre della faccia: quelle entro il diametro massimo dalla barra più esterna; h − d dal loro baricentro;
- hc,eff = min[2,5 (h − d); h/2] (DIN: (F.5) senza il termine (h − x)/3; DS: fascia baricentrica cercata in
  [0; h/2]);
- barre efficaci: quelle con Q ≥ Qmax − hc,eff e σs > 0; c = minima distanza dal lembo meno Ø/2;
- wk da (F.8)-(F.12) con h − x = h e k2 da (F.13).

L'ampiezza della sezione è la massima delle facce. DS aggiunge il **sistema grossolano** (DK NA 7.3.4(1)):

```math
w_{k,gr} = 0{,}5\; s_{r,max}\, (\varepsilon_{sm} - \varepsilon_{cm}) \quad \text{con}\ A_{c,eff} = A_c,\ A_{s,eff} = \textstyle\sum A_s,\ \sigma_s = \max \sigma_{s,i},\ h - x = \max(b;\, h) \qquad \text{(F.16)}
```

### 6.10 Superfici interne delle sezioni cave

Se una superficie interna è tesa, ogni parete di un foro rettangolare allineato agli assi, oppure l'anello interno di
una sezione circolare cava, è verificato a parte:

```math
h_{c,eff} = \min\left(\gamma\, a_{min};\; \frac{t}{2}\right), \qquad \gamma = \begin{cases} 2 & \text{DS} \\ 2{,}5 & \text{altri profili} \end{cases} \qquad \text{(F.17)}
```

con amin la distanza dalla superficie interna del baricentro della barra tesa più vicina (t se la parete non ha barre
tese) e t lo spessore della parete. Ac,eff è la
fascia tesa della parete, k2 = (max(0; εmin) + εmax)/(2 εmax) limitato a [0,5; 1], h − x = εmax/|∇ε|. Una superficie
interna tesa senza armatura lascia la verifica senza esito. L'ampiezza della sezione è l'inviluppo delle superfici
esterne e interne; se una regione non ha esito e le altre rispettano il limite, il risultato è incompleto.

### 6.11 Interasse delle barre

L'interasse s è il massimo delle distanze tra barre efficaci adiacenti:

- disposizione a file (rettangoli, sezioni a T, sezioni cave rettangolari): barre con la stessa ordinata (file
  orizzontali) o la stessa ascissa (file verticali), a coppie adiacenti, solo se il punto medio è nel calcestruzzo;
- anello (cerchio): arco tra barre adiacenti dello stesso raggio; con barre su più raggi solo se gli anelli
  concentrici sono confermati.

Con meno di due barre efficaci, o se la disposizione non è riconosciuta, l'interasse va assegnato.

### 6.12 Esito

```math
\eta = \frac{w_k}{w_{lim}} \le 1 \qquad \text{oppure} \qquad \sigma_{ct,max} \le \sigma_{ct,lim} \qquad \text{(F.18)}
```

> **Scostamento dichiarato — F-5 NS: fattore kc del limite**
>
> - Norma: secondo una fonte secondaria la Tabella NA.7.1N dà 0,30 kc mm per XC, XD e XS, con
>   kc = cnom/cmin,dur ≤ 1,3.
> - Programma: 0,30 mm senza kc.
> - Effetto: a favore di sicurezza (kc ≥ 1); fino al 23% sul limite.
> - Stato: da riscontrare sul testo dell'annesso norvegese.

> **Scostamento dichiarato — F-6 Model Code 2010: ritiro e minimo di εsm − εcm**
>
> - Norma: fib MC2010 §7.6.4 sottrae anche la deformazione da ritiro del calcestruzzo, wd = 2 ls,max (εsm − εcm − εcs),
>   e definisce i coefficienti per fase di formazione e di fessurazione stabilizzata.
> - Programma: εcs = 0; minimo (1 − kt) σs/Es; τbm/fctm = 1,8 o 1,35 secondo la durata.
> - Effetto: a sfavore di sicurezza dove il ritiro è significativo.
> - Stato: da riscontrare sul testo del Model Code 2010.

> **Scostamento dichiarato — F-7 DIN: coefficiente di hc,ef**
>
> - Norma: DIN EN 1992-1-1/NA Bild 7.1DE dà hc,ef/(h − d) in funzione di h/(h − d).
> - Programma: β = 2,5 per h/(h − d) ≤ 5, lineare fino a 5,0 per h/(h − d) = 30, 5,0 oltre (F.5). Una fonte secondaria
>   riporta la stessa interpolazione; un esempio svolto di un'altra fonte legge invece 3,25 per h/(h − d) = 25, dove
>   il programma dà 4,5.
> - Effetto: se il valore corretto è minore, il programma usa un'area efficace maggiore e, di norma, un'ampiezza
>   maggiore (a favore di sicurezza).
> - Stato: da riscontrare sul testo dell'annesso tedesco.

## 7. Coefficienti e valori predefiniti

| Simbolo | Valore | Profilo | Fonte | Modificabile | Dove nel codice |
| --- | --- | --- | --- | --- | --- |
| w1, w2, w3 | 0,2; 0,3; 0,4 mm | NTC, UNI, CNR-DT 200 | NTC Tab. 4.1.IV | no | `CrackRequirements.Ntc` |
| wlim da tabella | 6.1 | EN, DIN, DS, NS | Prospetti 7.1N, 7.1DE, 7.1 NA, NA.7.1N | sì (wlim di progetto) | `CrackRequirements.For` |
| σct,lim | 0; fctm/1,2 | NTC, UNI, CNR-DT 200 | NTC 4.1.2.2.4 | no | `SectionCrackCheck.Evaluate` |
| kt | 0,6 breve; 0,4 lunga durata (DIN 0,4) | tutti | EN 7.3.4(2) | durata sì | `CrackWidthCalculator.Width` |
| βmin | 0,6; Model Code 2010 1 − kt | tutti | EN (7.9) | no | idem |
| k1 | 0,8 aderenza migliorata; 1,6 liscia | NTC, EN, UNI, DS, NS | EN 7.3.4(3) | aderenza sì | idem |
| k3 | 3,4; DS 3,4 (25/c)^(2/3) | famiglia Eurocodice, NTC | EN 7.3.4(3); DK NA | no | idem |
| k4 | 0,425; DIN 1/3,6 | famiglia Eurocodice, NTC | EN 7.3.4(3); NA DIN | no | idem |
| τbm/fctm | 1,8 breve; 1,35 lunga durata | Model Code 2010 | MC2010 7.6.4 | durata sì | idem |
| Soglia delle barre distanziate | 5 (c + Ø/2) | tutti tranne Model Code 2010 | EN 7.3.4(3) | no (s assegnabile) | idem |
| Coefficiente delle barre distanziate | 1,3 (h − x); NTC 0,75 (h − x) su Δsm | — | EN (7.14); riquadro F-1 | no | idem, `UnbondedUpperBound` |
| Fattore 1,7 | wd = 1,7 Δsm | NTC, CNR-DT 200 | NTC 4.1.2.2.4 | no | `CrackWidthCalculator.Width` |
| hc,eff | (F.4), (F.5), fascia DS | — | EN 7.3.2(3); NA DIN; DK NA | no | `CrackSectionGeometry.EffectiveDepth` |
| γ superfici interne | 2,5; DS 2 | — | 2,5 (h − d) di EN; DS 2 (h − d) per la fascia baricentrica | no | `SectionCrackCheck.Inner` |
| Sistema grossolano DS | 0,5 · (7.8) | DS | DK NA 7.3.4(1) | no | `SectionCrackCheck.FullyTensioned` |
| c | copriferro nominale più staffa | — | — | sì (`CoverOverride`) | `SectionCrackInput` |
| s | automatico (6.11) | — | — | sì (`SpacingOverride`) | `CrackSectionGeometry.MaximumSpacing` |
| Riconoscimento del cerchio | ≥ 16 vertici equidistanti dall'origine entro 10⁻⁶ relativo | — | — | anelli concentrici da confermare | `CrackSectionGeometry.From` |
| Tolleranze | εmax ≤ 10⁻¹² compressa; \|∇ε\| ≤ 10⁻¹⁵ asse neutro indeterminato; 10⁻⁸ mm sulle quote | — | — | no | `SectionCrackCheck.Evaluate` |

## 8. Implementazione

Percorsi relativi alla radice del repository Checker.

- Profili: `CrackProfiles.TryResolve` per tipo esatto (`GPCChecker.Concrete/Cracking/CrackProfiles.cs:16-31`),
  CS-TR34 e CNR-DT 204 (righe 33-45), `Resolve` (righe 47-54), `IsNtc` per NTC e CNR-DT 200 (riga 57), riferimenti
  (righe 59-73).
- Requisito: `CrackRequirements.For` (`GPCChecker.Concrete/Cracking/CrackRequirements.cs:49-89`), elenco delle classi
  nell'ordine dei gruppi NTC (riga 44), Tab. 4.1.IV (righe 91-102).
- Ampiezza: `CrackWidthCalculator.Width` (`GPCChecker.Concrete/Cracking/CrackWidthCalculator.cs:43-92`): ramo NTC
  (righe 51-70), famiglia Eurocodice (righe 71-91) con DS (riga 76), Model Code 2010 (riga 79), DIN (righe 80-85),
  barre distanziate (riga 86). Limite superiore senza barre aderenti `UnbondedUpperBound` (righe 99-114), k2 dalle
  tensioni delle barre `K2` (righe 117-122).
- Geometria: `CrackSectionGeometry.From` (`GPCChecker.Concrete/Cracking/CrackSectionGeometry.cs:63-76`), ritaglio
  del poligono `Clip` (righe 83-94), regione efficace `Region` (righe 99-104), copriferro di una barra `BarCover`
  (righe 107-119), interasse `MaximumSpacing` (righe 124-164), hc,eff `EffectiveDepth` (righe 192-220; DIN righe
  196-201, DS con bisezione di 65 iterazioni righe 202-218).
- Verifica: `SectionCrackCheck.Evaluate` (`GPCChecker.Concrete/Cracking/SectionCrackCheck.cs:132-231`):
  - requisito e casi senza verifica (righe 135-147); decompressione e formazione (righe 149-159);
  - c.a.p. e analisi richiesta (righe 160-161); k2 dalle barre (riga 163);
  - classificazione (righe 164-172); k2 = 0,5 dei profili Eurocodice (riga 173);
  - gradiente, h e h − x (righe 175-181); asse neutro nel copriferro (righe 182-193);
  - h − d, hc,eff, regione efficace (righe 194-203); limite superiore senza barre aderenti (righe 205-215);
  - ampiezza ed esito (righe 216-230).
- Sezione interamente tesa: `FullyTensioned` (righe 236-312), k2 (righe 241-244), facce (righe 246-257 e 262-287),
  sistema grossolano DS (righe 288-303), governante (righe 304-311).
- Superfici interne: `Inner` (righe 315-410): anello (righe 363-372), foro rettangolare (righe 373-394), fori non
  supportati (righe 395-396), inviluppo ed esito incompleto (righe 397-409).
- Dati: `SectionCrackInput` (righe 20-76) con `OrdinaryBarStresses` (righe 69-75); `SectionCrackResult`
  (righe 91-118).

Iterazioni: solo la fascia baricentrica DS (bisezione di 65 passi sull'altezza della fascia, precisione relativa
2⁻⁶⁵). Le aree efficaci si ottengono per ritaglio esatto dei poligoni.

## 9. Limiti e casi non supportati

- Calcestruzzo armato precompresso e calcestruzzi fibrorinforzati (CNR-DT 204).
- Superfici interne di fori non rettangolari o non circolari; più fori nelle sezioni non circolari.
- Armatura minima per il controllo della fessurazione (EN 1992-1-1 7.3.2(2)) e verifica semplificata senza calcolo
  diretto (EN 1992-1-1 7.3.3, Circolare): non implementate.
- Fessure con armature in due direzioni ortogonali inclinate sulle tensioni principali (EN 1992-1-1 7.3.4(4),
  (7.15)): non implementate.
- Deformazioni impresse e ritiro (anche nel Model Code 2010, riquadro F-6).
- DS: sistema grossolano solo per le sezioni interamente tese.
- Barre lisce con Model Code 2010 e DIN.
- Interasse automatico solo per file allineate o anelli; negli altri casi va assegnato.

## 10. Esempio numerico verificato

Sezione 300 × 500 mm (contorno da (−150, −250) a (150, 250)), barre tese 3Ø20 a y = −200 mm (x = −100, 0, 100),
barre superiori 2Ø16 a y = +200 mm (x = ±100). Piano di deformazione con asse neutro a y = 60 mm e
ε = 1,25 · 10⁻³ alle barre tese: ε(y) = −4,8077 · 10⁻⁶ (y − 60). Tensioni delle barre Es ε: σs = 250 MPa nelle
barre tese, −134,6 MPa in quelle superiori. Es = 200 000 MPa, Ecm = 33 000 MPa, fctm = 2,9 MPa, carico di lunga
durata, barre ad aderenza migliorata, c = 40 mm. Combinazione quasi permanente, classe XC3, armature poco
sensibili: wlim = 0,3 mm per NTC 2018 e per EN 1992-1-1. Libreria GPCChecker.Concrete 0.0.15.0,
`SectionCrackCheck.Evaluate`.

**C1. 3Ø20 tese e 2Ø16 compresse, EN 1992-1-1 e NTC 2018.**

1. εmax = 4,8077 · 10⁻⁶ · 310 = 1,4904 · 10⁻³ al lembo inferiore; h = 500 mm; h − x = 310 mm.
2. h − d = 250 − 200 = 50 mm; hc,eff = min(2,5 · 50; 310/3; 500/2) = min(125; 103,33; 250) = 103,33 mm.
3. Ac,eff = 300 · 103,33 = 31 000 mm²; As,eff = 3 · 314,16 = 942,48 mm²; ρ = 0,030403; Øeq = 20 mm.
4. s = 100 mm ≤ 5 (40 + 10) = 250 mm: barre vicine.
5. kt fct/ρ = 0,4 · 2,9/0,030403 = 38,155 MPa; 1 + αe ρ = 1 + 6,0606 · 0,030403 = 1,18426;
   εsm − εcm = (250 − 45,185)/200 000 = 1,02407 · 10⁻³ ≥ 0,6 · 250/200 000 = 7,5 · 10⁻⁴.
6. EN: k2 = 0,5; sr,max = 3,4 · 40 + 0,8 · 0,5 · 0,425 · 20/0,030403 = 136 + 111,83 = 247,83 mm;
   wk = 247,83 · 1,02407 · 10⁻³ = 0,2538 mm; η = 0,846.
7. NTC: le barre superiori sono compresse, k2 = 0,5; Δsm = 247,83/1,7 = 145,78 mm; wd = 1,7 · 145,78 · 1,02407 · 10⁻³ =
   0,2538 mm.

**C2. Sole barre tese 3Ø20** (riquadro F-2). EN: wk = 0,2538 mm come in C1. NTC: nessuna barra compressa, k2 = 1;
sr = 136 + 223,67 = 359,67 mm; wd = 0,3683 mm > 0,3 mm: non soddisfatta.

**C3. 2Ø20 tese a x = ±130 mm (s = 260 mm > 250 mm) e 2Ø16 compresse** (riquadro F-1).

1. As,eff = 628,32 mm², ρ = 0,020268; kt fct/ρ (1 + αe ρ) = 57,232 · 1,12284 = 64,262 MPa;
   εsm − εcm = (250 − 64,262)/200 000 = 9,2869 · 10⁻⁴.
2. EN: sr,max = 1,3 · 310 = 403 mm; wk = 0,3743 mm.
3. NTC: Δsm,v = (136 + 167,75)/1,7 = 178,68 mm; 0,75 · 310 = 232,5 mm; Δsm = 232,5 mm; wd = 1,7 · 232,5 · 9,2869 · 10⁻⁴ =
   0,3671 mm. Con Δsm = 1,3 · 310/1,7 = 237,06 mm si avrebbe 0,3743 mm.

| Caso | Grandezza | Calcolo a mano | Libreria | Scarto |
| --- | --- | --- | --- | --- |
| C1 | h − x; hc,eff; Ac,eff | 310 mm; 103,333 mm; 31 000 mm² | 310; 103,3333333; 31 000 | < 10⁻⁹ |
| C1 | εsm − εcm | 1,024075 · 10⁻³ | 1,02407476 · 10⁻³ | < 10⁻⁹ |
| C1 | sr,max (EN); Δsm (NTC) | 247,8329 mm; 145,7840 mm | 247,8328733; 145,7840431 | < 10⁻⁹ |
| C1 | wk (EN e NTC) | 0,253799 mm | 0,2537993902 mm | < 10⁻⁹ |
| C2 | wd (NTC, k2 = 1) | 0,368325 mm | 0,3683246131 mm | < 10⁻⁹ |
| C3 | wk (EN) | 0,374261 mm | 0,3742612226 mm | < 10⁻⁹ |
| C3 | wd (NTC) | 0,367064 mm | 0,3670638914 mm | < 10⁻⁹ |

Requisiti per la stessa sezione (`CrackRequirements.For`): XC3 frequente, NTC 0,4 mm, EN non richiesta (richiesta
nella quasi permanente); XC3 quasi permanente, NTC e EN 0,3 mm; XD1 quasi permanente, NTC 0,2 mm, EN 0,3 mm.

## 11. Validazione

- **Casi congelati del motore precedente** (`CrackMigrationTests`, 8 test):
  - 936 stati di esercizio su 6 sezioni archiviate (rettangolare, a T, circolare, rettangolare cava, circolare cava
    con due anelli, quadrata con barre di lato), 7 norme, 13 azioni (anche trazione e sezioni interamente tese),
    combinazioni caratteristica, frequente e quasi permanente, con esposizione, sensibilità, durata, aderenza,
    copriferro, interasse e wlim a rotazione. Sono riprodotti con tolleranza 10⁻⁹ 362 ampiezze, 347 esiti,
    1104 regioni (chiave, area, armatura, ampiezza, barre) e 10 errori;
  - differenze volute rispetto al motore precedente, verificate a parte: 22 stati con asse neutro nel copriferro
    (wk = 0, riquadro F-3) e 8 stati con le barre tese fuori da Ac,eff (limite superiore (F.15), riquadro F-4), che
    il motore precedente lasciava senza esito;
  - 1400 ampiezze scalari casuali e l'intera tabella dei requisiti (1596 combinazioni di norma, combinazione,
    esposizione, sensibilità e wlim).
- **Calcoli a mano** negli stessi test:
  - EN 1992-1-1 7.3.4: wk = 0,258 mm, barre distanziate, DS con k3 ridotto, DIN, CNR-DT 200 uguale a NTC;
  - Tab. 4.1.IV NTC e requisiti Eurocodice, Model Code 2010 e NS;
  - asse neutro nel copriferro e barre fuori da Ac,eff (wk = 0,039 mm EN, 0,03825 mm NTC);
  - ritaglio, interassi su file e anelli, hc,eff EN, DIN e DS.
- **Integrazione**: i test del verificatore di modello eseguono la fessurazione per 10 norme e confrontano il
  risultato NTC con la chiamata diretta del metodo.
- **Esempi di questa pagina**: C1-C3 eseguiti con la libreria 0.0.15.0, scarto inferiore a 10⁻⁹.
- **Benchmark indipendenti pubblicati**: nessuno, per ora.

## 12. Bibliografia

- DM 17 gennaio 2018, *Aggiornamento delle Norme tecniche per le costruzioni*, §4.1.2.2.4.
- Circolare 21 gennaio 2019 n. 7 C.S.LL.PP., §C4.1.2.2.4.
- EN 1992-1-1:2004 + AC:2010, §7.3.
- UNI EN 1992-1-1, appendice nazionale: DM 31 luglio 2012, §7.3.
- DIN EN 1992-1-1/NA, §7.3.
- DS/EN 1992-1-1 DK NA:2024, §7.3.
- NS-EN 1992-1-1, *Nasjonalt tillegg*, §7.3.
- fib, *fib Model Code for Concrete Structures 2010*, Ernst & Sohn, 2013, §7.6.4.
- CNR-DT 200 R1/2013, *Istruzioni per la progettazione, l'esecuzione ed il controllo di interventi di
  consolidamento statico mediante l'utilizzo di compositi fibrorinforzati*.
