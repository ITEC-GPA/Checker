# GPCChecker.Concrete

Solutore sezionale del calcestruzzo armato e precompresso, con domini di rottura, analisi tensionali lineari e non
lineari e piani di deformazione. Comprende anche i nuclei di verifica trasferiti da ANTHEA (migrazione in
`docs/migrazione-anthea/MIGRAZIONE_ANTHEA.txt`; registro di dettaglio in `Model/CHECKER_PASSO_4.txt`).

Unità: N, Nmm, mm, MPa; compressione negativa.

## Verifiche trasferite da ANTHEA

| Namespace | Contenuto | Origine in ANTHEA (commit fe4652c) |
| --- | --- | --- |
| `GPC.Checkers.Concrete.Shear` | Taglio di sezione in una direzione: `SectionShearCalculator`, `SectionShearInput`, `SectionShearResult`, `ShearProfiles` | `ConcreteCodeChecks.Shear`, `Ntc2018Checks.Shear` |
| `GPC.Checkers.Concrete.Serviceability` | Limiti tensionali SLE di uno stato già calcolato: `StressLimitCheck` | `CheckerSection.DescribeStress` |
| `GPC.Checkers.Concrete.Torsion` | Torsione con interazione del taglio nelle due direzioni: `SectionTorsionCalculator`, `SectionTorsionInput`, `TorsionGeometry`, `TorsionProfiles` | `ConcreteTorsionCalculator`, `ConcreteShearAnalysis.Torsion` |
| `GPC.Checkers.Concrete.Cracking` | Fessurazione di sezione: `SectionCrackCheck`, `CrackRequirements`, `CrackWidthCalculator`, `CrackSectionGeometry`, `CrackProfiles` | `Ntc2018Checks.Cracking`, `ConcreteCodeChecks` (requisiti, wk, hc,eff), `ConcreteTensionCracking`, `ConcreteInnerCracking`, `TensionBarSpacing`, `SectionRegions` |
| `GPC.Checkers.Concrete.Detailing` | Aderenza, ancoraggi e sovrapposizioni (`AnchorageCalculator`), dettagli 1D di travi e pilastri (`MemberDetailingCalculator`), `DetailingProfiles` | `ConcreteBond`, `ConcreteAnchorageCalculator`, `ConcreteDetailingCalculator` |
| `GPC.Checkers.Concrete.Response` | Curva momento-curvatura a N costante (`MomentCurvatureAnalysis`): risposta numerica, non verifica | `MomentCurvatureCalculator`, `ConcreteCurvatureAnalysis` |
| `GPC.Checkers.Concrete.Durability` | Classi di esposizione e loro requisiti (`ExposureClasses`), copriferri per norma (`CoverRequirements`, `DurabilityProfiles`), classi minime di resistenza | `Materiali.Durability`, `NtcCover`, `MinimumConcrete`, `MaterialCover` |

I casi legacy di `GPCChecker.Test.Concrete/Fixtures` (CSV e archivi XML delle sezioni) sono catture del codice di
ANTHEA, non attesi indipendenti. Congelati il 1/10/2026 da ANTHEA fe4652c, sono stati ricatturati il 7/10/2026 dal
branch `refactoring/integrazione-d7b-d2` di ANTHEA (commit d2d3225, `supporto/test/CheckerMigration.Capture`, modalità
`tutte`), che nella fessurazione si comporta già come la libreria: wk = 0 con l'asse neutro nel copriferro, limite
superiore dell'eq. (7.14) con le barre tese fuori da Ac,eff, k2 = 0,5 con l'asse neutro interno (D7-b), h − x limitato
nelle fasce interne dei fori (0.0.17.0). I test di migrazione li riproducono con la regola corrente, senza casi
speciali; l'opzione legacy `NtcK2FromCompressedBars` resta provata nei test dedicati.

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
- Casi legacy congelati: `Fixtures/stress-legacy.csv` (2016 stati) e `Fixtures/stress-sections.xml`
  (`ServiceabilityMigrationTests`).
- Trefoli con predeformazione nulla: `NotSupportedException`.

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
- **Non supportati:**
  - CS-TR34: non applicabile;
  - CNR-DT 204: modello FRC non implementato;
  - CNR-DT 200: membratura NTC;
  - precompressione;
  - superfici interne di fori non rettangolari o non circolari.
- **Casi legacy congelati e riprodotti** (`CrackMigrationTests`):
  - `Fixtures/crack-legacy.csv`: 936 stati su 6 sezioni (`crack-sections.xml`), con 1112 regioni confrontate;
  - `Fixtures/crack-scalar-legacy.csv`: 1400 aperture e 1596 requisiti.
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
- **Dettagli 1D** (`MemberDetailingCalculator`), per travi e pilastri:
  - interferro;
  - copriferro nominale e margine di ogni barra (cmin,dur è un dato del progetto di durabilità);
  - armatura longitudinale minima e massima, staffe minime e passi.
  Le regole di piastre e pareti restano alle verifiche plate. Ogni controllo in sospeso indica se mancano dati o
  conferme oppure se la regola non è implementata.
- **M-curvatura** (`MomentCurvatureAnalysis`): ramo a momento crescente con N costante. Usa il punto limite del
  dominio di rottura nativo e le analisi non lineari, e raffina il primo snervamento per bisezione. È una risposta,
  non un esito normativo.
- **Casi legacy congelati e riprodotti** (`DetailingMigrationTests`):
  - `Fixtures/anchorage-legacy.csv`: 445 ancoraggi, 5 rifiuti, 18 resistenze di aderenza;
  - `Fixtures/detailing-legacy.csv`: 144 travi e pilastri NTC su `detailing-sections.xml`;
  - `Fixtures/curvature-legacy.csv`: 5 curve.
  Tolleranza delle curve:
  - 1e-7 sulle deformazioni dei punti con acciaio elastico;
  - 1e-3 sulle deformazioni dei punti con acciaio snervato. Con i materiali plastici la traslazione del piano è
    definita solo entro la tolleranza su N del solutore; momento, N e curvatura coincidono a 1e-7.
- I dettagli accettano anche le maggiorazioni di durabilità: superficie irregolare e abrasione sommate a cmin
  (`CoverAddition`) e il copriferro minimo dei getti contro terreno (`GroundCover`, 40 o 75 mm).

### Durabilità e copriferri

- **Classi di esposizione** (`ExposureClasses`): le 18 classi di EN 206 con
  - i valori del prospetto F.1 di UNI EN 206-1 (a/c, classe minima, cemento, aria), verificati sul testo;
  - il gruppo ambientale NTC (Tab. 4.1.III, verificata);
  - i requisiti UNI 11104 di ANTHEA (classe minima, a/c, cemento, aria per XF2-XF4). Fonte secondaria (ATECAP
    2020): XC3, XD1, XF4 e XA1 danno C30/37 dove UNI 11104:2004 dava C28/35, a favore di sicurezza;
  - le classi indicative dell'Appendice E: EN 1992-1-1 prospetto E.1N, DM 31/07/2012 (XC1 C25/30, XF2 C30/37),
    DK NA Tabel E.1(2) (12/30/35/40 MPa per gruppi). XF4 non è nel prospetto E.1N.
  Le combinazioni agiscono insieme; X0 non si combina.
- **Copriferri** (`CoverRequirements.Calculate`), profili per tipo esatto:
  - NTC 2018 e CNR-DT 200: tabella C4.1.IV della Circolare (verificata), con +10 mm per 100 anni, +5 mm sotto Cmin,
    −5 mm con controllo di qualità. Cmin è un dato (la classe pertinente all'esposizione);
  - EN 1992-1-1 e UNI (valori raccomandati, DM 31/07/2012): prospetto 4.4N con le classi strutturali S1-S6 del
    prospetto 4.3N;
  - DS: Tabel 4.4N NA senza classi strutturali, Δcdev ≥ 5 mm, solo 50 anni;
  - DIN, NS, Model Code 2010, CNR-DT 204: non supportati; ACI e AASHTO: implementazione futura.
  cmin = max(10; cmin,b; cmin,dur) + superficie + abrasione; cnom = max(cmin + Δcdev; getto contro terreno).
- **Classe minima di resistenza** (`ExposureClasses.MinimumStrength`): UNI 11104 per NTC e CNR-DT 200, Appendice E
  (informativa) per EN e UNI, DK NA E.1(2) per DS.
- **Casi legacy congelati e riprodotti** (`DurabilityMigrationTests`, `Fixtures/durability-legacy.csv`): 588
  copriferri EC2, 1932 NTC, 505 rifiuti e 23 requisiti UNI 11104 su 24 combinazioni, 7 resistenze e 6 insiemi di
  opzioni. Casi limite in `DurabilityEdgeCaseTests`, cinque esempi in `DurabilityExamplesTests`.
- Restano in ANTHEA la composizione della miscela (`MixAutomation`) e la presentazione dei diagrammi.
