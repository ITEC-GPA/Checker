---
id: ca.sle-tensioni
titolo: Limiti tensionali in esercizio di una sezione in calcestruzzo armato
libreria: GPCChecker.Concrete
classi:
  - GPC.Checkers.Concrete.Serviceability.StressLimitCheck
  - GPC.Checkers.Concrete.Serviceability.StressLimitResult
  - GPC.Checkers.Concrete.Serviceability.StressLimitPoint
  - GPC.Checkers.Concrete.Serviceability.ServiceabilityCombination
  - GPC.Checkers.Concrete.Results.StressAnalysisResult
versione: 0.0.17.0
norme:
  - ntc2018-4.1.2.2.5.1
  - ntc2018-4.1.2.2.5.2
  - ntc2018-4.1.8.1.5
  - circ2019-C4.1.2.2.5
  - en1992-1-1-7.1
  - en1992-1-1-7.2
  - na-uni-2012-7.2
  - na-din-7.2
  - na-dk-2024-7.2
  - na-ns-7.2
  - mc2010-7.6
stato: bozza
---

# Limiti tensionali in esercizio

## 1. Scopo

Il metodo confronta uno stato tensionale di esercizio già calcolato dal solutore sezionale con i limiti della
norma:

- combinazione caratteristica: compressione del calcestruzzo ai vertici della sezione, tensione delle barre e dei
  trefoli;
- combinazione quasi permanente: compressione del calcestruzzo;
- combinazione frequente: nessun limite tensionale.

Restituisce i limiti assoluti, i punti governanti, i rapporti |σ|/σlim del calcestruzzo e dell'acciaio e il loro
massimo. Il metodo non ripete l'equilibrio: usa il piano di deformazione e le tensioni del risultato passato.

## 2. Campo di applicazione

| Caso | Stato | Comportamento del programma |
| --- | --- | --- |
| Stato tensionale di una sezione in c.a. del solutore per le norme della famiglia Model Code 2010 (NTC 2018, EN 1992-1-1 e annessi UNI, DIN, DS, NS, Model Code 2010, CNR-DT 204, CNR-DT 200) | supportato | calcolo con i coefficienti della classe normativa |
| Analisi lineare (con φ per le barre e per i trefoli) o non lineare | supportato | vedi riquadro E-4 per l'analisi non lineare |
| Combinazione caratteristica | supportato | calcestruzzo, barre e trefoli |
| Combinazione quasi permanente | supportato | solo calcestruzzo |
| Combinazione frequente | nessun limite | rapporti assenti (`null`) |
| Getti sottili (fattore sul limite del calcestruzzo) | supportato | fattore esplicito in (0; 1] |
| Trefoli con predeformazione nulla | non supportato | errore (`NotSupportedException`): il solutore riconosce i trefoli dalla predeformazione |
| Stato non convergente (piano assente o tensioni non finite) | non valutabile | errore (`InvalidOperationException`) |
| Classe normativa non derivata da Model Code 2010, calcestruzzo non europeo | non supportato | errore (`NotSupportedException`) |
| CS-TR34 | non applicabile | `NotApplicableReason` restituisce il motivo; il chiamante non esegue la verifica |
| Profili in acciaio inglobati nelle sezioni composte | non verificati | il metodo considera solo calcestruzzo, barre e trefoli |
| Tensioni da deformazioni impresse (limite k4 fyk) | non supportato | non distinte dal metodo |

## 3. Riferimenti normativi

| Formula o grandezza | Norma | Edizione e appendice | Paragrafo | Eq. o tabella | Riscontro |
| --- | --- | --- | --- | --- | --- |
| σc ≤ 0,60 fck (caratteristica), σc ≤ 0,45 fck (quasi permanente), riduzione del 20% per elementi piani gettati in opera con spessore < 50 mm | NTC | 2018 | 4.1.2.2.5.1 | [4.1.15], [4.1.16] | testo |
| σs ≤ 0,80 fyk (caratteristica) | NTC | 2018 | 4.1.2.2.5.2 | [4.1.17] | testo |
| σp ≤ 0,80 fp(0,1)k (rinvio a 4.1.2.2.5.2 con fp(0,1)k, fp(1)k o fpyk al posto di fyk) | NTC | 2018 | 4.1.8.1.5 | — | testo |
| Analisi lineare con il calcestruzzo teso trascurato; viscosità con il modulo ridotto o n = 15 | Circolare | 2019 | C4.1.2.2.5 | — | testo |
| k1 fck in XD, XF, XS; k2 fck per il creep lineare; k3 fyk; k4 fyk per deformazioni impresse; k5 fpk | EN 1992-1-1 | 2004 + AC:2010 | 7.2(2), 7.2(3), 7.2(5) | — | da riscontrare |
| Analisi delle tensioni in esercizio | EN 1992-1-1 | 2004 + AC:2010 | 7.1(2) | — | da riscontrare |
| k1 = 0,60, k2 = 0,45 (−20% getti sottili per entrambi), k3 = 0,80, k4 = 0,90, k5 = 0,70 | UNI EN 1992-1-1 | DM 31/07/2012 | 7.2(2), 7.2(3), 7.2(5) | — | testo |
| k3 = 0,80, k4 = 1,0, k5 = 0,65 | DIN EN 1992-1-1 | NA | 7.2(5) | — | fonte secondaria (vedi riquadro E-5) |
| 7.2(2), 7.2(3), 7.2(5) invariati | DS/EN 1992-1-1 | DK NA:2024 | 7.2 | — | testo |
| 7.2 con i valori raccomandati | NS-EN 1992-1-1 | NA | 7.2 | — | fonte secondaria |
| Limiti tensionali | fib MC2010 | 2013 | 7.6 | — | da riscontrare |

## 4. Ipotesi

- Lo stato tensionale viene dal solutore sezionale per la combinazione indicata dal chiamante; la scelta del tipo di
  analisi (lineare o non lineare), del coefficiente di viscosità φ e della resistenza a trazione del calcestruzzo è
  del chiamante.
- Analisi lineare (vedi 6.1): calcestruzzo elastico lineare in compressione con il modulo del materiale (Ecm per i
  calcestruzzi europei), nullo in trazione salvo richiesta del cls teso; barre elastiche con modulo Es (1 + φ), che
  equivale al coefficiente di omogeneizzazione efficace n = Es (1 + φ)/Ecm; trefoli con un φ distinto.
- La tensione massima di compressione del calcestruzzo di un contorno poligonale con piano di deformazione è a un
  vertice: il metodo controlla i vertici del contorno e dei fori.
- Il limite del calcestruzzo è un limite di compressione: i vertici tesi non entrano nel rapporto.
- L'acciaio è controllato su tutte le barre e i trefoli, in valore assoluto.

## 5. Notazione, unità e convenzioni

| Simbolo | Significato | Unità | Nel codice |
| --- | --- | --- | --- |
| ε | deformazione del piano in un punto, compressione negativa | — | `StressAnalysisResult.StrainPlane` |
| σc | tensione del calcestruzzo a un vertice, compressione negativa | MPa | `StressLimitPoint.Stress` |
| σc,min | tensione minima (massima compressione) ai vertici | MPa | `ConcreteMinStress` |
| σs, σp | tensione di una barra o di un trefolo | MPa | `StressLimitPoint.Stress` |
| εp | predeformazione del trefolo | — | `ReinforcedConcreteRebar.EpsilonP` |
| Ecm, Es | moduli elastici di calcestruzzo e acciaio | MPa | materiali di Model |
| φ (ψ nel codice) | coefficiente di viscosità per barre e per trefoli | — | `PsiRebar`, `PsiTendon` |
| n | coefficiente di omogeneizzazione Es (1 + φ)/Ecm | — | — |
| fck, fyk, fpk, fp(0,1)k | resistenze caratteristiche | MPa | materiali di Model |
| k1, k2, k3, k5 | coefficienti dei limiti tensionali | — | classe normativa |
| fs | fattore sul limite del calcestruzzo (getti sottili) | — | `ConcreteLimitFactor` |
| σc,lim, σs,lim, σp,lim | limiti assoluti | MPa | `ConcreteLimit`, `StressLimitPoint.Limit` |
| ηc, ηs, η | rapporti del calcestruzzo, dell'acciaio e della verifica | — | `ConcreteRatio`, `SteelRatio`, `Ratio` |

Coordinate dei punti in mm nel piano della sezione (assi del solutore). I punti sono identificati come C1, C2, …
(vertici nell'ordine del contorno), B1, … (barre) e P1, … (trefoli).

## 6. Formulazione

### 6.1 Stato tensionale (solutore)

Analisi lineare:

```math
\sigma_c(\varepsilon) = \begin{cases} E_{cm}\, \varepsilon & \varepsilon < 0 \\ E_{ct}\, \varepsilon\ \text{con cls teso, altrimenti}\ 0 & \varepsilon \ge 0 \end{cases}, \qquad \sigma_s = E_s (1 + \varphi)\, \varepsilon + E_s\, \varepsilon_p \qquad \text{(E.1)}
```

L'equilibrio con le azioni del solutore conta le barre al netto del calcestruzzo che occupano: per una barra nel
calcestruzzo compresso il contributo è (σs − σc) As, cioè (n − 1) As nella sezione omogeneizzata.

Analisi non lineare: le leggi costitutive sono quelle di progetto del solutore, cioè la legge del calcestruzzo
moltiplicata per αcc/γc in compressione e la legge di progetto dell'acciaio (vedi riquadro E-4).

### 6.2 Limiti

```math
\sigma_{c,lim} = f_s\, k_1\, f_{ck}\ \ \text{(caratteristica)}, \qquad \sigma_{c,lim} = f_s\, k_2\, f_{ck}\ \ \text{(quasi permanente)}, \qquad 0 < f_s \le 1 \qquad \text{(E.2)}
```

```math
\sigma_{s,lim} = k_3\, f_{yk}\ \ \text{(barre)}, \qquad \sigma_{p,lim} = \begin{cases} k_5\, f_{pk} & \text{famiglia Eurocodice e Model Code 2010} \\ 0{,}80\, f_{p(0,1)k} & \text{NTC 2018, CNR-DT 200} \end{cases} \qquad \text{(E.3)}
```

Valori dei coefficienti per classe normativa:

| Classe normativa | k1 | k2 | k3 | Trefoli |
| --- | --- | --- | --- | --- |
| Model Code 2010, EN 1992-1-1, DIN, DS, NS, CNR-DT 204 | 0,60 | 0,45 | 0,80 | 0,75 fpk |
| UNI EN 1992-1-1 | 0,60 | 0,45 | 0,80 | 0,70 fpk |
| NTC 2018, CNR-DT 200 | 0,60 | 0,45 | 0,80 | 0,80 fp(0,1)k |

fs = 0,8 per gli elementi piani gettati in opera con spessore inferiore a 50 mm (NTC 2018 §4.1.2.2.5.1; DM
31/07/2012 7.2); fs = 1 negli altri casi. Il fattore non si applica all'acciaio.

### 6.3 Rapporti

```math
\eta_c = \max_{j:\ \sigma_{c,j} < 0} \frac{|\sigma_{c,j}|}{\sigma_{c,lim}} \quad (0\ \text{se nessun vertice è compresso}) \qquad \text{(E.4)}
```

```math
\eta_s = \max_{i} \frac{|\sigma_{s,i}|}{\sigma_{s,lim,i}} \quad \text{(solo combinazione caratteristica)} \qquad \text{(E.5)}
```

```math
\eta = \max\left(\eta_c;\; \eta_s\right) \qquad \text{(E.6)}
```

Nella combinazione frequente ηc, ηs e η non sono definiti; nella quasi permanente η = ηc. Il governante del
calcestruzzo è il vertice compresso con il rapporto massimo, quello dell'acciaio la barra o il trefolo con il
rapporto massimo.

> **Scostamento dichiarato — E-1 (R7) Limite dell'acciaio anche sulle barre compresse**
>
> - Norma: EN 1992-1-1 7.2(5) limita la tensione di trazione delle armature; NTC 2018 §4.1.2.2.5.2, [4.1.17], parla di
>   tensione massima.
> - Programma: confronta |σs| di tutte le barre e dei trefoli, compressi compresi.
> - Effetto: a favore di sicurezza per la famiglia Eurocodice; governa solo con barre compresse molto sollecitate
>   (per esempio con φ elevato). Nell'esempio con φ = 1 le barre compresse hanno |σs|/σs,lim = 0,18.
> - Stato: dichiarato.

> **Scostamento dichiarato — E-2 (R7) Limite k1 fck senza condizione sulla classe di esposizione**
>
> - Norma: EN 1992-1-1 7.2(2) indica il limite k1 fck nelle zone esposte alle classi XD, XF e XS (dove la
>   fessurazione longitudinale può ridurre la durabilità); NTC 2018 lo applica sempre.
> - Programma: applica k1 fck in tutte le classi e per tutte le norme.
> - Effetto: a favore di sicurezza per la famiglia Eurocodice e il Model Code 2010 nelle classi X0, XC e XA.
> - Stato: dichiarato.

> **Scostamento dichiarato — E-3 (R7) Limite k2 fck come verifica**
>
> - Norma: EN 1992-1-1 7.2(3) usa k2 fck come soglia oltre la quale si deve considerare il creep non lineare; NTC 2018
>   lo impone come limite.
> - Programma: per tutte le norme σc > k2 fck dà rapporto maggiore di 1 ed esito non soddisfatto.
> - Effetto: a favore di sicurezza per la famiglia Eurocodice e il Model Code 2010: il programma non consente di
>   superare la soglia con un'analisi del creep non lineare.
> - Stato: dichiarato.

> **Scostamento dichiarato — E-4 (R5) Analisi non lineare con le leggi costitutive di progetto**
>
> - Norma: le tensioni di esercizio si calcolano con le proprietà dei materiali in esercizio (EN 1992-1-1 7.1(2);
>   moduli Ecm ed Es, eventualmente con il modulo efficace per il creep), non con le leggi di progetto per lo stato
>   limite ultimo (EN 1992-1-1 3.1.7). La Circolare 2019 C4.1.2.2.5 indica le usuali ipotesi di comportamento
>   lineare con il calcestruzzo teso trascurato.
> - Programma: con l'analisi non lineare il solutore usa la legge del calcestruzzo moltiplicata per αcc/γc e la legge
>   di progetto dell'acciaio (snervamento a fyd). Il metodo accetta lo stato senza segnalarlo.
> - Effetto: a sfavore di sicurezza per il calcestruzzo. Nella trave dell'esempio (NTC, M = 80 kNm) la compressione
>   massima è 7,60 MPa invece di 10,96 MPa dell'analisi lineare (−31%); a 150 kNm il rapporto del calcestruzzo è
>   0,83 invece di 1,14 e l'acciaio si ferma a fyd = 391,3 MPa. A parità di deformazione la legge del calcestruzzo è
>   ridotta del fattore αcc/γc (0,567 con NTC), quindi anche il suo massimo.
> - Stato: da discutere (voce R5 del registro delle differenze: correzione nel solutore o avviso nel risultato).

> **Scostamento dichiarato — E-5 (R6) DIN: limite dei trefoli**
>
> - Norma: secondo una fonte secondaria l'annesso tedesco fissa k5 = 0,65 in 7.2(5); da riscontrare sul testo.
> - Programma: la classe DIN eredita k5 = 0,75 della classe base.
> - Effetto: se confermato, a sfavore di sicurezza per i trefoli con DIN (+15% sul limite).
> - Stato: da riscontrare (voce R6 del registro delle differenze).

## 7. Coefficienti e valori predefiniti

| Simbolo | Valore | Fonte | Modificabile | Dove nel codice |
| --- | --- | --- | --- | --- |
| k1 | 0,60 | EN 7.2(2), NTC 4.1.2.2.5.1 | sì (proprietà della classe normativa) | `StandardModelCode2010.ServiceabilityStressConcreteCoefficientForCharacteristicCombination` |
| k2 | 0,45 | EN 7.2(3), NTC 4.1.2.2.5.1 | sì | `StandardModelCode2010.ServiceabilityStressConcreteCoefficientForQuasiPermanentCombination` |
| k3 | 0,80 | EN 7.2(5), NTC 4.1.2.2.5.2 | sì | `StandardModelCode2010.ServiceabilityStressSteelCoefficientForCharacteristicCombination` |
| k5 | 0,75 su fpk (base); 0,70 su fpk (UNI); 0,80 su fp(0,1)k (NTC, CNR-DT 200) | EN 7.2(5); DM 2012 7.2; NTC 4.1.8.1.5 | sì (coefficiente); la grandezza di riferimento dipende dalla classe | `StandardModelCode2010.ServiceabilityStressPrestressSteelCoefficientForCharacteristicCombination`, `GetServiceabilityPrestressLimitStress` |
| fs | 1; 0,8 per getti sottili | NTC 4.1.2.2.5.1; DM 2012 7.2 | sì, in (0; 1] | argomento `concreteLimitFactor` di `StressLimitCheck.Evaluate` |
| φ | 0 | — | sì (opzioni del solutore) | `StressAnalysisResult.PsiRebar`, `PsiTendon` |

## 8. Implementazione

Percorsi relativi alla radice del repository Checker, salvo le classi di Model (repository Model).

- `StressLimitCheck.Evaluate` (`GPCChecker.Concrete/Serviceability/StressLimitCheck.cs:73-124`):
  - controlli su combinazione, fattore, classe normativa e materiale (righe 75-80) e sui trefoli senza
    predeformazione (righe 81-83);
  - tensioni ai vertici e nelle barre, lineari con φ o non lineari (righe 85-90);
  - combinazione frequente: solo le tensioni estreme, nessun limite (riga 97);
  - limiti e rapporti del calcestruzzo con le verifiche native del risultato, rapporto diviso per fs (righe 100-110);
  - limiti e rapporti di barre e trefoli nella combinazione caratteristica (righe 112-121).
- `StressLimitResult` (`StressLimitCheck.cs:31-53`): rapporti calcolati dalle proprietà `ConcreteRatio`,
  `SteelRatio`, `Ratio` (righe 45-50).
- `NotApplicableReason` per CS-TR34 (`StressLimitCheck.cs:69-71`).
- Tensioni del risultato (`GPCChecker.Concrete/Results/ResultType/StressAnalysisResult.cs`): barre con φ
  (righe 116-130), verifiche native di acciaio (righe 279-331) e calcestruzzo (righe 441-479), vertici
  (righe 347-355).
- Solutore (`GPCChecker.Concrete/SectionSolvers/SectionSolver.cs`): σc lineare (righe 1089-1109), σs lineare con
  (1 + φ) e predeformazione (righe 1207-1221), contributo delle barre al netto del calcestruzzo (righe 1177-1205).
  Leggi di progetto dell'analisi non lineare in `SectionSolverModelCode2010.cs:110-129`.
- Coefficienti (repository Model): `Model/Standards/StandardModelCode2010.cs:173-176` e limite dei trefoli
  (righe 147-150); `StandardNTC2018Concrete.cs:29-31` e 40-43; `StandardUNIEn1992p11.cs:24-26`. Limiti come prodotto
  coefficiente per resistenza: `Model/Materials/Concrete/ConcreteMaterialEuropeanCommon.cs:515-528`,
  `Model/Materials/Steel/SteelMaterial.cs:599-613`. Leggi di progetto: `ConcreteMaterialEuropeanCommon.cs:706-740`.

Il metodo non ha iterazioni; l'equilibrio è risolto dal solutore sezionale con la sua tolleranza.

## 9. Limiti e casi non supportati

- Combinazione frequente: nessun limite tensionale (coerente con le norme considerate).
- Limite k4 fyk per le tensioni dovute a deformazioni impresse: non distinto.
- Profili in acciaio delle sezioni composte: non verificati da questo metodo.
- Trefoli senza predeformazione: errore.
- L'analisi non lineare usa le leggi di progetto (riquadro E-4): per la verifica delle tensioni di esercizio va
  usata l'analisi lineare.
- Il metodo non sceglie la combinazione né φ: entrambi sono responsabilità del chiamante.

## 10. Esempio numerico verificato

Sezione rettangolare 300 × 500 mm, C30/37 (fck = 30 MPa, Ecm = 32 836,57 MPa), B450C (fyk = 450 MPa,
Es = 200 000 MPa). Barre tese 3Ø20 (As = 942,48 mm²) a 50 mm dal bordo inferiore (d = 450 mm), barre compresse 2Ø16
(As' = 402,12 mm²) a 48 mm dal bordo superiore. Momento M = 80 kNm che tende il lembo inferiore, N = 0. Analisi
lineare senza cls teso. Norma NTC 2018. Libreria GPCChecker.Concrete 0.0.17.0, stato dal solutore sezionale e
`StressLimitCheck.Evaluate`.

**φ = 0.**

1. n = 200 000/32 836,57 = 6,0908; (n − 1) As' = 2047,12 mm²; n As = 5740,42 mm².
2. Asse neutro: 150 x² + 2047,12 (x − 48) − 5740,42 (450 − x) = 0, cioè 150 x² + 7787,54 x − 2 681 451 = 0;
   x = 110,24 mm.
3. Icr = 300 · 110,24³/3 + 2047,12 · 62,24² + 5740,42 · 339,76² = 1,3398 · 10⁸ + 0,0793 · 10⁸ + 6,6265 · 10⁸ =
   8,0456 · 10⁸ mm⁴.
4. σc = 80 · 10⁶ · 110,24/8,0456 · 10⁸ = 10,962 MPa (compressione al lembo superiore).
5. σs = 6,0908 · 80 · 10⁶ · 339,76/8,0456 · 10⁸ = 205,77 MPa; barre compresse σs' = −37,69 MPa.
6. Caratteristica: σc,lim = 0,60 · 30 = 18,0 MPa, ηc = 0,6090; σs,lim = 0,80 · 450 = 360 MPa, ηs = 0,5716;
   η = 0,6090. Quasi permanente: σc,lim = 13,5 MPa, η = 0,8120. Getto sottile (fs = 0,8): σc,lim = 14,4 MPa,
   ηc = 0,7612.

**φ = 1** (combinazione quasi permanente con viscosità): n = 12,182, x = 143,51 mm, Icr = 1,4150 · 10⁹ mm⁴,
σc = 8,113 MPa, σs = 211,08 MPa; η = 8,113/13,5 = 0,6010.

| Caso | Grandezza | Calcolo a mano | Libreria | Scarto relativo |
| --- | --- | --- | --- | --- |
| φ = 0 | σc,min | −10,96159 MPa | −10,96140 MPa | 1,8 · 10⁻⁵ |
| φ = 0 | σs,max | 205,7671 MPa | 205,7684 MPa | 6,4 · 10⁻⁶ |
| φ = 0 | σs' (barre compresse) | −37,6945 MPa | −37,6933 MPa | 3,1 · 10⁻⁵ |
| φ = 0 | η caratteristica; η quasi permanente | 0,60898; 0,81197 | 0,60897; 0,81196 | 1,8 · 10⁻⁵ |
| φ = 0 | ηc con fs = 0,8 | 0,76122 | 0,76121 | 1,8 · 10⁻⁵ |
| φ = 1 | σc,min; σs,max | −8,11344 MPa; 211,0757 MPa | −8,11338 MPa; 211,0764 MPa | 7 · 10⁻⁶; 4 · 10⁻⁶ |

Lo scarto, dell'ordine di 10⁻⁵, viene dal solutore sezionale (integrazione numerica della zona compressa e
tolleranza di equilibrio); i limiti e i rapporti del metodo, dati gli stati del solutore, coincidono con il calcolo
a mano a 10⁻⁹.

Esempio del riquadro E-4, stessi dati con NTC e combinazione caratteristica:

| M | Analisi lineare: σc,min; σs,max; ηc | Analisi non lineare: σc,min; σs,max; ηc |
| --- | --- | --- |
| 80 kNm | −10,96 MPa; 205,8 MPa; 0,609 | −7,60 MPa; 212,0 MPa; 0,422 |
| 120 kNm | −16,44 MPa; 308,7 MPa; 0,913 | −10,78 MPa; 319,1 MPa; 0,599 |
| 150 kNm | −20,55 MPa; 385,8 MPa; 1,142 | −14,95 MPa; 391,3 MPa; 0,830 |

## 11. Validazione

- **Casi congelati del motore precedente**: 2016 stati di esercizio su 4 sezioni (rettangolare, a T, circolare,
  rettangolare con foro), 9 norme, analisi lineare e non lineare, φ, cls teso, getti sottili, combinazioni
  caratteristica, quasi permanente e frequente. Sono riprodotti tensione minima del calcestruzzo, tensione massima
  dell'acciaio, rapporti (compresi quelli assenti della frequente) e limiti con tolleranza relativa 10⁻⁹ (test
  `ServiceabilityMigrationTests.LegacyStressStatesAreReproduced`).
- **Coefficienti di ogni norma**: per 10 classi normative i limiti sono k1 fck, k2 fck e k3 fyk della classe e
  CS-TR34 è non applicabile (`ServiceabilityMigrationTests.LimitsFollowTheCoefficientsOfEveryStandard`;
  2 test in tutto nella classe).
- **Integrazione**: i test del verificatore di modello confrontano le tensioni con la chiamata diretta e con i
  coefficienti NTC 0,60 e 0,45 fck e 0,80 fyk, compreso il fattore dei getti sottili.
- **Esempio di questa pagina**: calcolo a mano della sezione fessurata con φ = 0 e φ = 1, ripetuto in modo
  indipendente, e libreria 0.0.17.0; scarto ≤ 3,1 · 10⁻⁵ dovuto al solutore.
- **Benchmark indipendenti pubblicati**: nessuno, per ora.

## 12. Bibliografia

- DM 17 gennaio 2018, *Aggiornamento delle Norme tecniche per le costruzioni*, §4.1.2.2.5 e §4.1.8.1.5.
- Circolare 21 gennaio 2019 n. 7 C.S.LL.PP., §C4.1.2.2.5.
- EN 1992-1-1:2004 + AC:2010, §7.1 e §7.2.
- UNI EN 1992-1-1, appendice nazionale: DM 31 luglio 2012, §7.2.
- DIN EN 1992-1-1/NA, §7.2.
- DS/EN 1992-1-1 DK NA:2024, §7.2.
- NS-EN 1992-1-1, *Nasjonalt tillegg*, §7.2.
- fib, *fib Model Code for Concrete Structures 2010*, Ernst & Sohn, 2013, §7.6.
