# Carichi distribuiti su tratti della paratia

`EmbeddedWallDistributedLoad` definisce un carico orizzontale lineare tra
due quote della paratia: intensità iniziale e finale in N/mm, riferite
all'intera fascia rappresentata. Non sono pressioni in MPa e non vengono
moltiplicate nuovamente per la larghezza. Quote in mm positive verso
l'alto, azione positiva verso destra. Segni diversi agli estremi ammessi;
tratti sovrapposti si sommano. Identificativo univoco nella fase e fonte
obbligatori. Il carico uniforme storico resta separato e additivo.

La mesh include gli estremi di tutti i tratti di tutte le fasi. In ciascun
elemento la quadratura Gauss a quattro punti integra il carico consistente
con le funzioni di Hermite: prodotto di grado al massimo quattro. Non si
concentra il risultante in un nodo. Il recupero esistente integra il campo
distribuito per ottenere V e M; conserva i valori unilaterali delle
intensità alle discontinuità e la continuità delle azioni interne in
assenza di forze/coppie concentrate.

Ogni fase descrive il proprio campo completo. Installazione, variazione,
spostamento o rimozione di un tratto interpolano i campi di carico durante
gli incrementi; non sommano risultati di storie non lineari. Gli input
sono copiati prima della prima iterazione. Rollback e annullamento
rimangono quelli del solutore.

## Prove indipendenti e regressione

Prima del codice di produzione, `tools/ParatieDistributedExpected.py` ha
creato `tests/EmbeddedWalls.Tests/distributed-expected.json` con aritmetica
razionale: integrazione esatta della funzione di Green della mensola
sotto forze infinitesime. Nessuna matrice FEM o funzione di forma è
riutilizzata. Sei campi: uniforme, due triangolari, trapezio parziale,
due intervalli sovrapposti con segni diversi, campo a risultante nulla.
Mensola di 6000 mm, EI=2e14 Nmm²; V, M, rotazioni, spostamenti e reazioni
in 13 quote. L'atteso originale non è stato riscritto.

`distributed-kernel-04`: 58 test Geotechnics PASS, di cui otto aggiunti
(sette funzionali e una cattura di regressione). Mesh 1500/500/125 mm,
incrementi 1/8/32, raffinamento della deformata interna, campo uniforme
equivalente su terreno elastoplastico, storia completa, dati invalidi,
mutazione degli input da callback, annullamento e fase non convergente.
Tolleranze: u nodale 5e-8 mm, rotazione 5e-11 rad, V 2e-5 N, M 0.05 Nmm,
equilibrio globale dei momenti 0.1 Nmm; includono il condizionamento della
mensola discretizzata. La deformata cubica interna converge alla soluzione
continua, mentre i nodi della mensola con carico consistente sono esatti
a meno dell'arrotondamento numerico.

Cattura prima/dopo di sei mensole uniformi e di una storia di scavo:
JSON identici, SHA-256
`7A404044A37A10BBBC5C40A0B9E8D271048808386639D1CF0B711C579A5C9EC3`.
Artefatti ANTHEA `supporto/artefatti/paratie/distributed-before` e
`distributed-after`. Nessuna fixture storica o lista di fallimenti ammessi
modificata. Primo tentativo: percorso di progetto Model errato; nuovo test
con costruttore del profilo globale errato. Il test di scavo ora ammette
la mancata convergenza prima di raggiungere il meccanismo finale e verifica
separatamente il modello già privo di vincoli: Unstable.

## Limiti espliciti

Si tratta di azioni assegnate direttamente alla struttura; diffusione di
carichi sul terreno, Culmann e pressioni sismiche non sono dedotti da questa
funzione. L'integrazione ANTHEA resta in corso.

## Trasferimento alla stabilità globale

Il primo candidato (`candidate-16`, 3148 PASS e 17 fallimenti storici Steel
ammessi) rifiutava esplicitamente questi carichi nel modello globale.
La successiva verifica del contratto `SlopeGeometry.Admissible` ha mostrato
che tutte le superfici ammesse passano sotto ogni vertice di ogni corpo
rigido: l'intera parete continua e ogni tratto caricato rimangono nella
massa scivolante. Non sono ammesse superfici che tagliano la parete.

Nel campo così delimitato si trasferiscono, per ogni tratto, una forza H e
una coppia C al punto medio y0, mantenendo un unico identificativo per i
coefficienti. Con L=ztop−zbottom e b larghezza della fascia:

H = (qtop+qbottom)L/(2b); C = (qbottom−qtop)L²/(12b).

Sono grandezze per unità di lunghezza fuori piano: H in N/mm, C in Nmm/mm.
La direzione della valle moltiplica entrambe le azioni. Il momento motore
rispetto al centro del cerchio è H(yc−y0)+C, esattamente uguale
all'integrale del campo originale. Non si divide per H: una risultante
nulla conserva la coppia. L'identificativo è `StructureInterval-<Id>`;
la selezione normativa esplicita applica una sola volta γ·ψ a forza e
coppia. Un tratto con entrambe le intensità nulle non aggiunge azioni.

Prima dell'estensione sono stati generati tre attesi razionali integrando
q(y) e yq(y), conservati in `global-distributed-expected.json`; generatore
`tools/ParatieGlobalDistributedExpected.py`. `distributed-global-01`:
60 test PASS. Due nuove prove verificano entrambi i versi di scorrimento,
traslazione delle quote, risultante nulla, forze e momenti dei conci,
equivalenza esatta al carico uniforme storico e stessa soluzione della
ricerca globale, rifiuto di coefficienti mancanti e applicazione una sola
volta. Slopes non è stato modificato. Questo trasferimento non si estende
implicitamente a superfici che intersecano la paratia o a carichi esterni
al corpo continuo.
