---
id: design.servizi
titolo: Servizi di progetto di GPC.Design per le sezioni, le armature minime e i prezzi
libreria: GPC.Design
classi:
  - GPC.Design.Sections.SectionDesignDemand
  - GPC.Design.Sections.SectionDesignSearch
  - GPC.Design.Costs.CostEstimator
  - GPC.Design.Costs.PriceAnalyses
versione: 0.20.0.0
norme: [ntc2018-4.1.6.1.2]
stato: bozza
---

# Servizi di progetto di GPC.Design per le sezioni, le armature minime e i prezzi

## 1. Scopo

GPC.Design raccoglie il codice di progetto che non è una verifica: proposta delle armature delle sezioni (ricerca,
graduatoria, Pareto), domande numeriche per i controlli, computo e prezzi. I controlli normativi restano nelle librerie
di verifica (GPCChecker.Concrete e le altre). Questa pagina descrive i tre servizi che contengono formule: la compressione
NEd dei dettagli costruttivi, il computo dei costi e le analisi composte dei prezzi.

## 2. Campo di applicazione

| Servizio | Ingresso | Uscita | Rifiuti |
| --- | --- | --- | --- |
| `SectionDesignDemand.DetailingCompression` | righe con nome, famiglia e N | NEd, riga che governa, valore di esercizio sì/no | N non finito, famiglia sconosciuta |
| `CostEstimator.Evaluate` | quantità misurate e prezzi risolti | righe, costo diretto, costo totale | quantità invalide, inclusioni cicliche |
| `PriceAnalyses.Resolve` | voci con prezzo e analisi | dettaglio di ogni analisi | componenti mancanti o senza prezzo, cicli, percentuali fuori da 0–100 |

## 3. Riferimenti normativi

| Formula | Norma | Paragrafo | Riscontro |
| --- | --- | --- | --- |
| (D-S1) NEd dell'armatura minima dei pilastri | NTC 2018 | §4.1.6.1.2 | testo |
| (D-S2), (D-S3) prezzi | — | — | — (pratica delle analisi dei prezzi; percentuali assegnate dal prezzario) |

## 4. Ipotesi

- Le righe di azione hanno il segno del codice: trazione positiva. La compressione di una riga è max(0; −N).
- I prezzi sono in una sola valuta; le conversioni di unità sono ammesse solo fra dimensioni compatibili (kg e t; ore solo
  in ore).

## 5. Notazione, unità e convenzioni

| Simbolo | Significato | Unità |
| --- | --- | --- |
| NEd | compressione di progetto dei dettagli costruttivi | N o kN (del chiamante) |
| qi, pi | quantità e prezzo del componente i di un'analisi | unità del componente, €/unità |
| g, u | spese generali e utile d'impresa | % |

## 6. Formulazione

```math
N_{Ed} = \max_{r}\ \max(0;\ -N_r), \quad r \in \{\text{SLU, SLV, taglio, SLE rara, frequente, quasi permanente}\} \qquad \text{(D-S1)}
```

A parità di valore governano le famiglie di progetto (SLU, SLV, taglio) e poi la prima riga. Se governa una riga SLE il
risultato lo segnala: è un valore di esercizio (decisione dell'utente di ANTHEA del 14/10/2026: «usa le N più gravose di
tutte le combo»; con combinazioni ultime complete le SLE non governano).

```math
S = \sum_i q_i\,p_i, \qquad G = S\,\frac{g}{100}, \qquad U = (S + G)\,\frac{u}{100} \qquad \text{(D-S2)}
```

```math
p_{analisi} = S + G + U \qquad \text{(D-S3)}
```

Un componente può essere un'altra analisi, con il suo prezzo esatto (senza arrotondamenti intermedi). g e u sono quelli del
prezzario, salvo valori propri dell'analisi.

## 7. Coefficienti e valori predefiniti

| Simbolo | Valore | Fonte | Modificabile | Codice |
| --- | --- | --- | --- | --- |
| g, u del prezzario | 15 % e 10 % proposti da ANTHEA alla prima analisi | prezzario dell'utente | sì, anche per analisi | `PriceAnalyses.Resolve` (parametri) |
| interasse tandem, carichi | vedere design.ponti-traffico | — | — | — |

## 8. Implementazione

- `src/GPC.Design/Sections/SectionDesignDemand.cs`: `DetailingCompression` (D-S1) con riga che governa e
  `FromServiceability`; `MaximumCompression` per i valori senza famiglia, invariato.
- `src/GPC.Design/Costs/CostEstimate.cs`: `CostEstimator.Evaluate` con inclusioni verificate prima del prezzo.
- `src/GPC.Design/Costs/PriceAnalyses.cs`: risoluzione in profondità con rifiuto dei riferimenti circolari; dettaglio con
  totale dei componenti, spese generali, utile e prezzo (`AnalysisBreakdown`); `Rate` per il computo.

## 9. Limiti e casi non supportati

- Nessun arrotondamento dei prezzi nella libreria: lo fa la presentazione.
- Le analisi non hanno coefficienti di sicurezza, oneri della sicurezza o ribassi: vanno espressi come componenti.

## 10. Esempio numerico verificato

NEd: SLU −100 kN, SLV nessuna, SLE rara −500 kN, quasi permanente −300 kN → NEd = 500 kN dalla rara, valore di esercizio;
aggiungendo SLV −800 kN → NEd = 800 kN dalla SLV.

Analisi «calcestruzzo in opera», per m³: 1,05 m³ di calcestruzzo a 110 €/m³, 0,80 h di manodopera a 30 €/h, 0,10 h di
autopompa a 90 €/h; g = 15 %, u = 10 %.

- S = 115,50 + 24,00 + 9,00 = 148,50 €; G = 22,275 €; U = 0,10 · 170,775 = 17,0775 €; prezzo 187,8525 €/m³.
- Analisi annidata «pilastro»: 1 m³ della precedente più 8 m² di casseri a 25 €/m², g = 13 % (dell'analisi):
  S = 387,8525 €; G = 50,420825 €; U = 43,8273325 €; prezzo 482,1006575 €/m³.

Libreria 0.20.0.0: stessi valori, esatti in decimale (prove sotto).

## 11. Validazione

- `tests/Design.Checks/SectionDesignDemandTests.cs`: `DetailingCompressionTakesTheMostSevereRowOfAllFamilies` (famiglie,
  parità, valore di esercizio, rifiuti) e le prove storiche di `MaximumCompression`.
- `tests/Design.Checks/PriceAnalysesTests.cs`: valori a mano dell'esempio, annidamento, rifiuti, ore.
- ANTHEA: `DetailingCompressionChecks` e `BridgePriceAnalysesChecks` (voci F4-44 e F4-45).

## 12. Bibliografia

- DM 17 gennaio 2018, Norme tecniche per le costruzioni, §4.1.6.1.2.
