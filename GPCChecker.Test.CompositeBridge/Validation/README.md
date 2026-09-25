# Confronto indipendente con OpenSees

Riferimento eseguito il 25 settembre 2026 con OpenSees 3.8.0, pacchetti `openseespy==3.8.0.0` e `openseespywin==3.8.0.0`, Python 3.12.14 Windows x64.

`opensees_reference.py` costruisce una sezione a fibre in un elemento `zeroLengthSection`, risolve con OpenSees e scrive `opensees-3.8.0.json`. Non importa Checker e non legge alcun risultato Checker. Il file JSON è congelato e incluso nei test. Per rigenerarlo:

```text
python -m pip install openseespy==3.8.0.0
python opensees_reference.py opensees-3.8.0.json
```

Unità N, mm, MPa. Il momento di riferimento nel JSON è all'origine y=0. Lo script trasporta esplicitamente il momento al baricentro geometrico usato internamente dalla FiberSection di OpenSees e riporta il piano di deformazione all'origine. Due punti di Gauss per striscia in entrambi i programmi: si verifica il solutore a discretizzazione uguale, non l'errore di discretizzazione della geometria.

## Casi e confronti

| Storia | Stati | Materiali / comportamento |
|---|---:|---|
| N–M elastico eccentrico | 4 | Acciaio elastico, inversioni e scarico completo |
| Ciclo assiale plastico centrato | 6 | Incrudimento isotropo, trazione, scarico, compressione, ricarico |
| Ciclo di flessione plastica | 6 | Plasticizzazione non uniforme, tensioni e deformazioni residue |
| Ciclo N–M non proporzionale | 5 | Forza e momento variabili, inversione dopo plasticizzazione |
| Composta non lineare | 5 | CLS senza trazione con inviluppo tabulato, acciaio elastoplastico |
| Ritiro libero della composta | 1 | Deformazione imposta al CLS, risultanti esterne nulle |
| Getto dopo flessione e ritiro | 2 | Riferimento di getto ricavato da una precedente analisi OpenSees del solo acciaio |

**29/29 stati confrontati.** Per ogni stato si confrontano ε₀, κ, tensione e deformazione di ogni fibra (non solo una risultante di equilibrio).

Modelli OpenSees: `Elastic`, `Hardening` con Hkin=0 e Hiso=E·Et/(E−Et), `ElasticMultiLinear`, `InitStrainMaterial`. Il materiale CLS è una tabulazione esplicita condivisa come dato di ingresso: verifica l'implementazione di `HistoryModelEnvelopeLaw`, non la correttezza normativa della tabella di catalogo. Il getto/ritiro elastico viene ricostruito con deformazioni iniziali; le storie plastiche non ricostruiscono gli stati fra i carichi e conservano la memoria nell'elemento OpenSees.

## Convergenza e tolleranze

Le inversioni plastiche sono sensibili al discretizzare il percorso e alle prove iterative alla cuspide di snervamento. Sono stati eseguiti 20, 200, 2.000 e 20.000 sottopassi; per il ciclo N–M non proporzionale il riferimento finale usa 200.000. Sono numeri da benchmark, non valori suggeriti per l'interfaccia. OpenSees usa `ModifiedNewton -initial` per evitare grandi prove fuori dal ramo allo scarico di Hardening; per il CLS viene usato Newton con line search. Tolleranza OpenSees NormUnbalance 0,001 nelle unità N/Nmm. La mancata convergenza interrompe lo script: nessuno stato fallito viene salvato come riferimento.

Esempio sulla deformazione ε₀ dopo inversione della flessione:

| Sottopassi | Scarto assoluto ε₀ Checker − OpenSees |
|---:|---:|
| 20 | 4,84e−6 |
| 200 | 3,28e−7 |
| 2.000 | 4,80e−8 |
| 20.000 | 5,00e−9 |

Nel riferimento finale lo scarto massimo di tensione è **0,00084431 MPa**, e di deformazione di fibra **7,6281e−9** (0,00763 µε). Il ciclo N–M a 200.000 sottopassi ha scarto massimo di tensione **0,00048580 MPa**. Questi sono scarti misurati, non tolleranze dichiarate a priori come accuratezza dell'intero modulo.

Il test applica 2e−7 relativo più 2e−10 assoluto sulle deformazioni, 2e−12 sulla curvatura e 2e−5 MPa sulle tensioni. Per i tre stati successivi a inversione plastica si usa il pavimento numerico giustificato dallo studio: 2e−8 sulle deformazioni e E·2e−8=0,004 MPa sulle tensioni. I valori effettivi sono registrati nel TRX. Non si richiede identità bit per bit a integratori e percorsi iterativi diversi.

Il confronto ha inoltre fatto emergere un problema del predittore Checker: alla cuspide plastica, arrotondamenti potevano assegnare tangenti diverse a fibre equivalenti e impedire la discesa del residuo. È stato corretto usando un predittore elastico a ogni nuovo equilibrio, poi la tangente costitutiva. Gli stati plastici si accettano solo a convergenza.

## Cosa non valida questo confronto

Non è una validazione sperimentale o una revisione di un progettista indipendente. Non valida instabilità locale/postcritica, viscosità nel tempo, danno ciclico del CLS, connessione parziale, collasso globale o coefficienti e verifiche normative. Non sono verificate tutte le combinazioni di legami/geometrie né la capacità di superare punti limite in controllo di carico. La convergenza della discretizzazione H ha test separati, che sono controlli interni e non un secondo solutore.

Fonti primarie:

- https://openseespydoc.readthedocs.io/en/latest/src/Hardening.html
- https://openseespydoc.readthedocs.io/en/stable/src/ElasticMultiLinear.html
- https://opensees.github.io/OpenSeesDocumentation/user/manual/section.html
- https://github.com/OpenSees/OpenSees/blob/master/SRC/material/uniaxial/HardeningMaterial.cpp

I pacchetti OpenSees sono strumenti di test locali; non sono incorporati nelle DLL distribuite da ANTHEA.

## Curve M–κ e N–ε

`opensees_curves.py` usa `DisplacementControl` e un elemento `zeroLengthSection`.
Il file congelato `opensees-curves-3.8.0.json` aggiunge **35 stati su 6 curve**:
M–κ elastica eccentrica, ciclo M–κ plastico, M–κ con compressione,
M–κ composta non lineare, ciclo N–ε dell'acciaio e ciclo N–ε composto.
In M–κ si mantiene N; in N–ε si blocca κ a zero e si misura anche la reazione M.
Il momento all'origine e quello alla quota scelta vengono entrambi confrontati.

```text
python opensees_curves.py opensees-curves-3.8.0.json 20000
```

Il parametro è il numero base di passi per tratto; il ciclo plastico M–κ usa
dieci volte il valore base, quindi 200.000 nel riferimento finale. Il default
veloce dello script è 200 e non riproduce la precisione del file congelato.
Lo script usa Newton e NormUnbalance 0,001. Interrompe la generazione in caso
di mancata convergenza e non legge alcun valore Checker.

`SectionResponseOpenSeesTests` confronta N, M, ε₀, κ e σ/ε di ogni fibra.
Tolleranza relativa 2e−7, più assoluta 2e−9 su ε, 2e−12 su κ,
0,0005 MPa su σ, 0,02 N su N e 5 Nmm su M.
Scarto massimo misurato: **0,000109108 MPa** su σ,
**5,135e−11** su ε di fibra e **0,394748 Nmm** su M.
La convergenza sul ciclo M–κ allo scarico passa da circa 4,226e−8 su ε₀
con 200 passi a 7,121e−9 con 2.000. A 20.000 lo scarto di una fibra
è 0,001506 MPa; a 200.000 il massimo scende sotto la tolleranza invariata.

Geometrie, leggi esplicite e valori obiettivo sono nello script e nel JSON.
Si confronta il medesimo modello a fibre, non si validano la tabella normativa
Model, l'instabilità locale o un modello di danno del CLS.
Complessivamente, storico e curve hanno **64 stati esterni**.

Il dossier Word di ANTHEA è in
`supporto/documentazione/Validazione_Sezione_Ponte/`.
Il generatore è `supporto/scripts/Build-BridgeValidation.py`.
Per emettere i risultati numerici letti dal generatore, impostare
`BRIDGE_VALIDATION_OUTPUT` a una cartella assoluta prima dei test
`ValidationDossierTests` e `SectionResponseOpenSeesTests`.
