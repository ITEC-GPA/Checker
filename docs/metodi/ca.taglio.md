---
id: ca.taglio
titolo: Taglio di sezione in calcestruzzo armato, con e senza armatura trasversale
libreria: GPCChecker.Concrete
classi:
  - GPC.Checkers.Concrete.Shear.SectionShearCalculator
  - GPC.Checkers.Concrete.Shear.SectionShearInput
  - GPC.Checkers.Concrete.Shear.SectionShearResult
  - GPC.Checkers.Concrete.Shear.ShearCalculationDetail
  - GPC.Checkers.Concrete.Shear.ShearProfiles
  - GPC.Checkers.Concrete.Shear.ShearProfile
  - GPC.Checkers.Concrete.Shear.ShearVerdict
versione: 0.0.15.0
norme:
  - ntc2018-4.1.2.3.5.1
  - ntc2018-4.1.2.3.5.2
  - en1992-1-1-6.2.1
  - en1992-1-1-6.2.2
  - en1992-1-1-6.2.3
  - na-uni-2012-6.2.2
  - na-uni-2012-6.2.3
  - na-din-6.2.2
  - na-din-6.2.3
  - na-dk-2024-5.6.1
  - na-dk-2024-6.2.2
  - na-dk-2024-6.2.3
  - na-ns-3.1.2
  - na-ns-6.2.2
  - na-ns-6.2.3
  - mc2010-7.3.3
  - cnr-dt204-2006-4.2.3
  - cnr-dt200-r1-2013-4.3.3
stato: bozza
---

# Taglio di sezione in calcestruzzo armato

## 1. Scopo

Il metodo verifica a taglio una sezione di calcestruzzo armato ordinario in una direzione. Calcola:

- la resistenza degli elementi senza armatura trasversale (formula empirica con il minimo vmin, oppure il modello
  del Model Code 2010 di livello II, oppure la formula dei calcestruzzi fibrorinforzati);
- la resistenza a traliccio degli elementi con staffe o ferri piegati: armatura trasversale VRd,s, bielle compresse
  VRd,max e il minimo dei due.

Restituisce VRd, il rapporto |VEd|/VRd, il cot θ adottato, la deformazione εx (solo Model Code 2010), l'esito e la
traccia dei valori intermedi. La norma si sceglie dal tipo esatto della classe normativa passata dal chiamante.

## 2. Campo di applicazione

| Caso | Stato | Comportamento del programma |
| --- | --- | --- |
| Sezione in c.a. ordinario, taglio in una direzione, bw, d, Asl e z/d assegnati dal chiamante | supportato | calcolo |
| NTC 2018, EN 1992-1-1 (valori raccomandati), UNI, DIN, DS, NS EN 1992-1-1, Model Code 2010 livello II | supportato | calcolo con il profilo della norma |
| CNR-DT 200 R1/2013 | supportato senza dati FRP | resistenza dell'elemento in c.a. secondo NTC, contributo FRP nullo dichiarato nella traccia |
| CNR-DT 204/2006 senza armatura trasversale | supportato | formula dei calcestruzzi fibrorinforzati con fFtuk e fctk assegnati |
| CNR-DT 204/2006 con armatura trasversale | non supportato | errore (`NotSupportedException`) |
| CNR-DT 204/2006 senza fFtuk ≥ 0 o senza fctk > 0 | dati non validi | errore (`ArgumentException`) |
| NTC 2018 e CNR-DT 200, elemento senza armatura trasversale con trazione assiale (N > 0) | non valutato | esito incompleto (`NotEvaluated`), nessun rapporto |
| Resistenza nulla con domanda non nulla (per esempio αc = 0 con σcp ≥ fcd) | supportato | esito non soddisfatto senza rapporto |
| CS-TR34 | non applicabile | errore (`NotSupportedException`) con il motivo |
| ACI 318, AASHTO | non supportato | errore (`NotSupportedException`): implementazione futura |
| Classe normativa derivata da una di quelle elencate | non supportato | errore: nessun ripiego sulla classe base |
| fck > 90 MPa | non supportato | errore (`ArgumentException`) |
| Dati geometrici o di materiale non positivi o non finiti; α fuori da [45°; 90°]; z/d fuori da (0; 0,9] | dati non validi | errore (`ArgumentException`) |
| cot θ assegnato fuori dal campo del profilo | dati non validi | errore (`ArgumentException`) |
| Model Code 2010 con Asl = 0 | dati non validi | errore (`ArgumentException`) |
| DIN con trazione tale che cot θmax < 1 | dati non validi | errore (`ArgumentException`) |
| Precompressione, riduzione dei carichi vicini agli appoggi, sezioni composte acciaio-calcestruzzo | non supportato | non considerati: il metodo non ha i dati per trattarli |
| Verifica dell'armatura longitudinale (traslazione del diagramma) e dettagli costruttivi | fuori dal metodo | lo stato "resistenza sufficiente" ricorda che i dettagli sono da verificare a parte |

## 3. Riferimenti normativi

| Formula o grandezza | Norma | Edizione e appendice | Paragrafo | Eq. o tabella | Riscontro |
| --- | --- | --- | --- | --- | --- |
| VRd senza armatura trasversale (T.4), k, ρl, vmin, σcp ≤ 0,2 fcd | NTC | 2018 | 4.1.2.3.5.1 | n. da riscontrare | testo NTC 2008 |
| VRd,s, VRd,max (T.11)-(T.13), αc (T.14), ν = 0,5, 1 ≤ cot θ ≤ 2,5 | NTC | 2018 | 4.1.2.3.5.2 | n. da riscontrare | testo NTC 2008 |
| cot θ di uguale resistenza (T.15) | NTC, Circolare | 2018, 2019 | 4.1.2.3.5.2, C4.1.2.3.5.2 | — | da riscontrare |
| VRd,c (T.4), k, ρl, vmin, k1, CRd,c | EN 1992-1-1 | 2004 + AC:2010 | 6.2.2(1) | (6.2.a), (6.2.b), (6.3N) | da riscontrare |
| VRd,s, VRd,max, campo di cot θ, αcw, ν1 | EN 1992-1-1 | 2004 + AC:2010 | 6.2.3(2), 6.2.3(3), 6.2.3(4) | (6.7N), (6.13), (6.14) | da riscontrare |
| ν = 0,6 (1 − fck/250) | EN 1992-1-1 | 2004 + AC:2010 | 6.2.2(6) | (6.6N) | da riscontrare |
| CRd,c, k1, vmin; ν = 0,5 fino a C70/85; 1 ≤ cot θ ≤ 2,5 | UNI EN 1992-1-1 | DM 31/07/2012 | 6.2.2(1), 6.2.2(6), 6.2.3(2) | — | testo |
| CRd,c = 0,15/γc, k1 = 0,12, vmin con κ1 dipendente da d | DIN EN 1992-1-1 | NA | 6.2.2(1) | — | fonte secondaria |
| ν1 = 0,75 ν2; VRd,cc e cot θmax | DIN EN 1992-1-1 | NA | 6.2.3(2), 6.2.3(3) | (6.7aDE), (6.7bDE) | fonte secondaria |
| vmin = 0,051/γc k^1,5 fck^0,5 | DS/EN 1992-1-1 | DK NA:2024 | 6.2.2(1) | — | testo |
| ν = 0,7 − fck/200 ≥ 0,45 | DS/EN 1992-1-1 | DK NA:2024 | 5.6.1(3)P (richiamato da 6.2.2(6) e 6.2.3(3)) | — | da riscontrare (formula in immagine) |
| tan(α/2) ≤ cot θ ≤ 2,5 (acciaio B, C), ≤ 2,0 con armatura interrotta | DS/EN 1992-1-1 | DK NA:2024 | 6.2.3(2) | (6.7a NA), (6.7b NA) | testo |
| CRd,c = 0,15/γc per dg < 16 mm; k1 = 0,30 in trazione | NS-EN 1992-1-1 | NA | 6.2.2(1) | — | fonte secondaria |
| cot θ ≤ 1,25 con trazione significativa (σct ≥ fctk,0,05) | NS-EN 1992-1-1 | NA | 6.2.3(2) | — | fonte secondaria |
| fck ≤ 60 MPa (C60/75) nelle formule del taglio | NS-EN 1992-1-1 | NA | 3.1.2(2)P | — | fonte secondaria |
| εx, kdg, kv, VRd,c (livello II) | fib MC2010 | 2013 | 7.3.3.2, 7.3.2 | n. da riscontrare | da riscontrare |
| VRd,s, VRd,max, kc, ε1, campo di θ (livello II) | fib MC2010 | 2013 | 7.3.3.3 | n. da riscontrare | da riscontrare |
| VRd,F dei calcestruzzi fibrorinforzati (T.10) | CNR-DT 204 | 2006 | 4.2.3 | n. da riscontrare | da riscontrare |
| VRd,F (stessa formula) | fib MC2010 | 2013 | 7.7.3.2.2 | n. da riscontrare | da riscontrare |
| VRd = min(VRd,s + VRd,f; VRd,max) | CNR-DT 200 | R1/2013 | 4.3.3 | — | da riscontrare |

## 4. Ipotesi

- Sezione di calcestruzzo armato ordinario, senza precompressione; taglio in una sola direzione. Il taglio nelle due
  direzioni si verifica con due chiamate indipendenti (l'interazione con la torsione è nella pagina
  [ca.torsione](ca.torsione.md)).
- La larghezza bw, l'altezza utile d, l'armatura longitudinale tesa ancorata Asl e il rapporto z/d sono dati del
  chiamante; il metodo non li ricava dal contorno.
- σcp è la tensione media della forza assiale sull'area lorda Ac, con la compressione positiva (convenzione delle
  norme); il dato N entra con la compressione negativa.
- Elementi con armatura trasversale: traliccio a inclinazione variabile; il contributo del calcestruzzo è nullo
  (NTC, famiglia Eurocodice, Model Code 2010 livello II).
- Il braccio delle forze interne è z = (z/d) · d, con z/d = 0,9 come valore predefinito.
- cot θ è assegnato dal chiamante oppure scelto dal metodo nel campo ammesso dal profilo, in modo da rendere
  massima la resistenza min(VRd,s; VRd,max).
- Le resistenze di progetto fcd e fyd, γc e il modulo Es sono dati espliciti: provenienza, coefficienti parziali e
  αcc restano a carico del chiamante.
- Conta solo |VEd|: il segno del taglio non influisce sulla resistenza.

## 5. Notazione, unità e convenzioni

| Simbolo | Significato | Unità | Nel codice |
| --- | --- | --- | --- |
| N | forza assiale, compressione negativa | N | `SectionShearInput.N` |
| VEd | taglio di calcolo nella direzione verificata (si usa \|VEd\|) | N | `V` |
| MEd | momento concomitante, solo Model Code 2010 | Nmm | `M` |
| Ac | area lorda di calcestruzzo | mm² | `Area` |
| bw | larghezza minima della sezione ortogonale a V | mm | `Bw` |
| d | altezza utile nella direzione di V | mm | `D` |
| Asl | armatura longitudinale tesa ancorata | mm² | `Asl` |
| fck, fcd | resistenza caratteristica e di progetto del calcestruzzo | MPa | `Fck`, `Fcd` |
| fyd (fywd) | resistenza di progetto dell'armatura trasversale | MPa | `Fyd` |
| γc | coefficiente parziale del calcestruzzo | — | `GammaC` |
| Es | modulo elastico delle barre | MPa | `Es` |
| Asw, s | area di tutti i bracci di uno strato di armatura trasversale e suo passo | mm², mm | `Asw`, `Spacing` |
| α | inclinazione dell'armatura trasversale sull'asse dell'elemento | ° | `AlphaDegrees` |
| θ | inclinazione delle bielle compresse | ° | `CotTheta` (cot θ) |
| z | braccio delle forze interne, z = (z/d) · d | mm | `LeverFactor` · `D` |
| dg | dimensione massima dell'aggregato | mm | `Aggregate` |
| Δe | eccentricità della forza assiale (Model Code 2010) | mm | `AxialEccentricity` |
| fFtuk | resistenza residua ultima caratteristica a trazione del calcestruzzo fibrorinforzato | MPa | `ResidualTensileStrength` |
| fctk | resistenza caratteristica a trazione (frattile 5%) della matrice | MPa | `MatrixTensileStrength` |
| σcp | tensione media di compressione, positiva se di compressione | MPa | traccia `σcp` |
| k, ρl | fattore d'effetto scala e rapporto d'armatura longitudinale | — | traccia |
| CRd,c, k1, vmin | coefficienti della formula senza armatura trasversale | —, —, MPa | traccia |
| αc, αcw | coefficienti della compressione assiale sulle bielle (NTC, Eurocodice) | — | traccia |
| ν, ν1 | coefficienti di riduzione della resistenza delle bielle fessurate | — | traccia |
| εx | deformazione longitudinale a metà altezza (Model Code 2010) | — | `LongitudinalStrain` |
| kv, kdg, kc, ε1 | coefficienti del Model Code 2010 | — | traccia |
| VRd,s, VRd,max, VRd | resistenze dell'armatura trasversale, delle bielle e di progetto | N | `VRsd`, `VRcd`, `VRd` |

Convenzioni dei risultati:

- negli elementi senza armatura trasversale dei profili NTC e Eurocodice `VRsd` contiene il termine principale della
  formula e `VRcd` il termine minimo (vmin + k1 σcp) bw d; `VRd` è il maggiore dei due;
- per il Model Code 2010 senza staffe e per CNR-DT 204 `VRsd` = 0 e `VRcd` = `VRd`;
- `CotTheta` = 0 negli elementi senza armatura trasversale.

## 6. Formulazione

### 6.1 Elementi senza armatura trasversale (NTC, famiglia Eurocodice)

```math
\sigma_{cp} = \min\left(-\frac{N}{A_c};\; 0{,}2\, f_{cd}\right) \qquad \text{(T.1)}
```

```math
k = \min\left(2;\; 1 + \sqrt{200/d}\right) \qquad \text{(T.2)}
```

```math
\rho_l = \min\left(0{,}02;\; \frac{A_{sl}}{b_w\, d}\right) \qquad \text{(T.3)}
```

```math
V_{Rd,c} = \max\left\{0;\; \left[C_{Rd,c}\, k\, (100\, \rho_l\, f_{ck})^{1/3} + k_1\, \sigma_{cp}\right] b_w d;\; \left(v_{min} + k_1\, \sigma_{cp}\right) b_w d\right\} \qquad \text{(T.4)}
```

```math
v_{min} = \kappa\, k^{3/2} f_{ck}^{1/2} \qquad \text{(T.5)}
```

Fonti: NTC 2018 §4.1.2.3.5.1; EN 1992-1-1 6.2.2(1), (6.2.a), (6.2.b), (6.3N). Differenze tra i profili:

| Profilo | CRd,c | k1 | κ in vmin | Note |
| --- | --- | --- | --- | --- |
| NTC 2018, CNR-DT 200 | 0,18/γc | 0,15 | 0,035 | solo σcp ≥ 0: con N > 0 la verifica non è valutata |
| EN 1992-1-1, UNI | 0,18/γc | 0,15 | 0,035 | σcp < 0 (trazione) riduce la resistenza |
| DIN | 0,15/γc | 0,12 | 0,0525/γc per d ≤ 600 mm, 0,0375/γc per d ≥ 800 mm, lineare tra i due | |
| DS | 0,18/γc | 0,15 | 0,051/γc | |
| NS | 0,18/γc (0,15/γc se dg < 16 mm) | 0,15 in compressione, 0,30 in trazione | 0,035 | fck ≤ 60 MPa, fcd ridotto in proporzione |

### 6.2 Elementi senza armatura trasversale (Model Code 2010, livello II)

```math
\varepsilon_x = \max\left\{0;\; \frac{1}{2 E_s A_{sl}}\left[\frac{|M_{Ed}|}{z} + |V_{Ed}| + N\left(\frac{1}{2} + \frac{\Delta e}{z}\right)\right]\right\} \qquad \text{(T.6)}
```

con N positivo di trazione (coincide con la convenzione del dato N).

```math
k_{dg} = \max\left(0{,}75;\; \frac{32}{16 + d_g}\right), \qquad d_g = 0 \ \text{se}\ f_{ck} > 70\ \text{MPa} \qquad \text{(T.7)}
```

```math
k_v = \frac{0{,}4}{1 + 1500\, \varepsilon_x} \cdot \frac{1300}{1000 + k_{dg}\, z} \qquad \text{(T.8)}
```

```math
V_{Rd,c} = k_v \, \frac{\min\left(\sqrt{f_{ck}};\, 8\right)}{\gamma_c}\, z\, b_w \qquad \text{(T.9)}
```

Fonte: fib MC2010 §7.3.3.2 (livello II di approssimazione).

### 6.3 Elementi in calcestruzzo fibrorinforzato senza armatura trasversale (CNR-DT 204)

```math
V_{Rd,F} = \max\left\{0;\; \left[\frac{0{,}18}{\gamma_c} k \left(100\, \rho_l \left(1 + 7{,}5\, \frac{f_{Ftuk}}{f_{ctk}}\right) f_{ck}\right)^{1/3} + 0{,}15\, \sigma_{cp}\right] b_w d;\; \left(v_{min} + 0{,}15\, \sigma_{cp}\right) b_w d\right\} \qquad \text{(T.10)}
```

con k, ρl, vmin e σcp da (T.1)-(T.3) e (T.5) con κ = 0,035; σcp può essere negativa (trazione). Con fFtuk = 0 la
formula coincide con (T.4) del profilo EN 1992-1-1. Fonti: CNR-DT 204/2006 §4.2.3; fib MC2010 §7.7.3.2.2.

### 6.4 Elementi con armatura trasversale

```math
V_{Rd,s} = z\, \frac{A_{sw}}{s}\, f_{yd}\, (\cot\theta + \cot\alpha) \sin\alpha \qquad \text{(T.11)}
```

```math
V_{Rd,max} = z\, b_w\, f_{c,s}\, \frac{\cot\theta + \cot\alpha}{1 + \cot^2\theta} \qquad \text{(T.12)}
```

```math
V_{Rd} = \min\left(V_{Rd,s};\; V_{Rd,max}\right), \qquad \eta = \frac{|V_{Ed}|}{V_{Rd}} \qquad \text{(T.13)}
```

Fonti: NTC 2018 §4.1.2.3.5.2; EN 1992-1-1 (6.13), (6.14); fib MC2010 §7.3.3.3. La resistenza delle bielle fc,s
dipende dal profilo.

**NTC 2018 e CNR-DT 200.** fc,s = αc ν fcd con ν = 0,5 e, con σc = max(0; −N/Ac):

```math
\alpha_c = \begin{cases} 1 + \sigma_c / f_{cd} & 0 \le \sigma_c \le 0{,}25 f_{cd} \\ 1{,}25 & 0{,}25 f_{cd} < \sigma_c \le 0{,}5 f_{cd} \\ \max\left[0;\; 2{,}5\,(1 - \sigma_c / f_{cd})\right] & \sigma_c > 0{,}5 f_{cd} \end{cases} \qquad \text{(T.14)}
```

Se cot θ non è assegnato, il metodo usa l'inclinazione per cui VRd,s = VRd,max, limitata al campo [1; 2,5]:

```math
\cot\theta = \min\left\{2{,}5;\; \max\left[1;\; \sqrt{\max\left(0;\; \frac{\nu f_{cd}\, b_w\, \alpha_c}{(A_{sw}/s)\, f_{yd} \sin\alpha} - 1\right)}\right]\right\} \qquad \text{(T.15)}
```

Il fattore (cot θ + cot α) si semplifica nell'uguaglianza, quindi (T.15) vale anche per staffe inclinate. Poiché
VRd,s cresce e VRd,max decresce con cot θ ≥ 1 (per α tra 45° e 90°), (T.15) è anche il valore che rende massima
min(VRd,s; VRd,max) nel campo.

**Famiglia Eurocodice.** fc,s = αcw ν1 fcd, con αcw = 1 (valore raccomandato per le strutture non precompresse,
EN 1992-1-1 6.2.3(3) nota 3) e:

```math
\nu_1 = \begin{cases} 0{,}6\,(1 - f_{ck}/250) & \text{EN 1992-1-1, NS; UNI con } f_{ck} > 70 \\ 0{,}5 & \text{UNI con } f_{ck} \le 70 \\ \max(0{,}45;\; 0{,}7 - f_{ck}/200) & \text{DS} \\ 0{,}75\, \min(1;\; 1{,}1 - f_{ck}/500) & \text{DIN} \end{cases} \qquad \text{(T.16)}
```

Se cot θ non è assegnato:

```math
\cot\theta^{*} = \arg\max_{\cot\theta \in [c_{min};\, c_{max}]} \min\left(V_{Rd,s};\; V_{Rd,max}\right) \qquad \text{(T.17)}
```

con il campo [cmin; cmax] del profilo:

| Profilo | cmin | cmax |
| --- | --- | --- |
| EN 1992-1-1, UNI | 1 | 2,5 |
| NS | 1 | 2,5; 1,25 se −σcp ≥ 0,7 fctm (trazione), con fctm = 0,3 fck^(2/3) per fck ≤ 50, 2,12 ln[1 + (fck + 8)/10] oltre |
| DS | tan(α/2) | 2 (vedi riquadro T-2) |
| DIN | 1 | da (T.18) |
| Model Code 2010 | 1 | cot 20° = 2,747 (vedi riquadro T-1) |

Per DIN, con σcp a compressione positiva:

```math
V_{Rd,cc} = 0{,}24\, f_{ck}^{1/3} \max\left(0;\; 1 - 1{,}2\,\frac{\sigma_{cp}}{f_{cd}}\right) b_w z, \qquad c_{max} = \begin{cases} 3 & |V_{Ed}| \le V_{Rd,cc} \\ \min\left[3;\; \dfrac{1{,}2 + 1{,}4\,\sigma_{cp}/f_{cd}}{1 - V_{Rd,cc}/|V_{Ed}|}\right] & |V_{Ed}| > V_{Rd,cc} \end{cases} \qquad \text{(T.18)}
```

(DIN EN 1992-1-1/NA (6.7aDE) e (6.7bDE), con c = 0,5.)

**Model Code 2010, livello II.** fc,s = kc fck/γc, con εx da (T.6):

```math
\varepsilon_1 = \varepsilon_x + (\varepsilon_x + 0{,}002)\cot^2\theta, \qquad k_c = \min\left(0{,}65;\; \frac{1}{1{,}2 + 55\,\varepsilon_1}\right) \min\left[1;\; \left(\frac{30}{f_{ck}}\right)^{1/3}\right] \qquad \text{(T.19)}
```

e cot θ da (T.17). Anche con le staffe il profilo richiede Asl > 0, perché εx entra in kc.

> **Scostamento dichiarato — T-1 Model Code 2010: campo di θ indipendente da εx**
>
> - Norma: nel livello II di approssimazione il Model Code 2010 (§7.3.3.3) fissa l'inclinazione minima delle bielle
>   θmin = 20° + 10000 εx, che si riduce a 20° solo per εx = 0.
> - Programma: θmin = 20° per ogni εx (cot θmax = 2,747); εx entra solo in kc.
> - Effetto: a sfavore di sicurezza quando governa l'armatura trasversale. Nella trave dell'esempio S5
>   (εx = 6,79 · 10⁻⁴, staffe Ø8/150) la norma darebbe θmin = 26,79°, cot θ = 1,980 e VRd = 215,0 kN; il programma
>   restituisce 298,3 kN (+38,8%). Lo stesso campo vale per la torsione con il profilo Model Code 2010.
> - Stato: da riscontrare sul testo del Model Code 2010 e da decidere.

> **Scostamento dichiarato — T-2 DS: cot θ ≤ 2 anche con acciaio di classe B o C**
>
> - Norma: DK NA 6.2.3(2) ammette tan(α/2) ≤ cot θ ≤ 2,5 con acciaio di classe B o C (6.7a NA) e limita
>   cot θ ≤ 2,0 solo con armatura longitudinale interrotta (6.7b NA).
> - Programma: cot θ ≤ 2,0 sempre, perché il metodo non sa se l'armatura è interrotta.
> - Effetto: a favore di sicurezza. Quando governa l'armatura trasversale VRd,s si riduce fino al 20% (2/2,5): nella
>   trave dell'esempio S6 VRd = 208,1 kN invece di 260,1 kN.
> - Stato: dichiarato.

### 6.5 Esito

- Se il rapporto η è definito: soddisfatto per η ≤ 1, non soddisfatto altrimenti.
- Se VRd ≤ 0 e |VEd| > 0: non soddisfatto, senza rapporto (mai un rapporto nullo).
- NTC 2018 e CNR-DT 200 senza armatura trasversale con N > 0: non valutato.

## 7. Coefficienti e valori predefiniti

| Simbolo | Valore | Profilo | Fonte | Modificabile | Dove nel codice |
| --- | --- | --- | --- | --- | --- |
| CRd,c | 0,18/γc | NTC, EN, UNI, DS, NS (dg ≥ 16), CNR-DT 204 | NTC 4.1.2.3.5.1; EN 6.2.2(1) | no (γc sì) | `SectionShearCalculator.Ntc`, `.Eurocode`, `.FibreReinforced` |
| CRd,c | 0,15/γc | DIN; NS con dg < 16 mm | NA DIN e NS 6.2.2(1) | no | `SectionShearCalculator.Eurocode` |
| k1 | 0,15 | NTC, EN, UNI, DS, NS in compressione, CNR-DT 204 | NTC; EN 6.2.2(1) | no | idem |
| k1 | 0,12 | DIN | NA DIN 6.2.2(1) | no | `SectionShearCalculator.Eurocode` |
| k1 | 0,30 | NS in trazione | NA NS 6.2.2(1) | no | `SectionShearCalculator.Eurocode` |
| κ (vmin) | 0,035 | NTC, EN, UNI, NS, CNR-DT 204 | NTC; EN (6.3N) | no | idem |
| κ (vmin) | 0,051/γc | DS | DK NA 6.2.2(1) | no | `SectionShearCalculator.Eurocode` |
| κ (vmin) | 0,0525/γc (d ≤ 600), 0,0375/γc (d ≥ 800), lineare | DIN | NA DIN 6.2.2(1) | no | `SectionShearCalculator.Eurocode` |
| σcp,max | 0,2 fcd | tutti tranne Model Code 2010 | NTC; EN 6.2.2(1) | no | `SectionShearCalculator.Ntc`, `.Eurocode`, `.FibreReinforced` |
| ν | 0,5 | NTC, CNR-DT 200; UNI con fck ≤ 70 | NTC 4.1.2.3.5.2; DM 2012 6.2.2(6) | no | `SectionShearCalculator.Ntc`, `.StrutEfficiency` |
| ν | 0,6 (1 − fck/250) | EN, NS, UNI con fck > 70 | EN (6.6N) | no | `SectionShearCalculator.StrutEfficiency` |
| ν | max(0,45; 0,7 − fck/200) | DS | DK NA 5.6.1(3)P | no | `SectionShearCalculator.StrutEfficiency` |
| ν1 | 0,75 min(1; 1,1 − fck/500) | DIN | NA DIN 6.2.3(3) | no | `SectionShearCalculator.Eurocode` |
| αcw | 1 | famiglia Eurocodice | EN 6.2.3(3) nota 3 | no | `SectionShearCalculator.Eurocode` |
| αc | (T.14) | NTC, CNR-DT 200 | NTC 4.1.2.3.5.2 | no | `SectionShearCalculator.Ntc` |
| c (DIN) | 0,5 (0,5 · 0,48 = 0,24) | DIN | NA DIN (6.7bDE) | no | `SectionShearCalculator.Eurocode` |
| cot θ, campo | tabella di 6.4 | tutti | vedi 6.4 | no; cot θ assegnabile nel campo | `SectionShearCalculator.Ntc`, `.Eurocode` |
| z/d | 0,9 | tutti | NTC (z = 0,9 d); EN 6.2.3(1) | sì, in (0; 0,9] | `SectionShearInput` (costruttore) |
| α | 90° | tutti | — | sì, in [45°; 90°] | `SectionShearInput` (costruttore) |
| dg | 20 mm | Model Code 2010, NS | — | sì | `SectionShearInput` (costruttore) |
| Δe | 0 mm | Model Code 2010 | — | sì | `SectionShearInput` (costruttore) |
| fck,max (NS) | 60 MPa, fcd scalato con fck | NS | NA NS 3.1.2(2)P | no | `SectionShearCalculator.Calculate` |
| dg efficace | 0 per fck > 70 MPa | Model Code 2010 | MC2010 7.3.3.2 | no | `SectionShearCalculator.Eurocode` |
| kdg,min | 0,75 | Model Code 2010 | MC2010 7.3.3.2 | no | idem |
| √fck,max | 8 MPa | Model Code 2010 | MC2010 7.3.3.2 | no | idem |
| kc,max | 0,65 | Model Code 2010 | MC2010 7.3.3.3 | no | idem |
| ηfc | min[1; (30/fck)^(1/3)] | Model Code 2010 | MC2010 | no | idem |
| 7,5 | coefficiente del termine delle fibre | CNR-DT 204 | CNR-DT 204 §4.2.3 | no | `SectionShearCalculator.FibreReinforced` |
| VRd,f | 0 | CNR-DT 200 | assenza di dati FRP nella sezione | no | `SectionShearCalculator.FrpStrengthened` |

## 8. Implementazione

Percorsi relativi alla radice del repository Checker.

- Scelta del profilo: `ShearProfiles.TryResolve` per tipo esatto, senza ripiego sulla classe base
  (`GPCChecker.Concrete/Shear/ShearProfiles.cs:16-32`); `Resolve` solleva `NotSupportedException` con il motivo
  (`ShearProfiles.cs:43-50`); CS-TR34 non applicabile (`ShearProfiles.cs:35-41`); riferimenti e nome del modello
  per la traccia (`ShearProfiles.cs:53-80`).
- Ingresso e risultato: `SectionShearInput` (`GPCChecker.Concrete/Shear/SectionShearContracts.cs:12-69`, valori
  predefiniti alla riga 56; `With` per la torsione alle righe 67-68), `SectionShearResult`
  (`SectionShearContracts.cs:87-104`).
- `SectionShearCalculator.Calculate` (`GPCChecker.Concrete/Shear/SectionShearCalculator.cs:16-44`):
  - controllo dei dati (righe 20-25);
  - riduzione NS a C60 (righe 27-28);
  - scelta del metodo per profilo (righe 30-33);
  - esito e stato (righe 34-42).
- `Ntc` (righe 47-79): senza armatura trasversale (righe 52-64), con armatura trasversale e cot θ di (T.15)
  (righe 65-78).
- `FrpStrengthened` (righe 85-92): risultato di `Ntc` più la voce VRd,f = 0.
- `FibreReinforced` (righe 100-120): (T.10).
- `Eurocode` (righe 123-207):
  - εx del Model Code 2010 (righe 131-137);
  - senza armatura trasversale: Model Code 2010 (righe 142-150), famiglia Eurocodice con i coefficienti degli annessi
    (righe 151-161);
  - con armatura trasversale: campo di cot θ (righe 164-165 e 176), αcw (riga 166), ν (riga 167), DIN (righe
    168-175), resistenze in funzione di cot θ (righe 178-188), ricerca di cot θ (righe 189-201), controllo del
    campo (riga 202).
- `StrutEfficiency` (righe 214-219): ν dei profili Eurocodice, condiviso con la torsione.
- `Cbrt` (riga 222): radice cubica come x^(1/3); la differenza da una radice cubica esatta è dell'ordine dell'ultimo
  bit.

Algoritmo della scelta di cot θ nei profili Eurocodice e Model Code 2010: ricerca ternaria di 80 iterazioni su
[cmin; cmax] del massimo di min(VRd,s; VRd,max). L'intervallo si riduce di (2/3)^80 ≈ 8 · 10⁻¹⁵ volte; la funzione è
unimodale perché VRd,s cresce e VRd,max decresce con cot θ ≥ 1 (nel Model Code 2010 anche kc decresce, perché ε1
cresce). Il cot θ assegnato o trovato si controlla nel campo con tolleranza 10⁻¹⁰.

Traccia: ogni risultato porta l'elenco `Details` (simbolo, valore, unità, espressione) con i valori intermedi
della formula usata.

## 9. Limiti e casi non supportati

- Precompressione e sezioni composte acciaio-calcestruzzo.
- Riduzione del taglio per carichi vicini agli appoggi (EN 1992-1-1 6.2.2(6) e 6.2.3(8)) e controllo
  VEd ≤ 0,5 bw d ν fcd degli elementi senza armatura trasversale: non applicati. Senza la riduzione il controllo
  non governa nelle sezioni usuali.
- EN 1992-1-1 6.2.3(3) nota 2 (ν1 = 0,6 se la tensione delle staffe è inferiore a 0,8 fyk): non usata; il metodo
  usa sempre ν1 = ν, a favore di sicurezza.
- Verifica dell'armatura longitudinale per l'effetto del taglio (traslazione del diagramma dei momenti) e armature
  minime: fuori dal metodo.
- NTC 2018 senza armatura trasversale con trazione assiale: non valutato.
- CNR-DT 204 con staffe e fibre insieme; contributo FRP di CNR-DT 200 (le sezioni non hanno dati FRP).
- DS con staffe di acciaio di classe A (DK NA 6.2.3(2), campo 1 ≤ cot θ ≤ 2 solo con T ≤ 0,1 V): il metodo non
  distingue la classe dell'acciaio.
- Model Code 2010: solo livello II di approssimazione; nessun contributo del calcestruzzo con le staffe (livello III).
- Norme americane (ACI 318, AASHTO).

## 10. Esempio numerico verificato

Dati comuni: sezione 300 × 500 mm (Ac = 150 000 mm²), bw = 300 mm, d = 460 mm, Asl = 4Ø20 = 1256,64 mm²,
calcestruzzo C30/37 (fck = 30 MPa, γc = 1,5), acciaio B450C (fyd = 450/1,15 = 391,30 MPa, Es = 200 000 MPa),
z = 0,9 d = 414 mm. Libreria GPCChecker.Concrete 0.0.15.0.

**S1. NTC 2018, senza armatura trasversale, N = −200 kN, VEd = 100 kN** (fcd = 0,85 · 30/1,5 = 17,0 MPa).

1. σcp = 200 000/150 000 = 1,333 MPa ≤ 0,2 fcd = 3,40 MPa.
2. k = 1 + √(200/460) = 1,6594; ρl = 1256,64/(300 · 460) = 0,009106.
3. (100 ρl fck)^(1/3) = 27,318^(1/3) = 3,0117; 0,12 · 1,6594 · 3,0117 = 0,5997 MPa.
4. Termine principale: (0,5997 + 0,15 · 1,333) · 138 000 = 0,7997 · 138 000 = 110 360,6 N.
5. vmin = 0,035 · 1,6594^1,5 · √30 = 0,4098 MPa; termine minimo (0,4098 + 0,2) · 138 000 = 84 149,2 N.
6. VRd = 110 360,6 N; η = 100 000/110 360,6 = 0,9061: soddisfatta.

**S2. NTC 2018, staffe Ø8/150 a due bracci (Asw = 100,53 mm²), N = −300 kN, VEd = 250 kN.**

1. σc = 2,00 MPa ≤ 0,25 fcd = 4,25 MPa, quindi αc = 1 + 2/17 = 1,1176; ν fcd = 8,5 MPa.
2. (Asw/s) fyd = 0,67021 · 391,30 = 262,25 N/mm.
3. cot θ = √(8,5 · 300 · 1,1176/262,25 − 1) = √9,867 = 3,141, limitato a 2,5.
4. VRd,s = 414 · 262,25 · 2,5 = 271 433,6 N.
5. VRd,max = 414 · 300 · 1,1176 · 8,5 · 2,5/(1 + 6,25) = 406 862,1 N.
6. VRd = 271 433,6 N; η = 0,9210: soddisfatta.

**S3. EN 1992-1-1, staffe Ø10/100 a due bracci (Asw = 157,08 mm²), N = 0, VEd = 450 kN** (fcd = 30/1,5 = 20 MPa).

1. ν1 = 0,6 (1 − 30/250) = 0,528.
2. VRd,s = A cot θ con A = 414 · 1,5708 · 391,30 = 254 469,0 N; VRd,max = B cot θ/(1 + cot²θ) con
   B = 414 · 300 · 0,528 · 20 = 1 311 552 N.
3. Il massimo di min(VRd,s; VRd,max) è all'uguaglianza: 1 + cot²θ = B/A = 5,1541, cot θ = 2,0382 ∈ [1; 2,5].
4. VRd = 254 469,0 · 2,0382 = 518 647,1 N; η = 0,8676: soddisfatta.

**S4. Model Code 2010 livello II, senza armatura trasversale, MEd = 100 kNm, VEd = 100 kN, N = 0, dg = 20 mm.**

1. εx = (100 · 10⁶/414 + 100 000)/(2 · 200 000 · 1256,64) = 341 545,9/502 654 825 = 6,7948 · 10⁻⁴.
2. kdg = 32/36 = 0,8889; kv = 0,4/(1 + 1,0192) · 1300/(1000 + 0,8889 · 414) = 0,19810 · 0,95029 = 0,18825.
3. VRd,c = 0,18825 · √30 · 414 · 300/1,5 = 85 373,5 N; η = 1,1713: non soddisfatta.

| Caso | Grandezza | Calcolo a mano | Libreria | Scarto |
| --- | --- | --- | --- | --- |
| S1 | VRd | 110 360,6 N | 110 360,6041 N | < 10⁻⁹ |
| S1 | termine minimo | 84 149,2 N | 84 149,2333 N | < 10⁻⁹ |
| S2 | VRd,s; VRd,max | 271 433,6 N; 406 862,1 N | 271 433,6053 N; 406 862,069 N | < 10⁻⁹ |
| S2 | cot θ | 2,5 | 2,5 | 0 |
| S3 | cot θ | 2,038154 | 2,038154478 | < 10⁻⁹ |
| S3 | VRd | 518 647,1 N | 518 647,142 N | < 10⁻⁹ |
| S4 | εx | 6,79484 · 10⁻⁴ | 6,79483966 · 10⁻⁴ | < 10⁻⁹ |
| S4 | VRd | 85 373,5 N | 85 373,5404 N | < 10⁻⁹ |

Esempi dei riquadri, con gli stessi dati:

- **S5** (riquadro T-1): Model Code 2010 con staffe Ø8/150, MEd = 100 kNm, VEd = 100 kN. Libreria: cot θ = 2,7475,
  VRd,s = 298 303,1 N, VRd,max = 339 745,3 N, VRd = 298 303,1 N. Con θmin = 20° + 10000 εx = 26,79°: cot θ = 1,9801,
  VRd,s = 214 987,0 N, ε1 = 0,011185, kc = 0,5509, VRd,max = 550 656,2 N, VRd = 214 987,0 N.
- **S6** (riquadro T-2): DS (γc = 1,45, γs = 1,2, fcd = 20,69 MPa, fyd = 375 MPa) con staffe Ø8/150, N = 0. Libreria:
  cot θ = 2, VRd = 208 099,1 N; con cot θ = 2,5: VRd,s = 260 123,9 N < VRd,max = 487 348 N.

## 11. Validazione

- **Casi congelati del motore precedente**: 2016 casi su 7 norme (NTC, Model Code 2010, EN, UNI, DIN, DS, NS), con
  griglia senza e con staffe, casi limite e 700 casi casuali. Esito atteso: 1840 casi con risultato e 176 dati
  rifiutati. Sono confrontati resistenze, rapporto, cot θ, esito e più di 5000 valori intermedi con tolleranza
  relativa 10⁻⁹ (test `ShearMigrationTests.LegacyFixturesAreReproduced`).
- **Calcoli a mano** nei test `ShearMigrationTests` (7 test):
  - EN 1992-1-1 senza staffe: 82,76 kN e minimo 56,55 kN;
  - traliccio EN 1992-1-1 e NTC: 271,4 kN, 384,4 kN e 364,0 kN;
  - CNR-DT 204 con fFtuk = 1,5 MPa: 154,8 kN, e coincidenza con EN per fFtuk = 0;
  - CNR-DT 200 uguale a NTC con VRd,f = 0 dichiarato;
  - resistenza nulla, trazione senza staffe, segno di V;
  - risoluzione dei profili per tipo esatto, CS-TR34 e ACI.
- **Integrazione**: i test del verificatore di modello confrontano il taglio nelle due direzioni con la chiamata
  diretta di questo metodo per tutte le norme non americane.
- **Esempi di questa pagina**: S1-S6 eseguiti con la libreria 0.0.15.0, scarto inferiore a 10⁻⁹.
- **Benchmark indipendenti pubblicati**: nessuno, per ora. I valori di DIN, NS, Model Code 2010 e CNR-DT 204 sono
  verificati con calcoli a mano sulle formule del codice, non su esempi delle norme.

## 12. Bibliografia

- Ministero delle Infrastrutture e dei Trasporti, DM 17 gennaio 2018, *Aggiornamento delle Norme tecniche per le
  costruzioni*, §4.1.2.3.5.
- Circolare 21 gennaio 2019 n. 7 C.S.LL.PP., *Istruzioni per l'applicazione dell'Aggiornamento delle Norme tecniche
  per le costruzioni*, §C4.1.2.3.5.
- EN 1992-1-1:2004 + AC:2010, *Eurocode 2: Design of concrete structures — Part 1-1: General rules and rules for
  buildings*, §6.2.
- UNI EN 1992-1-1, appendice nazionale: DM 31 luglio 2012 (G.U. n. 73 del 27 marzo 2013).
- DIN EN 1992-1-1/NA, *Nationaler Anhang — Eurocode 2*.
- DS/EN 1992-1-1 DK NA:2024, *Nationalt anneks til Eurocode 2*.
- NS-EN 1992-1-1, *Nasjonalt tillegg*.
- fib, *fib Model Code for Concrete Structures 2010*, Ernst & Sohn, 2013, §7.3 e §7.7.
- CNR-DT 204/2006, *Istruzioni per la progettazione, l'esecuzione ed il controllo di strutture di calcestruzzo
  fibrorinforzato*.
- CNR-DT 200 R1/2013, *Istruzioni per la progettazione, l'esecuzione ed il controllo di interventi di
  consolidamento statico mediante l'utilizzo di compositi fibrorinforzati*.
