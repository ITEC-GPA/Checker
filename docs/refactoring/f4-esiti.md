# F4.4a — esiti comuni dei pali e dei muri

13 ottobre 2026, branch `codex/f4-esiti-pali-muri`, base master `75e9adaa`.
Il prefisso `refactoring/` non è utilizzabile in questo checkout Windows perché
esiste già il ramo `Refactoring`; ANTHEA mantiene il proprio ramo di fase.

API additive: `PileSectionCheck` e `WallCheck` espongono `Outcome`, `Execution`
e `Data`, con gli enum già presenti in Model. Nessun nuovo enum duplicato,
nessuna dipendenza verso ANTHEA o WPF. `Status` e i messaggi precedenti restano
compatibili; selezione dei candidati e controllo dello sviluppo dei pali
decidono mediante l'esito tipizzato, senza interpretare testo tradotto.

| Situazione | Outcome | Execution | Data |
| --- | --- | --- | --- |
| Palo appena costruito, non eseguito | NotEvaluated | NotExecuted | Insufficient |
| N–M–V nominale completo e favorevole | Satisfied | Completed | Ready |
| Rapporto/minimo sfavorevole | NotSatisfied | Completed | Ready o Insufficient se un altro controllo è incompleto |
| Conferma delle azioni/taglio mancante | NotEvaluated, salvo violazione già dimostrata | Completed | Insufficient |
| Resistenza del palo non risolta/fuori dominio | NotEvaluated | Error | Ready |
| Sviluppo necessario non disponibile | NotEvaluated, salvo violazione già dimostrata | Completed | Insufficient |
| Muro senza resistenza/dato utilizzabile | NotEvaluated | NotExecuted | Insufficient |
| Muro con resistenza nulla e domanda non nulla, o perdita di equilibrio | NotSatisfied | Completed | Ready |

L'esito nominale N–M–V non certifica tutti i dettagli costruttivi. L'annullamento
di PrepareResistance continua a propagare OperationCanceledException e non
pubblica un lotto incompleto. Le API sincrone di singola verifica non inventano
un risultato cancellato che non hanno prodotto.

WallCheck conserva i valori legacy anche per ingressi non finiti ammessi dal
vecchio factory, ma il nuovo esito non li certifica. La ragione specifica di un
dato non disponibile resta nel messaggio: non si deduce dal testo se sia un
campo mancante oppure un metodo fuori campo.

## Verifica e versioni

Due catture prima/dopo, solo campi legacy: 11 casi di palo e 7 di muro, valori,
rapporti, sviluppo, minimi, etichette e diagnostica identici byte per byte.
SHA-256 palo: `5E75A6432480694166F77216554D772AF00FFFB079495F6A45BF12458851985E`.
SHA-256 muro: `14A345FB46085EDDBA07C889BFF0468356138FFC9EF5732D0B508B9B84D9043E`.
Evidenze ANTHEA: `supporto/artefatti/refactoring/f4/outcomes-before` e
`outcomes-after`. Le catture non sovrascrivono file esistenti.

`build/Verify.ps1`: 1565 PASS, 17 fallimenti Steel già elencati per nome,
zero nuovi fallimenti. API pubbliche compatibili: nessuna rimozione inattesa.
I 21 nuovi casi di test coprono anche esiti incompleti, dati non finiti,
testo tradotto, errori del solutore, annullamento e selezione dei candidati.

Versioni candidate: Concrete **0.1.0.0**, Geotechnics **0.2.0.0**. Incremento
del secondo componente per estensione delle API, secondo la regola delegata.
Questo commit non distribuisce un release set e non aggiorna lib/Checker.
F4.4b resta l'attivazione nei consumatori ANTHEA con un set coerente e i relativi
collaudi; le classi strutturali dei muri ancora locali verranno migrate in F4.7.
