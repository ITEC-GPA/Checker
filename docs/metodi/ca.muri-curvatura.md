---
id: ca.muri-curvatura
titolo: Curvatura e piano delle deformazioni totali nell'analisi lineare con viscosità
libreria: GPCChecker.Concrete
classi:
  - GPC.Checkers.Concrete.Results.StressAnalysisResult
  - GPC.Checkers.Concrete.Walls.WallSectionSession
  - GPC.Checkers.Concrete.Walls.WallServiceabilityResult
versione: 0.9.0.0
norme: [en1992-1-1-7.4.3, ntc2018-4.1.2.2.5]
stato: bozza
---

# Curvatura e piano delle deformazioni totali nell'analisi lineare con viscosità

## 1. Scopo

Deformazioni e curvatura reali di una sezione in calcestruzzo armato nell'analisi elastica lineare con viscosità, per le
deformazioni mostrate all'utente e per gli spostamenti dei muri (integrazione della curvatura lungo il paramento).
Restituisce il piano delle deformazioni totali `StressAnalysisResult.TotalStrainPlane` e, per le strisce di muro, la
curvatura `WallServiceabilityResult.Curvature`. Voci F4-08 e F4-43 del registro delle differenze di ANTHEA.

## 2. Campo di applicazione

| Caso | Comportamento |
| --- | --- |
| Analisi lineare con ψ > 0 (viscosità delle armature) | piano del solutore amplificato di 1 + ψ |
| Analisi lineare con ψ = 0 o analisi non lineare | piano del solutore, invariato |
| ψ dei cavi diverso da quello delle armature | il piano totale usa ψ delle armature; le deformazioni dei cavi restano con il proprio fattore |
| Combinazioni caratteristica, frequente e quasi permanente dei muri | stesso ψ in tutte e tre (scelta confermata dall'utente) |

## 3. Riferimenti normativi

| Formula | Norma | Paragrafo | Riscontro |
| --- | --- | --- | --- |
| (E-C1) modulo efficace | EN 1992-1-1 | §7.4.3 (5), formula 7.20 | testo |
| (E-C2) coefficiente di omogeneizzazione | NTC 2018 / EN 1992-1-1 | §4.1.2.2.5 / §7.4.3 | — (scelta del metodo: n = Es(1+ψ)/Ec) |

## 4. Ipotesi

- Sezioni piane; acciaio e calcestruzzo compresso elastici lineari; calcestruzzo teso trascurato salvo opzione.
- Viscosità con il metodo del modulo efficace: Ec,eff = Ec/(1 + ψ) per tutto il carico della combinazione.
- La parte variabile delle combinazioni caratteristica e frequente è trattata come quella permanente: la sovrastima dello
  spostamento è a favore di sicurezza (decisione dell'utente del 14/10/2026; la sovrapposizione è rinviata).

## 5. Notazione, unità e convenzioni

| Simbolo | Significato | Unità |
| --- | --- | --- |
| ψ | coefficiente di viscosità delle armature dell'analisi | — |
| n | Es(1 + ψ)/Ec | — |
| χ | curvatura, gradiente del piano | 1/mm |
| ε(x, y) | deformazione del piano, trazione positiva | — |

## 6. Formulazione

```math
E_{c,eff} = \frac{E_c}{1+\psi} \qquad \text{(E-C1)}
```

```math
n = \frac{E_s\,(1+\psi)}{E_c} \qquad \text{(E-C2)}
```

Il solutore integra il calcestruzzo con Ec e l'acciaio con Es (1 + ψ): le tensioni dipendono solo da n, ma il piano che
ne risulta è quello di una sezione 1 + ψ volte più rigida di quella reale.

```math
\varepsilon_{tot}(x,y) = (1+\psi)\,\varepsilon_{sol}(x,y), \qquad \chi_{tot} = (1+\psi)\,\chi_{sol} = \frac{M}{E_{c,eff}\,I_{cr}(n)} \qquad \text{(E-C3)}
```

Una barra a tensione σs sta sul piano totale a σs/Es; il calcestruzzo compresso a σc (1 + ψ)/Ec.

> **Scostamento dichiarato — E-C3 (F4-08) Curvatura dei muri senza il fattore di viscosità**
>
> - Metodo: lo spostamento dei muri integra la curvatura totale.
> - Programma: fino alla 0.4.0.1 il percorso dei muri di ANTHEA integrava la curvatura del piano del solutore; con
>   ψ = 2 mancava un fattore 3 nella componente flessionale dello spostamento.
> - Effetto: a sfavore di sicurezza nel percorso storico: nel corpus di ANTHEA fino a 3,54 mm in più di spostamento in
>   testa (componente flessionale; traslazione e rotazione della fondazione invariate).
> - Stato: `corretto in 0.4.0.2` per i muri; piano totale esplicito per tutte le sezioni dalla 0.9.0.0.

## 7. Coefficienti e valori predefiniti

| Simbolo | Valore | Fonte | Modificabile | Codice |
| --- | --- | --- | --- | --- |
| 1 + ψ | da ψ dell'analisi | metodo del modulo efficace | sì (ψ o n del chiamante) | `Homogenization.CreepFactor` |

## 8. Implementazione

- `GPCChecker.Concrete/Results/ResultType/StressAnalysisResult.cs`: `TotalStrainPlane` amplifica il piano del solutore
  con `Homogenization.CreepFactor(ψ)` nell'analisi lineare con ψ > 0; altrimenti restituisce `StrainPlane`.
- Stesso fattore negli accessori esistenti `GetVerticeStrain(point, ψ)` e `GetRebarStrain(rebar, ψ)`.
- `GPCChecker.Concrete/Walls/WallSectionSession.cs:49`: `Curvature` della striscia = gradiente del solutore × (1 + ψ);
  `ElasticEquivalentCurvature` conserva il gradiente del solutore solo per diagnosi.

## 9. Limiti e casi non supportati

- Nessuna sovrapposizione fra carichi permanenti (con viscosità) e variabili (senza): rinviata dall'utente.
- Ritiro, fessurazione progressiva (tension stiffening) e storia di carico non sono considerati.

## 10. Esempio numerico verificato

Rettangolo 1000 × 500 mm, 5Ø20 a 50 mm dal lembo teso (d = 450 mm, As = 1570,80 mm²), C25/30 (Ec = 31 475,81 MPa),
B450C (Es = 200 000 MPa), n = 15 (ψ = 15 · 31 475,81/200 000 − 1 = 1,36069), N = 0, M = 150 kNm, calcestruzzo teso escluso.

- ρ = 1570,80/(1000 · 450) = 0,0034907; nρ = 0,05236.
- x = d (−nρ + √((nρ)² + 2nρ)) = 123,954 mm.
- Icr = b x³/3 + n As (d − x)² = 3,13961·10⁹ mm⁴ (in unità di calcestruzzo).
- χ del solutore = M/(Ec Icr) = 1,517884·10⁻⁶ 1/mm; χ totale = 2,36069 · 1,517884·10⁻⁶ = 3,583247·10⁻⁶ 1/mm.
- σs = n M (d − x)/Icr = 233,66 MPa; σs/Es = 1,1683 ‰.

Libreria 0.9.0.0, analisi lineare con suddivisione 64: `StrainPlane.Chi` = 1,517885·10⁻⁶, `TotalStrainPlane.Chi` =
3,583249·10⁻⁶ 1/mm (scarto 5,7·10⁻⁷ relativo); barra a 233,64 MPa e, sul piano totale, a 1,16820 ‰ = σs/Es.

## 11. Validazione

- `GPCChecker.Test.Concrete/ConcreteControlTests.cs`, `TotalStrainPlaneCarriesThePhysicalStrainsOfTheLinearAnalysisWithCreep`:
  barre a σs/Es, calcestruzzo compresso a σc (1 + ψ)/Ec, stessi valori degli accessori; piano invariato nell'analisi non lineare.
- `tests/Concrete.Checks/WallSectionSessionTests.cs`: segno della curvatura e crescita con la viscosità.
- ANTHEA: sei spostamenti attesi calcolati a mano da σ/Eeff (F4-08: 2 su 6 prima, 6 su 6 dopo); 1572 curvature e 78 forme.

## 12. Bibliografia

- EN 1992-1-1:2004, §7.4.3, metodo del modulo efficace.
- DM 17 gennaio 2018, Norme tecniche per le costruzioni, §4.1.2.2.5.
