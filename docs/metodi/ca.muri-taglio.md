---
id: ca.muri-taglio
titolo: Taglio senza armatura trasversale delle strisce di muro, con lo sforzo normale
libreria: GPCChecker.Concrete
classi:
  - GPC.Checkers.Concrete.Walls.WallSectionSession
  - GPC.Checkers.Concrete.Shear.SectionShearCalculator
versione: 0.9.0.0
norme: [ntc2018-4.1.2.3.5.1]
stato: bozza
---

# Taglio senza armatura trasversale delle strisce di muro, con lo sforzo normale

## 1. Scopo

Resistenza a taglio di una striscia di muro in calcestruzzo armato senza armatura trasversale (paramento e fondazione dei
muri a mensola), per metro di larghezza. Restituisce la resistenza VRd,c, il rapporto VEd/VRd,c e la traccia dei passaggi.
Dal 14 ottobre 2026 la resistenza comprende il contributo della compressione assiale 0,15 σcp della norma (scelta
dell'agente in F4.3b, confermata dall'utente: voce F4-03 del registro delle differenze di ANTHEA).

## 2. Campo di applicazione

| Caso | Comportamento |
| --- | --- |
| Striscia rettangolare NTC 2018, calcestruzzo ordinario, armatura longitudinale ancorata | calcolo |
| Sforzo normale nullo o di compressione | calcolo, con σcp = −N/Ac limitata a 0,2 fcd |
| Sforzo normale di trazione (N > 0) | esito «non valutato»: il calcestruzzo senza staffe non si certifica in trazione |
| Armatura trasversale, cavi, sezioni non rettangolari | fuori campo della striscia di muro |
| Ancoraggio dell'armatura tesa e idoneità dell'elemento senza staffe | non verificati dal metodo: restano al progettista |

## 3. Riferimenti normativi

| Formula | Norma | Paragrafo | Riscontro |
| --- | --- | --- | --- |
| (T-W1) resistenza | NTC 2018 | §4.1.2.3.5.1, formula 4.1.23 | testo (G.U. 20/02/2018, S.O. 8, cap. 4 p. 79) |
| (T-W2) minimo | NTC 2018 | §4.1.2.3.5.1, formula 4.1.23 | testo |
| (T-W3) σcp e limite | NTC 2018 | §4.1.2.3.5.1 | testo |

## 4. Ipotesi

- Elemento senza armatura trasversale, con armatura longitudinale tesa prolungata e ancorata oltre la sezione.
- Striscia di larghezza bw = 1000 mm; altezza utile d dalla faccia tesa; area Ac della striscia.
- Lo sforzo normale è quello della combinazione di verifica, con il segno del codice (compressione negativa).

## 5. Notazione, unità e convenzioni

| Simbolo | Significato | Unità |
| --- | --- | --- |
| N | sforzo normale, trazione positiva | N |
| Ac | area della striscia | mm² |
| σcp | tensione media di compressione, positiva se compressione (convenzione della norma) | MPa |
| bw, d | larghezza e altezza utile | mm |
| ρl | Asl/(bw d), al più 0,02 | — |
| k | min(2; 1 + √(200/d)) | — |

## 6. Formulazione

```math
V_{Rd,c} = \left[\frac{0{,}18}{\gamma_c}\,k\,(100\,\rho_l\,f_{ck})^{1/3} + 0{,}15\,\sigma_{cp}\right] b_w\,d \qquad \text{(T-W1)}
```

```math
V_{Rd,c} \ge \left(v_{min} + 0{,}15\,\sigma_{cp}\right) b_w\,d, \qquad v_{min} = 0{,}035\,k^{3/2}\,f_{ck}^{1/2} \qquad \text{(T-W2)}
```

```math
\sigma_{cp} = \min\!\left(-\frac{N}{A_c};\ 0{,}2\,f_{cd}\right), \quad N \le 0 \qquad \text{(T-W3)}
```

> **Scostamento dichiarato — T-W1 (F4-03) Compressione nel taglio dei muri**
>
> - Norma: il contributo 0,15 σcp compare in entrambi i rami della 4.1.23, con σcp ≤ 0,2 fcd.
> - Programma: fino alla 0.8.0.0 il percorso dei muri di ANTHEA ometteva σcp anche in compressione; dalla
>   0.9.0.0 la striscia usa il calcolatore comune, che lo include, e rifiuta la trazione.
> - Effetto: a favore della resistenza (meno conservativo del percorso storico): nel corpus di ANTHEA 4192 capacità su
>   12512 aumentano, al massimo di 4,30 kN/m (+2,80 %); nessun esito cambia.
> - Stato: `dichiarato` (confermato dall'utente il 14/10/2026).

## 7. Coefficienti e valori predefiniti

| Simbolo | Valore | Fonte | Modificabile | Codice |
| --- | --- | --- | --- | --- |
| CRd,c | 0,18/γc | NTC 4.1.23 | γc dal profilo | `SectionShearCalculator` |
| k1 | 0,15 | NTC 4.1.23 | no | `SectionShearCalculator` |
| limite di σcp | 0,2 fcd | NTC §4.1.2.3.5.1 | no | `SectionShearCalculator` |

## 8. Implementazione

- `GPCChecker.Concrete/Walls/WallSectionSession.Shear.cs:20`: `CheckShear` costruisce l'ingresso del calcolatore con N, V,
  M, area, larghezza e armatura della faccia tesa della striscia.
- `GPCChecker.Concrete/Shear/SectionShearCalculator.cs:51-59`: σcp = −N/Ac, rifiuto della trazione (riga 54), limite
  0,2 fcd (riga 56), rami (T-W1) e (T-W2) (righe 58-59).
- Esito «non valutato» con resistenza nulla nel risultato della striscia: nessun esito positivo senza resistenza.

## 9. Limiti e casi non supportati

- La trazione non è verificata automaticamente: il rifiuto è un limite del supporto automatico, non una soglia normativa.
- Ancoraggio delle barre tese e condizioni per omettere le staffe non sono verificati dal metodo.

## 10. Esempio numerico verificato

bw = 1000 mm, d = 350 mm, Ac = 400 000 mm², Asl = 1500 mm², fck = 30 MPa, fcd = 17 MPa, γc = 1,5.

- k = 1 + √(200/350) = 1,75593; ρl = 1500/(1000·350) = 0,0042857; (100 ρl fck)^(1/3) = 12,857^(1/3) = 2,34273.
- Senza N: VRd,c = 0,12 · 1,75593 · 2,34273 · 350 000 = 172 771,08 N (vmin = 0,4461 MPa non governa).
- N = −400 kN: σcp = 400 000/400 000 = 1 MPa; VRd,c = 172 771,08 + 0,15 · 1 · 350 000 = 225 271,08 N.
- N = −1360 kN: −N/Ac = 3,4 MPa = 0,2 fcd; VRd,c = 172 771,08 + 0,15 · 3,4 · 350 000 = 351 271,08 N.
- N = −2000 kN: σcp limitata a 3,4 MPa, stesso valore 351 271,08 N.
- N = +40 kN: non valutato.

La libreria 0.9.0.0 restituisce gli stessi valori entro 1e-6 N (prova `WallStripCompressionAndCapMatchIndependentValues`).

## 11. Validazione

- `tests/Concrete.Checks/WallShearAxialTests.cs`: quattro casi di compressione e il rifiuto della trazione, con attesi
  calcolati in Python (`tools/qa/Study-F4WallShear.py` di ANTHEA), tolleranza 1e-6 N.
- ANTHEA: confronto del nuovo percorso con 12 512 attesi indipendenti dello studio F4.3b (26 muri), zero differenze non
  ammesse; transizione dell'interfaccia classificata nel confronto con la cattura F3 (968 valori).

## 12. Bibliografia

- DM 17 gennaio 2018, Norme tecniche per le costruzioni, §4.1.2.3.5.1.
