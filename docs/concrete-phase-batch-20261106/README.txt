BATCH PARALLELO DI STORIE N-M1-M2 / RITIRO
GPCChecker.Concrete 0.37.0.0 - 6 novembre 2026

API (namespace GPC.Checkers.Concrete.Response)

1. Molte storie della stessa sezione, con riuso della preparazione:
   CalculateBatch(IEnumerable<ConcretePhaseHistory>, Func<SectionChecker>,
                  int maxDegreeOfParallelism = 0, CancellationToken = default)
   CalculateBatchAsync(...) -> Task<ConcretePhaseBatchResult>

2. Sezioni diverse:
   CalculateBatch(IEnumerable<ConcretePhaseBatchRequest>,
                  int maxDegreeOfParallelism = 0, CancellationToken = default)
   CalculateBatchAsync(...) -> Task<ConcretePhaseBatchResult>

Ogni ConcretePhaseHistory contiene nome, lista delle fasi e ConcretePhaseOptions.
Ogni ConcretePhaseBatchRequest associa una storia alla propria factory del checker.
Le unita' restano N, Nmm, mm, MPa e deformazioni adimensionali; ShrinkageMicrostrain
accetta microdeformazioni. Si applicano i limiti del solver a fasi documentati in
../concrete-phases-20261106/README.txt (nessuna memoria, geometria/materiali fissi,
nessun TensionChord, stress block non lineare o viscosita').

Il batch parallelizza le STORIE indipendenti. Le fasi della stessa storia restano
sequenziali. Il numero di task/worker non cresce con il numero degli input:
una coda dinamica distribuisce gli input tra i worker disponibili. Non ci sono
Task.Run annidati per ogni fase o per ogni storia.
maxDegreeOfParallelism=0 sceglie min(4, Environment.ProcessorCount), ulteriormente
limitato al numero di input. Valori positivi fissano il massimo richiesto.
Il limite automatico e' prudente per storie brevi; non pretende di individuare
l'ottimo per qualsiasi sezione, mesh, runtime e dimensione degli output.

ISOLAMENTO
La factory viene chiamata dai worker e deve produrre un checker e una sezione
esclusivi. Non restituire sempre un checker catturato in una closure. Un controllo
per identita' rifiuta solver/sezioni condivisi contemporaneamente nello stesso
batch. Non mutare materiali, geometria o opzioni durante l'esecuzione; le factory
devono essere sicure da chiamare in parallelo, senza dipendenze dall'interfaccia.
La responsabilita' delle factory comprende eventuali riferimenti mutabili esterni.

Per la stessa sezione ogni worker riutilizza checker, geometria d'integrazione,
leggi riconosciute e dati dei campioni. Ogni storia riparte dal proprio equilibrio
iniziale: carichi, ritiro, deformazioni, contatori e stati non vengono trasferiti.
La factory deve produrre configurazioni equivalenti tra i worker. Dopo un'eccezione
il contesto viene scartato e ricreato per l'input successivo.
Per sezioni diverse la factory della richiesta viene chiamata prima di calcolarla:
non e' necessario costruire e tenere in memoria tutti i checker all'inizio.

RISULTATI E CANCELLAZIONE
Items mantiene esattamente ordine e numero degli input, anche con durate diverse.
Ogni elemento contiene Index, Name, Result, Completed, ErrorType, ErrorMessage
ed ElapsedMilliseconds. Un risultato numerico non convergente conserva Result
con l'ultimo equilibrio accettato. Un'eccezione della factory o dell'analisi
produce Result=null e una diagnostica, senza annullare gli altri input indipendenti.
Gli errori fatali di risorse non vengono trasformati in normali risultati.

La cancellazione richiesta al batch genera OperationCanceledException, senza
restituire un batch incompleto come concluso. Le factory sono codice chiamante:
una factory bloccante deve gestire autonomamente la propria cancellazione.
Le liste vengono copiate prima di avviare l'async; storie, fasi e opzioni sono
immutabili. I risultati numerici restano copie indipendenti dai checker.

Il tempo di ogni item include la preparazione eventualmente assegnata a quell'item,
ma non l'attesa in coda. Nel riuso il primo item di ciascun worker paga la preparazione.
Il tempo dell'intero batch e' tempo di parete, non somma dei tempi sovrapposti.
PreparedSections riporta quanti contesti sono stati costruiti; con successo regolare
e riuso sono al massimo tanti quanti i worker. Le diagnostiche del solver restano
accessibili in Items[i].Result, incluse iterazioni, valutazioni, residui e sottopassi.

ESEMPIO: STESSA SEZIONE
// CreaChecker deve costruire ogni volta una sezione/checker equivalente e distinto.
// fasiA e fasiB sono IEnumerable<ConcretePhase>, gia' definiti in N e Nmm.
var histories = new[]
{
    new ConcretePhaseHistory("A", fasiA),
    new ConcretePhaseHistory("B", fasiB)
};
var batch = await ConcretePhaseAnalysis.CalculateBatchAsync(
    histories, () => CreaChecker(), maxDegreeOfParallelism: 4,
    cancellationToken: cancellationToken);
foreach (var item in batch.Items)
{
    // Controllare item.Completed; per errori: item.ErrorMessage oppure item.Result.Message.
    // item.Result.Phases contiene l'analisi completa delle fasi tentate.
}

ESEMPIO: SEZIONI DIVERSE
var requests = new[]
{
    new ConcretePhaseBatchRequest(new ConcretePhaseHistory("S1", fasiA), () => CreaCheckerSezione1()),
    new ConcretePhaseBatchRequest(new ConcretePhaseHistory("S2", fasiB), () => CreaCheckerSezione2())
};
var batch = ConcretePhaseAnalysis.CalculateBatch(requests, maxDegreeOfParallelism: 2);

OUTPUT E MEMORIA
IncludeIntegrationPoints=true conserva i campioni di calcestruzzo in ogni fase.
Se occorrono solo vertici e armature, l'opzione esistente
new ConcretePhaseOptions(includeIntegrationPoints: false) evita di memorizzare i
campioni interni. Restano gli stessi punti d'integrazione, algoritmo, tolleranze,
azioni integrate e piani di deformazione: cambia il dettaglio dell'output salvato.
Il batch restituisce tutti i risultati: il limite di worker limita i calcoli
simultanei, non la memoria necessaria per conservare l'intera lista dei risultati.

OTTIMIZZAZIONI DEL SOLVER SINGOLO
- Riconoscimento della parabola una volta per contesto, anziche' per campione:
  elimina le copie ripetute degli array della tabella dei materiali.
- Coordinate, pesi e identificatori dei punti di Gauss preparati una sola volta.
- Trasformazione lineare delle azioni in uscita precalcolata; il controllo dei
  residui non costruisce oggetti ResultBeamForces a ogni tentativo di Newton.
- Capacita' delle liste dei campioni preallocata; eliminate lambda temporanee
  in alcune operazioni ripetute di integrazione e controllo dei limiti.
Leggi, integrazione analitica, tolleranze, criteri di arresto e sottopassi restano
quelli del solver precedente. Il riuso delle cache richiede configurazione fissa
durante il batch. Singole chiamate successive ricostruiscono il contesto e quindi
recepiscono eventuali modifiche effettuate tra una chiamata e l'altra.

Tutti i nuovi test e benchmark usano al massimo 6 fasi; il benchmark distribuisce
uniformemente storie di 4, 5 e 6 fasi. L'API non introduce un limite artificiale
al numero di fasi. Prestazioni ed esito delle suite sono in VERIFICA.txt.
