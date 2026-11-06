CALCOLO A FASI N-M1-M2 SENZA MEMORIA DEI MATERIALI
GPCChecker.Concrete 0.36.0.0 - 6 novembre 2026

API
Namespace GPC.Checkers.Concrete.Response:
  ConcretePhaseAnalysis.Calculate(checker, phases, options, cancellationToken)
  ConcretePhaseAnalysis.CalculateAsync(checker, phases, options, cancellationToken)

Il metodo e' aggiuntivo. I precedenti ingressi di analisi tensionale e resistenza
mantengono il loro comportamento. Non richiede modifiche a Model o CompositeBridge.
Rimane disponibile in .NET Standard 2.0, anche per chiamanti .NET Framework.

MODELLO
Sezione piana con geometria e materiali costanti, tutte le parti attive dall'inizio.
Ogni fase contiene alternativamente un incremento di N-M1-M2 oppure un incremento
uniforme di deformazione libera del calcestruzzo (ritiro). Gli incrementi si sommano.
La sezione si equilibra sulle azioni complessive, senza sommare risposte non lineari.
Non sono modellati attivazioni di getti, variazioni di modulo, maturazione, viscosita',
danno, isteresi, plasticita' con stato interno o deformazioni irreversibili residue.
La sequenza riprende l'organizzazione carico/ritiro del modulo ponti; non ne importa
le leggi con memoria o la formulazione monoassiale della flessione.

Le leggi possono essere non lineari, anche con plateau: carico e scarico seguono
sempre la stessa relazione tensione-deformazione. Lo stato precedente serve solo
come previsione numerica. Con softening o plateau possono esistere piu' equilibri:
la convergenza non dimostra unicita' o stabilita' energetica dell'equilibrio.

UNITA' E SEGNI
Coordinate mm, azioni N e Nmm, tensioni MPa, deformazioni adimensionali.
Compressione negativa; ritiro negativo. ShrinkageMicrostrain("ritiro", -200)
equivale a Shrinkage("ritiro", -0.0002). Un valore positivo e' espansione libera.
Ogni valore e' un INCREMENTO, non il valore cumulativo da raggiungere.
Gli assi delle azioni possono ruotare nel piano e avere origine diversa; devono
essere ortonormali destrorsi e mantenere il verso positivo dell'asse longitudinale.
Taglio e torsione devono essere nulli.

epsilon_tot(x,y) = epsilon_ref + ChiX*(x-Xref) + ChiY*(y-Yref)
epsilon_c,mecc = epsilon_tot - somma(delta_epsilon_ritiro)
epsilon_s,mecc = epsilon_tot + EpsilonP (predeformazione fissa gia' nelle barre)

ChiX e ChiY sono gradienti globali di deformazione in 1/mm, non i momenti M1 e M2.
Nel riferimento interno: N=int(sigma dA), Mx=-int(sigma*(y-Yref) dA),
My=int(sigma*(x-Xref) dA). I risultati vengono trasformati negli assi del checker.
epsilon_ref si riferisce al punto di integrazione: per confrontare checker con
origine diversa confrontare epsilon_tot negli stessi punti fisici.

Barre e profili immersi sostituiscono il calcestruzzo occupato. La sottrazione
usa sigma_c(epsilon_tot-epsilon_ritiro), non la deformazione meccanica dell'acciaio.
Le tensioni puntuali riportate sono quelle dei singoli materiali, senza sottrazione.
Per le barre FreeStrain=-EpsilonP, cosi' MechanicalStrain=TotalStrain-FreeStrain.
L'equilibrio iniziale a N=M1=M2=0 include le eventuali predeformazioni delle barre.

SCELTE DEL CHECKER
StressAnalysisType determina analisi Linear o NonLinear. Le leggi dei materiali
e i coefficienti di progetto sono quelli del checker esistente. FailureDomainType
e FailureAnalysisType non trasformano questo calcolo in una verifica di resistenza.
Il calcolo lineare resta lineare, senza un arresto automatico ai limiti SLU.
Il non lineare rifiuta deformazioni oltre il limite ultimo di compressione del
calcestruzzo e i limiti ultimi degli acciai, per non interpretare come equilibrio
lo zero restituito da alcune tabelle fuori dominio. La trazione del calcestruzzo
puo' invece proseguire nel ramo fessurato previsto dalla legge scelta.
Il limite di compressione del calcestruzzo viene controllato sui vertici; quello
delle barre sulla loro posizione; per i profili si controllano i bordi delle pareti.
L'integrazione dei profili usa la quadratura delle pareti sottili del solver.

La parabola analitica con taglio della mesh e' riutilizzata quando configurata e
quando il calcestruzzo teso e' escluso. Le altre leggi seguono la quadratura del
solver. Trazione ordinaria e TensionStiffening sono supportati con le rispettive
opzioni. TensionChord non e' supportato: l'accoppiamento barre/calcestruzzo in
presenza di ritiro richiede una formulazione specifica, non una sostituzione locale.
Il modello non lineare StressBlock viene rifiutato (diagramma di resistenza
discontinuo). PsiCoefficientRebar e PsiCoefficientTendon devono essere entrambi
zero: non vengono ignorati ne' reinterpretati come una storia viscosa.

METODO NUMERICO
Newton a tre incognite scalate: epsilon_ref, ChiX*L, ChiY*L, dove L e' il massimo
semingombro rispetto al riferimento, almeno 1 mm. Residui interni N, Mx/L, My/L.
Jacobiana per differenze finite centrali della stessa risposta integrata, con
passo 1e-8 nelle incognite scalate; differenza unilaterale ai limiti di dominio.
Sistema 3x3 con pivoting parziale, senza inversione esplicita. Ricerca lungo la
direzione di Newton, correzione massima scalata 0.002, dimezzamenti dei sottopassi.
Con tangente singolare si tenta la tangente di riferimento. Per scaricare da un
plateau si puo' riavviare una volta dal campo di deformazione libera; l'accettazione
usa sempre le tensioni effettive, senza aggiungere rigidezza ai materiali.

L'accettazione usa i residui negli assi DI USCITA del checker:
S=max(|N|, |M1|/L, |M2|/L) delle azioni applicate;
tolN=ForceTolerance + RelativeTolerance*S;
tolM=MomentTolerance + RelativeTolerance*S*L.
NormalizedResidual=max(|rN|/tolN, |rM1|/tolM, |rM2|/tolM), accettato se <=1.
Default: 0.001 N, 0.1 Nmm, termine relativo 1e-9; massimo 60 iterazioni,
24 tentativi di line search, 8 suddivisioni ricorsive, 4096 tentativi per fase.
Substeps della fase impone da 1 a 4096 intervalli iniziali, ulteriormente adattabili.
Queste tolleranze riguardano l'equilibrio numerico, non l'errore del modello fisico
o della mesh. Le tensioni campionate non certificano gli estremi della sezione.

OUTPUT E ARRESTO
InitialState: equilibrio prima delle fasi, oppure null se non trovato.
Phases: risultati delle fasi tentate, nell'ordine, con nome e input richiesto.
State: azioni cumulative applicate e integrate; ritiro cumulativo; piani totale
e meccanico del calcestruzzo; asse neutro meccanico; tensioni/deformazioni per
vertice, barra, punto di integrazione e profilo; residui e tangente 3x3.
La tangente ha righe N,M1,M2 negli assi di uscita e colonne epsilon_ref,ChiX,ChiY.
Non equivale a tre EI disaccoppiati. Puo' essere nulla o singolare sul plateau;
puo' mancare (null) se non e' valutabile presso un estremo del dominio.
ConcreteNeutralAxis e' null per deformazione uniforme, compreso il piano nullo.
IncludeIntegrationPoints=false riduce i campioni di calcestruzzo salvati, non la
precisione dell'integrazione. Restano vertici, barre e punti dei profili.
Gli output e le azioni di input sono copie indipendenti dagli oggetti chiamanti.

Ogni fase riporta Iterations (correzioni Newton tentate, anche fallite), Attempts
(tentativi di sottopasso), AcceptedSubsteps, ForceEvaluations (incluse derivate e
campionamento della tangente finale), ElapsedMilliseconds. Anche l'equilibrio
iniziale ha contatori e tempo. Il tempo totale somma equilibrio iniziale e fasi;
esclude la costruzione del checker e la preparazione del contesto dell'analisi.
Un riavvio Newton nello stesso sottopasso conta tra le iterazioni, non come fase.

Se una fase fallisce, State conserva l'ULTIMO SOTTOPASSO ACCETTATO, eventualmente
lo stato iniziale della fase; ReachedFraction<1 indica quanto dell'incremento e'
stato applicato. La fase richiesta resta in Phase. Le fasi successive non partono.
Stop distingue completamento, equilibrio non trovato e uscita dal dominio.
Non vengono mascherate eccezioni di integrazione restituendo forze nulle.
Gli input invalidi/non supportati generano eccezioni. La cancellazione genera
OperationCanceledException e non presenta una sequenza incompleta come conclusa.
Il checker e i materiali non devono essere modificati o usati in parallelo
durante Calculate/CalculateAsync. Per analisi contemporanee usare checker distinti.

ESEMPIO (checker gia' configurato, psi nulli)
using GPC.Checkers.Concrete.Response;
using GPC.Model.Results;

var axes = checker.SectionCheckerOptions.ForceReferenceCoordinateSystem;
var phases = new[]
{
    ConcretePhase.Load("Permanenti",
        new ResultBeamForces(-800000, 0, 0, 0, 30e6, 10e6, axes)),
    ConcretePhase.ShrinkageMicrostrain("Ritiro", -200),
    ConcretePhase.Load("Incremento variabili",
        new ResultBeamForces(-200000, 0, 0, 0, 15e6, 5e6, axes), substeps: 4)
};
var result = ConcretePhaseAnalysis.Calculate(checker, phases);
// Prima di usare il risultato finale controllare result.Completed.
// Per ogni fase: phase.Completed, phase.ReachedFraction, phase.State,
// phase.Iterations, phase.ElapsedMilliseconds, phase.State.NormalizedResidual.
// L'ultima sollecitazione completa e' N=-1000000 N, M1=45e6 Nmm, M2=15e6 Nmm.

VERIFICA
GPCChecker.Test.Concrete/ConcretePhaseAnalysisTests.cs contiene controlli nuovi:
ritiro libero lineare/non lineare, ritiro RC simmetrico e asimmetrico con soluzioni
chiuse, flessione deviata non lineare con ritiro e quadratura polinomiale indipendente,
invarianza per suddivisione e rototraslazione degli assi, scarico dopo snervamento,
piccolo scarico dal plateau, predeformazioni iniziali, profilo immerso, tension
stiffening, sottopassi adattivi, carico impossibile, copie indipendenti, async,
cancellazione e input/opzioni non supportati. Nessuna fixture esistente rigenerata.
I risultati delle suite finali vengono registrati in VERIFICA.txt.
