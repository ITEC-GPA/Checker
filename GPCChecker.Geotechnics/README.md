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

## Migrazione (prossime famiglie)

1. Geotecnica generale: Bishop e stabilità dei pendii, cedimento edometrico, Newmark, EN 1998-5 Annesso F.
2. Pali e micropali: Broms, Bustamante-Doix, Nq.
3. Muri di sostegno: spinte, equilibrio, sismica, stabilità globale; le sezioni c.a. passano a Concrete.

Ogni famiglia segue lo stesso schema:
- casi legacy congelati con l'harness `ANTHEA/supporto/test/CheckerMigration.Capture`;
- confronto dopo la conversione delle unità;
- test con calcoli a mano.
