---
id: design.ponti-traffico
titolo: Traffico stradale Schema 1 nella stima concettuale dei ponti
libreria: GPC.Design
classi:
  - GPC.Design.Bridges.RoadTraffic
  - GPC.Design.Bridges.BeamTrafficEnvelope
  - GPC.Design.Bridges.BridgeConcept
versione: 0.20.0.0
norme: [ntc2018-5.1.3.3, en1991-2-4.3.2]
stato: bozza
---

# Traffico stradale Schema 1 nella stima concettuale dei ponti

## 1. Scopo

Azioni globali del traffico stradale per il predimensionamento di Bridge Design: suddivisione della carreggiata in corsie
convenzionali, carichi dello Schema 1 e inviluppi longitudinali di momento, taglio, freccia e reazioni su una trave
prismatica. Sostituisce il carico uniforme equivalente storico, che resta per gli archivi precedenti (voce F4-29 del
registro delle differenze di ANTHEA).

## 2. Campo di applicazione

| Caso | Comportamento |
| --- | --- |
| Carreggiate da 3 a 100 m, anche divise da spartitraffico | corsie convenzionali e carichi dello Schema 1 |
| Travi semplicemente appoggiate o continue, da 1 a 30 campate di almeno 2 m | inviluppi con linee di influenza |
| Ripartizione trasversale, verifiche locali, fatica, Schemi 2-6, frenatura, folla | non trattati |
| Lunghezza caricata oltre 200 m | calcolo con avviso: serve una valutazione specifica |

## 3. Riferimenti normativi

| Formula | Norma | Paragrafo | Riscontro |
| --- | --- | --- | --- |
| (B-T1) corsie convenzionali | NTC 2018 | §5.1.3.3.2, Tabella 5.1.I | testo |
| (B-T2) carichi dello Schema 1 | NTC 2018 | §5.1.3.3.3, Tabella 5.1.II | testo |
| parte rimanente 2,5 kN/m² | EN 1991-2 | §4.3.2, Tabella 4.2 | testo |
| γQ sfavorevole 1,35 | NTC 2018 | Tabella 5.1.V | testo |
| righe SLU A1 dei ponti: γG1 1,35/1,00, γG2 1,50/0, γQ 1,35/0 | NTC 2018 | §5.1.3.12, Tabella 5.1.V | testo (G.U. S.O. n. 8 del 20/02/2018), GPC.Model `RoadBridgeCombinations` |

## 4. Ipotesi

- Trave prismatica di Euler–Bernoulli a EI costante; linee di influenza esatte (cubiche a tratti).
- Ogni ordinata ha la propria disposizione dei carichi: il carico distribuito solo sulle parti sfavorevoli, il tandem
  completo (due assi a 1,20 m) nella posizione più sfavorevole. Massimi e minimi non sono concomitanti.
- Numerazione delle corsie comune alle carreggiate divise; la corsia 1 occupa la corsia convenzionale più larga.

## 5. Notazione, unità e convenzioni

| Simbolo | Significato | Unità |
| --- | --- | --- |
| w | larghezza della carreggiata | m |
| n | numero di corsie convenzionali | — |
| q | carico distribuito per metro di impalcato | kN/m |
| Q | carico di un asse (somma sulle corsie) | kN |
| M, V, R | momento, taglio, reazione; momento positivo tende le fibre inferiori | kNm, kN |

## 6. Formulazione

```math
n = \begin{cases} 1 & w < 5{,}4\ \text{m} \\ 2 & 5{,}4 \le w < 6\ \text{m}\ (\text{corsie di } w/2) \\ \lfloor w/3 \rfloor & w \ge 6\ \text{m} \end{cases} \qquad \text{(B-T1)}
```

```math
q = 9 \cdot 3 + 2{,}5 \cdot 3\,(n-1) + 2{,}5\,w_{rim}, \qquad Q = 300 + 200 + 100 \quad (\text{corsie } 1, 2, 3) \qquad \text{(B-T2)}
```

con wrim parte rimanente della carreggiata. Gli inviluppi integrano q sulle parti di segno sfavorevole della linea di
influenza e massimizzano analiticamente l'effetto dei due assi Q a 1,20 m.

## 7. Coefficienti e valori predefiniti

| Simbolo | Valore | Fonte | Modificabile | Codice |
| --- | --- | --- | --- | --- |
| interasse del tandem | 1,20 m | NTC Tab. 5.1.II | no | `RoadTraffic.AxleSpacing` |
| q corsia 1 / altre / rimanente | 9 / 2,5 / 2,5 kN/m² | NTC Tab. 5.1.II, EN 1991-2 Tab. 4.2 | no | `RoadTraffic.Ntc2018` |
| Q corsie 1, 2, 3 | 300, 200, 100 kN | NTC Tab. 5.1.II | no | `RoadTraffic.Ntc2018` |
| γQ | 1,35 proposto | NTC Tab. 5.1.V | sì (ipotesi del progetto) | `BridgeAssumptions.GammaQ` |
| fonte dei coefficienti | moltiplicatori del progetto (predefinito) o righe A1 di GPC.Model | NTC Tab. 5.1.V | sì | `BridgeInput.PartialFactors` |

## 8. Implementazione

- `src/GPC.Design/Bridges/RoadTraffic.cs`: corsie e carichi (`Ntc2018`), distribuzione a leva sul piano esterno per le
  strutture superiori (`OuterPlane`).
- `src/GPC.Design/Bridges/BeamTrafficEnvelope.cs`: equazione dei tre momenti per le travi continue, linee di influenza,
  inviluppi di reazioni (`Reaction`) e sezioni (`At`).
- `src/GPC.Design/Bridges/BridgeConcept.Traffic.cs`: combinazione γG G + γQ Q, 41 sezioni per campata, avvisi.
- `src/GPC.Design/Bridges/BridgeConcept.PartialFactors.cs` (D8 del censimento delle combinazioni di ANTHEA, decisione del
  29/10/2026): con `PartialFactorSource.Ntc2018RoadBridges` massimi e minimi sono gli estremi delle 8 righe SLU A1 di
  GPC.Model (`RoadBridgeCombinations.Deck`): γG1 G1 + γG2 G2 + γQ Q, con G2 = finiture e barriere e i permanenti
  favorevoli nei minimi; le aste delle strutture superiori e le antenne usano i coefficienti sfavorevoli (γG1 1,35,
  γG2 1,50, γQ 1,35). Con i moltiplicatori del progetto (predefinito, archivi) il calcolo è quello di prima.

## 9. Limiti e casi non supportati

- Nessuna ripartizione trasversale fra le travi né verifica locale della soletta.
- Le reazioni dell'inviluppo non si sommano: ciascuna ha la propria disposizione dei carichi.

## 10. Esempio numerico verificato

Carreggiata w = 10,5 m, trave appoggiata di 20 m, valori caratteristici (γQ = 1).

- n = ⌊10,5/3⌋ = 3 corsie da 3 m, wrim = 1,5 m.
- q = 9·3 + 2,5·3 + 2,5·3 + 2,5·1,5 = 45,75 kN/m; Q = 300 + 200 + 100 = 600 kN per asse.
- Momento in mezzeria: q L²/8 = 45,75 · 400/8 = 2287,5 kNm; tandem con un asse in mezzeria e l'altro a 1,20 m:
  600 · (5 + 4,4) = 5640 kNm; totale 7927,5 kNm.
- Taglio in mezzeria: q L/8 = 114,375 kN più 600 · (0,5 + 0,44) = 564 kN, totale 678,375 kN.
- Reazione d'appoggio: q L/2 = 457,5 kN più 600 · (1 + 18,8/20) = 1164 kN, totale 1621,5 kN.

Libreria 0.20.0.0 (`RoadTraffic.Ntc2018(10.5)` e `BeamTrafficEnvelope`): 3 corsie, 45,75 kN/m, 600 kN, M = 7927,5 kNm,
V = 678,375 kN, R = 1621,5 kN, identici.

## 11. Validazione

- `tests/Design.Checks/TrafficEnvelopeTests.cs`: corsie ai limiti normativi e numerazione globale, treno di carichi su
  trave appoggiata e carico uniforme su trave continua contro le forme chiuse, inviluppi contro i risultati Python
  indipendenti con i segni opposti.
- `tests/Design.Checks/BridgeTrafficTests.cs`: tandem locali delle strutture superiori (`UpperStructuresUseLocalTandems…`).
- ANTHEA: confronto del costo fra modello uniforme e Schema 1 sulle tipologie di ponte (voce F4-29).

## 12. Bibliografia

- DM 17 gennaio 2018, Norme tecniche per le costruzioni, §5.1.3.3.
- EN 1991-2:2003, Azioni sui ponti dovute al traffico, §4.3.2.
