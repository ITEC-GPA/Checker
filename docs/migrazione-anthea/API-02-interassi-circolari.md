# API-02 — raggruppamento delle barre circolari

Correzione del 12 ottobre 2026, branch `codex/f2-api02-interassi`, da master
`8a2c036f`. Difetto geometrico, non modifica normativa.

## Causa e comportamento

`CrackSectionGeometry.MaximumSpacing` raggruppava le barre con `Math.Round(r, 5)`.
Vicino al confine di arrotondamento, scarti numerici del raggio dividevano una
stessa corona. Un gruppo con una sola barra generava inoltre l’arco di 2π,
confondendo la barra con il proprio vicino.

Ora i raggi sono ordinati e ogni gruppo ha estensione radiale massima di 1e-5 mm,
la precisione precedente, misurata dal suo raggio minimo. Non si concatenano
vicinanze che potrebbero fondere corone distinte. I gruppi con meno di due barre
non producono interassi. Gli archi si calcolano ancora tra vicini della corona
completa, solo quando entrambi selezionati. Coordinate, diametri, aree e firme
pubbliche non cambiano; nessun calcolo introdotto in ANTHEA.

## Attesi e confronto

Caso D400, copriferro 39.999995, staffa 8, 8Ø16 esterne, 6Ø12 interne e distanza
netta radiale 20 mm: raggi 144.000005 e 110.000005 mm. Gli archi nominali sono
`2π·144.000005/8` e `2π·110.000005/6`: il massimo indipendente è
**115.19173586761349 mm**. La discrepanza documentata nell’audit ANTHEA era
452.3893578248936 mm nel legacy e 904.778715649787 mm nella DLL distribuita.

38 nuove prove: confini di arrotondamento, perturbazioni a entrambi i lati,
rotazioni, ordine delle barre, traslazioni fino a 10⁶ mm, selezione dei vicini e
dell’arco di chiusura, barre isolate, corone distinte e assenza di concatenamento
della tolleranza. Prima della correzione: 31 PASS e 7 FAIL. Dopo: 38 PASS.

`build/Verify.ps1` completo: 1544 PASS, 17 fallimenti Steel già ammessi, zero nuovi
fallimenti. Concrete 718 PASS, ModelChecker 168 PASS; API pubbliche compatibili.
Contratti congelati K0, durabilità e fixture di migrazione non modificati.

Evidenze in ANTHEA, checkout principale:
`supporto/artefatti/refactoring/api02/{prima,dopo,checker-tutte}`.
I TRX conservano anche i fallimenti prima della correzione.

## Distribuzione ancora da completare

Il 12 ottobre l’utente delega la scelta delle versioni: ultimo componente per i fix,
secondo per le altre modifiche (AGENTS.md e build/RILASCIO.txt aggiornati). Per questo
fix geometrico Concrete passa da **0.0.27.1 a 0.0.27.2**, senza modifiche alle API
pubbliche. Il commit di versione non pubblica un rilascio e non cambia
`ANTHEA/lib/Checker`. Per attivare la correzione nel prodotto restano il set coerente
da commit pushati e il confronto ANTHEA prima/dopo con la differenza API-02 dichiarata. Fino ad allora i percorsi circolari conservativi di ANTHEA
restano presenti e il punto di integrazione rimane aperto.

## Riallineamento del treno a Model 5.1

Steel 2.0.4.2, CompositeBridge 1.5.0.2, Geotechnics 0.1.3.1 e ModelChecker 2.1.0.2 sono revisioni tecniche per il riferimento al nuovo GPCModel 5.1.0.0. Nessuna formula o contratto modificato. Concrete resta alla 0.0.27.2, ancora non distribuita, che include API-02; anche la versione di pacchetto viene allineata all’assembly. Le revisioni tecniche incrementano l’ultimo componente secondo la delega del 12 ottobre. Tutte le suite del treno devono essere rieseguite.
