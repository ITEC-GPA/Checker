# GPCChecker.Concrete

Solutore sezionale del calcestruzzo armato e precompresso, con domini di rottura, analisi tensionali lineari e non
lineari e piani di deformazione. Comprende anche i nuclei di verifica trasferiti da ANTHEA (migrazione in
`docs/migrazione-anthea/MIGRAZIONE_ANTHEA.txt`; registro di dettaglio in `Model/CHECKER_PASSO_4.txt`).

Unità: N, Nmm, mm, MPa; compressione negativa.

## Verifiche trasferite da ANTHEA

| Namespace | Contenuto | Origine in ANTHEA (commit fe4652c) |
| --- | --- | --- |
| `GPC.Checkers.Concrete.Shear` | Taglio di sezione in una direzione: `SectionShearCalculator`, `SectionShearInput`, `SectionShearResult`, `ShearProfiles` | `ConcreteCodeChecks.Shear`, `Ntc2018Checks.Shear` |
| `GPC.Checkers.Concrete.Serviceability` | Limiti tensionali SLE di uno stato già calcolato: `StressLimitCheck`; fattore dei getti sottili: `ThinCasting`; omogeneizzazione n ↔ φ: `Homogenization` | `CheckerSection.DescribeStress`, `Homogenization` (`ConcreteSectionProperties`) |
| `GPC.Checkers.Concrete.Torsion` | Torsione con interazione del taglio nelle due direzioni: `SectionTorsionCalculator`, `SectionTorsionInput`, `TorsionGeometry`, `TorsionProfiles` | `ConcreteTorsionCalculator`, `ConcreteShearAnalysis.Torsion` |
| `GPC.Checkers.Concrete.Cracking` | Fessurazione di sezione: `SectionCrackCheck`, `CrackRequirements`, `CrackWidthCalculator`, `CrackSectionGeometry`, `CrackProfiles` | `Ntc2018Checks.Cracking`, `ConcreteCodeChecks` (requisiti, wk, hc,eff), `ConcreteTensionCracking`, `ConcreteInnerCracking`, `TensionBarSpacing`, `SectionRegions` |
| `GPC.Checkers.Concrete.Detailing` | Aderenza, ancoraggi e sovrapposizioni (`AnchorageCalculator`), dettagli 1D di travi, pilastri, solette piene e pareti (`MemberDetailingCalculator`), `DetailingProfiles` | `ConcreteBond`, `ConcreteAnchorageCalculator`, `ConcreteDetailingCalculator` |
| `GPC.Checkers.Concrete.Response` | Curva momento-curvatura a N costante (`MomentCurvatureAnalysis`), anche nelle unità del chiamante, con esito strutturato e rifiuti tipizzati: risposta numerica, non verifica | `MomentCurvatureCalculator`, `ConcreteCurvatureAnalysis` |
| `GPC.Checkers.Concrete.Durability` | Classi di esposizione e loro requisiti (`ExposureClasses`), copriferri per norma (`CoverRequirements`, `DurabilityProfiles`), classi minime di resistenza | `Materiali.Durability`, `NtcCover`, `MinimumConcrete`, `MaterialCover` |

I casi legacy di `GPCChecker.Test.Concrete/Fixtures` (CSV e archivi XML delle sezioni) sono catture del codice di
ANTHEA, non attesi indipendenti. Congelati il 1/10/2026 da ANTHEA fe4652c, sono stati ricatturati il 7/10/2026 dal
branch `refactoring/integrazione-d7b-d2` di ANTHEA (commit d2d3225, `supporto/test/CheckerMigration.Capture`, modalità
`tutte`), che nella fessurazione si comporta già come la libreria: wk = 0 con l'asse neutro nel copriferro, limite
superiore dell'eq. (7.14) con le barre tese fuori da Ac,eff, k2 = 0,5 con l'asse neutro interno (D7-b), h − x limitato
nelle fasce interne dei fori (0.0.17.0). I test di migrazione li riproducono con la regola corrente, senza casi
speciali; l'opzione legacy `NtcK2FromCompressedBars` resta provata nei test dedicati.

### Resistenza a N assegnato: tolleranza su N

- `SectionSolvers.DomainPointAxialTolerance` dice se un punto del dominio cercato a N assegnato (N costante, N e Mx,
  N e My) è la resistenza a quello N: |NRd − N| ≤ tolleranza. `SectionSolver.CalculateDomainPoint` non applica il
  controllo: lo applica il chiamante (in ANTHEA `SectionMomentResistance`).
- Parabola-rettangolo, bilineare, non lineare e tabelle generiche: max(1000 N; 1e-6 |N|; 0,5e-4 b h fck). Il calcolo
  non integra la sezione.
- Il terzo termine è 2 volte (`ConvergenceFactor`) la tolleranza su N della prova di arresto della ricerca iterativa
  per quella sezione (`ConvergenceTolerance`): tolleranza di distanza delle forze adimensionali (0,25e-4) per la scala
  di N del solutore, b h fck (b e h lati del rettangolo che contiene il calcestruzzo; nelle sezioni composte più
  A 15 fck dei profili). Non dipende dal legame e vale anche con lo stress block. Esempi della prova di arresto:
  112,5 N per 300 × 500 C30/37, 640 N per D 800 C40/50, 3500 N per D 2000 C35/45; tolleranze: 1000 N (il minimo),
  1280 N, 7000 N.
- La prova di arresto non limita il punto restituito. La ricerca controlla la distanza dal bersaglio del punto prima
  dell'ultimo passo, poi fa il passo e restituisce il punto nuovo, che può essere più lontano. Campione di convalida
  di S-1 (.NET 8): fra i 6404 punti del percorso iterativo con i legami continui lo scarto su N arriva a 1,47 volte la
  tolleranza della prova (un punto: sezione 1000 × 200 C25/30, parabola-rettangolo, N di trazione, direzione a 30°);
  gli altri restano entro 0,91 volte. Il fattore 2 è un margine su queste misure, non un limite dimostrato.
- La regola usata da ANTHEA fino alla 0.0.17.0 era max(1000 N; 1e-6 |N|). È la stessa fino a b h fck = 2e7 N (per
  esempio un cerchio di D 756 mm in C35/45). Per sezioni più grandi poteva rifiutare punti in cui la ricerca si era
  fermata normalmente: nel modulo palo orizzontale di ANTHEA, con i dati usuali (C35/45, 16Ø24, N = 0), ogni palo da
  D 1600 mm in su dava «Soluzione non coerente con N e direzione assegnati», con |NRd − N| da 1,01 a 2,33 kN e
  tolleranze della prova di arresto da 2,24 a 5,47 kN.
- Cambia anche con i legami continui: sulle sezioni con b h fck oltre 2e7 N (per esempio la C2 del banco F2.1, D 800
  C40/50: 1280 N) la tolleranza è più larga di quella della 0.0.17.0. Lo scarto su N accettato resta sotto
  0,5e-4 b h fck.
- La regola cambia solo quali punti sono accettati, mai i loro valori: non è mai più stretta della regola della
  0.0.17.0, quindi ogni punto accettato prima resta accettato e identico. I punti dati dai ripieghi della ricerca su un
  salto delle forze (punto più vicino o bisezione, fino a 10 volte la tolleranza di distanza) possono restare rifiutati.
- Stress block: anche almeno 1/1000 della resistenza a compressione centrata NRd,c (`CentredCompressionResistance`:
  risultante del solutore a deformazione uniforme εc2 della parabola-rettangolo, εcu con i materiali ACI 318 (senza
  prove), con barre, profili e trefoli; per il c.a. con materiale europeo (Ac − As) η fcd + As σs). Esempi: 3,05 kN per 300 × 500 C30/37, 12,28 kN per D 800 C40/50; sotto circa 1 MN di
  NRd,c vale il minimo di 1 kN.
- **Con lo stress block il risultato può essere meno preciso.** Il diagramma ha un salto di tensione a (1 − λ) εcu e
  il calcestruzzo è integrato su punti di Gauss fissi: la risultante cambia a gradini e la ricerca iterativa può
  fermarsi con N diverso da quello assegnato. Campione di convalida (13 sezioni, 4 legami, 8840 punti, ottobre 2026,
  .NET 8, N da −0,90 a +0,19 NRd,c): scarto su N fino a 9e-4 NRd,c nei punti accettati; momento resistente diverso da
  quello a N esatto di meno dello 0,5 % nel 95 % dei punti, fino a circa 1-2 % vicino agli estremi di questo
  intervallo, con segno variabile. Con gli altri legami: scarto su N sotto 6e-5 NRd,c, momento entro 0,4 %.
- Questi sono valori misurati, non limiti. La differenza del momento vale circa lo scarto su N per il braccio, con lo
  scarto fino alla tolleranza. Vicino alla compressione centrata il momento tende a 0 e la differenza relativa
  cresce, circa |NRd − N| / (NRd,c − |N|): per esempio circa 5 % con 1e-3 NRd,c a |N| = 0,98 NRd,c, anche a sfavore
  di sicurezza.
- Con lo stress block il percorso della ricerca è caotico: la stessa sezione può dare un punto diverso con un altro
  runtime. Sezione C2 del banco F2.1 di ANTHEA (D 800, 12Ø16), N = −1500 kN, Mx+: NRd = −1502,05 kN con .NET 8
  (accettato), −1513,15 kN con .NET Framework 4.7.2 (scarto 1,07e-3 NRd,c, rifiutato).
- Prove: `DomainPointAxialToleranceTests`, con attesi a mano e Python (NRd,c di rettangolo, poligono di 144 lati e
  C70/85, tolleranze, punto di S-1, stati del banco F2.1 entro 5e-3 dalla forma chiusa; tolleranza della prova di
  arresto delle sezioni del banco e del palo di D 2000, punti del palo di ANTHEA da D 1400 a D 2500, MRd del palo di
  D 2000 a N = 0 entro 2e-3 da 2536,846 kNm). `LargeSectionPointBeyondTheStoppingTestIsAccepted` cerca il punto del
  campione con scarto 1,47 volte la prova di arresto, sulla sezione 1000 × 200 e sulla stessa scalata per 4
  (4000 × 800, b h fck = 8e7 N): forze 16 volte, momenti 64 volte, stesso rapporto. Sulla sezione grande lo scarto è
  2,93 kN: la regola della 0.0.17.0 (1 kN) e la sola prova di arresto (2 kN) lo rifiutano, la regola con il fattore 2
  (4 kN) lo accetta.
- Controllo sul campione di convalida (8840 punti, .NET 8): tre sezioni hanno b h fck oltre 2e7 N (D 800 C40/50 due
  volte: 2,56e7 N, tolleranza 1280 N; D 1200 C30/37: 4,32e7 N, 2160 N). Nessun esito cambia rispetto alla regola
  precedente (0 punti accettati in più, 0 rifiutati).
- **Contratto della 0.0.17.0** (`DomainPointContractTests`, `Fixtures/domain-point-contract.json`): fotografia di
  regressione di `CalculateDomainPoint` catturata dal codice di 4f54139a prima della regola, sul runtime della suite.
  Non è un atteso indipendente: non si rigenera per far passare il test. Registra 1024 punti (le 5 sezioni in forma
  chiusa del banco, 4 legami, SLU e SLV, 6 o 7 sforzi normali, 4 direzioni) con percorso del solutore, punto ed esito
  della regola della 0.0.17.0. `ContractVerdictsAreKeptForTheContinuousDiagrams` controlla che con i legami continui
  l'esito resti identico in tutti i 768 punti e la tolleranza resti identica bit per bit per R1, R2, R3 e C1 (2 volte
  la prova di arresto al massimo 540 N); per C2 la tolleranza è 1280 N e nessun punto ha scarto fra 1000 e 1280 N.
  Con lo stress block nessun punto accettato prima è rifiutato (164 confermati, 7 accettati in più).

### Taglio

- Profili scelti dal tipo esatto della classe Standard: NTC 2018, Model Code 2010 livello II, EN 1992-1-1 e annessi
  UNI, DIN, DS, NS (prima generazione), CNR-DT 204 (FRC senza armatura a taglio, con fFtuk), CNR-DT 200 (NTC più
  contributo FRP, nullo perché le sezioni non hanno dati FRP).
- CS-TR34 non definisce il taglio di trave (`ShearProfiles.NotApplicableReason`).
- ACI 318 e AASHTO: implementazione futura.
- Le altre classi, anche se derivate da quelle elencate, danno `NotSupportedException`: nessun ripiego su un'altra
  norma.
- Resistenze di progetto, γc e geometria resistente (bw, d, Asl, z/d) sono dati espliciti del chiamante.
- Escluse la precompressione e le riduzioni favorevoli vicino agli appoggi.
- Una resistenza nulla con domanda non nulla è `NotSatisfied`. NTC con trazione e senza staffe è `NotEvaluated`,
  come in ANTHEA.
- Casi legacy congelati: `GPCChecker.Test.Concrete/Fixtures/shear-legacy.csv` (2016 casi), verificati da
  `ShearMigrationTests` insieme a calcoli a mano.

### Limiti tensionali SLE

- Combinazione caratteristica: σc ≤ k1·fck ai vertici compressi, barre ≤ k3·fyk, trefoli ≤ limite di
  precompressione.
- Quasi permanente: σc ≤ k2·fck.
- Frequente: nessun limite.
- I coefficienti vengono dalla classe Standard. Il fattore sul limite del calcestruzzo (getti sottili) è esplicito.
- CS-TR34 non fissa limiti tensionali di sezione (`StressLimitCheck.NotApplicableReason`).
- `StressLimitResult.Satisfied`: vero con `Ratio` ≤ 1, falso oltre, null senza `Ratio` (combinazione frequente).
- `StressLimitCheck.SteelLimit(standard, materiale)`: limite dell'acciaio |k3·fyk| del materiale, con i coefficienti
  della classe (anche personalizzati). Non dipende dalla combinazione: serve a chi mostra il limite dell'acciaio
  anche nelle combinazioni quasi permanente e frequente, dove `Evaluate` non lo fissa. Per un trefolo resta k3·fyk.
- `ThinCasting.Factor(standard)` e `ThinCasting.Factor(standard, regola)`: fattore dei getti sottili, cioè degli
  elementi piani gettati in opera con spessore minore di 50 mm. Il chiamante sa se l'elemento è sottile e lo applica
  al limite del calcestruzzo (`concreteLimitFactor`) e ad αcc e fcd; i limiti dell'acciaio non cambiano.
  - Regola predefinita (`ThinCastingRule.Ntc2018Only`, valore 0, usata dal metodo senza regola): 0,8 per la
    classe esatta NTC 2018 (§4.1.2.1.1.1 per fcd, §4.1.2.2.5.1 per i limiti SLE), 1 per tutte le altre, comprese le
    derivate (CNR-DT 200). Sono i valori di ANTHEA prima di F2.7.
  - Opzione `ThinCastingRule.Ntc2018AndItalianAnnex`: 0,8 anche per UNI EN 1992-1-1 con l'appendice nazionale
    italiana (DM 31/07/2012 7.2), come indica la pagina del metodo `ca.sle-tensioni` (§6.2 e riga fs di §7).
  - La classe si riconosce dal tipo esatto. ANTHEA crea «UNI EN 1992-1-1» come `StandardUNIEN1992p11`, che in Model è
    per definizione la UNI EN 1992-1-1:2005 con l'appendice italiana e non ha membri che scelgano un'appendice: basta
    il tipo. I coefficienti personalizzati non cambiano la classe e conservano il fattore.
- `Homogenization`: rapporto modulare n = Es·(1 + φ)/Ec delle barre (o dei trefoli) omogeneizzate al calcestruzzo e
  coefficiente di viscosità φ che dà un n scelto.
  - `ModularRatio(es, ec, phi)` = `es * (1 + phi) / ec` e `CreepFromModularRatio(n, es, ec)` = `n * ec / es - 1`:
    pura aritmetica senza controlli, con l'ordine delle operazioni delle copie di ANTHEA
    (`ConcreteSectionProperties.cs:48, :50`, `ConcreteStress.cs:178, :183`, `ReportConcreteShort.cs:78`,
    `Ntc2018Checks.cs:238`), quindi uguali bit per bit. φ < 0 dà n < Es/Ec, come nella scheda delle tensioni di
    ANTHEA; NaN e infiniti si propagano.
  - `Resolve(es, ec, fromN, value)`: φ e n da φ o da n, con i controlli di ANTHEA (`ConcreteSectionProperties.cs:44-53`):
    moduli finiti e positivi e valore finito; φ, dato o ricavato da n, finito e non minore di −1e-12
    (`CreepTolerance`), poi max(0, φ); n finito. I rifiuti sono `ArgumentException` (tipo esatto) con messaggio inglese
    e motivo `HomogenizationRejection` (`InvalidInput`, `NegativeCreep`, `RatioOutOfRange`) in
    `Exception.Data[Homogenization.RejectionKey]`: il chiamante li mappa sui propri testi senza leggere il messaggio.
  - La forma Es/(Ec/(1 + φ)) di Model (`ConcreteSectionHelper`) è un'altra espressione e può differire nell'ultima
    cifra.
- Casi legacy congelati: `Fixtures/stress-legacy.csv` (2016 stati) e `Fixtures/stress-sections.xml`
  (`ServiceabilityMigrationTests`). Su tutti gli stati, compresi frequente e quasi permanente, `SteelLimit` riproduce
  il limite dell'acciaio del legacy, `Satisfied` il testo di stato e `ThinCasting.Factor` la riduzione dei getti
  sottili.
- Calcoli a mano (`ServiceabilityMigrationTests`), perché la fixture ha k3 = 0,8 e nessun rapporto uguale a 1:
  - `SteelLimit` con k3 personalizzato, per esempio 0,7 su B450C = 315 MPa, anche con lo standard restituito
    dall'analisi, uguale al limite delle barre di `Evaluate`;
  - `Satisfied` al bordo: vero con `Ratio` = 1 esatto, falso un ulp oltre, come `ratio <= 1` del legacy.
- Omogeneizzazione (`ServiceabilityMigrationTests`): le espressioni di ANTHEA, riscritte nel test, sono confrontate bit
  per bit su φ ∈ {−0,5, −1e-13, 0, 0,5, 2, 15} con 3 moduli dell'acciaio e 4 del calcestruzzo; `Resolve` dà gli
  stessi φ e n o lo stesso rifiuto, anche ai bordi della tolleranza.
- Trefoli con predeformazione nulla: `NotSupportedException`.
- Contratto di ModelChecker della 0.0.17.0 (`ModelCheckerContractTests`, `Fixtures/model-checker-contract.json`):
  uscite di `Evaluate` e `NotApplicableReason` con gli argomenti di ModelChecker sui 2016 stati di
  `stress-legacy.csv`, su tutte le norme e su fattori e combinazioni non validi. È una fotografia di regressione
  catturata da 4f54139a, non un atteso indipendente: con le opzioni predefinite resta identica byte per byte.

### Torsione

- Traliccio periferico a parete sottile con staffe chiuse a 90°: TRcd (bielle), TRsd (staffe), TRld (barre
  longitudinali), TRd = min.
- Profilo resistente (Ak, uk, tef) esplicito. `TorsionGeometry.Rectangle` e `Circle` lo propongono dal contorno con
  la regola di ANTHEA (NTC: spessore effettivo delle sezioni cave) o con quella di EN 1992-1-1 6.3.2(1).
- Taglio e torsione usano lo stesso cot θ. `Calculate` ricalcola il taglio delle due direzioni con
  `SectionShearCalculator` e quel cot θ, poi valuta:
  - l'interazione delle bielle, lineare o quadratica secondo la norma;
  - l'interazione delle staffe: T/TRsd + max(V/VRsd).
- Profili per tipo esatto, con il proprio campo di cot θ:

  | Profilo | Resistenza delle bielle | Interazione delle bielle |
  | --- | --- | --- |
  | NTC 2018 (trasferito da ANTHEA) | 0,5 fcd | lineare |
  | EN 1992-1-1 e NS | ν fcd, con ν del taglio | lineare |
  | UNI | ν fcd, ν = 0,5 fino a C70/85 | lineare |
  | DS | ν fcd, ν = 0,7 − fck/200 | lineare |
  | DIN | 0,525 ν2 fcd (0,75 ν2 per sezioni cave armate su entrambe le facce) | quadratica per le sezioni piene; il campo di cot θ è calcolato con VEd,T+V |
  | Model Code 2010 | kc fck/γc, con kc ricavato dall'εx del taglio | quadratica per le sezioni piene |
  | CNR-DT 200 | come NTC, con TRd,f = 0 | lineare |

- CS-TR34 non definisce la torsione di sezione. CNR-DT 204 è non supportata: richiede staffe, e la combinazione di
  fibre e staffe non è implementata.
- DS: la regola dell'annesso danese 6.3.2(6) per V, T, N e M combinati non è applicata (`Limitations`).
- Senza staffe chiuse la resistenza è nulla: con T ≠ 0 il risultato è `NotSatisfied` senza rapporto. ANTHEA dava
  invece un errore di input; è una differenza intenzionale.
- Casi legacy congelati (NTC): `Fixtures/torsion-legacy.csv` (986 casi) e `Fixtures/torsion-geometry-legacy.csv`
  (10 contorni), verificati da `TorsionMigrationTests` insieme a calcoli a mano per le altre norme.
- Le formule delle norme diverse da NTC (MC2010, annessi DIN e NS, interazione quadratica) vanno riscontrate sul
  testo. Le fonti estratte e verificate sono NTC 2018 §4.1.2.3.6, DM 31/07/2012 6.2.2(6) e DK NA 5.6.1(3)P e 6.3.2(6).

### Fessurazione

- **Requisito** (`CrackRequirements.For`), per norma, combinazione SLE, classe di esposizione e sensibilità delle
  armature:
  - NTC 2018 e UNI: Tab. 4.1.IV, con decompressione o formazione delle fessure per le armature sensibili in ambiente
    aggressivo;
  - famiglia Eurocodice: solo la quasi permanente (NS: la frequente per XD3/XS3), con le tabelle EN 7.1N (anche
    DIN), DK NA 7.1 NA e NS NA;
  - wlim di progetto facoltativo; Model Code 2010 lo richiede.
- **Classi di esposizione** (`CrackRequirements.ExposureClasses`, 0.0.18.0): le 18 classi nell'ordine dei gruppi
  ambientali NTC, cioè da X0 a XF1 ordinario, da XC4 a XF3 aggressivo, da XD2 a XF4 molto aggressivo.
  - È una vista di sola lettura (`IReadOnlyList<string>`, sempre lo stesso oggetto) sulla copia privata che i
    requisiti leggono, sia per riconoscere la classe sia per il gruppo ambientale.
  - L'array pubblico `Exposures` resta, con le stesse classi, per la compatibilità binaria con la 0.0.17.0, ma la
    libreria non lo legge più: modificarne gli elementi non sposta i gruppi e non cambia i requisiti.
  - Prove in `CrackMigrationTests`. L'ordine e i gruppi NTC sono quelli delle righe dei requisiti di
    `crack-scalar-legacy.csv`. Con l'array pubblico rovesciato e una classe sconosciuta al posto di XF4:
    - i 1596 requisiti si riproducono con `For` senza opzioni, con le opzioni predefinite e con `ValidateAtUse`;
    - XF4 resta accettata e la classe sconosciuta resta rifiutata;
    - `Evaluate` dà gli stessi risultati e lo stesso rifiuto.

    Il test ripristina l'array in un `finally`.
- **Apertura wk** (`CrackWidthCalculator`):
  - NTC: 1,7 Δsm (εsm − εcm) della Circolare;
  - Eurocodice: sr,max (εsm − εcm), con le varianti DS (k3), DIN (kt, limite σs Ø/(3,6 fct)) e MC2010 (sr e βmin).
  - Con MC2010 e DIN solo barre ad aderenza migliorata.
- **Verifica di sezione** (`SectionCrackCheck.Evaluate`). Riceve lo stato tensionale nativo: analisi lineare senza
  cls teso, e per decompressione e formazione la sezione non fessurata.
  - Sezione parzialmente compressa: zona tesa efficace oltre hc,eff lungo il gradiente di deformazione. hc,eff è
    quello di ciascuna norma (DIN NCI 7.3.2(3), DS fascia con baricentro sulle barre).
  - Sezione interamente tesa: facce ±x e ±y, o fasce radiali per i cerchi, verificate indipendentemente. DS aggiunge
    il sistema grossolano.
  - Sezioni cave: pareti o anello interno verificati a parte. Una superficie interna tesa senza armatura lascia la
    verifica senza esito.
  - Restituisce tutte le regioni (`CrackRegion`) e quella governante.
- **Geometria** (`CrackSectionGeometry.From`): contorno, fori e barre ordinarie della sezione di Model. Il cerchio è
  riconosciuto dai vertici equidistanti; gli anelli concentrici vanno confermati. L'interasse automatico vale per
  file allineate o anelli, altrimenti va assegnato.
- **Contorni a curve** (0.0.26.0, Model 4.1 / Geometry 2.3):
  `CrackSectionGeometry.FromCurves(section, chordTolerance, maxSegmentLength)` usa i contorni nativi
  di Model; un secondo overload riceve `SectionCurveOutline` e le barre ordinarie. La tolleranza
  obbligatoria, in mm, limita lo scostamento delle corde dalla curva, non l'errore finale della verifica.
  Contorno, fori, barre e piano di deformazione conservano lo stesso riferimento di coordinate.
  I cerchi analitici mantengono il proprio centro anche se traslati; gli anelli di barre concentrici
  richiedono `concentricRings: true`. Le ellissi e gli altri contorni usano le file allineate:
  quando l'interasse non e' determinabile va assegnato esplicitamente. La verifica della superficie
  interna circolare e' disponibile per un unico foro circolare concentrico; restano supportati anche
  i fori rettangolari allineati agli assi. Fori ellittici o circolari eccentrici restituiscono l'esito
  esplicito di superficie interna non supportata. Isole e regioni disconnesse sono rifiutate.
  `From(section)` mantiene il contratto poligonale precedente; il nuovo ingresso e' una scelta esplicita.
- **Opzioni** (`SectionCrackOptions`, 0.0.18.0). Sono membri nuovi: con `SectionCrackOptions.Default` comportamento
  e contratto della 0.0.17.0 non cambiano.
  - Le opzioni sono immutabili: si parte da `Default` e si usano i metodi `With…`, che restituiscono una copia.
  - Si passano con il costruttore nuovo di `SectionCrackInput`, che chiede tutti gli argomenti più le opzioni, senza
    valori predefiniti. Il costruttore esistente, quello di ModelChecker, usa `Default`. Anche
    `CrackRequirements.For` ha un overload con le opzioni obbligatorie.
  - `ValidateAtUse` (falso di default) controlla i dati dove entrano nel calcolo, come ANTHEA:
    - il wlim di progetto solo per la famiglia Eurocodice e MC2010 nella combinazione richiesta. NTC e UNI lo
      ignorano; nelle altre combinazioni l'esito è `NotRequired`;
    - copriferro nominale, copriferro e interasse assegnati nel ramo che li legge, con la stessa
      `ArgumentOutOfRangeException` e lo stesso parametro, prima il copriferro e poi l'interasse. Non li leggono
      decompressione, formazione delle fessure, combinazioni non richieste, asse neutro nel copriferro, barre tese
      fuori da Ac,eff e sezione interamente compressa. Fa eccezione il copriferro nominale con il profilo DIN e
      `EffectiveDepthCover` nullo: nella sezione parzialmente compressa con barre tese la condizione DIN di hc,eff
      lo legge, e quindi lo controlla, prima di stabilire quali barre sono in Ac,eff, anche quando poi non ce n'è
      nessuna. Con `EffectiveDepthCover` assegnato, come farà l'adattatore di ANTHEA, la regola vale anche per DIN;
    - le tensioni delle barre dopo il ritorno della sezione interamente compressa, che dà wk = 0 anche con tensioni
      mancanti o non finite. Il messaggio è «Cracking: bar stresses missing or not finite.». Con
      `NtcK2FromCompressedBars` le tensioni scelgono k2 e restano controllate prima di ogni ramo.
  - `EffectiveDepthCover` (null = copriferro nominale) è il c della condizione DIN (h − x)/3 ≥ c + 20 mm di hc,eff.
    La formula di wk usa sempre il copriferro assegnato o quello nominale.
- **Rifiuti con codice** (`CrackRejection`, 0.0.18.0). I rifiuti restano `ArgumentException` del tipo esatto, con il
  messaggio della 0.0.17.0. Il codice sta in `Exception.Data` sotto `CrackRejection.DataKey` (`CrackRejection.CodeOf`):
  - `WidthParameters` e `UpperBoundParameters`: parametri non validi della formula e del limite superiore senza barre
    aderenti, con lo stesso messaggio;
  - `RibbedBarsRequired`: MC2010 e DIN con barre lisce;
  - `BarStresses`: tensioni delle barre mancanti o non finite;
  - `UncrackedStressRequired`: decompressione o formazione delle fessure senza la sezione non fessurata.

  I rifiuti dei parametri (`ArgumentOutOfRangeException`) non hanno codice: si riconoscono da `ParamName`.
  Le prove sono in `CrackMigrationTests`: opzioni predefinite uguali al costruttore esistente, i casi di
  `ValidateAtUse`, hc,eff DIN calcolato a mano, i codici. Hanno codice anche i rifiuti delle catture: i 6 stati di
  `crack-legacy.csv` e le 57 aperture di `crack-scalar-legacy.csv` con barre lisce danno `RibbedBarsRequired`.
- **Traccia con chiavi stabili** (0.0.18.0, opzione `Trace`, falsa di default). `SectionCrackResult.Trace` elenca le
  voci calcolate di ANTHEA nello stesso ordine (`Ntc2018Checks.Cracking`, `ConcreteTensionCracking`,
  `ConcreteInnerCracking`, `ConcreteCodeChecks.CrackWidth` e `UnbondedCrackWidthBound`). Ogni `CrackTraceEntry` ha:
  - `Code`: una delle 91 costanti di `CrackTraceCodes`, ciascuna con il simbolo di ANTHEA nella documentazione;
  - `Region`: la chiave della faccia, della fascia radiale, del sistema grossolano DS o della superficie interna,
    null per le voci della sezione. ANTHEA le scrive con il nome della regione come prefisso, salvo quelle con il
    flag `Summary`, che ripetono la regione governante nel riepilogo;
  - `Value` (null per le note e per i valori che ANTHEA non calcola), `Unit`;
  - `Arguments`: i numeri dell'espressione (`CrackTraceArguments`), per esempio l'indice della barra (B01 = 0) o
    l'angolo della fascia radiale;
  - `Flags`: il ramo che sceglie il testo (`CrackTraceFlags`), per esempio la variante di sr,max, chi governa
    εsm − εcm o Δsm, la regola di k2 precedente a D7-b.

  Le voci che ripetono gli ingressi (Verifica, Modello, N, Mx, My, φ, γc, γs, normativa, criterio, wlim) le scrive
  il chiamante. Con la traccia attiva nient'altro cambia: risultato, `Details`, `Status`, `Outcome` e rifiuti sono
  quelli senza traccia.
  - Contesto d'analisi facoltativo (`WithAnalysisContext`: modulo del calcestruzzo e φ delle barre dell'analisi):
    con la traccia aggiunge «Ecls analisi» e «n analisi» = Es (1 + φ)/Ecls prima della formula di wk. n viene da
    `Homogenization.ModularRatio` con l'Es dell'ingresso (`SectionCrackInput.Es`), quindi coincide bit per bit con
    l'espressione di ANTHEA solo se il chiamante passa lo stesso modulo dell'acciaio e lo stesso φ (in ANTHEA il modulo
    della prima barra efficace e `PsiRebar ?? 0`).
  - `RegionOutcomes` (anch'esso solo con `Trace`): chiave, esito e larghezza di ogni regione raggiunta, cioè zona
    tesa o facce (anche quella che ferma la verifica), sistema grossolano DS, pareti o anello interno (verificati,
    senza armatura o senza interasse) e «InnerSurfaces» per le superfici interne non supportate. Con questi dati il
    chiamante compone gli stati delle superfici interne senza leggere il testo inglese.
  - Requisito della sezione interamente tesa (ciclo di prototipo di F2.7): con `Trace` anche il risultato della
    sezione interamente tesa, comprese le superfici interne, porta `Requirement` (lo stesso di `CrackRequirements.For`
    con i dati dell'ingresso). Senza la traccia resta nullo, come nella 0.0.17.0: il contratto K0 di ModelChecker
    registra nullo il criterio di quel ramo.
- **Motivo fine** (`SectionCrackResult.Reason`, 0.0.18.0, sempre valorizzato anche senza opzioni). Separa i tre
  rami che `Outcome` riunisce in `NoEffectiveArea`: `ZeroEffectiveDepth` (hc,eff nullo), `NoEffectiveSteelOrArea`
  (barre efficaci ma Ac,eff nullo), `FaceWithoutAreaOrSteel` (faccia della sezione interamente tesa senza area o
  armatura efficace). `EntirelyCompressed` (ciclo di prototipo di F2.7) segna la sezione interamente compressa
  (εc,max ≤ 1e-12, wk = 0, esito `Evaluated`), l'unico ramo valutato senza una voce propria nella traccia: asse
  neutro nel copriferro, limite superiore e sezione interamente tesa l'hanno (`NearestBarDepth`, flag `UpperBound`,
  `GoverningFace`). Negli altri casi vale `None`. `Outcome` e `Status` non cambiano.
- **Famiglie dei profili** (`CrackProfiles`, ciclo di prototipo di F2.7, 0.0.18.0). Dicono al chiamante ciò che
  prima doveva ricopiare dalla libreria; un profilo fuori dall'enumerazione dà `ArgumentOutOfRangeException`:
  - `WidthFormula`: `CrackWidthFormula.Ntc2018` (NTC 2018 e CNR-DT 200: formula della Circolare, voci della traccia
    da Es a wk, limite superiore 1,7 · 0,75 (h − x)) o `Eurocode` (EN, UNI, DIN, DS, NS e Model Code 2010);
  - `UsesDesignLimit`: vero se il requisito legge il wlim di progetto (famiglia Eurocodice e Model Code 2010), falso
    per NTC 2018, UNI e CNR-DT 200, il cui requisito è la Tab. 4.1.IV;
  - `EffectiveDepthReadsCover`: vero solo per DIN, l'unico profilo la cui hc,eff legge il copriferro (condizione
    (h − x)/3 ≥ c + 20 mm, `EffectiveDepthCover`).

  Prove in `CrackMigrationTests`: la tabella scritta a mano dal legacy di ANTHEA e il comportamento della verifica
  (voci NTC solo con la formula NTC, variante del limite superiore, wlim di progetto che cambia il requisito solo
  dove è letto, hc,eff DIN da 100 a 150 mm con il copriferro della condizione); requisito della sezione
  interamente tesa con e senza traccia, anche cava; `EntirelyCompressed` con ogni profilo, con e senza opzioni.

  Le prove sono in `CrackMigrationTests`. La traccia riproduce entro 1e-9, nello stesso ordine, i 16 simboli della
  colonna `details` di `crack-legacy.csv` (13 339 valori su 400 stati, anche con il prefisso di faccia, fascia,
  sistema grossolano, superficie interna e barra). L'ordine e i valori del caso inflesso NTC sono calcolati a mano,
  con tolleranza relativa 1e-12 e costanti esatte. «n analisi» è confrontato bit per bit con l'espressione di
  ANTHEA su valori per cui gli altri ordini delle operazioni danno un'altra ultima cifra.
  Ci sono poi i rami (decompressione, limite superiore, Eurocodice, sezione interamente tesa, DS, fasce radiali,
  regola di k2 prima di D7-b), un test per ciascun motivo, gli esiti delle superfici interne e la traccia vuota di
  default.
- **Chiavi stabili e costanti** (0.0.18.0). Codici e chiavi sono `public const string` (e `CreepTolerance` è
  `public const double`): le 91 costanti di `CrackTraceCodes`, quelle di `CrackTraceFlags` e `CrackTraceArguments`,
  `CrackRejection.DataKey` e i codici di rifiuto, `Homogenization.RejectionKey` e `Homogenization.CreepTolerance`.
  Il compilatore ne copia il valore nei chiamanti, come per i valori delle enumerazioni (`CrackReason`,
  `HomogenizationRejection`, `ThinCastingRule`). I valori sono stabili e non cambiano nelle versioni successive;
  se uno cambiasse, ANTHEA e ogni altro chiamante andrebbero ricompilati con la DLL nuova, perché sostituire solo la
  DLL lascerebbe nei chiamanti il valore vecchio. Una costante nuova si aggiunge senza toccare quelle esistenti.
- **Non supportati:**
  - CS-TR34: non applicabile;
  - CNR-DT 204: modello FRC non implementato;
  - CNR-DT 200: membratura NTC;
  - precompressione;
  - superfici interne di fori non rettangolari o non circolari.
- **Casi legacy congelati e riprodotti** (`CrackMigrationTests`):
  - `Fixtures/crack-legacy.csv`: 936 stati su 6 sezioni (`crack-sections.xml`), con 1112 regioni confrontate;
  - `Fixtures/crack-scalar-legacy.csv`: 1400 aperture e 1596 requisiti.
- **Contratto di ModelChecker della 0.0.17.0** (`ModelCheckerContractTests`, `Fixtures/model-checker-contract.json`).
  Registra Details, Status, Outcome, Verdict, Reference e gli altri membri del risultato, oppure tipo, messaggio e
  parametro dell'eccezione, con il costruttore di `SectionCrackInput` senza opzioni:
  - i 936 stati di `crack-legacy.csv`;
  - ingressi limite: wlim di progetto non valido, override NaN o negativi, tensioni non finite, i tre casi di
    `NoEffectiveArea`, barre lisce con MC2010 e DIN, CS-TR34, CNR-DT 204 e ACI 318;
  - la griglia dei requisiti attraverso `Evaluate` e la tabella dei profili.

  È catturato da 4f54139a. I membri e le opzioni aggiunti dopo non lo cambiano.
- Ac,eff si ottiene ritagliando il poligono invece che tagliando la mesh di ANTHEA: risultato identico entro 1e-9.

### Aderenza, ancoraggi e dettagli

- **Profili** per tipo esatto:
  - NTC 2018 (trasferito da ANTHEA) e CNR-DT 200 (membratura NTC);
  - EN 1992-1-1 con i valori raccomandati;
  - UNI con DM 31/07/2012: st,max ≤ 300 mm; per i pilastri Ømin 12, As,min 0,003 Ac, passo staffe ≤ min(12 Ømin; b; 250);
  - DS con DK NA: capitolo 9 invariato, tranne As,min e ρw,min delle travi, che restano non implementati;
  - DIN, NS e Model Code 2010: non supportati (regole nazionali o modello di aderenza non disponibili);
  - CS-TR34: non applicabile.
- **Ancoraggi e sovrapposizioni** (`AnchorageCalculator`): barre rettilinee ad aderenza migliorata, α1…α5 = 1.
  - fbd = 2,25 η1 η2 αct fctk,0,05/γc.
  - NTC: lbd = max(lb,rqd; 20Ø; 150), l0 = max(α6 lb,rqd; 0,3 α6 lb,rqd; 20Ø; 200), interferro ≤ 4Ø.
  - Eurocodice: lb,min e l0,min (8.6, 8.11); la sovrapposizione si allunga dell'interferro oltre min(4Ø; 50 mm).
  - `Calculate` riceve fctk,0,05 dal chiamante: il tetto C60/75 resta una scelta del chiamante (R10).
- **Aderenza completa** (0.0.18.0):
  - `BondStrengthClassLimit` = 60: limite di classe di fctk,0,05 per l'aderenza, EC2 8.4.2(2);
  - `BondFctk05(fck, capAtC60 = true)`: |fctk,0,05| di `ConcreteMaterialEN1992` di Model con fck limitato a 60 se
    richiesto (fctk,0,05 = 0,7 fctm non dipende dal diagramma), senza validazione;
  - `Bond(fck, Ø, η1, αct, γc, capAtC60 = true)` → `BondResult` (`Fctk05`, `Capped`, `Fctd`, `Eta1`, `Eta2`,
    `Fbd`), con controlli e ordine di `ConcreteBond.Calculate` di ANTHEA: prima fck finito e positivo, αct finito
    e ≤ 1, γc ≥ 1 (γc NaN o infinito e αct ≤ 0 passano), poi fctk,0,05, poi `BondStrength` con il suo rifiuto. I
    due rifiuti sono `ArgumentException` con messaggi distinti.
- **Dettagli 1D** (`MemberDetailingCalculator`), per travi e pilastri:
  - interferro;
  - copriferro nominale e margine di ogni barra (cmin,dur è un dato del progetto di durabilità);
  - armatura longitudinale minima e massima, staffe minime e passi.
  Ogni controllo in sospeso indica se mancano dati o conferme oppure se la regola non è implementata.
- **Solette piene e pareti** (0.0.18.0, `MemberDetailingKind.Slab` e `Wall`), solo con il profilo NTC 2018 e con
  CNR-DT 200; con gli altri profili `NotSupportedException`. Regole e ordine di ANTHEA (`ConcreteDetailing.cs` a
  98a21d4):
  - i dati che le barre della sezione non danno stanno in `PlateDetailingData`: armatura secondaria (soletta) od
    orizzontale (parete) in mm²/m, somma delle due facce; il suo passo (0 = non dato, controllo in sospeso); zona
    critica della soletta. Si passano con il costruttore nuovo di `MemberDetailingInput` (25 argomenti, l'ultimo
    `MemberDetailingOptions`); senza questi dati soletta e parete danno `ArgumentException`;
  - soletta: striscia rettangolare senza fori (b = larghezza, h = spessore), controllata prima della validazione
    numerica; As,min e As,max delle due facce come la trave ma senza staffe; interasse principale min(2h; 250) in
    zona critica, min(3h; 400) altrove, senza controllo in sospeso se la disposizione non è riconosciuta; armatura
    secondaria ≥ 20 % della principale; passo secondario min(3h; 400) o min(3,5h; 450); ripartizione sulle facce e
    bordi, appoggi e punzonamento in sospeso; niente trattenimento delle barre compresse né ancoraggio agli appoggi;
  - parete: As,v ≥ 0,002 Ac e ≤ 0,04 Ac fuori dalle sovrapposizioni, interasse verticale ≤ min(3t; 400),
    orizzontale ≥ max(0,25 As,v; 0,001 Ac) per metro, passo orizzontale ≤ 400, facce e legature in sospeso;
    nella zona di sovrapposizione As ≤ 0,08 Ac, come i pilastri;
  - i dati di `PlateDetailingData`, se presenti, si validano per ogni tipo di elemento (finiti e non negativi),
    come fa ANTHEA anche per travi e pilastri; con travi e pilastri non cambiano i controlli;
  - un valore di tipo non definito (fuori da 0-3) resta una trave, come nella 0.0.17.0. I valori 2 e 3, non
    definiti nella 0.0.17.0 e trattati allora come trave, ora sono `Slab` e `Wall`: senza i dati della piastra il
    costruttore di `MemberDetailingInput` dà `ArgumentException`. Nessun chiamante converte interi in questo tipo.
- **Opzione legacy `MemberDetailingOptions.LegacyNegativeLinkLegs`** (falsa con `Default`): accetta un numero di
  rami negativo e lo usa com'è in Ast/s della trave, come ANTHEA prima della 0.0.18.0 (`rami_y` del pannello dei
  parametri). Con la famiglia Eurocodice l'interasse trasversale dei rami resta in sospeso. È il comportamento di
  ANTHEA conservato per l'adattatore; la correzione è la proposta F2.8-U2 all'utente.
- **M-curvatura** (`MomentCurvatureAnalysis`): ramo a momento crescente con N costante. Usa il punto limite del
  dominio di rottura nativo e le analisi non lineari, e raffina il primo snervamento per bisezione. È una risposta,
  non un esito normativo. Aggiunte della 0.0.18.0, senza effetto sui chiamanti di oggi (contratto L0):
  - sovraccarico con `MomentCurvatureUnits(force, moment)`: forze, momenti e `AxialTolerance` nelle unità coerenti
    del chiamante (per esempio kN e kNm), etichettate nello Status e nei messaggi; i metodi di oggi usano
    `MomentCurvatureUnits.NewtonMillimetre` («N», «Nmm»). La tolleranza di default di `MomentCurvatureRequest`
    (1000) vale 1000 N senza unità e 1000 nell'unità del chiamante con il sovraccarico (1000 kN con i kN): con altre
    unità il chiamante passa la sua tolleranza. Momenti dei punti e curvature seguono le unità del chiamante;
  - esito strutturato: `InterruptionMessage` (messaggio grezzo che ferma la curva al passo `InterruptedAtStep`) e
    `YieldRefinement` (`Applied`, `Bisections`, `Moment` nelle unità del chiamante, `InterruptionMessage`; nullo se il
    raffinamento non è tentato): bastano a ricostruire lo Status senza leggere il testo inglese, tranne il messaggio
    dell'interruzione. `InterruptionMessage` è il messaggio dell'eccezione lanciata da una funzione del chiamante
    oppure uno dei due testi inglesi della libreria: «Response not finite.» (curvatura o deformazione dell'acciaio non
    finite, anche al punto limite) e «Response: strain state of the limit point not available.» (punto limite pigro
    che restituisce null). Il chiamante che vuole i suoi testi convalida le deformazioni nelle sue funzioni e lancia
    prima il suo messaggio, oppure traduce questi due;
  - punto limite pigro: costruttore di `MomentCurvatureLimit` con `Func<MomentCurvatureStrains>`, valutata al primo
    accesso e conservata nell'istanza. La curva la legge solo al passo limite (frazione 1) e dentro il passo, quindi
    un errore ferma la curva a quel passo; con frazione < 1 non viene mai valutata;
  - rifiuti tipizzati del sovraccarico con le unità: `MomentCurvatureException` (derivata da `ArgumentException`)
    con `Reason` (`InvalidRequest`, `LimitPointNotAvailable`, `AxialResidual`, `NonPositiveLimitMoment`) e i valori
    `AxialForce`, `LimitAxialForce`, `AxialTolerance`, `LimitMoment` (NaN se non raggiunti), con il messaggio di oggi.
    I metodi di oggi lanciano ancora il tipo esatto `ArgumentException` con lo stesso messaggio; entrambi portano il
    motivo in `Exception.Data[MomentCurvatureAnalysis.RejectionKey]`. `MomentCurvatureException` è serializzabile
    con i suoi valori (confini di AppDomain negli host .NET Framework);
  - solo nel sorgente: un `null` o un `default` letterale come ultimo argomento del costruttore di
    `MomentCurvatureLimit` è ambiguo (CS0121; basta il cast al tipo voluto). Come argomento delle unità di
    `Calculate`, `null` sceglie il sovraccarico nuovo, che lo rifiuta con `ArgumentNullException`, mentre `default`
    resta legato al sovraccarico della 0.0.17.0 come token di annullamento.
- **Casi legacy congelati e riprodotti** (`DetailingMigrationTests`):
  - `Fixtures/anchorage-legacy.csv`: 445 ancoraggi, 5 rifiuti, 18 resistenze di aderenza;
  - `Fixtures/bond-legacy.csv`: 1918 casi di `ConcreteBond.Calculate`, cioè 712 calcoli identici bit per bit
    (fctk,0,05 col tetto, fctd, η2, fbd) e 1206 rifiuti nelle stesse righe (746 del primo controllo, 460 della
    resistenza di aderenza); `BondFctk05` coincide bit per bit con Model per 4 diagrammi su fck da 12 a 90 con
    passo 0,5;
  - `Fixtures/detailing-legacy.csv`: 144 travi e pilastri NTC su `detailing-sections.xml`;
  - `Fixtures/detailing-plate-legacy.csv`: 282 righe su `detailing-plate-sections.xml`, cioè 272 calcoli (143
    solette, 123 pareti, 3 travi, 3 pilastri) e 10 rifiuti (7 contorni di soletta, 3 numerici). Le 12 righe con rami
    −2, 3 per tipo, si riproducono con l'opzione legacy e si rifiutano senza. 2610 controlli, 701 in sospeso:
    stesse chiavi nello stesso ordine, stessi esiti e unità, valori e limiti a 1e-9. Le fixture di solette, pareti
    e aderenza vengono dalla cattura A0 di ANTHEA (riferimento F2-pre-f28, `a/tutte`, ANTHEA 1baeb60);
  - `Fixtures/curvature-legacy.csv`: 5 curve.
  Tolleranza delle curve:
  - 1e-7 sulle deformazioni dei punti con acciaio elastico;
  - 1e-3 sulle deformazioni dei punti con acciaio snervato. Con i materiali plastici la traslazione del piano è
    definita solo entro la tolleranza su N del solutore; momento, N e curvatura coincidono a 1e-7.
- I dettagli accettano anche le maggiorazioni di durabilità: superficie irregolare e abrasione sommate a cmin
  (`CoverAddition`) e il copriferro minimo dei getti contro terreno (`GroundCover`, 40 o 75 mm).
- **Contratto della 0.0.17.0** (`DetailingContractTests`): fotografia di regressione di `Detailing/` e `Response/`
  catturata dal codice di 4f54139a prima delle modifiche di F2.8. Non è un atteso indipendente: non si rigenera per
  far passare il test. Con gli argomenti e le opzioni di oggi deve restare identica byte per byte.
  - `Fixtures/detailing-contract-0.0.17.csv`:
    - `MemberDetailingCalculator` su tutti i profili, travi, pilastri e un valore di tipo non definito (7, trattato
      come trave), sulle 6 sezioni di `detailing-sections.xml`, con gli argomenti di ModelChecker (`coverAddition`,
      `groundCover`) e dei pali (22 argomenti) e con gli ingressi limite (rami −1, copriferro NaN, Fctm 0,
      compressione NaN, cmin,dur +∞, NaN e −5, sezione senza barre);
    - `AnchorageCalculator` su tutti i profili con le righe di `anchorage-legacy.csv`, barre lisce e αct ≠ 1;
    - `BondStrength` con le righe di aderenza e i rifiuti.

    Per ogni caso: chiave, valore, limite, unità, esito, riferimento, spiegazione e `NotImplemented` di ogni
    controllo nell'ordine, oppure tipo esatto e messaggio dell'eccezione.
  - `Fixtures/curvature-contract-0.0.17.csv`: le 5 curve di `curvature-legacy.csv` (Status, `InterruptedAtStep`,
    risultati e punti) e, su funzioni analitiche, curve complete, parziali, interrotte e raffinate e i quattro rifiuti
    (richiesta non valida, punto limite assente, residuo su N, momento limite non positivo), in cultura invariante e
    italiana.
  - I double sono scritti in formato round-trip, quindi il confronto è bit per bit.

### Durabilità e copriferri

- **Classi di esposizione** (`ExposureClasses`): le 18 classi di EN 206 con
  - i valori del prospetto F.1 di UNI EN 206-1 (a/c, classe minima, cemento, aria), verificati sul testo;
  - il gruppo ambientale NTC (Tab. 4.1.III, verificata);
  - i requisiti della UNI 11104:2016, prospetto 5, ripresi da ANTHEA (classe minima, a/c, cemento, aria per
    XF2-XF4) come riportati in ATECAP 2020 p. 19 (fonte secondaria). La UNI 11104:2025 (in vigore dal 24/07/2025),
    prospetto 6, secondo un estratto del 28/07/2025 (fonte secondaria, da riscontrare sul testo della norma), ha le
    stesse classi minime tranne XF1 (C30/37), a/c 0,55 per XF1 e cementi minimi più bassi. XC3, XD1, XF4 e XA1 sono
    C30/37 in entrambe le edizioni (C28/35 è attribuito alla UNI 11104:2004). La libreria usa i valori della 2016;
  - le classi indicative dell'Appendice E: EN 1992-1-1 prospetto E.1N, DM 31/07/2012 (XC1 C25/30, XF2 C30/37),
    DK NA Tabel E.1(2) (12/30/35/40 MPa per gruppi). XF4 non è nel prospetto E.1N.
  Le combinazioni agiscono insieme; X0 non si combina.
  `ExposureClasses.All` è una vista di sola lettura (`ReadOnlyCollection`) del catalogo privato che `Get` e `Resolve`
  leggono: non si converte in array e non si può modificare. Il tipo del campo e l'ordine delle classi non cambiano
  (`DurabilityEdgeCaseTests.ExposureCatalogIsReadOnly`).
- **Copriferri** (`CoverRequirements.Calculate`), profili per tipo esatto:
  - NTC 2018 e CNR-DT 200: tabella C4.1.IV della Circolare (verificata), con +10 mm per 100 anni, +5 mm sotto Cmin,
    −5 mm con controllo di qualità. Cmin è un dato (la classe pertinente all'esposizione);
  - EN 1992-1-1 e UNI (valori raccomandati, DM 31/07/2012): prospetto 4.4N con le classi strutturali S1-S6 del
    prospetto 4.3N;
  - DS: Tabel 4.4N NA senza classi strutturali, Δcdev ≥ 5 mm, solo 50 anni;
  - DIN, NS, Model Code 2010, CNR-DT 204: non supportati; ACI e AASHTO: implementazione futura.
  cmin = max(10; cmin,b; cmin,dur) + superficie + abrasione; cnom = max(cmin + Δcdev; getto contro terreno).
- **Classe minima di resistenza** (`ExposureClasses.MinimumStrength`): UNI 11104:2016, prospetto 5, per NTC e
  CNR-DT 200, Appendice E (informativa) per EN e UNI, DK NA E.1(2) per DS. Per NTC e CNR-DT 200 il riferimento
  restituito resta «UNI 11104 prospetto 5 (NTC 2018 §11.2.11)».
- **Casi legacy congelati e riprodotti** (`DurabilityMigrationTests`, `Fixtures/durability-legacy.csv`): 588
  copriferri EC2, 1932 NTC, 505 rifiuti e 23 requisiti UNI 11104 su 24 combinazioni, 7 resistenze e 6 insiemi di
  opzioni. Casi limite in `DurabilityEdgeCaseTests`, cinque esempi in `DurabilityExamplesTests`.
- **Contratto della 0.0.17.0** (`DurabilityContractTests`, `Fixtures/durability-contract.json`): fotografia di
  regressione di `Durability/` catturata dal codice di 4f54139a prima delle modifiche di F2.9. Non è un atteso
  indipendente: non si rigenera per far passare il test. Registra:
  - tutti i campi di `ExposureClasses.All` e `Get` sui 18 codici e su codici non validi;
  - `DurabilityProfiles` (`TryResolve`, `NotSupportedReason`, `Resolve`, `Reference`) su tutte le norme di Model;
  - su 37 insiemi di esposizioni (i 24 di `durability-legacy.csv`, insiemi per i gruppi danesi, insiemi non validi):
    `Resolve`, `MinimumStrength` sui 5 profili, `Uni11104MinimumStrength`, `En206MinimumStrength`, `Uni11104Mix` e
    `Uni11104Air` con 15 valori di Dmax;
  - `StructuralClass` e `Calculate` sui 5 profili con fck, vita, opzioni, Ø, Dmax, Δcdev, abrasione, getto contro
    terreno e Cmin pertinente, anche non validi e combinati.

  Per ogni stato ci sono i campi del risultato, oppure tipo, messaggio e parametro dell'eccezione. I double sono
  scritti in formato round-trip, quindi il confronto è bit per bit.

  La sezione `standards` dipende anche da Model: registra `Standard.Name` delle norme di Model 5ad56681, che compare
  pure nel testo di `NotSupportedReason`. Se Model cambia il nome di una norma, il test fallisce anche senza modifiche
  in Checker. Va trattato come un cambio del contratto da verificare e registrare, non come un errore del test.
- Restano in ANTHEA la presentazione della composizione della miscela (`MixAutomation`, con i testi e le note della
  scheda) e quella dei diagrammi. I limiti di composizione sono in libreria: `ExposureClasses.Uni11104Mix` (a/c
  massimo e cemento minimo) e `ExposureClasses.Uni11104Air` (aria per XF2-XF4).
