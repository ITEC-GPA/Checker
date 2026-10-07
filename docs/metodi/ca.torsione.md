---
id: ca.torsione
titolo: Torsione di sezione in calcestruzzo armato e interazione con il taglio
libreria: GPCChecker.Concrete
classi:
  - GPC.Checkers.Concrete.Torsion.SectionTorsionCalculator
  - GPC.Checkers.Concrete.Torsion.SectionTorsionInput
  - GPC.Checkers.Concrete.Torsion.SectionTorsionResult
  - GPC.Checkers.Concrete.Torsion.TorsionGeometry
  - GPC.Checkers.Concrete.Torsion.TorsionThicknessRule
  - GPC.Checkers.Concrete.Torsion.TorsionShearComponent
  - GPC.Checkers.Concrete.Torsion.TorsionProfiles
  - GPC.Checkers.Concrete.Torsion.TorsionProfile
  - GPC.Checkers.Concrete.Torsion.TorsionVerdict
versione: 0.0.17.0
norme:
  - ntc2018-4.1.2.3.6
  - circ2019-C4.1.2.3.6
  - en1992-1-1-6.3.1
  - en1992-1-1-6.3.2
  - en1992-1-1-6.2.2
  - na-uni-2012-6.2.2
  - na-din-6.2.2
  - na-din-6.3.2
  - na-dk-2024-5.6.1
  - na-dk-2024-6.2.3
  - na-dk-2024-6.3.2
  - na-ns-3.1.2
  - mc2010-7.3.4
  - cnr-dt200-r1-2013-4.4
stato: bozza
---

# Torsione di sezione in calcestruzzo armato e interazione con il taglio

## 1. Scopo

Il metodo verifica a torsione una sezione di calcestruzzo armato ordinario con staffe chiuse, con il modello a
traliccio periferico di parete sottile, e ne valuta l'interazione con il taglio delle due direzioni principali
calcolato con la stessa inclinazione delle bielle. Restituisce:

- le resistenze delle bielle TRcd, delle staffe TRsd, dell'armatura longitudinale TRld e TRd = min;
- tre rapporti: torsione |TEd|/TRd, interazione delle bielle, interazione delle staffe; il rapporto della verifica
  è il massimo dei tre;
- l'armatura longitudinale e l'area di un braccio di staffa per unità di lunghezza richieste dalla torsione;
- i risultati del taglio di ciascuna direzione con il cot θ comune (pagina [ca.taglio](ca.taglio.md)).

## 2. Campo di applicazione

| Caso | Stato | Comportamento del programma |
| --- | --- | --- |
| Sezione piena o cava con staffe chiuse a 90° e barre longitudinali distribuite sul profilo, con una barra in ogni spigolo | supportato | calcolo |
| Profilo resistente Ak, uk, tef assegnato dal chiamante | supportato | calcolo |
| Proposta del profilo da rettangolo o cerchio, pieni o con foro centrato (`TorsionGeometry.Rectangle`, `Circle`) | supportato come proposta | valori da confermare dal chiamante |
| Altri contorni (per esempio a T) | non supportato dalla proposta | il profilo va assegnato |
| NTC 2018, EN 1992-1-1, UNI, DIN, DS, NS, Model Code 2010 | supportato | resistenza e interazione delle bielle della norma |
| CNR-DT 200 R1/2013 | supportato senza dati FRP | elemento in c.a. secondo NTC, TRd,f = 0 dichiarato nella traccia |
| DS: regola di DK NA 6.3.2(6) per V, T, N e M combinati | non applicata | avviso in `Limitations` di ogni risultato DS |
| Torsione senza staffe chiuse (area del braccio nulla) | supportato | resistenza nulla: con TEd ≠ 0 esito non soddisfatto senza rapporto |
| Direzione di taglio assegnata senza armatura trasversale, anche con taglio nullo (una direzione senza taglio si omette) | dati non validi | errore (`ArgumentException`) |
| Componenti di taglio date con cot θ diverso da quello della torsione | dati non validi | errore (`ArgumentException`) |
| cot θ fuori dal campo del profilo (anche il campo di ciascuna direzione di taglio) | dati non validi | errore (`ArgumentException`) |
| DIN senza dati di taglio e cot θ > 1 | dati non validi | errore: il campo di cot θ non si può calcolare |
| Staffe inclinate | non supportato | errore (`NotSupportedException`) |
| fck > 90 MPa | non supportato | errore (`ArgumentException`) |
| CNR-DT 204 | non supportato | errore (`NotSupportedException`): servono staffe e la combinazione fibre più staffe non è implementata |
| CS-TR34 | non applicabile | errore (`NotSupportedException`) con il motivo |
| ACI 318, AASHTO; classi derivate da quelle elencate | non supportato | errore (`NotSupportedException`), nessun ripiego |
| Precompressione | non supportato | non considerata |

## 3. Riferimenti normativi

| Formula o grandezza | Norma | Edizione e appendice | Paragrafo | Eq. o tabella | Riscontro |
| --- | --- | --- | --- | --- | --- |
| TRcd, TRsd, TRld (R.2)-(R.5), interazione delle bielle (R.7), θ comune a taglio e torsione, spessore delle sezioni piene e cave | NTC | 2018 | 4.1.2.3.6 | [4.1.35]-[4.1.37], [4.1.39], [4.1.40] | testo |
| Campo 1 ≤ cot θ ≤ 2,5 in torsione; in torsione pura cot θ = (al/as)^(1/2) nel campo | NTC | 2018 | 4.1.2.3.6 | [4.1.38] | testo |
| f'cd = ν fcd con ν = 0,5 | Circolare | 2019 | C4.1.2.3.6 | — | testo |
| Spessore efficace tef = A/u ≥ 2 a; sezioni cave | EN 1992-1-1 | 2004 + AC:2010 | 6.3.2(1) | — | da riscontrare |
| Flusso di taglio, armatura longitudinale | EN 1992-1-1 | 2004 + AC:2010 | 6.3.2(1)-(3) | (6.26), (6.27), (6.28) | da riscontrare |
| Interazione delle bielle, TRd,max | EN 1992-1-1 | 2004 + AC:2010 | 6.3.2(4) | (6.29), (6.30) | da riscontrare |
| ν di (6.30) | EN 1992-1-1 | 2004 + AC:2010 | 6.2.2(6) | (6.6N) | da riscontrare |
| ν = 0,5 fino a C70/85 | UNI EN 1992-1-1 | DM 31/07/2012 | 6.2.2(6) | — | testo |
| ν = 0,525 ν2 (0,75 ν2 per sezioni cave armate sulle due facce); interazione quadratica per sezioni piene; campo di cot θ con VEd,T+V | DIN EN 1992-1-1 | NA | 6.3.2(2), 6.3.2(4) | (6.29) modificata | fonte secondaria (ν), da riscontrare (interazione e VEd,T+V) |
| νv = 0,7 − fck/200 ≥ 0,45 (taglio); νt = 0,7 (0,7 − fck/200) (torsione), νt = νv con pareti armate su entrambe le facce; media pesata con taglio e torsione | DS/EN 1992-1-1 | DK NA:2024 | 5.6.1(3)P, informazione supplementare | (5.103 NA), (5.104 NA) | testo (νt: riquadro R-4) |
| cot θ ≤ 2,0 | DS/EN 1992-1-1 | DK NA:2024 | 6.2.3(2) | (6.7b NA) | testo |
| Regola per V, T, N e M combinati (non applicata) | DS/EN 1992-1-1 | DK NA:2024 | 6.3.2(6) | — | testo |
| fck ≤ 60 MPa nelle formule del taglio | NS-EN 1992-1-1 | NA | 3.1.2(2)P | — | fonte secondaria |
| TRd,max con kc, interazione quadratica per sezioni piene | fib MC2010 | 2013 | 7.3.4 | n. da riscontrare | da riscontrare |
| TRd con contributo FRP | CNR-DT 200 | R1/2013 | 4.4 | — | da riscontrare |

## 4. Ipotesi

- Traliccio spaziale periferico: la torsione è portata da una parete sottile di spessore tef lungo la linea media di
  area Ak e perimetro uk; staffe chiuse a 90° e armatura longitudinale distribuita sul perimetro con una barra in
  ogni spigolo.
- ΣAl è l'armatura longitudinale disponibile per la torsione in aggiunta a quella richiesta dalla flessione
  (NTC 2018; EN 1992-1-1 6.3.2(3)).
- Taglio e torsione usano lo stesso cot θ (NTC 2018 §4.1.2.3.6; EN 1992-1-1 6.3.2(2)). Il valore è assegnato dal
  chiamante; il metodo non lo sceglie.
- Il taglio delle due direzioni (V1, V2) sollecita bielle diverse: nell'interazione delle bielle i loro contributi si
  sommano (vedi riquadro R-1).
- Interazione delle staffe: la torsione impegna ogni braccio della staffa; il taglio di ciascuna direzione impegna i
  bracci paralleli a quella direzione. La somma |T|/TRsd + |Vi|/VRsd,i equivale a sommare l'area richiesta da
  torsione e taglio su un braccio, se la staffa che resiste al taglio è la stessa della torsione e tutti i bracci
  hanno la stessa area. Conta la direzione più sollecitata.
- Nessun contributo del calcestruzzo senza staffe; nessuna precompressione.
- Le resistenze di progetto fcd, fyd e γc sono dati espliciti del chiamante.

## 5. Notazione, unità e convenzioni

| Simbolo | Significato | Unità | Nel codice |
| --- | --- | --- | --- |
| TEd | momento torcente di calcolo (segno conservato; si usa \|TEd\|) | Nmm | `SectionTorsionInput.T` |
| Ak | area racchiusa dalla linea media delle pareti, fori compresi | mm² | `TorsionGeometry.EnclosedArea` |
| uk | perimetro di Ak | mm | `TorsionGeometry.Perimeter` |
| tef | spessore della parete resistente | mm | `TorsionGeometry.Thickness` |
| Ast | area di un braccio della staffa chiusa | mm² | `LinkLegArea` |
| s | passo delle staffe | mm | `Spacing` |
| ΣAl | armatura longitudinale disponibile per la torsione | mm² | `LongitudinalArea` |
| fyd,w, fyd,l | resistenze di progetto di staffe e barre longitudinali | MPa | `LinkFyd`, `LongitudinalFyd` |
| fck, fcd, γc | resistenze del calcestruzzo e coefficiente parziale | MPa, MPa, — | `Fck`, `Fcd`, `GammaC` |
| θ | inclinazione delle bielle, comune a taglio e torsione | ° | `CotTheta` (cot θ) |
| fc,s | resistenza delle bielle usata in TRcd | MPa | `StrutStrength` |
| ν, ν2 | coefficienti di riduzione della resistenza delle bielle | — | traccia |
| kc, εx, ε1 | coefficienti e deformazioni del Model Code 2010 | — | traccia |
| Vi | taglio della direzione i (1, 2) | N | `TorsionShearComponent.Demand` |
| VRcd,i, VRsd,i | resistenze delle bielle e delle staffe a taglio nella direzione i, con lo stesso cot θ | N | `VRcd`, `VRsd` |
| TRcd, TRsd, TRld, TRd | resistenze a torsione di bielle, staffe, barre longitudinali e di progetto | Nmm | `TRcd`, `TRsd`, `TRld`, `TRd` |
| ηT, ηc, ηs | rapporti di torsione, di interazione delle bielle e delle staffe | — | `TorsionRatio`, `ConcreteInteraction`, `LinkInteraction` |
| a | distanza dal bordo al baricentro delle barre longitudinali (proposta del profilo) | mm | argomento `axisDistance` |

Il segno di TEd e di Vi non influisce sulle resistenze.

## 6. Formulazione

### 6.1 Profilo resistente proposto dal contorno

Per un rettangolo b × h (foro rettangolare centrato bi × hi) o un cerchio di diametro D (foro concentrico Di):

```math
t_{ef} = \begin{cases} \max\left(A_c / u;\; 2a\right) & \text{sezione piena} \\ \min(b - b_i,\, h - h_i)/2 \ \text{oppure}\ (D - D_i)/2 & \text{sezione cava, regola NTC (spessore reale)} \\ \min\left[\max\left(A / u;\; 2a\right);\; t_{parete}\right] & \text{sezione cava, regola EN 1992-1-1 6.3.2(1)} \end{cases} \qquad \text{(R.1)}
```

con Ac area di calcestruzzo, u perimetro esterno e A area racchiusa dal contorno esterno, fori compresi. Poi
Ak = (b − tef)(h − tef) e uk = 2(b + h − 2 tef) per il rettangolo, Ak = π (D − tef)²/4 e uk = π (D − tef) per il
cerchio. La proposta è rifiutata se tef < 2a o tef ≥ min(b, h).

### 6.2 Resistenze

```math
T_{Rcd} = 2\, A_k\, t_{ef}\, f_{c,s}\, \frac{\cot\theta}{1 + \cot^2\theta} \qquad \text{(R.2)}
```

```math
T_{Rsd} = 2\, A_k\, \frac{A_{st}}{s}\, f_{yd,w} \cot\theta \qquad \text{(R.3)}
```

```math
T_{Rld} = 2\, A_k\, \frac{\Sigma A_l}{u_k}\, \frac{f_{yd,l}}{\cot\theta} \qquad \text{(R.4)}
```

```math
T_{Rd} = \min\left(T_{Rcd};\; T_{Rsd};\; T_{Rld}\right), \qquad \eta_T = \frac{|T_{Ed}|}{T_{Rd}} \qquad \text{(R.5)}
```

Fonti: NTC 2018 §4.1.2.3.6, [4.1.35]-[4.1.37] e [4.1.39]; EN 1992-1-1 (6.28) e (6.30), con
cot θ/(1 + cot²θ) = sin θ cos θ. La resistenza delle bielle fc,s dipende dal profilo:

| Profilo | fc,s |
| --- | --- |
| NTC 2018, CNR-DT 200 | f'cd = ν fcd con ν = 0,5 (Circolare 2019 C4.1.2.3.6) |
| EN 1992-1-1, NS | ν fcd, ν = 0,6 (1 − fck/250), αcw = 1; NS con fck ≤ 60 MPa e fcd ridotto in proporzione |
| UNI | ν fcd, ν = 0,5 per fck ≤ 70 MPa, altrimenti come EN |
| DS | ν fcd, ν = max(0,45; 0,7 − fck/200), cioè νv del taglio anche in torsione (riquadro R-4) |
| DIN | ν fcd, ν = 0,525 ν2 (0,75 ν2 per sezioni cave armate sulle due facce delle pareti), ν2 = min(1; 1,1 − fck/500) |
| Model Code 2010 | kc fck/γc, con kc da (R.6) |

```math
\varepsilon_1 = \varepsilon_x + (\varepsilon_x + 0{,}002)\cot^2\theta, \qquad k_c = \min\left(0{,}65;\; \frac{1}{1{,}2 + 55\,\varepsilon_1}\right) \min\left[1;\; \left(\frac{30}{f_{ck}}\right)^{1/3}\right] \qquad \text{(R.6)}
```

con εx la maggiore delle deformazioni longitudinali del taglio delle due direzioni (pagina
[ca.taglio](ca.taglio.md), (T.6)); senza dati di taglio εx = 0.

### 6.3 Interazione con il taglio

Interazione delle bielle, lineare:

```math
\eta_c = \frac{|T_{Ed}|}{T_{Rcd}} + \frac{|V_1|}{V_{Rcd,1}} + \frac{|V_2|}{V_{Rcd,2}} \le 1 \qquad \text{(R.7)}
```

oppure quadratica per DIN e Model Code 2010 con sezione piena (non cava armata sulle due facce):

```math
\eta_c = \sqrt{\left(\frac{|T_{Ed}|}{T_{Rcd}}\right)^2 + \left(\frac{|V_1|}{V_{Rcd,1}} + \frac{|V_2|}{V_{Rcd,2}}\right)^2} \le 1 \qquad \text{(R.8)}
```

Interazione delle staffe:

```math
\eta_s = \frac{|T_{Ed}|}{T_{Rsd}} + \max_i \frac{|V_i|}{V_{Rsd,i}} \le 1 \qquad \text{(R.9)}
```

Rapporto ed esito:

```math
\eta = \max\left(\eta_T;\; \eta_c;\; \eta_s\right), \qquad \text{soddisfatta se}\ \eta_T \le 1,\ \eta_c \le 1,\ \eta_s \le 1 \qquad \text{(R.10)}
```

VRcd,i e VRsd,i sono le resistenze di bielle e armatura trasversale a taglio nella direzione i, calcolate con il
cot θ della torsione (pagina [ca.taglio](ca.taglio.md), (T.11)-(T.12)). Una resistenza nulla rende il rapporto non
definito: esito non soddisfatto, rapporto assente. Un'azione inferiore a 10⁻⁶ Nmm (torsione) o 10⁻⁹ N (taglio) dà
rapporto nullo.

Fonti: NTC 2018 §4.1.2.3.6, [4.1.40] (interazione delle bielle lineare); EN 1992-1-1 (6.29); DIN EN 1992-1-1/NA
6.3.2(4) e fib MC2010 §7.3.4 per la forma quadratica.

### 6.4 Armature richieste

```math
\Sigma A_{l,req} = \frac{|T_{Ed}|\, u_k \cot\theta}{2\, A_k\, f_{yd,l}}, \qquad \left(\frac{A_{st}}{s}\right)_{req} = \frac{|T_{Ed}|}{2\, A_k\, f_{yd,w} \cot\theta} \qquad \text{(R.11)}
```

(EN 1992-1-1 (6.28); la seconda è l'area di un braccio per unità di lunghezza.)

### 6.5 Campo di cot θ

Il cot θ assegnato deve stare in [1; cmax] con cmax = 2,5 (NTC 2018 [4.1.38]; EN, UNI, NS con il campo di 6.2.3(2)
richiamato da 6.3.2(2); CNR-DT 200), 2 (DS), 3 (DIN), cot 20° = 2,747 (Model Code 2010); inoltre il taglio di ogni
direzione lo controlla nel proprio campo (pagina [ca.taglio](ca.taglio.md), 6.4). Per DIN il campo di ciascuna direzione si calcola con il taglio aumentato del flusso
di torsione sulla larghezza bw:

```math
V_{Ed,T+V} = |V_{Ed}| + \frac{|T_{Ed}|\, z\, b_w}{2\, A_k\, t_{ef}} \qquad \text{(R.12)}
```

usato in (T.18) al posto di |VEd|; le resistenze si calcolano poi con |VEd|. Senza dati di taglio il profilo DIN
accetta solo cot θ = 1.

> **Scostamento dichiarato — R-1 (R7) Interazione delle bielle con il taglio nelle due direzioni**
>
> - Norma: NTC 2018 [4.1.40] e EN 1992-1-1 (6.29) scrivono l'interazione con un solo taglio VEd.
> - Programma: somma i rapporti del taglio delle due direzioni (R.7), (R.8).
> - Effetto: a favore di sicurezza; coincide con la norma quando una delle due direzioni ha taglio nullo.
> - Stato: dichiarato (estensione del modello; voce R7 del registro delle differenze).

> **Scostamento dichiarato — R-2 DS: regola di DK NA 6.3.2(6) non applicata**
>
> - Norma: DK NA 6.3.2(6) limita le regole 6.3.2(4)-(5) alla compressione del calcestruzzo per V e T combinati; per
>   V, T, N e M combinati chiede Σ SEd/SRd ≤ 1 con le resistenze delle singole azioni da sole, oppure la sezione
>   efficace con il metodo dell'Annesso F.
> - Programma: controlla solo l'interazione delle bielle (6.29) e l'armatura del traliccio; ogni risultato DS porta
>   l'avviso in `Limitations`.
> - Effetto: verifica incompleta per DS con N e M concomitanti; segno non determinabile in generale.
> - Stato: dichiarato.

> **Scostamento dichiarato — R-3 DIN: altezza delle pareti nel flusso di torsione**
>
> - Norma: DIN EN 1992-1-1/NA 6.3.2(2) calcola il taglio di parete VEd,T = τt,i tef zi con zi altezza della parete
>   e lo combina con il taglio per il campo di cot θ.
> - Programma: zi = z della direzione di taglio (R.12).
> - Effetto: approssimazione; con zi > z il campo calcolato è più ampio di quello della norma (a sfavore), con zi < z
>   più stretto (a favore).
> - Stato: dichiarato.

> **Scostamento dichiarato — R-4 (R16) DS: fattore di efficienza delle bielle in torsione**
>
> - Norma: DK NA 5.6.1(3)P, informazione supplementare, distingue il fattore di efficienza del taglio,
>   νv = 0,7 − fck/200 ≥ 0,45 (5.103 NA), da quello della torsione, νt = 0,7 (0,7 − fck/200) (5.104 NA); νt si
>   può porre uguale a νv solo se le pareti del profilo resistente sono armate con staffe chiuse sul perimetro e
>   barre longitudinali distribuite su entrambe le facce. Con taglio e torsione insieme si usa la media di νv e νt
>   pesata sulle due azioni.
> - Programma: νv in ogni caso, anche per le sezioni piene armate sulla sola faccia esterna; l'indicatore
>   `HollowWithReinforcementOnBothFaces` non è usato dal profilo DS.
> - Effetto: a sfavore di sicurezza. TRcd e la parte di torsione dell'interazione delle bielle valgono 1/0,7 = 1,43
>   volte quelli della norma (1,61 volte con fck = 60 MPa, dove νv è limitato a 0,45 e νt no). Con il profilo
>   dell'esempio (DS, fcd = 30/1,45 MPa, cot θ = 1,5): TRcd = 83,11 kNm con νv = 0,55 invece di 58,17 kNm con
>   νt = 0,385.
> - Stato: da discutere (voce R16 del registro delle differenze, a sfavore di sicurezza).

Il campo di cot θ del profilo Model Code 2010 non dipende da εx: vale il riquadro T-1 della pagina
[ca.taglio](ca.taglio.md).

## 7. Coefficienti e valori predefiniti

| Simbolo | Valore | Profilo | Fonte | Modificabile | Dove nel codice |
| --- | --- | --- | --- | --- | --- |
| f'cd | 0,5 fcd | NTC, CNR-DT 200 | NTC [4.1.35]; Circolare C4.1.2.3.6 | no | `SectionTorsionCalculator.Evaluate` |
| ν | 0,6 (1 − fck/250) | EN, NS (fck ≤ 60) | EN (6.6N) | no | `SectionShearCalculator.StrutEfficiency` |
| ν | 0,5 (fck ≤ 70) | UNI | DM 2012 6.2.2(6) | no | idem |
| ν | max(0,45; 0,7 − fck/200) | DS | DK NA (5.103 NA), νv del taglio; per la torsione vedi riquadro R-4 | no | idem |
| ν | 0,525 ν2; 0,75 ν2 sezioni cave armate sulle due facce | DIN | NA DIN 6.3.2 | no (indicatore della sezione cava sì) | `SectionTorsionCalculator.Evaluate` |
| ν2 | min(1; 1,1 − fck/500) | DIN | NA DIN 6.2.3(3) | no | idem |
| kc,max, ηfc | 0,65; min[1; (30/fck)^(1/3)] | Model Code 2010 | MC2010 7.3.3.3 | no | idem |
| αcw | 1 | famiglia Eurocodice | EN 6.2.3(3) | no | implicito in fc,s |
| cmax | 2,5; DS 2; DIN 3; MC2010 cot 20° | vedi 6.5 | vedi tabella 3 | no | `TorsionProfiles.MaximumCotTheta` |
| cmin | 1 | tutti | NTC [4.1.38]; EN 6.2.3(2) richiamato da 6.3.2(2) | no | `SectionTorsionCalculator.Validate` |
| Inclinazione delle staffe | 90° | tutti | — | no (altri valori: errore) | `SectionTorsionInput` |
| Sezione cava armata sulle due facce | no | DIN, Model Code 2010 | — | sì | `SectionTorsionInput` |
| Regola dello spessore delle sezioni cave | spessore reale (NTC) | proposta del profilo | NTC 4.1.2.3.6 / EN 6.3.2(1) | sì (`TorsionThicknessRule`) | `TorsionGeometry.Rectangle`, `.Circle` |
| Soglie di azione nulla | 10⁻⁶ Nmm (T), 10⁻⁹ N (V) | tutti | — | no | `SectionTorsionCalculator.Evaluate` |
| Tolleranza sul cot θ comune | 10⁻⁸ | tutti | — | no | idem |
| TRd,f | 0 | CNR-DT 200 | assenza di dati FRP | no | idem |

## 8. Implementazione

Percorsi relativi alla radice del repository Checker.

- Profili: `TorsionProfiles.TryResolve` per tipo esatto (`GPCChecker.Concrete/Torsion/TorsionProfiles.cs:17-32`),
  CS-TR34 non applicabile e CNR-DT 204 non supportata (righe 35-48), `Resolve` (righe 50-57), profilo di taglio
  corrispondente (righe 60-74), cmax (righe 77-86), riferimenti (righe 89-114).
- Dati: `TorsionGeometry` con la proposta del profilo (`GPCChecker.Concrete/Torsion/SectionTorsionContracts.cs:22-67`,
  regola dello spessore alle righe 56-61), `TorsionShearComponent` (righe 73-91), `SectionTorsionInput`
  (righe 98-130), `SectionTorsionResult` (righe 139-170).
- `SectionTorsionCalculator.Calculate` (`GPCChecker.Concrete/Torsion/SectionTorsionCalculator.cs:23-51`):
  - senza staffe passa direttamente a `Evaluate` senza taglio (riga 28);
  - per ogni direzione controlla norma e staffe (righe 33-34), per DIN calcola VEd,T+V (R.12) e controlla il campo
    di cot θ (righe 36-42), poi ricalcola il taglio con |V| e il cot θ comune (righe 42-44);
  - `Evaluate` con i due componenti (riga 47).
- `SectionTorsionCalculator.Evaluate` (righe 54-143):
  - riduzione NS a C60 (righe 64-65), scelta dell'interazione quadratica (righe 66-67), DIN senza taglio (righe
    68-69);
  - fc,s per profilo (righe 71-95);
  - resistenze (R.2)-(R.5) (righe 96-99);
  - rapporti con le soglie di azione nulla (righe 103-105), controllo del cot θ comune (righe 106-108);
  - interazioni (R.7)-(R.9) e armature richieste (R.11) (righe 110-116);
  - traccia e avviso DS (righe 118-127); esito e rapporto (R.10) (righe 129-141).
- `Validate` (righe 145-156): dati positivi, fck ≤ 90, campo di cot θ, staffe a 90°.
- Il taglio di ogni direzione usa `SectionShearCalculator` con `SectionShearInput.With`
  (`GPCChecker.Concrete/Shear/SectionShearContracts.cs:67-68`); ν dei profili Eurocodice viene da
  `SectionShearCalculator.StrutEfficiency` (`GPCChecker.Concrete/Shear/SectionShearCalculator.cs:214-219`).

Il metodo non ha iterazioni: cot θ è un dato.

## 9. Limiti e casi non supportati

- Staffe inclinate, precompressione, sezioni composte.
- Scelta automatica del cot θ comune; in particolare l'inclinazione cot θ = √(al/as) della torsione pura di NTC.
- Contributo del calcestruzzo senza staffe e semplificazione di EN 1992-1-1 6.3.2(5) (sola armatura minima se
  TEd/TRd,c + VEd/VRd,c ≤ 1): non usati; la verifica richiede sempre staffe chiuse.
- Torsione di congruenza e ridistribuzione: il metodo verifica la torsione assegnata.
- Sezioni composte da più rettangoli (EN 1992-1-1 6.3.1(3)): il profilo resistente va assegnato come un'unica parete.
- DS: regola di DK NA 6.3.2(6) (riquadro R-2) e fattore νt della torsione (riquadro R-4).
- CNR-DT 204 (fibre più staffe) e contributo FRP di CNR-DT 200.
- Norme americane.
- Testo dei riferimenti (`TorsionProfiles.Reference`): per NTC 2018 il risultato cita le equazioni «4.1.27-4.1.32», che
  in NTC 2018 sono del taglio (la torsione è in [4.1.34]-[4.1.40]); per DS cita «θ 6.7a NA» mentre il limite
  applicato è cot θ ≤ 2 di (6.7b NA). Il valore calcolato non cambia (voce R19 del registro delle differenze).

## 10. Esempio numerico verificato

Trave 300 × 500 mm, distanza bordo-baricentro delle barre a = 48 mm, C30/37 (fck = 30 MPa, γc = 1,5),
B450C (fyd = 391,30 MPa), staffe chiuse Ø8/150 (Ast = 50,27 mm², due bracci nel taglio: Asw = 100,53 mm²),
ΣAl = 4Ø16 = 804,25 mm², cot θ = 1,5. Azioni: TEd = 20 kNm, V2 = 80 kN (bw = 300 mm, d = 460 mm, z = 414 mm,
N = 0), V1 = 0. Libreria GPCChecker.Concrete 0.0.17.0, `SectionTorsionCalculator.Calculate`.

**Profilo resistente** (`TorsionGeometry.Rectangle`): Ac/u = 150 000/1600 = 93,75 mm < 2a = 96 mm, quindi
tef = 96 mm; Ak = 204 · 404 = 82 416 mm²; uk = 2 (204 + 404) = 1216 mm.

**T1. NTC 2018** (fcd = 17,0 MPa, f'cd = 8,5 MPa).

1. cot θ/(1 + cot²θ) = 1,5/3,25 = 0,46154.
2. TRcd = 2 · 82 416 · 96 · 8,5 · 0,46154 = 62 078 267 Nmm = 62,08 kNm.
3. TRsd = 2 · 82 416 · (50,265/150) · 391,30 · 1,5 = 32 420 974 Nmm = 32,42 kNm.
4. TRld = 2 · 82 416 · (804,25/1216) · 391,30/1,5 = 28 439 451 Nmm = 28,44 kNm.
5. TRd = 28,44 kNm (governa l'armatura longitudinale); ηT = 20/28,44 = 0,7032.
6. Taglio V2 con cot θ = 1,5: VRsd = 414 · (100,53/150) · 391,30 · 1,5 = 162 860,2 N;
   VRcd = 414 · 300 · 1 · 8,5 · 0,46154 = 487 246,2 N.
7. ηc = 20/62,078 + 80/487,246 = 0,3222 + 0,1642 = 0,4864.
8. ηs = 20/32,421 + 80/162,860 = 0,6169 + 0,4912 = 1,1081: interazione delle staffe non soddisfatta.
9. η = max(0,7032; 0,4864; 1,1081) = 1,1081: verifica non soddisfatta.
10. ΣAl,req = 20 · 10⁶ · 1216 · 1,5/(2 · 82 416 · 391,30) = 565,59 mm²;
    (Ast/s)req = 20 · 10⁶/(2 · 82 416 · 391,30 · 1,5) = 0,20672 mm²/mm.

**T2. EN 1992-1-1**, stessi dati con fcd = 20 MPa: ν = 0,528, fc,s = 10,56 MPa,
TRcd = 2 · 82 416 · 96 · 10,56 · 0,46154 = 77 123 118 Nmm; VRcd = 414 · 300 · 0,528 · 20 · 0,46154 = 605 331,7 N;
ηc = 20/77,123 + 80/605,332 = 0,3915. TRsd, TRld e ηs sono quelli di T1.

| Caso | Grandezza | Calcolo a mano | Libreria | Scarto |
| --- | --- | --- | --- | --- |
| T1 | Ak; uk; tef | 82 416 mm²; 1216 mm; 96 mm | 82 416; 1216; 96 | 0 |
| T1 | TRcd | 62 078 267 Nmm | 62 078 267,08 Nmm | < 10⁻⁹ |
| T1 | TRsd | 32 420 974 Nmm | 32 420 973,93 Nmm | < 10⁻⁹ |
| T1 | TRld | 28 439 451 Nmm | 28 439 450,82 Nmm | < 10⁻⁹ |
| T1 | ηc; ηs | 0,48636; 1,10810 | 0,4863620002; 1,108103575 | < 10⁻⁹ |
| T1 | ΣAl,req; (Ast/s)req | 565,586 mm²; 0,206720 mm²/mm | 565,5859704; 0,2067200184 | < 10⁻⁹ |
| T2 | TRcd; ηc | 77 123 118 Nmm; 0,39148 | 77 123 117,69 Nmm; 0,3914845646 | < 10⁻⁹ |

Con cot θ = 0,8 il profilo NTC rifiuta i dati (`ArgumentException`: cot θ fuori da [1; 2,5]), come prescrive NTC 2018
[4.1.38] anche in torsione pura.

## 11. Validazione

- **Casi congelati del motore precedente (NTC 2018)**: 986 casi, con griglia di 576 combinazioni (3 profili, segno di T,
  staffe, passo, ΣAl, cot θ, taglio concomitante), 10 casi limite e 400 casi casuali; 936 con risultato,
  49 dati rifiutati e 1 caso senza staffe. Sono confrontati resistenze, rapporti, ΣAl richiesta ed esito con
  tolleranza relativa 10⁻⁹. Il caso senza staffe è una differenza voluta: il motore precedente dava un errore di
  dati, il metodo dà resistenza nulla ed esito non soddisfatto (test `TorsionMigrationTests.LegacyFixturesAreReproduced`).
- **Profilo resistente**: 10 contorni congelati (rettangoli e cerchi pieni e cavi, sezione a T rifiutata) e la regola
  EN 6.3.2(1) per un cassone 1200 × 1000 con pareti di 300 mm (tef = 272,7 mm) (`LegacyGeometryIsReproduced`).
- **Calcoli a mano** nei test `TorsionMigrationTests` (8 test):
  - EN 1992-1-1: TRcd = 77,12 kNm, TRsd = 36,03 kNm, TRld = 31,59 kNm;
  - NTC e UNI 73,03 kNm, DS 80,34 kNm, NS con fck = 90 ridotto a C60;
  - DIN con 0,525 ν2 e 0,75 ν2, interazione quadratica e lineare; Model Code 2010 con kc = 0,65 (94,94 kNm) e
    riduzione con εx;
  - taglio delle due direzioni ricalcolato con lo stesso cot θ e uguale alla chiamata diretta;
  - campo DIN con VEd,T+V; resistenza nulla senza staffe; limiti di cot θ, classe e staffe inclinate; profili per
    tipo esatto; avviso DS.
- **Integrazione**: i test del verificatore di modello eseguono la torsione per 10 norme e confrontano il risultato
  NTC con la chiamata diretta del metodo.
- **Esempi di questa pagina**: T1 e T2 eseguiti con la libreria 0.0.17.0 e ricalcolati in modo indipendente dalle
  formule della norma, scarto inferiore a 10⁻⁹.
- **Benchmark indipendenti pubblicati**: nessuno, per ora.

## 12. Bibliografia

- DM 17 gennaio 2018, *Aggiornamento delle Norme tecniche per le costruzioni*, §4.1.2.3.6.
- Circolare 21 gennaio 2019 n. 7 C.S.LL.PP., §C4.1.2.3.6.
- EN 1992-1-1:2004 + AC:2010, §6.3.
- UNI EN 1992-1-1, appendice nazionale: DM 31 luglio 2012.
- DIN EN 1992-1-1/NA, *Nationaler Anhang — Eurocode 2*.
- DS/EN 1992-1-1 DK NA:2024.
- NS-EN 1992-1-1, *Nasjonalt tillegg*.
- fib, *fib Model Code for Concrete Structures 2010*, Ernst & Sohn, 2013, §7.3.4.
- CNR-DT 200 R1/2013, §4.4.
