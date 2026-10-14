# F4.3b — confronto dello sforzo normale nel taglio dei muri

13 ottobre 2026. Si riusa `SectionShearCalculator`: nessun nuovo calcolatore,
nessuna modifica del codice di produzione o della versione della libreria.

Le prove esistenti `ShearMigrationTests` coprono 2016 osservazioni legacy e
calcoli manuali. Il nuovo `tests/Concrete.Checks/WallShearAxialTests.cs` aggiunge
cinque vettori indipendenti dedicati alla striscia di muro e al limite di σcp.
Gli attesi sono ottenuti in Python, prima di chiamare la API, e fissati come
costanti nel test: non si aggiornano con i risultati di Checker.

Input: bw=1000 mm, d=350 mm, Ac=400000 mm², Asl=1500 mm², fck=30 MPa,
fcd=17 MPa, γc=1,5; domanda 200000 N. N negativo a compressione.

| N (N) | σcp adottata (MPa) | VRd indipendente (N) | Esito API |
| ---: | ---: | ---: | --- |
| 0 | 0 | 172771,0814352394 | Non soddisfatta |
| −400000 | 1 | 225271,0814352394 | Soddisfatta |
| −1360000 | 3,4 | 351271,0814352394 | Soddisfatta |
| −2000000 | 3,4 | 351271,0814352394 | Soddisfatta |
| 40000 | non applicabile | non valutata | Non valutata |

Tolleranza della resistenza 1e-6 N, rapporto e σcp 1e-12. Le **12 prove**
selezionate (cinque nuove e sette di migrazione) passano; queste ultime
confrontano anche tutte le 2016 osservazioni. Evidenze nel repository ANTHEA:
`supporto/artefatti/refactoring/f4/wall-sigma-tests/wall-sigma.trx`.

Il testo NTC parla di trazione significativa; l'API rifiuta automaticamente
qualsiasi N positivo. È una restrizione conservativa del supporto automatico,
non una nuova soglia normativa. Va esposta nel futuro adattatore, senza PASS
dedotti da rapporti nulli. Rimangono necessari i controlli separati su armatura
effettivamente ancorata e ammissibilità del membro senza staffe.

La scheda ANTHEA `docs/refactoring/f4-taglio-muri.md` contiene fonte primaria,
studio Python ripetibile e misura su 12512 controlli di 26 muri congelati:
4192 capacità varierebbero, massimo +4,301578125 kN/m (+2,80197%), nessun
cambio di esito in questo corpus. È una previsione dell'effetto della formula,
non il collaudo del futuro adattatore. Il codice locale è ancora attivo.
