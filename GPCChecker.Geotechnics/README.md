# GPCChecker.Geotechnics

Metodi geotecnici trasferiti da ANTHEA: pendii e stabilità globale, cedimenti, Newmark, capacità sismica delle
fondazioni, pali e micropali, spinte ed equilibrio dei muri. Organizzazione decisa il 1 ottobre 2026 (fase M1 di
`docs/migrazione-anthea/MIGRAZIONE_ANTHEA.txt`).

## Convenzioni

- **Unità di Model in tutte le API:**
  - forze N, lunghezze e quote mm, tensioni, resistenze e moduli MPa (N/mm²);
  - pesi di volume N/mm³, angoli rad;
  - quota z verso l'alto.
  La conversione dalle unità del foglio (kN, m, kPa, kN/m³, gradi) avviene nell'interfaccia chiamante, con
  `GPC.Model.Geotechnics.SoilUnits` (per esempio 18 kN/m³ = 18 · `SoilUnits.KiloNewtonPerCubicMetre` = 1,8·10⁻⁵ N/mm³).
- **Dati in Model:**
  - `GPC.Model.Geotechnics.Soil`: parametri caratteristici con provenienza obbligatoria; i parametri facoltativi
    mancanti restano nulli;
  - `SoilLayer` e `SoilProfile`: stratigrafia, falda idrostatica, tensioni geostatiche.
- **Norme in Model** (`GPC.Model.Standards`):
  - `StandardNTC2018Geotechnics`: capitoli 6 e 7.11, valori usati dal codice ANTHEA;
  - `StandardEN1997p1`: EN 1997-1:2004, valori raccomandati dell'Allegato A; approccio DA1/DA2/DA3 ed esecuzione dei
    pali espliciti.

  Entrambe forniscono:
  - gli insiemi A e M e i coefficienti γR per verifica e situazione;
  - le combinazioni richieste;
  - i coefficienti di correlazione dei pali.

  Le sostituzioni sono esplicite (`SetResistanceFactor`, `SetMaterialSet`, `SetActionSet`). Una verifica che la norma
  non definisce non ha combinazioni: `GeotechnicalActions.Required` dà `NotSupportedException`, mai i coefficienti di
  un'altra verifica.
- **Dipendenze:** Model, Geometry e Utilities. Nessuna da ANTHEA, dai fogli JSON o dal solver del calcestruzzo. Le
  verifiche c.a. di muri e pali restano in GPCChecker.Concrete.

## Contenuto

| Tipo | Funzione |
| --- | --- |
| `DesignSoil` | Parametri di progetto di un terreno per un insieme M (tan φ'/γφ', c'/γc', cu/γcu, γ/γγ) |
| `GeotechnicalActions` | Coefficiente di un'azione per categoria e insieme A; combinazioni richieste di una verifica |
| `Slopes.BishopSolver` | Bishop semplificato su conci assegnati |
| `Slopes.SlopeGeometry` | Cerchi per uscita, ingresso e profondità; famiglia tangente; divisioni in conci; ammissibilità |
| `Slopes.SlopeStability` | Conci di un cerchio (pesi, falda, corpi rigidi, carichi, sisma pseudostatico) e ricerca del cerchio critico per combinazione |
| `Foundations.FoundationSettlement` | Cedimento edometrico 1D sotto una striscia (Boussinesq, carico trapezio, scarico dello scavo) |
| `Foundations.ShallowFoundationSeismic` | Portanza sismica di una striscia su terreno incoerente asciutto, EN 1998-5 Annesso F |
| `Seismic.NewmarkSliding` | Blocco rigido di Newmark su accelerogramma lineare a tratti |

## Geotecnica generale (trasferita da ANTHEA, commit fe4652c)

- **Pendii.** I terreni sono `Soil` di Model a strati orizzontali, con una seconda colonna facoltativa a valle. Si
  modellano falda idrostatica, corpi rigidi (per esempio il muro, che sostituisce il terreno occupato) e carichi
  identificati, ciascuno con il proprio coefficiente.
  - `SlopeFactors` contiene i coefficienti di una combinazione. `FromCombination` li ricava dalle norme di Model (NTC
    2018 A2+M2+R2, γR 1,1; sisma: fattori 1, γR 1,2). Un insieme A con γG1 diversi per favorevole e sfavorevole (EN DA1
    A1) è rifiutato: i coefficienti vanno dati esplicitamente.
  - La ricerca combina una griglia di uscite, ingressi e profondità, la famiglia tangente e il raffinamento intorno a
    sei minimi distinti, e controlla il cerchio critico con il doppio dei conci.
  - Esiti: `SlopeSearchStatus` (nessuna superficie, ricerca incompleta, minimo sul bordo, non convergente, convergente),
    il rapporto η = γR/F e `IsConclusive`.
  - `RefinedFactor` distingue il caso non convergente da quello in cui il controllo non ha soluzione (trazione).
- **Cedimenti.** Cedimento edometrico sotto una striscia. `SettlementLayer.FromProfile` ricava gli strati da un
  `SoilProfile` di Model; un Eoed mancante è un dato mancante, mai un valore assunto.
- **Newmark.** Blocco rigido in una sola direzione, con g = 9,81 m/s² come nel legacy. All'arresto la velocità è posta
  a zero: nel legacy restava un residuo di ±1e-17 che poteva aggiungere un punto finale fittizio.
- **Annesso F.** Coefficienti dei terreni incoerenti. Nmax = ½ γ (1 − kv) B² Nγ con Nγ = 2 (Nq − 1) tan φ'. Verificato
  sul testo di EN 1998-5:2004 (F.1-F.8, Tab. F.1, Tab. F.2 di γRd): la (F.7) dà F̄ = ag S/(g tan φ'd) senza γRd, che
  ANTHEA applicava anche a F̄. Per difetto la libreria segue la norma; `modelFactorOnInertia: true` riproduce ANTHEA (più
  cautelativo per γRd > 1). Un γR nazionale aggiuntivo è esplicito.
- **Unità.** Le forze per unità di lunghezza hanno lo stesso valore numerico (1 kN/m = 1 N/mm). Le tolleranze
  geometriche del legacy, in metri, sono riportate in mm.
- **Differenza intenzionale.** I cerchi della griglia costruiti esattamente ai limiti di profondità non vengono più
  scartati per arrotondamento (tolleranza di 1e-9 mm). Il legacy, per esempio, scarta 9 dei 24 cerchi della ricerca
  S2/Q2N.
- **Casi legacy congelati e riprodotti** (`GeneralGeotechnicsMigrationTests`, `Fixtures/geotechnics-*.csv`, harness
  ANTHEA dadea50):
  - Bishop: 321 soluzioni, 90 senza soluzione, 3 rifiuti;
  - 72 funzioni geometriche;
  - 6 cerchi assegnati con tutti i conci;
  - 15 ricerche: S1, S3 e S4 identiche in ogni contatore. Su S2 il cerchio critico del legacy è rivalutato in modo
    identico e il minimo del porting non è superiore;
  - 121 tensioni e 36 cedimenti con tutte le fette;
  - 50 integrazioni di Newmark con tutti i punti;
  - 3900 casi dell'Annesso F.
- **Test aggiuntivi.** Casi limite e dati rifiutati in `SlopeEdgeCaseTests` e `FoundationSeismicEdgeCaseTests`, cinque
  esempi in `GeneralGeotechnicsExamplesTests`.

## Pali e micropali (trasferiti da ANTHEA, commit fe4652c)

Tutti i dati vengono da Model:
- terreni e stratigrafie: `Soil`, `SoilProfile` (testa del palo al piano campagna, falda del profilo);
- tubolari dei micropali: `SectionCHS`, dai cataloghi di ModelData (EN 10210-2, gamma Celsius EN 10210, EN 10219-2: ci
  sono tutti i 64 tubi del catalogo di ANTHEA) o da D e t;
- acciaio: `SteelMaterial`, con γM0 della norma acciaio `StandardEN1993p11` (NTC 1,05, EN 1,00); il peso del tubo usa la
  densità del materiale (t/mm³) per `SoilUnits.Gravity` = 9810 mm/s² (7850 kg/m³ · 9,81 m/s² per gli acciai di Model);
- coefficienti: ξ3, ξ4, γb, γs, γst, γT e γG dalle norme geotecniche di Model (`FromStandard`).

I parametri propri del metodo stanno nella libreria: comportamento granulare o coesivo dello strato, addensamento per K,
Nc, terreno e α di Bustamante-Doix.

| Tipo | Funzione |
| --- | --- |
| `Piles.BearingCapacityFactors` | Nq (D ≤ 0,80 m) e Nq* (D > 0,80 m), versione NQ-2026-09-09 |
| `Piles.BustamanteDoix` | Tab. 13.12-13.13 e abachi 13.16-13.19 di Viggiani; tratti iniettati di un micropalo |
| `Piles.MicropileTube` | Peso del micropalo (acciaio e malta); classe del CHS; momento resistente con interazione N-M lineare |
| `Piles.LateralPileCapacity` | Capacità trasversale: Broms ed estensione stratificata; diagrammi, diagnostica delle tensioni; verifica con ξ, γR ed efficienza (manuale o Reese e Van Impe) |
| `Piles.AxialPileCapacity` | Portanza verticale di pali e micropali lungo la profondità, con più verticali indagate, curve di progetto, azioni con il peso, efficienza di gruppo (Converse-Labarre, Feld, assegnata) |

**Nq.** Equazioni, non tabelle di valori:
- D ≤ 0,80 m: rette del diagramma semilogaritmico fornito dall'utente, Nq = 10^(1 + (φ − φ10)/(φ100 − φ10)), con gli
  ancoraggi φ10 e φ100 dati per L/D = 5, 10, 20, 50;
- D > 0,80 m: cubiche a tratti in u = φ − 34° (continuità C2 a 34° e 38°) adattate alla figura per L/D = 4 e 32;
- tra le curve z/D è interpolato in scala logaritmica, con media geometrica di Nq e aritmetica di Nq*;
- fuori dal tratto visibile si usa il bordo, segnalato.

Le fonti (immagini, digitalizzazione, punti di controllo) sono in `ANTHEA/supporto/documentazione/riferimenti_nq`. Le curve
dei pali medi sono quelle di Berezantzev et al. (1961), fig. 13.6 di Viggiani, Fondazioni (stessi assi e curve).

**φ ridotto (opzione).** `NqFrictionAngle` sceglie l'angolo di Nq: quello dello strato (predefinito, come ANTHEA) o quello
suggerito da Kishida (1967) per l'effetto dell'installazione (Viggiani, Fondazioni, §13.1.2 p. 376):
- pali battuti: φ' = (φ'1 + 40°)/2 (aumenta sotto 40°, riduce sopra);
- pali trivellati, anche a elica continua: φ' = φ'1 − 3°, non sotto 0.

`BearingCapacityFactors.Kishida(installazione)` dà la regola della tecnologia; `AxialPile.BaseFrictionAngle` la applica
solo alla punta (il fusto resta con φ' dello strato), con avviso se la regola scelta non è quella della tecnologia. φ'
ridotto fuori dal tratto visibile delle curve usa il bordo, segnalato. `NqResult` riporta φ'1 e φ' adottato.

**Bustamante-Doix.**
- Formula: Rs = Σ π α D L s; pl = pressione d'iniezione (ipotesi progettuale di ANTHEA).
- Tabelle 13.12 e 13.13 di Viggiani verificate sulle pagine scansionate.
- Abachi digitalizzati, senza estrapolazione; le curve R sono il limite inferiore.
- Tabelle e abachi stanno qui, in Checker, come dati del metodo; terreno di Bustamante-Doix e α per strato sono
  parametri del metodo (`MicropileSurvey`), i terreni restano i `Soil` di Model.

**Broms.** Viggiani pp. 400-415, verificato con le formule chiuse nei test:
- testa impedita, palo corto: 9 cu D (L − 1,5 D) e 1,5 γ D L² Kp;
- testa libera, palo lungo in argilla: My = H (1,5 D + f/2);
- testa libera, palo lungo in sabbia: My = (2/3) H √(H/(1,5 Kp γ D)).

L'estensione stratificata con reazioni distribuite (G. Pacini) è un modello sperimentale.

**Portanza verticale** (motore di `Calcolo.cs`, che in ANTHEA resta solo adattatore).
- Fusto: τ = c' + K μ σ'v,media in condizioni drenate; α(cu) cu sotto falda in condizioni non drenate.
- Base: A σ'v Nq; nei coesivi sotto falda in condizioni non drenate A (Nc cu + σv).
- Micropali: fusto di Bustamante-Doix lungo l'asse, quota di punta 0-15%.
- Curve di progetto: min(media/ξ3; minimo/ξ4) con γb, γs, γst ed ηg.
- K e μ per tipo di palo e α(cu): Viggiani, Fondazioni, Tab. 13.2 e 13.3, verificate sulla scansione. Unica differenza:
  per i pali battuti il libro stampa α = 1 − 0,011 (cu − 25), ANTHEA 0,0111 (continuo a 70 kPa, al più 0,9% più basso, a
  favore di sicurezza), mantenuto.

**Tab. 6.4.II NTC.** I coefficienti dei pali dipendono dall'esecuzione: γb vale 1,15 per i pali infissi, 1,35 per i
trivellati e 1,30 per quelli a elica continua. `StandardNTC2018Geotechnics.PileExecution` li seleziona; il valore
predefinito è trivellato, come ANTHEA, che usava 1,35 per ogni palo.

**Casi legacy congelati e riprodotti** (`PilesMigrationTests`, `PileCapacityMigrationTests`, `Fixtures/piles-*.jsonl`,
harness ANTHEA 2663c96):
- 805 Nq e 566 valori delle curve;
- Bustamante-Doix: intervalli di α, letture degli abachi, tratti, coseni;
- 388 pesi CHS e 121 sezioni;
- 126 pali orizzontali con diagrammi e diagnostica, 12 rifiuti;
- 37 pali e micropali verticali con le curve a tutte le quote, 10 rifiuti.

Gli input non esprimibili con i tipi (nomi sconosciuti, strati disattivati nel modello orizzontale) sono elencati nei test.
Casi limite in `PileEdgeCaseTests`, cinque esempi in `PileExamplesTests`; φ ridotto, peso del tubo dal materiale e tubi di
ANTHEA nei cataloghi di ModelData in `PileOptionsTests`.

## Muri di sostegno (trasferiti da ANTHEA, commit fe4652c) — fase W1

Dati di Model: terreni `Soil` e due colonne `SoilProfile` (a monte con il piano campagna in testa al muro, a valle alla quota
Dv; granulari, c' = 0, falda come dato del muro `WallWater`), terreno di posa `Soil`. Origine al piede di valle sul piano di posa.

| Tipo | Funzione |
| --- | --- |
| `Walls.EarthPressure` | Rankine, Coulomb/Mononobe-Okabe (componente orizzontale, attrito del paramento), Jaky (Wood), φd = atan(tan φ/γM) |
| `Walls.WallInterface` | attrito del paramento e della base: assegnato o k·atan(tan φcv/γM), k = 1, 2/3, 0 |
| `Walls.WallContact` | contatto senza trazione (trapezio nel terzo medio, triangolo 3x altrimenti), integrazione delle pressioni |
| `Walls.WallCombinations` | preset NTC A1+M1+R3 (SLE, SLU con ogni fattore, valle, acqua, permanenti, variabili principali), sisma assegnato o SLV generale e ribaltamento, eccezionali |
| `Walls.RetainingWallAnalysis` | equilibrio per metro: spinte, sottospinta, pesi, terreno a valle e passiva limitata, scorrimento, ribaltamento, portanza EN 1997-1 Annesso D e sismica EN 1998-5 Annesso F, sollecitazioni di fusto, mensola a valle e a monte, verifiche geotecniche |
| `Seismic.NtcSiteAmplification` | amax = Ss St ag (NTC Tab. 3.2.IV, 3.2.V), con Ss o St assegnati |

Fonti NTC 2018 verificate sul testo: Tab. 6.5.I (γR 1,4 portanza, 1,1 scorrimento, 1,15 ribaltamento sulle azioni
stabilizzanti), §7.11.6.2.1 (kh = βm amax/g, kv = ±0,5 kh, βm 0,38 allo SLV, 1 per muri non liberi di spostarsi, +50% e ≤ 1
per il ribaltamento), Tab. 7.11.III (1,2, 1,0, 1,0), Tab. 3.2.IV (Ss) e 3.2.V (St). La NTC indica di non contare in generale
la passiva a valle nello scorrimento: in `WallValley` è un'opzione, spenta per difetto, con la frazione mobilitata.

Casi legacy congelati e riprodotti (`WallsMigrationTests`, harness ANTHEA cbed972, `Fixtures/walls-*`): 798 valori delle
leggi, 110 generazioni di combinazioni (2380 righe), 88 muri completi (149.922 sezioni, 8332 verifiche) e 14 documenti
rifiutati; 8 documenti rifiutati da ANTHEA per motivi fuori da W1 (strutturali o d'interfaccia) sono elencati nel test.
Differenze intenzionali: quote in mm (i tagli coincidenti sono unificati entro 1e-6 mm); alla quota di un carico concentrato
ANTHEA include la forza secondo l'arrotondamento di ht − z in metri (36 tagli: il port la include alla quota e la esclude
1e-4 mm sopra); 35 momenti NaN di ANTHEA (colonna di valle con falda) sono finiti nel port.

**W3 — esercizio e stabilità globale.**
- `Walls.WallServiceability`: cedimenti edometrici sotto il contatto della base in ogni combinazione SLE (piede, centro,
  tallone; convergenza 40/80 suddivisioni; tensione residua al fondo ≤ 10% del netto o base rigida), rotazione, spostamento
  elastico del fusto dalle curvature e spostamento della testa disaccoppiato u = u_fusto + H/k − θ H, scorrimento di
  Newmark con accelerogrammi SLD/SLV confermati (non con Wood).
- `Walls.WallGlobalStability`: profilo (proposto dal muro con `Propose`, da confermare), combinazioni A2+M2+R2 dalle
  ordinarie (G1 1, le altre × 1,3/1,5, M2, γR 1,1), eccezionali, SLV con βs 0,38 e γR 1,2; muro come corpo rigido, azioni
  come carichi del pendio; verifiche con γR/F solo se il risultato è un verdetto.
- Casi legacy riprodotti (`WallsServiceGlobalMigrationTests`): 6 documenti di esercizio (18 casi, 12 accelerogrammi, 75
  verifiche), 380 combinazioni globali di 102 documenti, 3 documenti di stabilità globale (8 combinazioni).
- Difetto legacy corretto anche nei pendii (G): `Divisions` calcolava l'estremo destro dell'arco come L + (R − L)·n/n,
  che può superare R di un ulp ed essere scartato con l'ultimo concio (2 combinazioni non drenate: F legacy 3,630 su 34
  conci, il test lo ritrova togliendo l'ultimo concio). Ora gli estremi sono esatti; le fixture di G restano riprodotte.

Prossime fasi: W2 verifiche strutturali (gravità e c.a. con Concrete, comprese le curvature per gli spostamenti), W4
dettagli delle armature.

## Migrazione (prossime famiglie)

1. Muri di sostegno: W2-W4 (strutture, esercizio, stabilità globale, armature).

Ogni famiglia segue lo stesso schema:
- casi legacy congelati con l'harness `ANTHEA/supporto/test/CheckerMigration.Capture`;
- confronto dopo la conversione delle unità;
- test con calcoli a mano.
