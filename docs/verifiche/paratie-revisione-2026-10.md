# Revisione del modulo paratie (ottobre 2026)

Revisione dei calcoli del modulo paratie sviluppato con Codex sul modello di Paratie Plus: molle elasto-plastiche
alla Winkler, fasi costruttive, sostegni, filtrazione, verifiche idrauliche, stabilità globale, tiranti, infissione,
verifiche strutturali e collegamento in ANTHEA. Codice esaminato: Checker master `57f87bf8`, Model e ANTHEA del 21/10/2026.
Branch dei test: `claude/verifica-paratie`. Correzioni applicate sullo stesso branch: P1, P2 e P3 in Geotechnics 0.14.0.1
(con GPC.Design 0.30.0.1, ricompilato contro la nuova Geotechnics). A1 è corretto in ANTHEA (`refactoring/f5` 573d9c73,
guide Rev57), con i test `LockOffBetweenStagesTests` in Checker. Restano aperti P4, P5, S1, A2–A4 e i punti minori ANTHEA.

## Metodo

1. **Lettura del motore** (`src/Geotechnics/EmbeddedWalls`) riga per riga: elementi Hermite, punti di Gauss, legge
   elasto-plastica, interpolazione delle fasi, sostegni, recupero di M e V, inviluppi, coefficienti NTC.
2. **Risolutore indipendente** (`tools/attesi/paratie_indipendente.py`): stessa legge documentata in `docs/metodi/paratie.md`,
   ma implementazione diversa (molle concentrate ai nodi, rigidezza esatta della trave, pressioni calcolate a parte,
   ricerca lineare sull'energia potenziale). Due mesh (50 e 25 mm) e valore di Richardson `2·f25 − f50`.
3. **Equilibrio limite** (appoggio libero) per verificare l'esistenza della soluzione nel caso tirantato.
4. **Quattro revisioni in parallelo**, solo in lettura, con calcoli a mano in Python: filtrazione e verifiche idrauliche;
   stabilità globale, tiranti e infissione; verifiche strutturali; collegamento ANTHEA (unità, fasi, segni).
5. **Test di libreria** che ripercorrono ogni verifica. Quelli che documentano un bug descrivono il comportamento corretto
   con un atteso indipendente: oggi falliscono e devono passare dopo la correzione.

Gli attesi non vengono mai dal codice C#: vengono da formule chiuse, dal risolutore indipendente o dagli script della revisione
(`tools/attesi/paratie_revisione`). Solo `stabilita_globale/port_check.py` è un port del C#, usato per prevedere l'esito attuale.

## Esito in sintesi

Il nucleo del calcolo è corretto quando converge. Su quattro casi tipo il motore coincide con il risolutore indipendente
entro lo 0,1 % (spostamenti, momenti, forze nei tiranti). Pressioni di Rankine, acqua, sovraccarichi, legge non drenata,
segni, conversioni di unità e coefficienti NTC sono giusti.

I problemi trovati, in ordine di gravità:

| # | Problema | Effetto | Dove |
|---|---|---|---|
| A1 | In ANTHEA una fase successiva che rielenca un tirante riparte con pretensione 0 e la rilascia; la guida teorica dice il contrario | **a sfavore di sicurezza**: forza nel tirante sottostimata fino alla pretensione — **corretto in ANTHEA** (`refactoring/f5` 573d9c73) | `ANTHEA.Application/EmbeddedWalls/EmbeddedWallAdapter.cs:64-65`, `EmbeddedWallDocument.cs:35`, `guida-teorica-anthea.md:3538` (prima della correzione) |
| P1 | Puntone o tirante che si stacca: il motore lo fa "seguire" la paratia invece di lasciare il distacco | risultati errati quando la paratia torna indietro (esempio: 15,7 kN invece di 7,8 kN, −2,03 mm invece di +0,78 mm) — **corretto in 0.14.0.1** | `EmbeddedWallAnalysis.cs:250-252` (su `57f87bf8`) |
| P2 | Falsa non convergenza: la ricerca lineare usa il massimo del residuo e si blocca sui punti angolosi della plasticità | paratia stabile dichiarata "non convergente" (caso tirantato con margine 1,17) — **corretto in 0.14.0.1** | `EmbeddedWallAnalysis.cs:91` (su `57f87bf8`) |
| P3 | Tolleranze più strette della precisione di macchina sui termini EI | falsa non convergenza con mesh fini (50 mm) o pareti rigide — **corretto in 0.14.0.1** | `WallAnalysisResults.cs:10-11`, `EmbeddedWallAnalysis.cs:80` (su `57f87bf8`) |
| P4 | Stabilità globale: il profilo di pressioni interstiziali assegnato viene ignorato | **a sfavore di sicurezza** (F = 5,94 invece di 4,44); oggi ANTHEA non ci arriva | `EmbeddedWallGlobalStability.cs:130-139` |
| P5 | Filtrazione: il bordo laterale lato scavo ha sempre carico imposto; con la semilarghezza di uno scavo simmetrico l'asse fa da dreno | **a sfavore di sicurezza**: gradiente medio 0,22 invece di 0,55, verifica HYD 0,65 invece di 1,59 | `EmbeddedWallSeepageMesh.cs:49` |
| A2 | Nessun coefficiente parziale applicato alle azioni o agli effetti; le verifiche SLU si attivano con un testo libero | azioni SLU 23–33 % troppo basse se si marca SLU una storia caratteristica | `EmbeddedWallModule.cs:30` e verificatori ANTHEA |
| S1 | Le verifiche di pali, tubi e diaframmi non confrontano la larghezza passata con quella dell'analisi | azioni scalate male se il chiamante sbaglia; oggi ANTHEA passa quella giusta | `EmbeddedWallRcPiles.cs:48,75`, `EmbeddedWallSteelTubes.cs:41,66`, `EmbeddedWallConcrete.cs:71,90` |
| A3 | Una fase nuova in ANTHEA riparte da terreno e falda a quota 0 | rischio di "riempire" lo scavo per errore | `EmbeddedWallDocument.cs:33-34` |
| A4 | Prima fase non geostatica: lo squilibrio iniziale non viene applicato per incrementi | possibile non convergenza e risultato diverso da una storia a fasi | `EmbeddedWallAnalysis.cs:33` |
| S2 | Taglio dei pali: d dipende da un arrotondamento (una barra a 180° cade nella metà sbagliata); modello bw = D non documentato | −6 % sul taglio resistente (a favore di sicurezza) | `PileReinforcement.cs:208-211,227` |
| S3 | Passo delle barre per la fessurazione preso sulla faccia più rada | a favore di sicurezza | `WallSectionSession.cs:215` |
| — | Punti minori ANTHEA: verso resistente ±1 non spiegato, segni diversi fra le schede, etichette senza "per fascia", schema globale sempre con valle a sinistra, "1.000" letto come 1 | interpretazione dei risultati | vedi § ANTHEA |

## Verifiche superate

| Verifica | Atteso indipendente | Risultato | Test |
|---|---|---|---|
| Pressioni: σv, u, σ'v, K0, Ka, Kp con falda e sovraccarico; legge non drenata σv ∓ 2cu | aritmetica a mano | scarto ≤ 1e-16 MPa | `IndependentVerificationTests.RankineLimitsWaterAndSurchargeMatchHandArithmetic` |
| Mensola in sabbia asciutta, scavo 2 e 4 m | risolutore indipendente | vedi tabella numerica | `CantileverInSandMatchesIndependentSolver` |
| Mensola in argilla non drenata, scavo 3 m | risolutore indipendente | vedi tabella numerica | `UndrainedCantileverMatchesIndependentSolver` |
| Paratia tirantata con falda, fasi fino a −2 m con tirante pretesato | risolutore indipendente | vedi tabella numerica | `AnchoredWallBeforeTheDeepExcavationMatchesIndependentSolver` |
| Sostegno bilaterale caricato nei due versi | H/(3EI/L³ + EA/L) | esatto | `BilateralSupportReloadsWithCantileverAndSupportInParallel` |
| Stabilità globale, cerchio asciutto, azioni ×1 e ×1,3, con falda | Bishop con 400 000 conci | F = 5,9407 / 5,7934 / 4,4414, scarto < 0,1 % | `GlobalStabilityIndependentTests` |
| Filtrazione sotto palancola in strato finito | Harr: Q/kH = 0,5, carico H/2 sotto la punta | +2,1 % con mesh 500 mm; 2000 mm | `SeepageIndependentTests.SheetPileInFiniteLayerMatchesHarr` |
| Van Genuchten-Mualem | aritmetica a 50 cifre | 1e-10 relativo | `VanGenuchtenMualemMatchesFiftyDigitArithmetic` |
| Profilo interstiziale con zero inserito e peso saturo | aritmetica a mano | 19,2 kPa (19,5 senza lo zero) | `ProfileZeroCrossingControlsTheSaturatedWeight` |
| Gradiente di efflusso NTC 2018 (γR 3 e 2) | ic = (γsat − γw)/γw | esatto | `FreeOutflowGradientsUseNtc2018ResistanceFactors` |
| Taglio senza staffe del diaframma, NTC (4.1.23) | formula chiusa | esatto | `EmbeddedWallReviewTests.StripShearWithoutLinksMatchesNtc2018Formula41` |

Verificati dai revisori e già coperti da test esistenti (nessun test nuovo): momento resistente di diaframmi, pali circolari
e tubi CHS (integrazione a fibre indipendente, scarto < 0,3 %); taglio dei tubi e stabilità dei puntoni; sfilamento dei tiranti
(Bustamante-Doix, ξa3/ξa4 e γR NTC tab. 6.6.II-III); infissione (Hetenyi, ricerca esaustiva); sollevamento UPL
(G1 1,1/0,9, G2 1,5/0,8, Q 1,5/0); sifonamento di fondo non drenato (Terzaghi con sovraccarico); conversioni di unità
di ANTHEA (m→mm, kN→N, kNm→Nmm, kPa→MPa, kN/m³→N/mm³, gradi→radianti).

### Confronto numerico motore / risolutore indipendente

Motore Geotechnics 0.14.0.1 con mesh 250 mm e opzioni predefinite; risolutore indipendente con valore di Richardson
(mesh 50 e 25 mm). Fino a S2 i valori coincidono con quelli di `57f87bf8`.

| Caso | Fase | Grandezza | Motore | Indipendente | Scarto |
|---|---|---|---|---|---|
| Mensola, sabbia asciutta | S1 (scavo 2 m) | u testa [mm] | 3,1682 | 3,1680 | 0,01 % |
| Mensola, sabbia asciutta | S1 | M max [kNm/m] | 23,87 | 23,87 | 0,01 % |
| Mensola, sabbia asciutta | S2 (scavo 4 m) | u testa [mm] | 26,2773 | 26,2769 | 0,00 % |
| Mensola, sabbia asciutta | S2 | M max [kNm/m] | 146,35 | 146,34 | 0,01 % |
| Mensola, argilla non drenata | S1 (scavo 3 m) | u testa [mm] | 3,3005 | 3,2999 | 0,02 % |
| Mensola, argilla non drenata | S1 | M max [kNm/m] | 21,98 | 21,97 | 0,03 % |
| Tirantata con falda | S1 (scavo 2 m) | u testa [mm] | 5,2251 | 5,2220 | 0,06 % |
| Tirantata con falda | S1 | M max [kNm/m] | 59,16 | 59,12 | 0,06 % |
| Tirantata con falda | S2 (tirante, P0 150 kN) | u testa [mm] | 4,1429 | 4,1399 | 0,07 % |
| Tirantata con falda | S2 | M max [kNm/m] | 40,30 | 40,27 | 0,08 % |
| Tirantata con falda | S2 | T1 [kN] | 135,55 | 135,55 | 0,00 % |
| Tirantata con falda | S3 (scavo 7 m) (*) | u testa [mm] | 17,9671 | 17,9625 | 0,03 % |
| Tirantata con falda | S3 (*) | M max [kNm/m] | −621,62 | −621,54 | 0,01 % |
| Tirantata con falda | S3 (*) | T1 [kN] | 579,17 | 579,12 | 0,01 % |

(*) Su `57f87bf8` la fase S3 non convergeva (P2); con 0.14.0.1 converge. Il rapporto di equilibrio limite della fase S3 è 1,17.
Con mesh 50 mm (P3) il motore 0.14.0.1 converge su tutti i casi con le opzioni predefinite.

## Problemi in dettaglio

### P1. Sostegno monolatero che si scarica

`EmbeddedWallAnalysis.cs:250-252`: quando la forza di prova di un puntone o di un tirante scende sotto zero, il codice la
porta a zero e sposta anche l'offset plastico. Il sostegno resta così "attaccato" alla paratia che si allontana, come se si
allungasse da solo; quando la paratia torna, riprende carico subito invece che al punto di contatto originario.

Esempio (`ReviewModels.ReversedSupport`): mensola L = 6 m, EI = 2e14 Nmm², puntone in testa EA/L = 10 000 N/mm.
Fase "Away" con −10 kN: il puntone si stacca, testa a −3,6 mm (corretto). Fase "Back" con +10 kN: il risultato fisico è
+0,7826 mm e 7,83 kN nel puntone; il motore dà −2,0348 mm e 15,65 kN. Stesso comportamento per i tiranti.

Correzione applicata (0.14.0.1): un puntone o tirante con forza di prova negativa resta scarico senza spostare
`SupportPlastic`; il riferimento si sposta solo al raggiungimento della capacità (allungamento permanente). I sostegni
bilaterali non cambiano.

### P2. Ricerca lineare sul massimo del residuo

`EmbeddedWallAnalysis.cs:91` accetta un passo di Newton solo se riduce il massimo modulo del residuo. Con molle
elasto-plastiche il residuo ha punti angolosi: la direzione di Newton non garantisce di ridurre quella norma e il solutore
si ferma, anche dimezzando l'incremento fino a 1/4096.

Esempio (`ReviewModels.AnchoredWater`): scavo a −7 m con un tirante, rapporto fra momento passivo e momento spingente
attorno al tirante 1,17 (equilibrio limite indipendente). Il motore si ferma alla frazione 0,875 dell'ultima fase con ogni
mesh e ogni tolleranza.

Correzione applicata (0.14.0.1): ricerca lineare di Armijo sull'energia potenziale. Con molle e sostegni elasto-plastici
l'energia è convessa e derivabile una volta, quindi ogni direzione di Newton con tangente definita positiva la riduce.
La variazione di energia (`Step.EnergyChange`) è sommata termine per termine (trave, molle con acqua, carichi, sostegni)
per non perderla nelle cancellazioni; la riduzione della norma 2 del residuo resta come ripiego per le differenze perse per
arrotondamento vicino alla convergenza. In una copia isolata, togliendo il ripiego, tutti i test passano ugualmente: il
criterio energetico basta da solo. Il caso converge a 17,967 mm, 621,6 kNm/m e 579,2 kN nel tirante, contro 17,963 mm,
621,5 kNm/m e 579,1 kN del risolutore indipendente.

### P3. Tolleranze sotto la precisione di macchina

`WallSolverOptions`: 1e-5 N assoluti più 1e-8 di una scala che contiene carichi esterni, terreno e sostegni ma non le forze
di flessione EI·u''. Con elementi corti i termini EI/h³ arrivano a 1e10 N/mm e gli arrotondamenti del residuo superano
1e-5 N. Esempio: `ReviewModels.CantileverSand(50)` non converge con le opzioni predefinite e converge con 0,01 N.

Correzione applicata (0.14.0.1): la scala della tolleranza relativa comprende anche i contributi di flessione
`|w·EI·κ·N''|` ai punti di Gauss. Con la mesh 50 mm e i momenti del caso la tolleranza sale a qualche centesimo di newton,
trascurabile rispetto alle forze nodali del terreno (dell'ordine di 10⁴ N), e sopra l'arrotondamento.

### P4. Stabilità globale senza profilo interstiziale

`EmbeddedWallGlobalStability.cs:130-139` legge solo `WaterElevation`, il profilo `profile.Water` o un campo FEM.
Un profilo `PorePressures` assegnato (permesso dalla validazione con `WaterElevation` nullo) viene ignorato e la sezione è
calcolata asciutta. Con il modello di `EmbeddedWallGlobalTests` e un profilo idrostatico da −1,5 m: F = 5,9407 invece di
4,4414 (sopravvalutato di 1,34 volte). In ANTHEA i profili si usano solo con la filtrazione FEM, che passa il campo:
oggi non è raggiungibile dall'interfaccia. Il test accetta come correzione anche un rifiuto esplicito (`NotSupportedException`).

### P5. Asse di simmetria nella filtrazione

`EmbeddedWallSeepageMesh.cs:49` impone il carico su entrambi i bordi laterali per tutta l'altezza. Il campo ANTHEA si chiama
"Estensione a destra dall'asse", quindi per uno scavo simmetrico si inserisce la semilarghezza: il bordo a carico imposto
sotto lo scavo fa da dreno. Scavo 6 m, infissione 6 m, B = 10 m: gradiente medio lungo la parete 0,224 invece di 0,550,
utilizzo HYD 0,65 invece di 1,59, pressione al piede lato scavo 72 kPa invece di 91 kPa (resistenza passiva al piede
sovrastimata di circa il 67 %). Non dipende dalla mesh (250 mm: 0,221 contro 0,547). Serve un'opzione di bordo a flusso nullo.

### S1. Larghezza delle verifiche strutturali

I verificatori ricevono la larghezza come argomento e non la confrontano con `analysis.TributaryWidth`. Con analisi su
1500 mm e verifica chiamata con 1000 mm le azioni per palo sono moltiplicate per 1,0 invece di 0,667 (tubi: 2,0 invece di
1,333). Con un diaframma da 2000 mm su un'analisi da 1000 mm le azioni di 1 m sono confrontate con 2 m di sezione.

### ANTHEA

- **A1 pretensione.** Il motore ricalcola a ogni fase `F = P0 + k·(e − ep)` e interpola P0 fra fase precedente e fase
  corrente. In ANTHEA ogni riga di attivazione nuova parte con 0 kN e nessuno copia il valore della fase precedente. Nell'esempio
  della guida (mensola 6 m, tirante in testa EA/L 10 000 kN/m, P0 50 kN) il tirante passa da 10,9 kN a 0 e la testa da −0,31
  a +3,60 mm.
  **Corretto il 21/10/2026** in ANTHEA (`fix/paratie-pretensione`, unito in `refactoring/f5` come 573d9c73) con l'opzione
  scelta dall'utente: la pretensione resta, con avviso, senza migrazione dei documenti.
  - Una riga nuova di attivazione nasce con la pretensione vuota.
  - Se il sostegno era già presente nella fase precedente, la cella vuota ne mantiene la pretensione; all'installazione o alla
    reinstallazione vale 0.
  - Un numero sostituisce la pretensione e 0 la rilascia.
  - Una riduzione rispetto alla fase precedente viene segnalata nel disegno e nei risultati.
  - I documenti già salvati conservano i valori scritti, con risultati e impronte invariati.

  Il codice sta in `EmbeddedWallDocument.LockOffs` (`EmbeddedWallLockOffs.cs`). Le guide sono state aggiornate alla Rev57,
  compresa la frase della guida teorica. Le prove ANTHEA stanno in `tests/ANTHEA.Presentation.Tests/EmbeddedWallPrestressChecks.cs`
  (C1–C11: conversioni, tirante in testa, esempio della guida, ripetizione, rilascio, ritesatura, reinstallazione, editor) e
  sono documentate in `docs/paratie/pretensione-fra-le-fasi.md` di ANTHEA. In Checker, `tests/EmbeddedWalls.Tests/LockOffBetweenStagesTests.cs`
  (7 test, master 5a01af62) controlla la stessa semantica sul motore. Gli attesi vengono da `tools/attesi/paratie_revisione/anthea`,
  in aritmetica razionale esatta.
- **A2 coefficienti parziali.** I fattori A1/A2/M2 per le paratie sono definiti in Model ma non usati. Paratie Plus di solito
  moltiplica gli effetti di una storia caratteristica (1,3 sulle spinte in A1+M1); qui una storia marcata SLU deve essere già
  fattorizzata e nessun controllo lo garantisce.
- **A3–A4** come in tabella. **Punti minori**: verso resistente ±1 non spiegato nelle guide; forza del puntone +30 in una scheda
  e −30 in un'altra; momento del palo con segno opposto alla parete; etichette di EI e delle azioni senza "per fascia"; schema
  della stabilità globale sempre con valle a sinistra; "1.000" letto come 1 (`StratigraphyDocument.cs:43`).

I casi di prova ANTHEA di conversioni, pretensione fra le fasi e righe nuove (script `tools/attesi/paratie_revisione/anthea`)
sono stati tradotti nei test ANTHEA `EmbeddedWallPrestressChecks` e nei test Checker `LockOffBetweenStagesTests`. A2–A4 e i punti
minori non hanno ancora test: dipendono dalle decisioni elencate sotto.

## Differenze rispetto a Paratie Plus (non sono bug)

- Coefficienti di Rankine con attrito parete nullo (Paratie Plus usa δ, Coulomb per la spinta attiva e Caquot-Kerisel o
  Lancellotta per la passiva): a favore di sicurezza.
- Un solo modulo kh per carico e scarico (Paratie distingue vergine e scarico-ricarico); kh assegnato dall'utente, non da E.
- Dopo lo scavo la tensione orizzontale di riferimento è K0·σ'v ridotta, senza effetto di sovraconsolidazione.
- Stabilità globale: solo cerchi sotto il piede, Bishop semplificato, fasi con sostegni rifiutate (i bulbi non contano).
- Verifiche idrauliche secondo NTC 2018 (γR 3/2/2 sui gradienti), prisma di Terzaghi definito dall'utente.
- Sifonamento di fondo non drenato con Nc assegnato: con Nc di Bjerrum-Eide e taglio esterno attivo il contributo verrebbe contato due volte.
- Mancano palancole e profilati (EN 1993-5), verifica strutturale dei trefoli, corrosione dei tubi, armatura minima dei diaframmi.

## Test aggiunti

Tutti in Checker, branch `claude/verifica-paratie`. La categoria `ReviewFinding` raccoglie i test dei problemi ancora aperti;
`ReviewFixesTests` quelli dei problemi corretti in 0.14.0.1.

| Test | Progetto / file | Cosa verifica | Atteso | Esito su `57f87bf8` → 0.14.0.1 |
|---|---|---|---|---|
| `RankineLimitsWaterAndSurchargeMatchHandArithmetic` | EmbeddedWalls.Tests / `IndependentVerificationTests.cs` | pressioni drenate e non drenate in 10 punti | aritmetica nel test | passa |
| `CantileverInSandMatchesIndependentSolver` | idem | mensola in sabbia, S1 e S2: u, M max e sua quota | `independent-expected.json` (0,5 %) | passa |
| `UndrainedCantileverMatchesIndependentSolver` | idem | argilla non drenata, S1 | idem | passa |
| `AnchoredWallBeforeTheDeepExcavationMatchesIndependentSolver` | idem | tirantata, S1 e S2, forza nel tirante | idem | passa |
| `BilateralSupportReloadsWithCantileverAndSupportInParallel` | idem | sostegno bilaterale nei due versi | formula chiusa | passa |
| `DryCircleMatchesIndependentFineSlices` | `GlobalStabilityIndependentTests.cs` | Bishop, cerchio asciutto, azioni ×1 e ×1,3 | `stabilita_globale/circle_check.py` (0,1 %) | passa |
| `HydrostaticWaterOnSliceBasesMatchesIndependentFineSlices` | idem | Bishop con falda | idem | passa |
| `SheetPileInFiniteLayerMatchesHarr` | `SeepageIndependentTests.cs` | portata e carico sotto la punta | Harr (3 %), simmetria | passa |
| `VanGenuchtenMualemMatchesFiftyDigitArithmetic` | idem | Se e kr in 3 casi | `filtrazione/testcases.py` | passa |
| `ProfileZeroCrossingControlsTheSaturatedWeight` | idem | profilo con zero inserito, peso saturo | aritmetica nel test | passa |
| `FreeOutflowGradientsUseNtc2018ResistanceFactors` | idem | gradiente medio e di uscita NTC | ic/3, ic/2 | passa |
| `UnilateralSupportReengagesAtItsSlackPoint` (Strut, Anchor) | `ReviewFindingsTests.cs`, classe `ReviewFixesTests` | P1 | formula chiusa: 0,7826 mm, 7,83 kN | fallisce (−2,0348 mm) → **passa** |
| `AnchoredExcavationWithPassiveMarginConverges` | idem | P2 | risolutore indipendente, S3 | fallisce (non converge a 0,875) → **passa** |
| `FineMeshConvergesWithDefaultTolerances` | idem | P3 | risolutore indipendente, mesh 50 mm | fallisce (non converge a 0,585) → **passa** |
| `GlobalStabilityHonoursPrescribedPorePressures` | `ReviewFindingsTests.cs`, classe `ReviewFindingsTests` | P4 | F = 4,4414 o rifiuto esplicito | **fallisce** (F = 5,9407), ammesso |
| `SymmetricExcavationAxisCarriesNoFlow` | idem | P5 | FEM indipendente con asse a flusso nullo: 0,5497 | **fallisce** (0,2236), ammesso |
| `StripShearWithoutLinksMatchesNtc2018Formula41` | Design.Checks / `EmbeddedWallReviewTests.cs` | taglio senza staffe in 4 casi | formula NTC nel test e script | passa |
| `ChecksRefuseAWidthDifferentFromTheAnalysedStrip` | idem | S1 per pali, tubi e diaframmi | eccezione | **fallisce** (nessuna eccezione), ammesso |

Totale con 0.14.0.1: EmbeddedWalls.Tests 91 test (74 esistenti + 17 nuovi), 89 passano e 2 falliscono (P4, P5);
Design.Checks 102 test, 101 passano e 1 fallisce (S1). I tre fallimenti aperti sono ammessi per nome in
`build/known-failures.json`, con motivo e rimando a questo resoconto: vanno tolti da lì quando arriva la correzione.

Esecuzione:

```
dotnet test tests/EmbeddedWalls.Tests -c Release
dotnet test tests/Design.Checks -c Release --filter "FullyQualifiedName~EmbeddedWall"
dotnet test tests/EmbeddedWalls.Tests -c Release --filter "TestCategory!=ReviewFinding"
```

Verifica completa: `powershell -NoProfile -ExecutionPolicy Bypass -File build\Verify.ps1` (tutte le suite, net472 comprese).

## Domande aperte

1. ~~Pretensione: deve restare implicitamente dopo l'installazione, o l'interfaccia deve copiarla e avvisare quando cala?~~
   Decisa il 21/10/2026: resta, con avviso quando cala, senza migrazione dei documenti (vedi A1).
2. SLU in un'analisi non lineare: fattorizzare gli effetti di una storia caratteristica (come Paratie Plus) o le azioni?
3. Taglio dei pali: quale d (metà tesa effettiva, anello continuo R + 2rs/π, 0,45D + 0,64rs) e quale bw?
4. Filtrazione: aggiungere il bordo a flusso nullo e chiarire l'etichetta "Estensione a destra dall'asse"?
5. Avviso quando la prima fase non è geostatica o con falde diverse senza filtrazione (pressioni passive sovrastimate)?
6. Stabilità globale non drenata: lo stesso cerchio di prova (F indipendente 3,7086 = cu/1,4 · R · arco / D) viene scartato da
   `BishopSolver.cs:50` perché un concio avrebbe N' < 0. Con φ = 0 la resistenza non dipende da N': scartare il cerchio può far
   perdere quello critico. Va mantenuto il rifiuto anche in condizioni non drenate?
