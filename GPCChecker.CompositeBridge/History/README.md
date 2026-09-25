# Analisi con storico delle deformazioni

Nuovo metodo separato, namespace `GPC.Checkers.CompositeBridge.History`.
Non richiama `HBridgeSection.Calculate` o `CumulativePhaseAnalysis`; questi
rimangono invariati. Il metodo nuovo è nella libreria Checker; ANTHEA offre ora
i selettori Storico lineare e Storico non lineare tramite HBridgeHistoryResults.

## Uso

```csharp
// Stessi dati geometrici/materiali del modulo H; fasi senza limite prefissato.
var result = HBridgeHistoryAnalysis.Calculate(input, new HBridgeHistoryOptions {
    MaterialMode = HistoryMaterialMode.Linear,
    WebLayers = 160,
    ConcreteLayers = 32
}, cancellationToken);

var last = result.Stages.Last();
double strainAtInterface = last.TotalPlane.At(0);
double curvaturePerMetre = last.TotalPlane.Curvature * 1000;
foreach (var fiber in last.Fibers.Where(f => f.Active)) {
    // Identità verificata: Total = Activation + Imposed + Mechanical.
    double mechanical = fiber.MechanicalStrain;
    double stress = fiber.Stress;
}
```

Esiste anche `Calculate(input, IEnumerable<BridgePhase>, options, cancellation)`.
Il numero di fasi/ritiri non ha il limite 20 del vecchio metodo. Ogni ritiro è
un **incremento**: due righe -100 µε e -200 µε producono -300 µε accumulati.
Il relativo φ, ψL o n è quello della singola riga.

L'API generale `BridgeHistoryAnalysis.Calculate(HistorySection, IEnumerable<HistoryPhase>, …)`
usa componenti e fibre, senza ipotesi di profilo H o numero di anime. Per conservare
solo i risultati necessari, usare `BridgeHistoryAnalysis.Enumerate(...)`: restituisce
le situazioni progressivamente e non legge le fasi successive in anticipo. Resta
il normale limite di memoria se si raccolgono tutte le istantanee.

## Convenzioni e algoritmo

L'API generale usa **N, Nmm, mm, MPa**, deformazioni adimensionali, curvature in
1/mm. L'adattatore H converte kN/kNm e microdeformazioni. N e σ positivi sono di
trazione; M positivo comprime le fibre superiori. Il piano comune è

```text
epsilon_tot(y) = epsilon_0 - kappa*y
epsilon_mecc,i = epsilon_tot(y_i) - epsilon_attivazione,i - epsilon_imposta,i
N_int = somma(sigma_i * Aeff_i)
M_int,0 = -somma(sigma_i * Aeff_i * y_i)
```

1. La fase parte da piano, tensioni, aree efficaci e stati materiali accettati
   della fase precedente.
2. Al primo inserimento di un componente si salva `epsilon_attivazione,i` dal
   piano precedente, prima di applicare le azioni della fase. Il nuovo materiale
   nasce scarico sulla configurazione già deformata. È questa la differenza
   essenziale rispetto a un calcolo di tutte le azioni sulla sezione finale.
3. Si aggiungono gli incrementi di deformazione imposta ai soli componenti
   selezionati. Per H il ritiro interessa il CLS netto, senza imporlo alle barre.
4. Le azioni vengono sommate rispetto all'origine fissa y=0. L'incremento di
   momento è `DeltaM - DeltaN*yN`. Un nuovo baricentro non sposta i carichi delle
   fasi già completate. Il baricentro efficace iterativo riguarda il solo carico
   introdotto nella fase corrente; quello lordo viene risolto una volta all'inizio
   della fase. Entrambi sono baricentri di rigidezza **elastica iniziale**, non
   baricentri della rigidezza tangente plastica.
5. Newton risolve equilibrio N–Mx integrando σ e la tangente dei materiali sulle
   fibre attive. Le due incognite sono condizionate con una lunghezza e una quota
   di riferimento interne. La ricerca del passo dimezza gli incrementi se il
   residuo cresce o il tentativo esce dal dominio costitutivo.
6. L'eventuale iterazione esterna aggiorna le aree efficaci e riequilibra anche
   le tensioni già presenti. Tutti i tentativi del sottopasso partono dallo stesso
   stato materiale accettato: le deformazioni plastiche non vengono accumulate
   a ogni tentativo Newton o di larghezza efficace.
7. Solo dopo entrambe le convergenze si accetta lo stato del sottopasso. Per
   azioni grandi o fortemente non lineari si possono assegnare più sottopassi.
   Le istantanee pubbliche sono per fase; se servono istantanee intermedie si
   possono suddividere esplicitamente le fasi di ingresso.

Il bilancio delle fibre fornisce anche i residui indipendenti delle azioni.
In caso di mancata convergenza viene sollevata `HistoryConvergenceException`,
con indice/nome fase, sottopasso e ultima fase completata. Il risultato della
fase fallita non è esposto come utilizzabile. È disponibile l'annullamento.

## Legami costitutivi e significato di φ/n

- `HistoryElasticLaw`: `sigma_new = sigma_old + E_fase * Delta_epsilon_mecc`.
  L'adattatore H usa `Ec,fase = Ecm/(1 + psiL*phi) = Ea/n`. Cambiare φ/n senza
  nuove azioni/deformazioni non riscrive tensioni e deformazioni pregresse.
  Nell'API generale `ModulusFactors` persiste finché una fase non lo sostituisce;
  l'adattatore H lo assegna esplicitamente per ogni fase composta/ritiro.
- `HistoryBilinearSteelLaw`: legge elastoplastica con incrudimento isotropo,
  ritorno sulla superficie di snervamento, deformazione plastica e plastica
  accumulata. Scarico elastico e deformazioni residue sono conservati. Il factory
  `FromModel` legge E, fy, fu, deformazione ultima e tipo di curva da Model;
  non pretende di riprodurre una legge ciclica sperimentale dell'acciaio.
- `HistoryModelEnvelopeLaw`: copia i diagrammi tabulati di Model, usa la loro
  interpolazione nativa e la tangente del segmento. Per il CLS il ramo teso è
  esattamente quello del materiale fornito. È un inviluppo non lineare a funzione
  univoca: lo scarico segue quel diagramma, senza una legge aggiunta di danno,
  chiusura delle fessure o isteresi. Oltre la deformazione ultima di un ramo
  resistente il tentativo viene rifiutato, evitando di interpretare lo zero
  restituito fuori tabella come un equilibrio valido dopo rottura.
- `IHistoryMaterialLaw`: consente altre leggi con stati interni immutabili.
  `Evaluate` deve essere puro rispetto allo stato precedente; il motore decide
  quando accettare la risposta. Le istanze di legge devono essere immutabili
  oppure essere usate senza modifiche concorrenti.

Per il non lineare H assegnare `MaterialMode = Nonlinear` e `Class4 = false`.
Acciaio e barre usano la legge plastica, il CLS l'inviluppo Model; si possono
iniettare leggi alternative tramite le opzioni. Le resistenze dei diagrammi
sono quelle del materiale: **nessun gamma/alpha viene applicato automaticamente**
dal nuovo motore. L'eventuale diagramma di progetto va fornito esplicitamente.

Questa versione conserva lo **storico costruttivo**, ma non calcola l'evoluzione
viscosa automatica in funzione di età e durata. Mancano ancora un nucleo di
compliance/retardazione e le variabili temporali necessarie. Non è corretto
interpretare la sola variazione di φ come una redistribuzione viscosa completa.
Le leggi non lineari fornite rifiutano fattori E diversi da uno: per associare
viscosità e non linearità occorre una legge storica specifica, non la riscalatura
arbitraria di un diagramma. Le deformazioni imposte possono invece essere usate
anche nel non lineare entro il dominio del materiale.

## Geometria ed efficacia

L'adattatore H riusa la geometria validata di Model e mantiene la piattabanda
inferiore equivalente. Piatti rettangolari: due punti di Gauss per striscia.
Per il CLS netto vengono sottratti area, momento statico e momento secondo
effettivi delle barre circolari, poi costruiti due punti con gli stessi momenti
per striscia. Ogni barra usa due punti che conservano area e inerzia propria.
Nel lineare le proprietà lorde così integrate coincidono con quelle geometriche,
incluse le inerzie proprie che il precedente solver a pareti sottili ometteva.

La classe 4 automatica H usa le riduzioni elastiche già disponibili in Checker.
Le fibre restano nelle stesse posizioni per conservare la memoria: per una
striscia tagliata dal limite efficace l'area è pesata con la frazione trattenuta.
Questo approssima posizione e inerzia del bordo efficace; è necessario controllare
la convergenza della discretizzazione. I test confrontano 80 e 320 strisce di anima.
Il risultato riporta l'area realmente usata per ogni fibra e il residuo sulle
riduzioni; non si sostituisce a posteriori la geometria usata per le tensioni.

L'automatismo H rifiuta la combinazione con materiali non lineari: le larghezze
elastiche non sono un modello completo di instabilità postcritica plastica.
L'interfaccia generica `IHistoryEffectiveAreaModel` permette uno sviluppo dedicato.
La classe 4 attuale non conserva danno locale da instabilità: la zona efficace
può recuperare area quando cambia lo stato tensionale.

Disattivare un componente toglie la sua area resistente e riequilibra i carichi.
Lo stato del componente inattivo rimane congelato. Riattivarlo conserva il
riferimento di nascita e la memoria; per rappresentare un nuovo getto usare un
nuovo componente. Le deformazioni imposte a componenti inattivi vengono rifiutate.

L'API generale accetta più anime e componenti; questo non aggiunge torsione,
distorsione, scorrimento parziale o flussi di taglio di celle chiuse. V è
trasportato nello storico come azione; le verifiche di taglio, connessione,
fatica e dettagli restano nel metodo precedente, non sono ricalcolate qui.

## Dati per il debug

Ogni `HistoryStageResult` contiene il piano totale e incrementale, azioni
cumulative rispetto a y=0, quota dell'incremento N, resultanti integrate,
residui, conteggi Newton/efficacia, pannelli, copia della fase applicata e fibre.
Ogni fibra espone area efficace, deformazione alla nascita, totale, imposta,
meccanica e suo incremento, tensione e incremento, fattore E e stato materiale.
Per componenti inattivi la deformazione totale riportata è l'ultima connessa,
non il piano corrente; la loro area partecipante è zero.

## Test e riferimenti

`GPCChecker.Test.CompositeBridge` contiene **52 nuovi casi** sullo storico:
analitici N/M, quota di N, attivazione, ordine costruttivo, residui dopo scarico,
ritiri liberi/vincolati/eccentrici e multipli, φ/n, inerzie del CLS netto, oltre
1.200 fasi, enumerazione progressiva, annullamento, stati immutabili, plastica,
scarico/inversione, diagramma Model, mancata convergenza, classe 4 e mesh.

Eseguiti: **80/80** (52 nuovi + 28 precedenti), sia sulle dipendenze distribuite
ANTHEA sia sulle dipendenze dei progetti sorgente. Inoltre **267/267** regressioni
del metodo precedente con la nuova DLL, comprese le otto istantanee complete.
I filtri dell'audit escludono esplicitamente i difetti già noti e i casi del
costruttore corretto soltanto nei sorgenti Model: non se ne dichiara la soluzione.
La build con lo snapshot non ha avvisi; la build sorgente mantiene i due avvisi
preesistenti di Checker.Concrete sui riferimenti UnsafeEx/architettura GMsh.Net.

Il contratto prova/accettazione è coerente con la separazione fra stato trial e
committed descritta nell'[API ufficiale OpenSees UniaxialMaterial](https://opensees.berkeley.edu/OpenSees/api/doxygen2/html/classUniaxialMaterial.html).
L'integrazione per fibre e materiali uniaxiali è confrontabile con la
[documentazione ufficiale delle sezioni OpenSees](https://opensees.github.io/OpenSeesDocumentation/user/manual/section.html).
Sono riferimenti di architettura/formulazione, non una validazione normativa del
nuovo metodo né un confronto numerico eseguito con OpenSees. Per le riduzioni
elastiche si riusa `EffectivePlateReduction` già oggetto dell'audit Checker.

```powershell
dotnet test GPCChecker.Test.CompositeBridge/GPCChecker.Test.CompositeBridge.csproj -c Release
# Oppure aggiungere: -p:BridgeLibraryDir=<cartella assoluta delle DLL coerenti>
```
# Integrazione e verifica esterna del 25 settembre 2026

`HBridgeHistoryResults.Calculate` restituisce un `HBridgeAnalysisResult` condiviso con la presentazione preesistente. `BridgeStage.GetHistory()` espone lo stato a fibre, le deformazioni e i residui; `BridgeContribution.GetHistoryProfile()` espone il vero incremento di tensione tra stati. Per lo storico i campi affini di `BridgeContribution` descrivono il piano incrementale ma **non vanno utilizzati per ricostruire σ**: chiamare `Stress`/`SteelStress`, che leggono il profilo a fibre. Le proprietà trasformate sono riferite ai moduli elastici della fase, non una rigidezza tangente non lineare.

L'opzione `InstantaneousConcrete` è esplicita: calcola con φ=0 senza mutare gli ingressi archiviati. Il default dell'API resta `false`; la UI la presenta come scelta visibile nel non lineare. Se non attiva, le leggi non lineari che non supportano creep continuano a rifiutare fattori E diversi da 1.

Il confronto numerico eseguito con OpenSees 3.8.0 comprende 29 stati: dati congelati, script esterno e limiti della validazione in `GPCChecker.Test.CompositeBridge/Validation/README.md`. Non è una validazione sperimentale o normativa. È stato corretto il predittore alla cuspide di snervamento: primo tentativo elastico, successive iterazioni con tangente costitutiva, commit sempre a convergenza.

