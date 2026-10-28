---
id: ca.colonna-modello
titolo: Colonna modello del pilastro in c.a. (effetti del secondo ordine nella sezione critica)
libreria: GPCChecker.Concrete
classi:
  - GPC.Checkers.Concrete.Slenderness.ModelColumnAnalysis
  - GPC.Checkers.Concrete.Slenderness.ModelColumnRequest
  - GPC.Checkers.Concrete.Slenderness.ModelColumnResult
  - GPC.Checkers.Concrete.Slenderness.ModelColumnImperfection
  - GPC.Checkers.Concrete.Slenderness.ModelColumnCreep
  - GPC.Checkers.Concrete.Slenderness.ModelColumnProfiles
  - GPC.Checkers.Concrete.Slenderness.ModelColumnBiaxialCheck
  - GPC.Checkers.Concrete.Checkers.SectionCheckerModelCode2010
versione: 0.27.0.0
norme:
  - ntc2018-4.1.2.3.4.2
  - ntc2018-4.1.2.3.9.2
  - ntc2018-4.1.2.3.9.3
  - ntc2018-11.2.10.7
  - en1992-1-1-5.2
  - en1992-1-1-5.8.3
  - en1992-1-1-5.8.4
  - en1992-1-1-5.8.6
  - en1992-1-1-5.8.8
  - en1992-1-1-5.8.9
  - en1992-1-1-6.1
  - na-uni-2012-5.2
stato: bozza
---

# Colonna modello del pilastro in c.a.

## 1. Scopo

Il metodo verifica un pilastro snello in c.a. con gli effetti del secondo ordine concentrati nella sezione critica
(«colonna modello»). Per uno sforzo normale NEd costante e una lunghezza libera l0 restituisce:

- la capacità M1Rd,max, il massimo momento del primo ordine sopportabile nella sezione critica, con il modo di crisi
  (instabilità oppure rottura della sezione);
- per un momento del primo ordine assegnato, la prima configurazione di equilibrio stabile: curvatura, spostamento e
  momento del secondo ordine, momento di progetto e rapporto di utilizzo;
- la snellezza e la snellezza limite, l'imperfezione, l'eccentricità minima e il coefficiente di viscosità efficace;
- a confronto, la curvatura nominale di EN 1992-1-1 5.8.8;
- la traccia dei passaggi in italiano per la relazione.

La flessione deviata si tratta con due colonne modello nelle direzioni principali e con il controllo di
EN 1992-1-1 5.8.9 (`ModelColumnBiaxialCheck`).

## 2. Campo di applicazione

| Caso | Stato | Comportamento del programma |
| --- | --- | --- |
| Elemento isolato (o assimilabile), sezione e sforzo normale costanti, sezione critica unica | supportato | l0, c e momento nella sezione critica dati dal chiamante |
| Sezioni in c.a. ordinario di forma qualsiasi, con fori, barre comunque disposte | supportato | solutore sezionale della libreria, leggi di progetto del verificatore |
| Flessione retta nella direzione θ | supportato | piani di deformazione con l'asse neutro parallelo al momento |
| Sezione non simmetrica rispetto al piano di flessione | rifiuto (oppure avviso) | momento trasversale oltre l'1 % del momento: `TransverseMomentBeyondLimit`, avviso con `RejectTransverseMoment = false` (riquadro K-4) |
| Flessione deviata | supportato con due analisi | (5.38) e (5.39) di EN 1992-1-1 5.8.9 |
| N di compressione o nullo | supportato | N = 0: nessun effetto del secondo ordine, avviso |
| N di trazione | rifiuto | `TensionAxialForce` |
| |NEd| oltre la resistenza a compressione centrata | rifiuto | `AxialForceBeyondResistance`, con NRd,c |
| NTC 2018, CNR-DT 200, EN 1992-1-1 (valori raccomandati), UNI EN 1992-1-1 con DM 31/07/2012 | supportato | profilo dalla classe di norma esatta |
| Altre norme (DS, DIN, NS, Model Code 2010, ACI) | rifiuto | `UnsupportedStandard` |
| Calcestruzzo fibrorinforzato, profili d'acciaio, sezioni senza barre | rifiuto | `UnsupportedSection` |
| Dominio elastico, calcestruzzo teso del verificatore | rifiuto | `UnsupportedChecker` (NTC §4.1.2.3.9.2: calcestruzzo teso trascurato) |
| Tension stiffening, legge (3.14) di EN 1992-1-1 per la rigidezza | non supportato | fase successiva |
| Inviluppo di più combinazioni, dominio ridotto N–M1Rd | non supportato | ripetere il calcolo; fase successiva |
| Telai, colonne a sezione o sforzo normale variabili | non supportato | metodo generale (integrazione delle curvature) |

## 3. Riferimenti normativi

| Formula o grandezza | Norma | Edizione e appendice | Paragrafo | Eq. o tabella | Riscontro |
| --- | --- | --- | --- | --- | --- |
| Analisi del secondo ordine con imperfezioni, viscosità, legami non lineari, calcestruzzo teso trascurabile | NTC | 2018 | 4.1.2.3.9.2 | — | testo |
| Snellezza λ = l0/i con i della sezione di calcestruzzo non fessurata; λlim = 25/√ν | NTC | 2018 | 4.1.2.3.9.2 | [4.1.41], [4.1.42] | testo |
| Colonna modello fra i metodi «basati sulla concentrazione dell'equilibrio nelle sezioni critiche», con rinvio a documenti di comprovata validità; difetto di rettilineità l/300 | NTC | 2018 | 4.1.2.3.9.3 | — | testo |
| Eccentricità minima e ≥ l0/200 e ≥ 20 mm nei pilastri compressi | NTC | 2018 | 4.1.2.3.4.2 | — | testo (riquadro K-1) |
| Coefficiente di viscosità φ(∞, t0) | NTC | 2018 | 11.2.10.7 | Tab. 11.2.VI-VII | testo |
| Circolare 2019 | Circolare | 2019 | — | — | non tratta snellezza né colonna modello (C4.1.2.3.9 assente) |
| Imperfezioni: θi = θ0 αh αm, ei = θi l0/2, ei = l0/400 | EN 1992-1-1 | 2004 + AC:2010 | 5.2(5)-(7) | (5.1), (5.2) | testo |
| θ0 = 1/200 | UNI EN 1992-1-1 | DM 31/07/2012 | 5.2(5) | — | estratto (valore raccomandato) |
| Snellezza limite λlim = 20 A B C/√n | EN 1992-1-1 | 2004 + AC:2010 | 5.8.3.1 | (5.13N) | testo; per UNI da riscontrare (riquadro K-5) |
| φef = φ(∞, t0) M0Eqp/M0Ed; condizioni per φef = 0 | EN 1992-1-1 | 2004 + AC:2010 | 5.8.4(2), (4) | (5.19) | testo (riquadro K-2) |
| Metodo generale; legame del calcestruzzo con deformazioni × (1 + φef); sezione critica con distribuzione di curvatura assunta | EN 1992-1-1 | 2004 + AC:2010 | 5.8.6(3), (4), (6) | — | testo |
| e2 = (1/r) l0²/c, c = 10 (8 con momento costante); curvatura nominale | EN 1992-1-1 | 2004 + AC:2010 | 5.8.8 | (5.31)-(5.37) | testo |
| Flessione deviata | EN 1992-1-1 | 2004 + AC:2010 | 5.8.9 | (5.38), (5.39) | testo |
| Eccentricità minima e0 = h/30 ≥ 20 mm | EN 1992-1-1 | 2004 + AC:2010 | 6.1(4) | — | testo (riquadro K-1) |
| Limiti di deformazione della sezione (εcu, εc2 al punto C, εud) | EN 1992-1-1 | 2004 + AC:2010 | 6.1(5) | Fig. 6.1 | testo |

Il metodo è quello di EN 1992-1-1 5.8.6(6), il solo testo con formule verificabili per la colonna modello; le NTC lo
nominano e rinviano a documenti di comprovata validità (§4.1.2.3.9.3), fra i quali l'Eurocodice.

## 4. Ipotesi

- Sezione critica unica, deformata della colonna con e2 = χ l0²/c (c = 10 ≈ π² per la deformata sinusoidale,
  c = 8 per curvatura costante): EN 5.8.8.2(3)-(4) applicata al metodo generale semplificato di 5.8.6(6).
- Curvatura dalla risposta della sezione a NEd costante: sezioni piane, aderenza perfetta, leggi di progetto del
  verificatore (parabola-rettangolo con fcd, εc2, εcu; acciaio delle barre con fyd ed εud), sezione netta,
  calcestruzzo teso nullo (NTC §4.1.2.3.9.2), nessun tension stiffening.
- Viscosità (EN 5.8.6(4)): tutte le deformazioni del legame del calcestruzzo per (1 + φef), comprese εc2 ed εcu;
  la legge dell'acciaio non cambia.
- Deformazioni ultime come nel dominio plastico della libreria: εcu' al lembo compresso, εc2' al punto C a
  (1 − εc2/εcu) h dal lembo compresso, εud e la deformazione ultima di compressione ai bordi delle barre.
- M0Ed è il momento del primo ordine nella sezione critica; per gli elementi controventati con momenti d'estremità
  diversi il chiamante dà il momento equivalente M0e (5.32) e verifica a parte M02 all'estremità.

## 5. Notazione, unità e convenzioni

Unità: N, mm, Nmm, 1/mm. N è negativo in compressione nella richiesta e nei risultati; nelle formule NEd è il
modulo della compressione.

| Simbolo | Significato | Unità | Nel codice |
| --- | --- | --- | --- |
| NEd | sforzo normale (compressione negativa) | N | `ModelColumnRequest.AxialForce` |
| θ | direzione del momento negli assi delle forze | ° | `DirectionDegrees` |
| l0, l | lunghezza libera di inflessione, lunghezza dell'elemento | mm | `EffectiveLength`, `MemberLength` |
| c | fattore della distribuzione di curvatura, [8; 10] | — | `CurvatureFactor` |
| M0Ed | momento del primo ordine senza imperfezione (vuoto: sola capacità) | Nmm | `FirstOrderMoment` |
| ei, e0 | eccentricità dell'imperfezione e minima | mm | `ModelColumnImperfectionResult.Eccentricity`, `ModelColumnFirstOrder.MinimumEccentricity` |
| M0Ed,tot | momento totale del primo ordine | Nmm | `ModelColumnFirstOrder.TotalMoment` |
| φef | coefficiente di viscosità efficace | — | `ModelColumnCreepResult.EffectiveRatio` |
| χ | curvatura nella direzione del momento | 1/mm | `ModelColumnPoint.Curvature` |
| M(χ) | momento della sezione a NEd nella direzione θ | Nmm | `ModelColumnPoint.Moment` |
| k | NEd l0²/c, M2 = k χ | N·mm² | `ModelColumnResult.SecondOrderStiffness` |
| M1Rd,max, χ*, e2* | capacità, curvatura e spostamento del massimo | Nmm, 1/mm, mm | `ModelColumnCapacity` |
| χeq, e2, M2, MEd | equilibrio con M0Ed | 1/mm, mm, Nmm | `ModelColumnEquilibrium` |
| χu | curvatura ultima della sezione a NEd | 1/mm | `ModelColumnCapacity.UltimateCurvature` |
| w | distanza lungo la curvatura, w = (y − y0) cos ϑ − (x − x0) sin ϑ | mm | piano ε = ε0 − χ w |

## 6. Formulazione

### 6.1 Sezione a curvatura imposta

Per la direzione θ del momento, ϑ è la stessa direzione negli assi del solutore. Il piano di deformazione con
curvatura χ ≥ 0 è ε(w) = ε0 − χ w, con l'asse neutro parallelo al vettore momento. Per ogni χ si cerca ε0 tale che

```math
N(\varepsilon_0, \chi) = N_{Ed}, \qquad \varepsilon_0 \in \left[\max\left(\varepsilon_{cu}' + \chi w_{max};\ \varepsilon_{c2}' + \chi w_C;\ \varepsilon_{uc,i} + \chi (w_i + r_i)\right);\ \min_i\left(\varepsilon_{ud,i} + \chi (w_i - r_i)\right)\right] \qquad \text{(K.1)}
```

con εcu' = εcu (1 + φef), εc2' = εc2 (1 + φef), wC = wmax − (1 − εc2/εcu)(wmax − wmin), ri raggio della barra i.
N cresce con ε0, quindi la radice si trova con la regula falsi (Illinois) fra i limiti; se nessun piano ammissibile
porta NEd la curvatura è oltre χu. La curvatura ultima χu è la più grande con un piano ammissibile, cercata per
raddoppio e bisezione.

Resistenze del piano (sezione netta, calcestruzzo con le deformazioni divise per 1 + φef):

```math
N = \int_{A_c}\sigma_c\!\left(\frac{\varepsilon}{1+\varphi_{ef}}\right)dA + \sum_i\left[\sigma_s(\varepsilon_i) - \delta_i\,\sigma_c\!\left(\frac{\varepsilon_i}{1+\varphi_{ef}}\right)\right]A_{s,i}, \qquad M = \ldots \qquad \text{(K.2)}
```

Con φef = 0 è l'integrazione del dominio di rottura, bit per bit.

### 6.2 Capacità e modo

```math
M_{1Rd,max} = \max_{0 \le \chi \le \chi_u}\left[M(\chi) - N_{Ed}\,\frac{l_0^2}{c}\,\chi\right], \qquad e_2^* = \chi^*\frac{l_0^2}{c} \qquad \text{(K.3)}
```

Modo: instabilità se il massimo è prima di χu (punto limite dell'equilibrio), rottura della sezione se è a χu.

> **Scostamento dichiarato — K-6 Modo vicino alla curvatura ultima**
>
> - Norma: EN 5.8.6(6) non distingue i modi; la colonna modello classica chiama instabilità il massimo interno.
> - Programma: rottura della sezione solo se χu − χ* ≤ 1e-9 χu.
> - Effetto: con la sezione netta M(χ) è quasi piatto vicino a χu e il massimo resta interno anche per colonne tozze
>   (400 × 400, NEd = 500 kN: interno a l0 = 1 m, a χu a l0 = 0,5 m); M1Rd,max differisce comunque da MRd(N) di meno
>   dello 0,6 % a l0 = 1 m e dello 0,16 % a l0 = 0,5 m.
> - Stato: `dichiarato`.

### 6.3 Momento del primo ordine, imperfezione ed eccentricità minima

```math
\theta_i = \theta_0\,\alpha_h\,\alpha_m,\ \ \alpha_h = \frac{2}{\sqrt{l}} \in \left[\tfrac{2}{3}; 1\right],\ \ \alpha_m = \sqrt{0{,}5\left(1 + \tfrac{1}{m}\right)},\ \ e_i = \theta_i\frac{l_0}{2}; \qquad M_{0Ed,tot} = \max\left(M_{0Ed} + N_{Ed}\,e_i;\ N_{Ed}\,e_0\right) \qquad \text{(K.4)}
```

Imperfezioni: EN 5.2(7)a per default con θ0 = 1/200 (anche per le NTC, che rinviano ai documenti di comprovata
validità), oppure l0/400 (EN 5.2(7)), l/300 (NTC §4.1.2.3.9.3, telai a nodi fissi), un valore assegnato, nessuna.
L'imperfezione agisce nella direzione del momento (sfavorevole); in flessione deviata il chiamante la mette nella
sola direzione più sfavorevole (EN 5.8.9(2)). e0 = max(l0/200; 20 mm) per le NTC, max(h/30; 20 mm) per l'EN.

> **Scostamento dichiarato — K-1 Eccentricità minima come minimo del totale**
>
> - Norma: NTC §4.1.2.3.4.2 chiede per i pilastri compressi MEd = e NEd con e ≥ l0/200 e ≥ 20 mm nella verifica della
>   sezione; EN 6.1(4) dà e0 = h/30 ≥ 20 mm come minimo del progetto della sezione (nota a 5.2(1)).
> - Programma: il minimo vale sul momento totale del primo ordine, M0Ed,tot = max(M0Ed + NEd ei; NEd e0), non è sommato
>   all'imperfezione.
> - Effetto: nessuna doppia imperfezione; con momenti piccoli governa e0 (400 × 400, l0 = 12 m: e0 = 60 mm NTC contro
>   ei = 24,5 mm).
> - Stato: `dichiarato` (decisione Q3 del progetto del 28/10/2026).

### 6.4 Viscosità

φef assegnato oppure dalla (5.19), con l'imperfezione in entrambi i momenti del primo ordine:

```math
\varphi_{ef} = \varphi(\infty, t_0)\,\frac{M_{0Eqp} + N_{Eqp}\,e_i}{M_{0Ed} + N_{Ed}\,e_i} \qquad \text{(K.5)}
```

Con φef = 0 il programma controlla le condizioni di 5.8.4(4) (φ(∞, t0) ≤ 2, λ ≤ 75, M0Ed/NEd ≥ h) e avvisa se non sono
dimostrate (anche quando φ(∞, t0) non è noto).

> **Scostamento dichiarato — K-2 Imperfezione nei momenti della (5.19)**
>
> - Norma: (5.19) con M0Eqp e M0Ed momenti del primo ordine; 5.8.8.2(1) comprende le imperfezioni nel momento del primo
>   ordine, 5.8.4 non lo precisa.
> - Programma: entrambi i momenti con l'imperfezione ei (stessa geometria, sforzi normali delle due combinazioni),
>   senza l'eccentricità minima.
> - Effetto: φef definito anche con M0Ed = 0; per momenti dominati dall'imperfezione il rapporto tende a NEqp/NEd.
> - Stato: `dichiarato`.

### 6.5 Equilibrio con M0Ed

```math
M(\chi_{eq}) = M_{0Ed,tot} + N_{Ed}\frac{l_0^2}{c}\chi_{eq},\ \ 0 \le \chi_{eq} \le \chi^*; \qquad e_2 = \chi_{eq}\frac{l_0^2}{c},\ \ M_2 = N_{Ed}\,e_2,\ \ M_{Ed} = M_{0Ed,tot} + M_2,\ \ \eta = \frac{M_{0Ed,tot}}{M_{1Rd,max}} \qquad \text{(K.6)}
```

Prima intersezione stabile (ramo crescente di M − M2); senza intersezione (M0Ed,tot > M1Rd,max) non c'è equilibrio:
instabilità, non verificato.

### 6.6 Snellezza (informativa)

λ = l0/i con i della sezione lorda di calcestruzzo nella direzione di flessione; NTC [4.1.41] λlim = 25/√ν; EN (5.13N)
λlim = 20 A B C/√n con A = 1/(1 + 0,2 φef), B = √(1 + 2ω), C = 1,7 − rm (0,7 se rm non è dato). Il calcolo si esegue
comunque; λ ≤ λlim è riportato come «effetti del secondo ordine trascurabili» per la norma del profilo.

> **Scostamento dichiarato — K-5 Snellezza limite del profilo UNI**
>
> - Norma: λlim è un parametro nazionale di EN 5.8.3.1(1); l'appendice di DM 31/07/2012 non è stata letta per intero.
> - Programma: per il profilo UNI la (5.13N) con i valori raccomandati, come per l'EN.
> - Effetto: solo informativo, il calcolo non dipende da λlim.
> - Stato: `da riscontrare`.

### 6.7 Curvatura nominale (confronto informativo)

EN 5.8.8: n = NEd/(Ac fcd), ω = As fyd/(Ac fcd), Kr = (1 + ω − n)/(1 + ω − 0,4) ≤ 1 (5.36), β = 0,35 + fck/200 − λ/150,
Kφ = 1 + β φef ≥ 1 (5.37), d = h/2 + is (5.35), 1/r0 = εyd/(0,45 d), 1/r = Kr Kφ/r0 (5.34), e2 = (1/r) l0²/c, M2 = NEd e2
(5.33), MRd(NEd) dal dominio della libreria nella direzione θ, MEd = M0Ed,tot + M2 (5.31). Kr è limitato anche
inferiormente a 0.

### 6.8 Flessione deviata

Con due risultati nelle direzioni principali (stesso NEd): (5.38a) λ1/λ2 ≤ 2 e λ2/λ1 ≤ 2; (5.38b)
(e1/heq,1)/(e2/heq,2) ≤ 0,2 o l'inverso, ej = MEd,j/NEd con il secondo ordine, heq,j = ij √12. Se valgono entrambe
bastano le due verifiche; altrimenti (5.39) (MEd,1/MRd,1)^a + (MEd,2/MRd,2)^a ≤ 1, a = 2 per sezioni circolari ed
ellittiche, altrimenti 1,0 / 1,5 / 2,0 per NEd/NRd = 0,1 / 0,7 / 1,0 con NRd = Ac fcd + As fyd.

> **Scostamento dichiarato — K-3 Esponente delle sezioni non rettangolari**
>
> - Norma: (5.39) dà a per sezioni circolari ed ellittiche e per sezioni rettangolari.
> - Programma: per le altre forme l'esponente delle rettangolari.
> - Effetto: a ≤ 2, quindi a favore di sicurezza rispetto ad a = 2.
> - Stato: `dichiarato`.

> **Scostamento dichiarato — K-4 Momento trasversale**
>
> - Norma: EN 5.8.9(2) tratta separatamente le direzioni principali.
> - Programma: piani con l'asse neutro parallelo al momento; il rapporto max|M⊥|/max|M| sui punti della curva oltre
>   `TransverseMomentLimit` (1 %) rifiuta il calcolo, oppure lo segnala con `RejectTransverseMoment = false`.
> - Effetto: nelle sezioni simmetriche rispetto al piano di flessione il rapporto è nullo (prove: quadrato con 8 barre
>   a 0/90/180/270°, 72-gono con 12 barre a 30°); fuori dalle direzioni principali il calcolo non è una flessione retta.
> - Stato: `dichiarato`; la ricerca dell'asse neutro che annulla il momento trasversale è rinviata.

## 7. Coefficienti e valori predefiniti

| Simbolo | Valore | Fonte | Modificabile | Dove nel codice |
| --- | --- | --- | --- | --- |
| c | 10, in [8; 10] | EN 5.8.8.2(4) | sì | `ModelColumnRequest.CurvatureFactor` |
| imperfezione | EN 5.2(7)a, θ0 = 1/200, m = 1 | EN 5.2(5)-(7); DM 31/07/2012 | sì | `ModelColumnImperfection`, `ModelColumnProfiles.BaseInclination` |
| eccentricità minima | della norma del profilo | NTC §4.1.2.3.4.2; EN 6.1(4) | sì | `ModelColumnMinimumEccentricity` |
| φef | 0 con controllo di 5.8.4(4) | EN 5.8.4 | sì | `ModelColumnCreep` |
| rm | non noto (C = 0,7) | EN 5.8.3.1(1) | sì | `EndMomentRatio` |
| passi di curvatura | 200, in [10; 500] | scelta del metodo | sì | `Steps` |
| tolleranza su N di MRd(N) | 1000 N | scelta del metodo | sì | `AxialTolerance` |
| limite del momento trasversale | 1 % | scelta del metodo | sì | `TransverseMomentLimit`, `RejectTransverseMoment` |
| tolleranza dell'equilibrio su N | 1e-11 (Ac fcd + As fyd) | scelta del metodo | no | `ModelColumnEngine.Run` |

## 8. Implementazione

File `GPCChecker.Concrete/Slenderness/`: `ModelColumnRequest.cs` (richiesta, profili, imperfezione, viscosità),
`ModelColumnResult.cs` (risultati, traccia, rifiuti), `ModelColumnAnalysis.cs` (validazione e calcolo),
`ModelColumnBiaxialCheck.cs` (5.8.9).

1. `ModelColumnAnalysis.Calculate`: validazione, profilo (`ModelColumnProfiles.TryResolve`, classe esatta), controlli di
   verificatore e sezione, rifiuto della trazione.
2. `ModelColumnEngine.Directions`: direzione ϑ negli assi del solutore e rotazione dei momenti negli assi delle forze.
3. Sezione lorda (poligoni del contorno e dei fori), snellezza, imperfezione, momento del primo ordine, viscosità.
4. `Solve`: (K.1) con Illinois su ε0 (al più 200 iterazioni, |N − NEd| ≤ 1e-11 (Ac fcd + As fyd)); `Integrate`: (K.2),
   con φef = 0 `SectionSolver.CalculateForceResultantForDomain`.
5. `UltimateCurvature`: raddoppio da (|εcu'| + εud)/h/16, poi 64 bisezioni.
6. Campionamento uniforme di [0; χu] con `Steps` passi, primo snervamento di una barra (|ε| ≥ εyd, anche compressa)
   inserito con 50 bisezioni, rapporto del momento trasversale.
7. Massimo di M − kχ: miglior campione e sezione aurea fra i campioni vicini (80 iterazioni).
8. `Equilibrium`: prima radice di M − kχ = M0Ed,tot sui campioni fino a χ*, 100 bisezioni.
9. `Nominal` e `Resistance`: curvatura nominale e MRd(NEd) con `SectionSolver.CalculateConvergedDomainPoint` a N
   costante.

## 9. Limiti e casi non supportati

- Sezione critica unica, N e sezione costanti, l0 dato dal chiamante (per i telai EN (5.15)-(5.16)).
- Solo la parabola-rettangolo di progetto per la rigidezza; la (3.14) con fcd ed Ecd = Ecm/γCE (5.8.6(3)) è rinviata.
- L'acciaio con incrudimento segue la legge di Model (riquadro M-2 di ca.momento-curvatura).
- Nessun inviluppo di combinazioni, nessun dominio ridotto N–M1Rd.
- Il primo snervamento comprende le barre compresse, come la curva momento-curvatura.

## 10. Esempio numerico verificato

Sezione 400 × 400, 3Ø24 + 3Ø24 a y = ±160 mm, C25/30 (fcd = 14,167 MPa), B450C (fyd = 391,30 MPa, Es = 200000 MPa),
NTC 2018, NEd = −500 kN, l0 = 12 m, l = 6 m, c = 10, nessuna imperfezione né eccentricità minima, φef = 0,
M0Ed = 137,5 kNm.

| Grandezza | A mano / riferimento | Libreria 0.27.0.0 |
| --- | --- | --- |
| i, λ | 115,47 mm, 103,92 | 115,47 mm, 103,92 |
| ν, λlim NTC | 0,2206, 25/√ν = 53,23 | 0,2206, 53,23 |
| k = NEd l0²/c | 7,2e12 N·mm² | 7,2e12 N·mm² |
| χu, M(χu) | 3,0922e-5 1/mm, 246,264 kNm (Python) | 3,0922e-5 1/mm, 246,264 kNm; MRd(NEd) del dominio 246,264 kNm |
| χ* = χy, e2* | 1,0699e-5 1/mm, 154,07 mm (Python) | 1,0699e-5 1/mm, 154,07 mm |
| M1Rd,max | 156,083 kNm (Python) | 156,083 kNm, instabilità |
| χeq, e2, MEd | 8,8318e-6 1/mm, 127,18 mm, 201,089 kNm (Python) | 8,8318e-6 1/mm, 127,18 mm, 201,089 kNm |
| η | 137,5/156,083 = 0,881 | 0,881, verificato |
| curvatura nominale: Kr, 1/r0, e2, M2 | 1, 1,20773e-5 1/mm, 173,91 mm, 86,957 kNm | gli stessi |

Passaggi: per χ = χ* il piano porta la barra tesa a εyd = 1,957 ‰ (primo snervamento) con il lembo compresso a
1,895 ‰; M(χ*) = 233,12 kNm, M2* = k χ* = 77,03 kNm, M1Rd,max = 156,08 kNm. Con la sezione lorda il riferimento dà
157,39 kNm (−0,8 % per la sezione netta).

## 11. Validazione

`GPCChecker.Test.Concrete/ModelColumnTests.cs` (16 prove), con il riferimento indipendente
`ReferenceCalculations/model-column/derive_model_column_oracles.py` (Python, libreria standard, nessun codice GPC) e
due esempi esterni (provenienza nel README della cartella):

- caso R400-3+3D24-l0-12: M a 11 curvature entro 0,03 % dal riferimento, M1Rd,max −0,0002 %, contro l'esempio esterno
  −0,74 % (sezione netta); equilibrio a 137,5 kNm verificato come l'esempio;
- caso R500-6+6D24-N1200, l0 = 8 / 13 / 18 m: entro 0,0001 % dal riferimento, +0,4 / +1,1 / +2,9 % dalla lettura grafica
  dell'esempio esterno (tolleranza 5 %);
- casi limite: l0 = 0,5 m (rottura della sezione, M1Rd,max entro 0,16 % da MRd(N)), N = 0, NEd = 2000 e 3200 kN, rifiuto
  oltre NRd,c, c = 8, φef = 1 e 2, imperfezioni e minimi in forma chiusa, (5.19) e 5.8.4(4);
- proprietà: M1Rd,max decrescente con l0 e con φef; simmetrie (0/90/180/270°, 30° sul 72-gono); momento trasversale;
  percorso viscoso a φef = 0 identico bit per bit; flessione deviata (5.38)/(5.39).

## 12. Bibliografia

- DM 17/01/2018, Norme tecniche per le costruzioni, §4.1.2.3.4.2, §4.1.2.3.9, §11.2.10.7.
- EN 1992-1-1:2004 + AC:2010, 5.2, 5.8, 6.1.
- DM 31/07/2012, Appendice nazionale di UNI EN 1992-1-1 (5.2(5)).
