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
- **Annesso F.** Coefficienti dei terreni incoerenti. Nmax = ½ γ (1 − kv) B² Nγ con Nγ = 2 (Nq − 1) tan φ'. γRd
  moltiplica anche F̄. Un γR nazionale aggiuntivo è esplicito.
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

## Migrazione (prossime famiglie)

1. Pali e micropali: Broms, Bustamante-Doix, Nq.
2. Muri di sostegno: spinte, equilibrio, sismica, stabilità globale; le sezioni c.a. passano a Concrete.

Ogni famiglia segue lo stesso schema:
- casi legacy congelati con l'harness `ANTHEA/supporto/test/CheckerMigration.Capture`;
- confronto dopo la conversione delle unità;
- test con calcoli a mano.
