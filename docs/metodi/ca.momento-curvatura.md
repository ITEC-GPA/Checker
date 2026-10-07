---
id: ca.momento-curvatura
titolo: Risposta momento-curvatura a sforzo normale costante
libreria: GPCChecker.Concrete
classi:
  - GPC.Checkers.Concrete.Response.MomentCurvatureAnalysis
  - GPC.Checkers.Concrete.Response.MomentCurvatureRequest
  - GPC.Checkers.Concrete.Response.MomentCurvatureResult
  - GPC.Checkers.Concrete.Response.MomentCurvaturePoint
  - GPC.Checkers.Concrete.Response.MomentCurvatureLimit
  - GPC.Checkers.Concrete.Response.MomentCurvatureStrains
  - GPC.Checkers.Concrete.Checkers.SectionCheckerModelCode2010
  - GPC.Checkers.Concrete.SectionSolvers.SectionSolver
versione: 0.0.17.0
norme:
  - ntc2018-4.1.2.1.2.1
  - ntc2018-4.1.2.1.2.2
  - ntc2018-4.1.2.3.4.1
  - ntc2018-4.1.2.3.4.2
  - circ2019-C4.1.2.3.4.2
  - en1992-1-1-3.1.7
  - en1992-1-1-3.2.7
  - en1992-1-1-6.1
stato: bozza
---

# Risposta momento-curvatura a sforzo normale costante

## 1. Scopo

Il metodo costruisce la curva momento-curvatura di una sezione in c.a. con sforzo normale N costante e momento in una
direzione assegnata del piano della sezione. La curva comprende:

- il ramo a momento crescente, da zero fino al punto limite del dominio di rottura a N costante;
- il primo snervamento, raffinato per bisezione;
- per ogni punto, le deformazioni del calcestruzzo e delle barre.

È una risposta numerica della sezione, non una verifica: il metodo non produce esiti e non confronta la curvatura con
una domanda.

## 2. Campo di applicazione

| Caso | Stato | Comportamento del programma |
| --- | --- | --- |
| Sezioni in c.a. ordinario di forma qualsiasi, con fori, barre comunque disposte | supportato | equilibrio della sezione con il solutore sezionale della libreria |
| Flessione retta e deviata (qualsiasi direzione θ), N di compressione, nullo o di trazione | supportato | momento proiettato sulla direzione (cos θ; sin θ) |
| Norme europee del verificatore sezionale (NTC 2018, EN 1992-1-1 e annessi, Model Code 2010, CNR-DT 200, CNR-DT 204, CS-TR34) | supportato | leggi dei materiali e coefficienti della classe di norma |
| Verificatore ACI 318 | non supportato in forma diretta | disponibile solo la variante generica, con funzioni di punto limite e di risposta fornite dal chiamante |
| Analisi tensionale lineare | non supportato | `ArgumentException`: serve l'analisi non lineare |
| Ramo di softening dopo il punto limite | non supportato | la curva termina al punto limite |
| Carico ciclico, scarico, storia di carico | non supportato | ogni punto è un'analisi indipendente da stato vergine |
| Tension stiffening, calcestruzzo confinato (NTC [4.1.8]-[4.1.12]) | non supportato | calcestruzzo teso nullo, salvo l'opzione del verificatore; nessun incremento di resistenza o di deformazione per confinamento |
| Leggi medie (fcm, Ecm) per l'analisi non lineare della struttura | non supportato direttamente | le leggi sono quelle di progetto della norma; i coefficienti si possono modificare sulla classe di norma (per esempio γc = γs = 1) |
| Bilinearizzazione, fattore di duttilità μφ (NTC §4.1.2.3.4.2), lunghezza di cerniera | non supportato | il metodo restituisce la curvatura al primo snervamento e al punto limite (riquadro M-1) |
| Sezioni precompresse | non verificato | il solutore tiene conto della predeformazione, ma il primo snervamento è valutato sulle deformazioni del piano senza predeformazione |

## 3. Riferimenti normativi

| Formula o grandezza | Norma | Edizione e appendice | Paragrafo | Eq. o tabella | Riscontro |
| --- | --- | --- | --- | --- | --- |
| Parabola-rettangolo: fcd, εc2 = 0,20%, εcu = 0,35% fino a C50/60; espressioni oltre C50/60; εc2 come deformazione ultima in compressione uniforme | NTC | 2018 | 4.1.2.1.2.1 | Fig. 4.1.1 | testo |
| Acciaio bilineare con εud = 0,9 εuk e incrudimento k = (ft/fy)k, oppure elastico-perfettamente plastico indefinito | NTC | 2018 | 4.1.2.1.2.2 | Fig. 4.1.3 | testo (riquadro M-2) |
| Ipotesi della flessione: sezioni piane, aderenza perfetta, resistenza a trazione del calcestruzzo nulla | NTC | 2018 | 4.1.2.3.4.1 | — | testo |
| MRd(NEd) e duttilità di curvatura μφ(NEd) come capacità della sezione; μφ = φu/φyd, φyd = (MRd/M'yd) φ'yd, φ'yd minore fra la curvatura allo snervamento dell'armatura tesa e quella alla deformazione di picco del calcestruzzo | NTC | 2018 | 4.1.2.3.4.2 | [4.1.18a], [4.1.18b] | testo (riquadro M-1) |
| Relazione momento-curvatura e fattore di duttilità di curvatura | Circolare | 2019 | C4.1.2.3.4.2 | Fig. C4.1.12 | testo |
| Parabola-rettangolo per il progetto delle sezioni | EN 1992-1-1 | 2004 + AC:2010 | 3.1.7 | (3.17), (3.18) | testo |
| Diagramma di progetto dell'acciaio; εud = 0,9 εuk raccomandato | EN 1992-1-1 | 2004 + AC:2010 | 3.2.7 | Fig. 3.8 | testo (riquadro M-2) |
| εud = 0,9 εuk; αcc = 0,85 | UNI EN 1992-1-1 | DM 31/07/2012 | 3.2.7(2), 3.1.6(1)P | — | testo |
| Sezioni piane, aderenza perfetta, resistenza a trazione del calcestruzzo trascurata, limiti di deformazione | EN 1992-1-1 | 2004 + AC:2010 | 6.1(2) | — | testo |

## 4. Ipotesi

- Conservazione delle sezioni piane: deformazione lineare ε(x, y) = ε0 + χx (x − x0) + χy (y − y0) nel piano della
  sezione, con ε0 la deformazione nel punto di riferimento (x0, y0) e χx, χy le componenti del gradiente lungo x e y.
  Il codice usa una propria convenzione per i segni dei gradienti.
- Aderenza perfetta fra barre e calcestruzzo.
- Leggi di progetto dei materiali della sezione, con i coefficienti della classe di norma:
  - calcestruzzo compresso: diagramma del materiale (parabola-rettangolo nei materiali europei), con tensioni
    moltiplicate per αcc/γc;
  - calcestruzzo teso: nullo, salvo l'opzione del verificatore;
  - acciaio: curva caratteristica nel campo elastico; oltre lo snervamento di progetto la curva caratteristica
    traslata lungo la retta elastica e abbassata di fyk − fyd (riquadro M-2); deformazione ultima di progetto
    εud = k · εuk (k = 0,9 nelle norme europee del modello).
- Nelle tabelle dei materiali europei il ramo parabolico è descritto da 9 punti fra 0 e εc2, con interpolazione
  lineare. Rispetto alla parabola esatta l'area del ramo diminuisce di 1/384 dell'area del rettangolo fcd · εc2
  (−0,39% dell'area del ramo). Sul risultante della zona compressa a εcu l'effetto è −0,18%.
- Sezione netta: nelle barre interne al calcestruzzo il solutore sottrae la tensione del calcestruzzo (σs − σc)·As.
  L'integrale del calcestruzzo è valutato su una mesh triangolare della sezione con punti di Gauss.
- Il punto limite è il punto del dominio di rottura plastico a N costante nella direzione del momento: rottura del
  calcestruzzo (εcu, εc2 in compressione uniforme) oppure dell'acciaio (εud).
- Primo snervamento: primo stato in cui il massimo modulo della deformazione delle barre raggiunge εy, dato del
  chiamante (di norma fyd/Es). Valgono anche le barre compresse.
- I punti sono analisi tensionali non lineari indipendenti a N costante: nessuna memoria di carico.

## 5. Notazione, unità e convenzioni

Unità: N, Nmm, mm, MPa; curvature in 1/mm. N è negativo in compressione (convenzione della libreria); nel solutore e
in (M.1) anche tensioni e deformazioni sono negative in compressione, mentre le leggi (M.2) sono scritte con la
compressione positiva (6.1). Nei punti della curva la deformazione del calcestruzzo è restituita come modulo della
compressione e quella delle barre come massimo modulo.

| Simbolo | Significato | Unità | Nel codice |
| --- | --- | --- | --- |
| N | sforzo normale costante (compressione negativa) | N | `MomentCurvatureRequest.AxialForce` |
| θ | direzione del momento negli assi delle forze del verificatore | ° | `DirectionDegrees` |
| n | numero di passi (10-500) | — | `Steps` |
| φend | frazione finale del momento limite, in (0; 1] | — | `EndFraction` |
| tolN | tolleranza su N al punto limite | N | `AxialTolerance` |
| ky | numero di bisezioni del primo snervamento (0-30) | — | `YieldRefinementSteps` |
| εy | deformazione di snervamento delle barre | — | argomento `steelYieldStrain` |
| Mx, My | componenti del momento negli assi del verificatore | Nmm | `MomentCurvaturePoint.Mx`, `My` |
| M | momento nella direzione θ | Nmm | `Moment` |
| Mr | momento limite nella direzione θ | Nmm | `MomentCurvatureResult.LimitMoment` |
| χx, χy | componenti del gradiente di deformazione | 1/mm | `GradientX`, `GradientY` |
| χ | curvatura, modulo del gradiente | 1/mm | `Curvature` |
| ε0 | deformazione nel punto di riferimento | — | `ReferenceStrain` |
| xr, yr | punto di riferimento dell'integrazione delle forze, negli assi della sezione | mm | `SectionSolver.IntegrationReferencePoint` |
| δi | 1 per la barra i interna al calcestruzzo, 0 altrimenti (sezione netta) | — | `SectionSolver.IntegrateRebarStress` |
| εc | modulo della massima compressione del calcestruzzo ai vertici del contorno | — | `ConcreteCompressionStrain` |
| εs | massimo modulo della deformazione delle barre | — | `SteelStrain` |
| χy,1, χu | curvatura al primo snervamento e al punto limite | 1/mm | `YieldCurvature`, `UltimateCurvature` |

## 6. Formulazione

### 6.1 Equilibrio e leggi dei materiali

Equilibrio della sezione (EN 6.1; NTC §4.1.2.3.4.1), per un piano di deformazione ε(x, y):

```math
N = \int_{A_c}\sigma_c\left(\varepsilon(x,y)\right)dA + \sum_i \left[\sigma_s(\varepsilon_i) - \delta_i\,\sigma_c(\varepsilon_i)\right]A_{s,i}, \qquad M_x = -\left\{\int_{A_c}\sigma_c\,(y - y_r)\,dA + \sum_i\left[\sigma_s - \delta_i\,\sigma_c\right]A_{s,i}\,(y_i - y_r)\right\}, \qquad M_y = \int_{A_c}\sigma_c\,(x - x_r)\,dA + \sum_i\left[\sigma_s - \delta_i\,\sigma_c\right]A_{s,i}\,(x_i - x_r) \qquad \text{(M.1)}
```

Qui δi = 1 per le barre interne al calcestruzzo (0 per le altre) e (xr, yr) è il punto di riferimento
dell'integrazione del solutore (`SectionSolver.IntegrationReferencePoint`).

**Convenzione di segno di (M.1)**, come in `SectionSolver`: tensioni e deformazioni sono negative in compressione,
quindi N è negativo in compressione, come nel §5; Mx positivo comprime le fibre con y > yr, My positivo quelle con
x < xr. Le componenti sono negli assi della sezione; le forze del verificatore, date negli assi `axes` della chiamata,
sono riportate dal verificatore in questi assi. Le leggi (M.2) sono invece scritte, come nelle norme, con la
compressione positiva: in (M.1) la tensione del calcestruzzo per ε ≤ 0 vale −σc(−ε) di (M.2) ed è nulla in trazione,
salvo l'opzione del verificatore; la legge dell'acciaio di (M.2) è dispari e vale con entrambe le convenzioni.

Leggi di progetto (NTC §4.1.2.1.2.1-2; EN (3.17), 3.2.7), con compressione positiva nelle espressioni:

```math
\sigma_c(\varepsilon) = \begin{cases} f_{cd}\left[1-\left(1-\varepsilon/\varepsilon_{c2}\right)^2\right] & 0 \le \varepsilon \le \varepsilon_{c2} \\ f_{cd} & \varepsilon_{c2} < \varepsilon \le \varepsilon_{cu} \end{cases}, \quad f_{cd} = \frac{\alpha_{cc}f_{ck}}{\gamma_c}; \qquad \sigma_s(\varepsilon) = \operatorname{sgn}(\varepsilon)\min\left(E_s|\varepsilon|;\ f_{yd}\right)\ \ \text{per}\ |\varepsilon| \le \varepsilon_{ud} \qquad \text{(M.2)}
```

L'espressione dell'acciaio vale per il ramo superiore orizzontale (acciaio senza incrudimento, come nell'esempio); con
incrudimento vale il riquadro M-2. Si ha εud = k · εuk con k = 0,9 (`SteelCoefficientStrainTension`).

> **Scostamento dichiarato — M-2 Ramo di incrudimento dell'acciaio di progetto**
>
> - Norma: EN 1992-1-1 3.2.7(2) e Fig. 3.8 (anche NTC Fig. 4.1.3(a)): ramo superiore inclinato da fyd in εyd fino a
>   k fyk/γs in εuk, con il limite εud; in alternativa ramo orizzontale senza limite di deformazione.
> - Programma (repository Model, `SteelMaterial`): oltre lo snervamento usa la curva caratteristica traslata lungo la
>   retta elastica e abbassata di fyk − fyd, σ(ε) = σk(ε + (fyk − fyd)/Es) − (fyk − fyd); l'incremento di
>   incrudimento è quindi riferito a fyk invece che a fyd.
> - Effetto: nessuno con acciaio senza incrudimento; con incrudimento il ramo di progetto supera quello della norma di
>   circa (k − 1)(fyk − fyd)(ε − εyd)/(εuk − εyd): per B450C con k = 1,15 ed εuk = 7,5% circa 8 MPa (2%) in εud, a
>   sfavore di sicurezza. Riguarda anche il dominio di rottura del solutore.
> - Stato: da discutere (nuovo).

### 6.2 Punto limite e campionamento

Punto limite a N costante nella direzione θ (dominio plastico) e momento limite nella direzione:

```math
\left(N_{lim}, M_{x,lim}, M_{y,lim}\right) = \text{dominio}(N, \theta), \qquad \left|N_{lim} - N\right| \le tol_N, \qquad M_r = M_{x,lim}\cos\theta + M_{y,lim}\sin\theta > 0 \qquad \text{(M.3)}
```

Campionamento del ramo crescente (passi i = 0…n):

```math
f_i = \varphi_{end}\left(\frac{i}{n}\right)^{2}\ \text{(campionamento quadratico)} \quad\text{oppure}\quad f_i = \varphi_{end}\,\frac{i}{n}, \qquad M_i = f_i\,M_r \qquad \text{(M.4)}
```

Per i < n, oppure φend < 1, il punto è l'analisi tensionale non lineare con (N; Mi cos θ; Mi sin θ). Per i = n e
φend = 1 il punto è lo stato di deformazione del punto limite. Curvatura e indicatori del punto:

```math
\chi = \sqrt{\chi_x^2 + \chi_y^2}, \qquad \varepsilon_s = \max_i\left|\varepsilon(x_i,y_i)\right|, \qquad \varepsilon_c = \max\left(0;\ -\min_{v}\varepsilon(x_v,y_v)\right), \qquad \text{snervato} \iff \varepsilon_s \ge \varepsilon_y \qquad \text{(M.5)}
```

### 6.3 Primo snervamento e curvature

Siano Ma l'ultimo campione non snervato e Mb il primo snervato. Si ripetono ky bisezioni su M:

```math
M_m = \frac{M_a + M_b}{2}; \qquad \text{se } \varepsilon_s(M_m) \ge \varepsilon_y\ \Rightarrow\ M_b = M_m,\ \text{altrimenti}\ M_a = M_m; \qquad \chi_{y,1} = \chi(M_b) \qquad \text{(M.6)}
```

Il punto Mb raffinato è inserito nella curva. La curvatura ultima è quella del punto limite, χu = χ(Mr).

> **Scostamento dichiarato — M-1 (R14) Curvature convenzionali**
>
> - Norma: NTC 2018 §4.1.2.3.4.2 definisce la duttilità di curvatura come rapporto fra la curvatura a cui corrisponde
>   una riduzione del 15% della massima resistenza a flessione, oppure il raggiungimento della deformazione ultima del
>   calcestruzzo o dell'acciaio, e la curvatura convenzionale di prima plasticizzazione φyd = (MRd/M'yd) φ'yd, con
>   φ'yd minore fra la curvatura allo snervamento dell'armatura tesa e quella alla deformazione di picco del
>   calcestruzzo (εc2 per la parabola-rettangolo); la Circolare 2019 la rappresenta nella Fig. C4.1.12.
> - Programma: non calcola il ramo dopo il picco; χu è la curvatura del punto limite del dominio (εcu oppure εud) e
>   χy,1 è la curvatura al primo snervamento di una barra, anche compressa, senza il confronto con εc2 e senza
>   bilinearizzazione.
> - Effetto: χu non tiene conto della parte di curva dopo il picco fino a 0,85 Mmax; il rapporto χu/χy,1 non coincide
>   con μφ e non va usato in sua vece senza la bilinearizzazione.
> - Stato: da discutere (voce R14 del registro delle differenze).

## 7. Coefficienti e valori predefiniti

| Simbolo | Valore | Fonte | Modificabile | Dove nel codice |
| --- | --- | --- | --- | --- |
| n | dato in [10; 500] | scelta del metodo | sì | `MomentCurvatureRequest` |
| φend | 1 | scelta del metodo | sì | `MomentCurvatureRequest` |
| campionamento | quadratico | scelta del metodo | sì (`QuadraticSampling`) | `MomentCurvatureRequest` |
| tolN | 1000 N | scelta del metodo | sì (`AxialTolerance`) | `MomentCurvatureRequest` |
| ky | 12 bisezioni (0-30) | scelta del metodo | sì (`YieldRefinementSteps`) | `MomentCurvatureRequest` |
| εy | dato, di norma fyd/Es | NTC §4.1.2.1.2.2; EN 3.2.7 | sì (argomento) | `MomentCurvatureAnalysis.Calculate` |
| αcc, γc, γs, εud/εuk | della classe di norma (NTC: αcc 0,85, γc 1,5, γs 1,15, 0,9) | NTC §4.1.2.1.1, §4.1.2.1.2.2; EN 3.1.6, 2.4.2.4, 3.2.7 | sì, sulla classe di norma | `StandardModelCode2010` e derivate |
| εc2, εcu | del materiale (0,002 e 0,0035 fino a C50/60) | NTC §4.1.2.1.2.1; EN prospetto 3.1 | sì, sul materiale | `ConcreteMaterialEuropeanCommon` |
| punti del ramo parabolico | 9 fra 0 e εc2 | tabella del materiale | no | `ConcreteMaterialEuropeanCommon` |
| calcestruzzo teso | trascurato | NTC §4.1.2.3.4.1; EN 6.1(2) | sì (opzione del verificatore) | `SectionOptions` |
| analisi richiesta | non lineare | requisito del metodo | no | `MomentCurvatureAnalysis.Calculate` |

## 8. Implementazione

Percorsi relativi alla radice del repository Checker (namespace `GPC.Checkers.Concrete.Response`, file
`GPCChecker.Concrete/Response/MomentCurvatureAnalysis.cs`), salvo i materiali del repository Model.

- `MomentCurvatureRequest` (righe 19-34), `MomentCurvatureStrains` (righe 37-55; `From` ricava εs e εc da un piano
  di deformazione, righe 48-54), `MomentCurvatureLimit` (righe 58-66), `MomentCurvaturePoint` (righe 69-82),
  `MomentCurvatureResult` (righe 88-99).
- `MomentCurvatureAnalysis.Calculate`, variante generica (righe 108-168), che riceve le funzioni del punto limite e
  della risposta:
  1. controlla i dati (righe 111-116); calcola la direzione (riga 117);
  2. ricava il punto limite e controlla il residuo su N e Mr > 0, (M.3) (righe 119-123);
  3. costruisce i punti, (M.5) (righe 125-132), con il campionamento (M.4) (righe 134-147); un errore di una risposta
     interrompe la curva e registra il passo (riga 146);
  4. raffina il primo snervamento per bisezione, (M.6) (righe 148-164); se la bisezione fallisce resta il primo
     campione snervato (riga 163);
  5. compone il risultato con N al punto limite, residuo e stato (righe 165-167).
- `MomentCurvatureAnalysis.Calculate` sul verificatore `SectionCheckerModelCode2010` (righe 174-197):
  - richiede l'analisi tensionale non lineare (righe 178-179);
  - imposta il dominio a N costante (righe 180-181);
  - punto limite dal dominio, con le componenti di direzione passate come momenti unitari (righe 182-188);
  - risposte dalle analisi tensionali, rifiutando i piani non finiti (righe 189-195).
- Solutore sezionale (`GPCChecker.Concrete/SectionSolvers/SectionSolver.cs`): integrazione del calcestruzzo sulla
  mesh con punti di Gauss (`IntegrateSectionStress`, righe 1048-1077); contributo delle barre con la sezione netta
  (σs − σc) (`IntegrateRebarStress`, righe 1119-1142). Leggi dei materiali europei in
  `GPCChecker.Concrete/SectionSolvers/SectionSolverModelCode2010.cs` (`CalculateSigmaC`, righe 110-123;
  `CalculateStressRebar`, righe 126-129).
- Materiali (repository Model, progetto GPCModel):
  - `Model/Materials/Concrete/ConcreteMaterialEuropeanCommon.cs`: tabelle del calcestruzzo (righe 756-783) e tensione
    di progetto αcc/γc (righe 734-740);
  - `Model/Materials/Steel/SteelMaterial.cs`: tensione di progetto dell'acciaio (righe 537-543, con
    `CalculateDesignStressCommon` alle righe 857-880) ed εud = k εuk (righe 500-506);
  - `Model/Materials/StressStrainTable.cs`: interpolazione lineare, tensione nulla oltre l'ultimo punto (righe
    189-204).

Il metodo accetta un `CancellationToken`. L'annullamento interrompe il calcolo e non è trattato come un errore della
curva.

## 9. Limiti e casi non supportati

- Nessun ramo dopo il picco, nessuna bilinearizzazione, nessun μφ (riquadro M-1).
- La curva usa le leggi di progetto: per una curva con valori medi o caratteristici occorre modificare i coefficienti
  della classe di norma.
- Con il campionamento quadratico i punti si addensano a momenti bassi; l'ultimo campione prima del limite è a
  ((n − 1)/n)² Mr (circa 0,95 Mr con n = 40). La parte finale della curva non è campionata oltre quel valore e il
  punto limite.
- Se l'analisi non lineare non converge vicino al limite, la curva si interrompe e restituisce il passo
  (`InterruptedAtStep`).
- Il punto limite rispetta N a meno di tolN; il residuo è restituito (`AxialResidual`). Con acciaio snervato e
  calcestruzzo plastico la traslazione del piano limite è definita solo entro questa tolleranza.
- Nelle sezioni non simmetriche la direzione della curvatura può differire da quella del momento: χ è il modulo del
  gradiente, le componenti sono restituite.
- Il primo snervamento conta anche le barre compresse; con N di compressione elevato può essere la barra compressa a
  snervare per prima. Se nessuna barra raggiunge εy prima del punto limite, χy,1 non è definita; se la curva si
  interrompe, χu non è definita.
- Calcestruzzo confinato, tension stiffening ed effetti viscosi non sono considerati.

## 10. Esempio numerico verificato

Sezione R300x500 dei casi congelati:

- contorno 300 × 500 mm;
- 3Ø20 inferiori in y = −202 mm (As1 = 942,48 mm², d = 452 mm);
- 2Ø16 superiori in y = 204 mm (As2 = 402,12 mm², d2 = 46 mm);
- calcestruzzo fck = 30 MPa, parabola-rettangolo con εc2 = 0,002 ed εcu = 0,0035;
- acciaio fyk = 450 MPa senza incrudimento (ft = fy), Es = 200000 MPa, εuk = 0,10.

Norma NTC 2018: fcd = 0,85 · 30/1,5 = 17 MPa, fyd = 391,30 MPa, εyd = 0,0019565, εud = 0,09. N = 0, momento che
comprime il lembo superiore (θ = 0° con gli assi del verificatore di versori (−1; 0; 0) e (0; −1; 0), pari a θ = 180°
con gli assi globali). n = 40, campionamento quadratico, 12 bisezioni, εy = fyd/Es.

Calcolo a mano del punto limite (calcestruzzo a εcu):

1. Coefficienti della parabola-rettangolo con a = εc2/εcu = 0,5714:
   - risultante ψ = 1 − a/3 = 0,80952;
   - posizione dal lembo compresso β2 = 1 − (1/2 − a²/12)/ψ = 0,41597.
2. Trazione T = As1 fyd = 942,48 · 391,30 = 368 795,7 N.
3. Equilibrio con la sezione netta: fcd b x ψ + As2 [σs(εs2) − σc(εs2)] = T, con εs2 = εcu (x − d2)/x. Per tentativi:
   - x = 68,0 mm: compressione 366 262 N;
   - x = 68,6 mm: 370 344 N;
   - soluzione x = 68,372 mm, con εs2 = 0,0011452 (σs2 = 229,05 MPa, σc = 13,895 MPa) ed
     εs1 = 0,0035 · (452 − 68,372)/68,372 = 0,019638 < εud: l'acciaio teso è snervato.
4. C = 17 · 300 · 68,372 · 0,80952 = 282 278 N a β2 x = 28,44 mm; Cs = 402,12 · (229,05 − 13,895) = 86 517 N.
5. Mr = C (d − β2 x) + Cs (d − d2) = 282 278 · 423,56 + 86 517 · 406 = 154 687 729 Nmm (154,69 kNm).
6. χu = εcu/x = 0,0035/68,372 = 5,1191 · 10⁻⁵ 1/mm.

Calcolo a mano del primo snervamento (εs1 = εyd, calcestruzzo nel ramo parabolico):

1. εc = εyd x/(d − x); η = εc/εc2. Risultante fcd b x (η − η²/3), con il baricentro a
   x [1 − (2η/3 − η²/4)/(η − η²/3)] dal lembo compresso.
2. Equilibrio con la sezione netta, per tentativi:
   - x = 151,0 mm: 367 124 N;
   - x = 151,7 mm: 370 838 N;
   - soluzione x = 151,315 mm, con εc = 0,00098459 ed η = 0,49229.
3. Cc = 317 565 N a 52,914 mm dal lembo; εs2 = 0,00068527; Cs = 402,12 · (137,05 − 9,654) = 51 231 N.
4. My,1 = 317 565 · (452 − 52,914) + 51 231 · 406 = 147 535 472 Nmm.
5. χy,1 = εyd/(d − x) = 0,0019565/300,685 = 6,5069 · 10⁻⁶ 1/mm.

Valori della libreria (GPCChecker.Concrete 0.0.17.0, eseguita):

| Grandezza | A mano | Libreria | Scarto |
| --- | --- | --- | --- |
| Mr [Nmm] | 154 687 729 | 154 674 877,6 | −0,008% |
| χu [1/mm] | 5,11906 · 10⁻⁵ | 5,10771 · 10⁻⁵ | −0,22% |
| εs1 al punto limite | 0,019638 | 0,019587 | −0,26% |
| My,1 (dopo 12 bisezioni) [Nmm] | 147 535 472 | 147 539 361 | +0,003% |
| χy,1 [1/mm] | 6,50688 · 10⁻⁶ | 6,51527 · 10⁻⁶ | +0,13% |
| εc al primo snervamento | 0,00098459 | 0,00098837 | +0,38% |
| N al punto limite [N] | 0 | −51,8 (residuo entro tolN = 1000 N) | — |
| numero di punti | 41 campioni + 1 raffinato | 42 | 0 |

Gli scarti residui hanno tre cause: il ramo parabolico tabellato a 8 tratti (−0,18% sul risultante a εcu),
l'integrazione sulla mesh e il residuo su N del punto limite.

Senza la sezione netta (calcestruzzo integrato anche al posto delle barre compresse) il calcolo a mano darebbe
x = 67,566 mm e χu = 5,1801 · 10⁻⁵ 1/mm (+1,4%). La sottrazione σc As delle barre interne fa quindi parte del modello.

In questo esempio il primo snervamento cade fra gli ultimi due campioni: 0,9506 Mr al passo 39, non snervato, e il
punto limite al passo 40.

## 11. Validazione

- **Casi congelati del motore precedente** (`curvature-legacy.csv` del progetto di test): 5 curve sulle sezioni di
  `detailing-sections.xml` (rettangolari, circolare e a T), con NTC 2018, EN 1992-1-1 e Model Code 2010, sforzi
  normali −200, 0, −1200, −500 e 0 kN e direzioni 0°, 180°, 30°, 45° e 0°; 210 punti in tutto. Tolleranze: 10⁻⁷ su
  momento limite, curvature, N e gradienti; sulle deformazioni 10⁻⁷ nei punti con acciaio elastico e 10⁻³ in quelli
  con acciaio snervato, perché la traslazione del piano è definita solo entro la tolleranza su N del solutore.
- **Test**: `DetailingMigrationTests.LegacyMomentCurvatureIsReproduced`.
- **Esempio di questa pagina**: eseguito con la libreria 0.0.17.0 e ricalcolato in modo indipendente (integrazione
  numerica della zona compressa con la parabola esatta), scarti nella tabella.
- **Benchmark indipendenti pubblicati**: nessuno, per ora.

## 12. Bibliografia

- DM 17 gennaio 2018, *Aggiornamento delle «Norme tecniche per le costruzioni»*, §4.1.2.1.2, §4.1.2.3.4.
- Circolare 21 gennaio 2019 n. 7 C.S.LL.PP., §C4.1.2.3.4.2, Fig. C4.1.12.
- EN 1992-1-1:2004 + AC:2010, *Eurocode 2: Design of concrete structures — Part 1-1: General rules and rules for
  buildings*, §3.1.7, §3.2.7, §6.1.
- UNI EN 1992-1-1, appendice nazionale: DM 31 luglio 2012.
