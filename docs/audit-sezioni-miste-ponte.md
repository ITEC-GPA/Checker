# Audit del metodo per sezioni miste da ponte

**Aggiornamento:** questo documento conserva la prima verifica. Il successivo
[approfondimento del solver](approfondimento-solver-lineare-ponte.md) descrive la
correzione autorizzata di B01 nel sorgente Model, isola B02 e riproduce tre
ulteriori bug Checker. I conteggi seguenti si riferiscono alla prima esecuzione.

25 settembre 2026. Revisione di lettura e test; nessuna correzione ai motori.
Il report Word del modulo è stato aggiunto in ANTHEA. Qui sono raccolti i rilievi
di calcolo e tutti i nuovi test, come richiesto.

## Esito e copertura

I confronti esaminati confermano il calcolo elastico N–Mx delle API native quando
si usano gli overload lineari con φ esplicito e la loro effettiva discretizzazione.
Sono emersi due difetti riproducibili, una criticità d'uso delle API e limiti del
modello da tenere distinti dai bug. Non è stata eseguita una validazione normativa
completa del ponte.

| Esecuzione | Casi | Passati | Falliti |
| --- | ---: | ---: | ---: |
| Nuova suite .NET 8, DLL distribuite in ANTHEA | 117 | 115 | 2 |
| Nuovi casi nativi + Bridge storici, progetto .NET Framework | 68 | 67 | 1 |
| Soli Bridge storici, inclusi nella riga precedente | 16 | 16 | 0 |

I 52 casi nativi nuovi sono gli stessi nelle due esecuzioni. I casi nuovi distinti
sono 117: 52 nativi e 65 dell'adattatore. Nessun test è ignorato. I difetti restano
rossi con categoria `KnownBug`; istruzioni e comandi nel
[README della suite](../GPCChecker.Test.BridgeAudit/README.md).
Evidenze complete in `TestResults/BridgeAuditSnapshot` e `TestResults/BridgeSourceAudit`
(cartelle locali ignorate da Git); ogni prova nativa registra le DLL effettive nel TRX.

| Fase/aspetto | Verifiche introdotte |
| --- | --- |
| Geometria nativa | Nessuna/una/due file, numero e posizione delle barre, quote faccia-asse, contratto del profilo nullo |
| G1 acciaio | N e M di entrambi i segni, azioni nulle, area/baricentro/inerzia e tensioni da rettangoli indipendenti, materiali inattivi |
| G2 composta | φ=0/2,2/5, combinazioni N–M, proprietà omogeneizzate, equilibrio, Es/Ea distinto, file facoltative |
| G2 omogeneizzazione | Inversa φ↔n, ψL variabile, identità dei campi con ingresso φ o n, rifiuto di n<n0 |
| Q e situazioni cumulative | Sovrapposizione, scala e inversione, tre prefissi di carico, somma dei contributi, suddivisione di un incremento, fase disattiva |
| Soletta esclusa | Quattro configurazioni delle file, proprietà e tensioni dell'acciaio più barre, CLS inattivo |
| Trasporto delle azioni | Cambio della quota di N e del momento equivalente per tutti i tipi di fase |
| Classe 4 | Benchmark JRC, kσ nei rami di ψ, inversione dei bordi, pannello tozzo, sola trazione, sbalzi snelli compressi/tesi, convergenza con anima e piattabande snelle, equilibrio sulla geometria efficace |
| Piattabande | Equivalenza esatta con larghezze uguali, errore quantificato con larghezze diverse |
| Risultati/validazione | Limiti SLU/SLE, CLS teso senza esito favorevole, proprietà a carico nullo, input nulli non ammessi, ordine delle fasi e nessuna fase attiva |

## B01 — Contratto del profilo H nullo violato in Model

**Difetto confermato; priorità media per l'API, nessun impatto sul percorso attuale
ANTHEA che richiede il profilo.**

Nel costruttore indicato dall'utente, la documentazione di `steelShapeH` ammette
`null` per non inserire carpenteria. L'implementazione evita l'inserimento ma
accede subito dopo a `_steelSections[0]`: la lista vuota genera
`ArgumentOutOfRangeException`.

- Sorgente: `Model/Model/Sections/Concrete/ReinforcedConcreteSection.cs`, costruttore
  da ponte, righe 238–285 circa, assegnazione `IsInsideConcrete`.
- Riproduzione: `Constructor_NullSteelShouldHonorDocumentedContract` in
  [BridgeElasticStagesTests.cs](../GPCChecker.Test.Concrete/BridgeElasticStagesTests.cs).
- Ingresso minimo: CLS 2000×240, armature nulle, `steelShapeH=null`, S355.
- Atteso: soletta priva di carpenteria secondo il contratto documentato.
- Ottenuto: eccezione, sia con le DLL ANTHEA sia con il progetto storico.
- Da decidere nella correzione futura: rispettare il contratto oppure rendere
  obbligatorio il profilo con validazione e documentazione coerenti.

## B02 — Inerzia di integrazione dipendente da azioni nulle in ANTHEA

**Difetto confermato; priorità bassa, riguarda la proprietà esposta.**

In `ANTHEA/X.Core/BridgeSection.cs`, metodo `Solve`, la sottrazione delle inerzie
proprie delle piattabande/barre da `SolverInertia` è nel ramo che chiama Checker
solo se N o M sono diversi da zero. Con N=M=0 resta l'inerzia geometrica Model,
pur essendo presentata come inerzia dell'integrazione Checker.

- Riproduzione: `ZeroCompositeActionShouldReportSameIntegrationInertiaAsNonzeroAction`
  in [BridgeModuleStagesTests.cs](../GPCChecker.Test.BridgeAudit/BridgeModuleStagesTests.cs).
- Geometria di default, classe 4 disattivata, fase composta, φ effettivo=2,2.
- M=0: `SolverInertia = 69 779 283 266,85039 mm⁴`.
- M=1000 kNm: `SolverInertia = 69 776 941 198,49962 mm⁴`.
- Differenza: `2 342 068,35077 mm⁴`, circa 0,00336%.
- La medesima sezione deve conservare la stessa proprietà di integrazione.
  Le tensioni del caso nullo sono comunque zero; non è un errore dimostrato
  sulle tensioni dei casi caricati. La discrepanza compare anche nei report che
  stampano il risultato. Nessuna correzione applicata.

## A01 — Gli overload senza φ non conservano il tipo di analisi lineare

**Criticità d'uso confermata; non classificata come bug del solver.**

In `GPCChecker.Concrete/Results/ResultType/StressAnalysisResult.cs`,
`GetRebarsTension()`, `GetConcreteVerticesTension()` e
`GetStructuralSteelVerticesTension()` richiamano le leggi non lineari/default,
anche quando il risultato ha `LinearElasticAnalysis=true` e `PsiRebar` valorizzato.
Gli overload con φ selezionano invece il percorso lineare. La distinzione è
esplicita in alcuni commenti, ma facile da trascurare utilizzando un risultato
già denominato lineare. Anche `CalculateStrainPlaneResult()` ha valori predefiniti
indipendenti dai parametri memorizzati nel risultato.

Il test `LinearResult_DefaultSteelGettersDoNotReuseStoredPhi` caratterizza il
comportamento per φ=0,5/2,2/5. Con φ=2,2 e M=200 kNm, al primo vertice dell'acciaio:
overload esplicito 6,239963 MPa, overload predefinito 1,949989 MPa. Le armature
mostrano lo stesso fattore 1/(1+φ) finché si resta nel tratto elastico.
Usare il getter predefinito in un report lineare darebbe qui una sottostima del
68,75%; non è un errore nel piano risolto, ma un uso incompatibile dell'overload.
ANTHEA passa φ esplicitamente al getter dell'acciaio e mantiene i rapporti
modulari per CLS e barre: questa criticità non è riprodotta nel modulo attuale.

## A02 — La piattabanda risultante non è un'equivalenza meccanica completa

**Approssimazione già dichiarata, con possibile effetto non conservativo.**

La richiesta di una piattabanda risultante è implementata conservando area e
spessore complessivo. Con larghezze diverse non si conservano baricentro e inerzia.
Il test `UnequalWidthBottomPlatesQuantifyApproximation` confronta il G1 lordo con
un'integrazione indipendente delle quattro piastre reali, N=0 e M=1000 kNm.
Prima piastra inferiore 700×30 mm; restante geometria di default:

| Seconda piastra | σ al lembo inferiore reale [MPa] | σ equivalente [MPa] | Scostamento |
| --- | ---: | ---: | ---: |
| 500×20 mm | 17,789316 | 17,730542 | −0,33039% |
| 200×60 mm | 17,661495 | 17,250769 | −2,32555% |

Queste percentuali valgono per i casi indicati, non sono un limite generale.
Le tensioni sono inferiori al riferimento reale: l'approssimazione non è sempre
conservativa. Non è stata validata l'equivalenza dei pannelli locali delle due
piastre per l'instabilità. Con larghezze uguali i test confermano l'equivalenza.
La UI e il Word espongono proprietà reali/equivalenti e l'avviso; nessuna formula
è stata cambiata durante l'audit.

## A03 — Le situazioni cumulative non conservano la storia costruttiva

**Limite del modello corrente, non un errore aritmetico di sovrapposizione.**

L'iterazione in `BridgeSection.Calculate` riparte per ciascuna situazione e risolve
tutti gli incrementi sulla medesima carpenteria efficace di quella situazione.
Il contributo G1 già applicato può quindi cambiare quando si aggiunge G2 o Q.
Il test `Class4_PriorSteelContributionIsRecomputedOnEachCommonEffectiveSection`
lo quantifica con i default e anima da 8 mm:

| Tensione dovuta al solo G1 [MPa] | Situazione dopo G1 | Situazione finale |
| --- | ---: | ---: |
| Estradosso acciaio | −61,060720 | −56,865701 |
| Intradosso acciaio | 36,553657 | 37,501799 |

Il comportamento è coerente con l'ambito dichiarato “geometria efficace comune”,
ma non rappresenta il mantenimento delle deformazioni/tensioni maturate durante
la costruzione né la loro redistribuzione nel tempo. Se le fasi sono da intendere
come vera storia costruttiva, occorre un metodo dedicato prima di attribuire quel
significato ai risultati; i test qui passati non certificano questa interpretazione.

## Metodo nativo, classe 4 e riferimenti

Il metodo da ponte esiste: `SectionSolver.GetLinearStressAnalysisResult`, in
`GPCChecker.Concrete/SectionSolvers/SectionSolver.cs` intorno alla riga 641,
risolve il piano elastico della geometria assegnata. `GetHomogeneizedMechanicalProperties(phi)`
appartiene a Model. I test confrontano le rispettive proprietà con oracoli separati:
Model comprende le inerzie geometriche proprie; Checker usa pareti sulla linea
media e barre come aree concentrate. La differenza non è di per sé un bug.

Le riduzioni iterative del modulo sono in ANTHEA. Il file Checker
`GPCChecker.Steel/EuroCode/ECClass4ThinWallSection.cs` è commentato;
`GPCChecker.Steel/PanelsStability/EffectiveSection.cs` è escluso con `#if NEVER`.
Il successo dei test Bridge storici non valida pertanto un'iterazione classe 4
nativa attiva: vengono verificati separatamente il solver e l'adattatore.

Il test JRC riproduce il sottopannello 492×8 mm con ψ=0,406, fy=235 MPa:
kσ≈5,632, λp≈0,912, ρ≈0,871. È un controllo indipendente locale della formula,
non un benchmark dell'intero ponte.
[JRC, Commentary and worked examples to EN 1993-1-5, §17.5.2, p. 200 stampata](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2021-12/EUR22898EN.pdf).
Per l'impostazione dei rapporti modulari sono stati consultati gli esempi
[JRC, Design of composite bridges, Davaine](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/D1.8Davaine.pdf).

Restano fuori dalla revisione completa: evoluzione della viscosità/ritiro e della
fessurazione, pannelli irrigiditi, interazione M–V, connettori, fatica e instabilità
globale. fy è nominale o assegnato dall'utente, senza riduzione automatica per
spessore. Le due file di barre nulle sono invece ammesse e coperte dai test.

## Identità delle librerie

Snapshot ANTHEA usato dal runner .NET 8 (SHA-256):

```text
GPCChecker.Concrete.dll C376B007E803013F01393D23136CECBA95447409E7F4DF5DD9E5D291895DE69D
GPCModel.dll           7F74722EE6FA87EC7A4FB8CAD83BB09E53DC57A19CD660D5E892AD8C8AE19D5D
GPCModelData.dll       48832395E1E3A77F8BFBFD55B7FE9A661D62A1A7721CF1A5444EAF3C58C24DAF
```

Le DLL del progetto storico sono differenti. Nel TRX della verifica sorgente
25/09/2026 Model ha hash `47E18FBC09AB77CD4C3B59EF3EB46F0BDEE8B9D8EFADD3D217B2063D5F378BEE`.
Non si è assunto che i vecchi numeri tabulati provassero la stessa versione:
gli oracoli indipendenti sono stati eseguiti in entrambi gli ambienti e il profilo
nullo fallisce in entrambi. Le librerie distribuite in ANTHEA non sono state sostituite.
