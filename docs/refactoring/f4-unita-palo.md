# F4.6a — unità e soglie del palo elastico

13 ottobre 2026. Nessuna modifica al solutore in questo punto. Si riusano
ElasticPile e i convertitori GPC.Model.Units; non si crea un altro motore.

## Inventario

| Confine/soglia | Dimensione e comportamento |
| --- | --- |
| ElasticPileInput | Forze/lunghezze coerenti, F e L; EI in F·L²; momento F·L |
| ConstantKh e LinearKhReference | F/L³; il distribuito è rispettivamente kh·D e kh,ref·z |
| ConstantDistributed | F/L², senza seconda moltiplicazione per il diametro |
| Copertura strati | Relativa: fondo >= L − 1e-10 L |
| Criterio di convergenza | Variazione relativa <= 0.001; denominatore relativo alla scala 1e-12 |
| Ricerca degli zeri | Ascissa normalizzata dell'elemento; soglia relativa ai coefficienti 1e-12; 48 bisezioni |
| Cholesky | Soglia 1e-14 dopo equilibratura diagonale; non è una rigidezza fisica assoluta |
| Mesh | Passo nelle unità di L; massimo 4000 elementi, raffinamento 1–8 |
| ElasticPileSection | Geometria mm, modulo MPa, EI esposto in kN·m²; metadati da distinguere da Input.EI |
| PileAction/armature | Contratto separato kN/kNm/m; ingresso al solutore di sezione N/Nmm già convertito |
| Adattatore ANTHEA | Quote m con selezione tratti ±1e-9 m; diametro m→mm esplicito ma ancora con fattori letterali |
| Resistenza del palo CA | Tolleranza su N max(1000 N, 0.001 |N|), su momento trasversale max(1e6 Nmm, 0.001 |Mx|) |

Il solutore elastico è già indipendente dalle unità. Non serve riscriverlo per
ottenere N/mm; restano F4.6b per i confini nominati degli adattatori e il riuso
dei convertitori già presenti, senza cambiare il contratto pubblico kN/m.

## Prove nuove

56 casi: 48 combinazioni delle tre leggi, quattro vincoli, momento esplicito o
eccentricità e due segni; tre prove di convergenza; cinque bordi della soglia
di copertura. Confrontati spostamenti, rotazioni, azioni, reazioni, rigidezze,
nodi tributari, sollecitazioni di sezione ed estremi; input invariati.
Trasformazioni indipendenti nel test secondo F/L: da kN/m a N/mm.
Tolleranza fissata prima dell'esecuzione: 2e-8·max(1, |a|, |b|), nelle unità
normalizzate del riferimento. Non è una tolleranza normativa.

Le prime prove assumevano erroneamente stesso numero di campioni adattivi e
stessa quota dell'estremo anche su tratti costanti. Il caso acquisito ha
247/248 campioni, ma nessuna nuova quota distante oltre 1e-9 m da quelle
dell'altra rappresentazione: una radice numerica prossima al bordo aggiunge
un punto. Il taglio è costante nel tratto libero, quindi l'argmax non è unico.
Il banco definitivo confronta ogni campione in entrambe le direzioni per
posizione e lato del materiale e verifica che entrambe le quote degli estremi
raggiungano lo stesso valore nell'altro campo. Nessun valore ignorato e nessun
aumento della tolleranza. La rappresentazione dei campioni non è dichiarata
identica byte per byte fra sistemi di unità diversi.

Esito finale: **56/56 nuove prove**, **173/173 suite geotecnica**.
Evidenze ANTHEA in `supporto/artefatti/refactoring/f4/pile-units-final`;
diagnostica completa in `pile-units-diagnostic`, primo tentativo in `pile-units`.

## Difetto individuato da correggere separatamente (F4.6c)

Con uno strato che termina a L·(1−5e-11), la validazione di copertura ammette
l'input, ma la mesh crea un ultimo elemento quasi nullo e lo rifiuta come mal
condizionato. Accade in entrambi i sistemi di unità; non è un difetto di scala.
Lo scostamento L·(1−2e-10) è invece correttamente rifiutato per copertura.
Preparare una regressione dedicata prima della correzione: usare la tolleranza
già esistente per coincidere con la punta senza introdurre un elemento spurio,
lasciando inalterati i confini interni, gli input e il rifiuto oltre tolleranza.
