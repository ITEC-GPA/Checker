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
funzione. Il verificatore globale attuale non può distribuire un'azione
lungo una frontiera verticale parzialmente interna alla superficie:
`Prepare` restituisce un esito non supportato quando questi carichi sono
presenti. Non vengono omessi né sostituiti implicitamente da un risultante
concentrato. L'estensione del trasferimento globale e l'integrazione ANTHEA
restano aperte al momento di questo documento.
