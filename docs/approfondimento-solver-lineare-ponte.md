# Analisi del solver lineare delle sezioni miste

> **Aggiornamento 27 settembre 2026 (Checker.Concrete 0.0.13.0).** B03, B04 e B05 sono corretti.
> - B03: le forze sono convertite negli assi −X, −Y del solutore (`SectionSolver.SolverAxes`) e i punti del dominio riportati negli assi
>   dell'utente.
> - B04: il CLS sostituito dal profilo inglobato è sottratto con `CalculateElasticSigmaC`.
> - B05: `GetNeutralAxis` trasla l'asse relativo del punto di riferimento.
>
> Nella stessa revisione l'integrazione non lineare del profilo usa la tensione di progetto (fy/γM0) invece della curva caratteristica.
> I test corrispondenti non sono più ignorati. Il testo seguente descrive lo stato del 25 settembre.

25 settembre 2026, approfondimento successivo al [primo audit](audit-sezioni-miste-ponte.md).
Unica correzione di produzione autorizzata e applicata: il costruttore Model con
profilo H nullo. Il solver Checker, i suoi risultati e il calcolo ANTHEA restano
invariati. Le modifiche già presenti in Model su mesh e integrazione sono state
preservate; non fanno parte di questo intervento.

## Risultati principali

| Rilievo | Stato | Riproduzione | Ambito |
| --- | --- | --- | --- |
| B01, costruttore con H nullo | Corretto nel sorgente Model | 4 configurazioni di armature ora passano | Costruzione della sezione |
| B02, inerzia dichiarata a carico nullo | Causa isolata, non corretto | Differenza uguale alle inerzie proprie non sottratte | Metadato ANTHEA, non tensioni native |
| B03, rotazione del riferimento del solver | Bug confermato, non corretto | 3 rotazioni falliscono su sorgente e DLL | API Checker con riferimento ruotato |
| B04, CLS sostituito dall'acciaio inglobato | Bug confermato, non corretto | 4 equilibri lineari indipendenti falliscono | Acciaio dentro il CLS, anche parzialmente |
| B05, asse neutro globale | Bug confermato, non corretto | 3 piani con riferimento traslato falliscono | `StrainPlane.GetNeutralAxis()` |

I tre nuovi bug Checker non sono stati riprodotti nel percorso attuale ANTHEA:
il modulo usa un riferimento con orientamento fisso, il profilo sotto la soletta,
e ricava l'asse a tensione nulla direttamente dal campo tensionale. Questo delimita
l'impatto osservato, senza attestare la correttezza generale del modulo.

## Come lavora effettivamente il metodo

Il percorso è:

```text
SectionSolver.GetLinearStressAnalysisResult(force, psi, psiTendon)
  -> ConvertToForceTuple(options.ForceReferenceCoordinateSystem)
  -> CalculateStrainPlaneLinearStressAnalysis(...)
  -> CalculateStrainPlaneStressAnalysis(..., psiRebars, psiTendon)
     -> CalculateForceResultant(...)
        -> IntegrateSectionStressLinearElastic
        -> IntegrateRebarLinearStress
        -> IntegrateStructuralSteelLinearStress
     -> CalculateIncrementStressAnalysis: Jacobiano numerico 3 x 3
  -> StressAnalysisResult: piano, carico, solver, flag lineare, psi memorizzati
```

Riferimenti: `GPCChecker.Concrete/SectionSolvers/SectionSolver.cs`, righe 641,
865, 2203, 2350, 2428; `GPCChecker.Concrete/Results/StrainPlane.cs`, riga 102.

Il piano incognito è affine nelle coordinate globali della sezione:

```text
epsilon_raw(x,y) = epsilon0 + ChiX*(x-x0) + ChiY*(y-y0)
```

Nel codice `ChiX` e `ChiY` sono i gradienti rispetto a x e y. Non bisogna
identificarli dal solo nome con la curvatura rispetto all'asse omonimo.
N–Mx del ponte attiva soprattutto il gradiente rispetto a y.

Per l'analisi lineare la libreria lascia Ec invariato e moltiplica i moduli degli
acciai per `(1+psi)`. Nel modulo `psi` è il prodotto **ψL φ**, non il solo ψL.
Per carpenteria e armature ordinarie, senza predeformazioni e con lo stesso psi:

```text
sigma_c = Ec * epsilon_raw              (ramo teso solo se abilitato)
sigma_a = Ea * (1+psi) * epsilon_raw
sigma_s = Es * (1+psi) * epsilon_raw

epsilon_fisica_incrementale = (1+psi) * epsilon_raw
Ec_eff = Ec/(1+psi)
n = Ea/Ec * (1+psi)
```

È una formulazione algebricamente equivalente all'omogeneizzazione con Ec_eff,
ma il piano restituito direttamente da `StrainPlane` è quello scalato. I getter
lineari con psi e `CalculateStrainPlaneResult(true, psi, ...)` applicano i fattori
necessari alle grandezze che espongono. Quattro test verificano la compatibilità
σ/E per tutti i materiali con psi=0/0,5/2,2/5. I getter senza psi restano una API
diversa, come illustrato in A01 del primo audit.

Il solver integra il CLS con punti di Gauss sulla mesh, gli acciai strutturali
sulle linee medie delle pareti moltiplicate per lo spessore, e le barre come aree
concentrate. Dove c'è acciaio interno al CLS, deve sottrarre il CLS sostituito.
Le pareti vengono spezzate alle intersezioni con la soletta e classificate tramite
la posizione del punto centrale: non basta il flag dell'intero profilo per
determinare quali tratti siano inglobati.

Le risultanti integrate hanno convenzione:

```text
N  = integral(sigma dA)
Mx = -integral(sigma*(y-y_ref) dA)
My = +integral(sigma*(x-x_ref) dA)
```

Il metodo parte dal piano nullo, calcola il residuo N–Mx–My e aggiorna le tre
incognite con un Jacobiano a differenze centrali. La divisione per `dCX` invece
di `dChiX` non è, da sola, un errore: lavora su incrementi scalati e rimoltiplica
per `deltaChiXLimit` alla fine. Lo stesso vale per gli altri due parametri.
Nei quattro casi non fessurati controllati, il sistema lineare converge in un
solo aggiornamento: ID iniziale 1, ID finale 2.

Il controllo di ingresso usa il quadrato della tolleranza; il ciclo successivo
usa 1E-5 sulle forze normalizzate. Le scale dipendono dalle dimensioni, da fck e,
per l'acciaio, da una stima `15*fck`: non sono rapporti di resistenza. Il limite
è ID 50. La mancata convergenza può restituire un risultato con piano nullo e log,
che il chiamante deve controllare.

Se `considerTensileConcrete=false`, anche il percorso detto lineare esclude il
CLS teso: la zona reagente dipende dal piano. Non è allora un unico sistema
lineare non fessurato. I test dell'equivalenza elastica qui citati abilitano
esplicitamente il CLS teso per isolare il sistema lineare.

### Piani delle fasi

`StressAnalysisResult` e `StrainPlane` bastano per questa diagnosi; i nuovi test
registrano nel TRX origine, epsilon0, entrambi i gradienti, fattore fisico e ID.
Non è stata introdotta una nuova classe di produzione durante la revisione.
Una futura classe del ponte dovrebbe distinguere piano scalato, piano fisico
incrementale, materiali attivi e situazione efficace utilizzata.

Non si devono sommare direttamente i piani raw di fasi con psi differenti.
Inoltre G1 produce deformazione nell'acciaio prima dell'attivazione della soletta:
un singolo piano totale attribuito indistintamente a tutti i materiali non
rappresenterebbe automaticamente quella storia. Il solver nativo risolve una
azione sulla sezione assegnata; non ricostruisce da solo la successione costruttiva.

## B01 — Correzione del costruttore

In `Model/Model/Sections/Concrete/ReinforcedConcreteSection.cs`, righe 274–281,
`IsInsideConcrete=false` è ora assegnato all'oggetto effettivamente creato,
nell'initializer di `SteelSectionPosition`. È stato eliminato l'accesso
incondizionato a `_steelSections[0]` dopo il ramo facoltativo.

Passano quattro test con H nullo e zero/una/due file, più tre controlli del
posizionamento di un H presente con eccentricità −200/0/+200 mm. Restano invariati
distribuzione delle barre, materiali, posizionamento e calcolo.
Build Model: zero errori e zero avvisi.

Le DLL sotto `ANTHEA/lib/Checker` non sono state sostituite: contengono ancora
la versione precedente. La verifica della correzione usa la build sorgente Model
nel progetto storico Checker. Questo evita di distribuire insieme alla correzione
anche le altre modifiche preesistenti nella copia di lavoro Model.

## B02 — Carico nullo: causa esatta

`ANTHEA/X.Core/BridgeSection.cs:209` chiama Checker solo con N o M diversi da zero.
La correzione da inerzia geometrica a inerzia di integrazione si trova nello
stesso ramo, alla riga 226. Quindi il caso nullo conserva I geometrica, mentre
quello caricato espone I delle linee medie/barre concentrate.

Il test ora controlla prima che la differenza sia precisamente:

```text
sum(b_flange * t_flange^3 / 12)
  + sum(I_propria_barra) * (Es/Ea - 1/n)
```

Nel caso già riportato: `2 342 068,35077 mm^4`. Questa identità passa; fallisce
invece l'aspettativa che I di integrazione sia indipendente dal valore del carico.
Tre ulteriori test chiamano direttamente Checker con N=M=0, anche dopo un
calcolo caricato sullo stesso solver: piano non nullo, gradienti e tensioni
esattamente nulli. **L'anomalia segnalata non nasce dal calcolo nativo a carico
nullo, ma dall'inizializzazione della proprietà esposta nell'adattatore.**
Nessuna correzione applicata, come richiesto.

## B03 — Riferimento ruotato non rispettato

**Priorità alta per chi usa riferimenti ruotati.** Il residuo è espresso nel
riferimento passato dalle opzioni; le integrazioni lavorano invece con x e y
della sezione. `GetExternalForces(ForceTuple, CoordinateSystem)`, riga 2468,
usa l'origine per il trasporto ma non ruota le componenti. Anche il Jacobiano
è costruito sulle risultanti di integrazione senza quella rotazione.

Il test mantiene geometria e carico fisico invariati e converte correttamente
il `ResultBeamForces` al nuovo riferimento con `ToCoordinateSystem`. Con N=−200 kN,
M1=2000 kNm, M2=500 kNm, psi=2,2, al medesimo primo vertice globale dell'acciaio:

| Rotazione delle opzioni rispetto alla base | σ [MPa] |
| --- | ---: |
| 0° | 47,494411 |
| 30° | 78,710575 |
| 90° | 75,147719 |
| 180° | −47,523641 |

Le tre uguaglianze attese falliscono sia sul sorgente sia sulle DLL ANTHEA.
Il controllo complementare ruota solo la descrizione del carico, mantenendo
fisse le opzioni del solver: tutti e tre i casi passano. Il difetto è quindi
isolato alla gestione del riferimento del solver, non alla semplice conversione
del vettore di carico di questo esempio.

Test: `RotatedSolverReference_WithSamePhysicalForceShouldPreserveStress`.
Non corretto. Non coinvolge il riferimento fisso attualmente usato dal modulo.

## B04 — Legge del CLS incoerente nell'integrazione lineare dell'acciaio interno

**Priorità alta per sezioni con carpenteria inglobata.** In
`IntegrateStructuralSteelLinearStress`, `SectionSolver.cs:1206`, il termine da
sottrarre per il CLS sostituito richiama `CalculateSigmaC(strain)`, ossia la legge
di progetto non lineare. Il CLS lordo è invece integrato con
`CalculateElasticSigmaC(strain)`. Per le barre, il codice usa già la sottrazione
lineare coerente.

Caso indipendente: CLS 500×500 mm, H centrato interamente interno, altezza 300 mm,
anima 10 mm, ali 200×20 mm, As=10 600 mm², nessuna barra, C35/45 e S355,
trazione del CLS abilitata. Per N centrato, il riferimento lineare esatto è:

```text
epsilon_raw = N / [Ec*(250000-10600) + Ea*(1+psi)*10600]
```

| psi | N assegnato [kN] | N ricostruito con il modello lineare corretto [kN] | Scostamento |
| --- | ---: | ---: | ---: |
| 0 | −1000 | −984,441632 | −1,55584% sul modulo |
| 0 | +1000 | +966,383178 | −3,36168% |
| 2,2 | −1000 | −989,372964 | −1,06270% sul modulo |
| 2,2 | +1000 | +992,183225 | −0,78168% |

La causa è verificata numericamente, non soltanto dedotta dal nome del metodo:
aggiungendo al risultante corretto il termine errato
`[Ec*epsilon_raw - sigma_c_design(epsilon_raw)]*As`, si ricostruisce il carico
assegnato entro circa 1,1 N in tutti e quattro i casi. Per esempio, nel caso
psi=0 in trazione il CLS lineare vale 3,171356 MPa, mentre quello di progetto
usato nella sottrazione vale zero; la differenza genera circa 33,616 kN.

Un semplice raddoppio di una piccola compressione può ancora passare un test
di proporzionalità: la rigidezza resta approssimativamente costante nello stesso
ramo, ma è sbagliata. Per questo il test usa l'equilibrio analitico indipendente.

Test: `EmbeddedSteel_LinearSolverMustSubtractLinearConcrete` (quattro casi).
Non corretto. Il profilo della sezione da ponte corrente è esterno alla soletta,
perciò i test N–Mx di quella geometria non attivano il ramo difettoso.

## B05 — Asse neutro globale traslato in modo errato

`StrainPlane.GetNeutralAxis()`, riga 128, omette termini della posizione del
riferimento. Con `ReferencePoint=(1000,500)`, epsilon0=−0,001, ChiX=0,
ChiY=1E−6, il piano si annulla a y=1500 mm. Il metodo restituisce y=1000 mm,
dove la deformazione vale −0,0005.

Sono provati anche gradiente solo x e gradiente obliquo: i punti della retta
restituita non appartengono al piano a deformazione nulla. Il metodo relativo
`GetNeutralAxisRespectReferencePoint()` seguito dalla traslazione del riferimento
passa gli stessi tre controlli. Quest'ultimo è il percorso usato dal riepilogo
`CalculateStrainPlaneResult`, quindi il difetto non va attribuito indistintamente
a tutti i risultati di asse neutro.

Test: `GlobalNeutralAxis_MustLieOnZeroStrainPlane`. Non corretto.

## Esecuzioni finali

Nuovi casi di approfondimento: 31, di cui 21 passati e 10 falliti sui tre bug
Checker. I test del costruttore nullo sono passati da uno a quattro casi.

| Esecuzione completa | Totale | Passati | Falliti |
| --- | ---: | ---: | ---: |
| Checker sorgente + Model corretto, inclusi 16 Bridge storici | 102 | 92 | 10 |
| DLL ANTHEA conservate + adattatore ANTHEA | 151 | 136 | 15 |

I 15 fallimenti della seconda riga sono: 10 sui nuovi bug Checker, 1 su B02,
4 sul vecchio costruttore ancora presente nelle DLL conservate. Nessun test è
ignorato. Tutti i 16 Bridge storici passano dopo la correzione del costruttore.
Le righe non si sommano: 86 casi nativi sono condivisi fra i due runner.

Codice in [BridgeLinearSolverAuditTests.cs](../GPCChecker.Test.Concrete/BridgeLinearSolverAuditTests.cs),
[BridgeElasticStagesTests.cs](../GPCChecker.Test.Concrete/BridgeElasticStagesTests.cs) e
[BridgeModuleStagesTests.cs](../GPCChecker.Test.BridgeAudit/BridgeModuleStagesTests.cs).
Comandi nel [README](../GPCChecker.Test.BridgeAudit/README.md).
TRX locali in `TestResults/BridgeDeepSource` e `TestResults/BridgeDeepSnapshot`,
con piani e hash delle DLL caricati nei messaggi di ogni prova.

Hash Model della build sorgente corretta:
`08024CD10DD5CAB8A26A97FE22EBC7FDDDAA8F938E2189B769E72F01BE15C930`.
Gli hash delle DLL ANTHEA sono invariati rispetto al primo audit.
