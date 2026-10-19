# Campi recuperati e inviluppi delle paratie

Incremento del 19 ottobre 2026, PAR-27. API in Geotechnics:
`EmbeddedWallAnalysis.Sample` ed `Envelope`. Nessuna combinazione lineare
di risposte non lineari e nessuna nuova soluzione di equilibrio.

## Campo restituito dal motore

Ogni fase conclusa conserva, per elemento, i polinomi in
`t = (z_top - z)/(z_top - z_bottom)`. Lo spostamento di Hermite è cubico;
la rotazione è la sua derivata rispetto alla profondità. Il carico netto
e le due pressioni sono il recupero cubico dai quattro punti di Gauss già
usato dal motore. Integrandolo si ottengono V quartico e M quintico, con
le azioni di estremità equilibrate. N è lineare nel tratto per il peso
proprio; i salti sono ai nodi dei carichi e dei sostegni.

Unità: mm, radianti, N, Nmm, MPa, N/mm. N positivo a compressione,
rotazione `du/d(profondità)`. Tutte le azioni della parete riguardano la
fascia dichiarata. Il recupero non modifica il modello di Winkler né
migliora implicitamente la discretizzazione del campo fisico.

`Sample` richiede quota e lato: Above o Below. Alle estremità il lato
esterno è rifiutato. I valori non derivano da interpolazione delle righe
della tabella, che possono avere quote diverse fra fasi. Gli indicatori
plastici restano quelli del punto di Gauss più vicino, come nella tabella
originaria; non sono una localizzazione continua della frontiera plastica.

## Inviluppo e provenienza

Gli input sono risultati di storie già analizzate indipendentemente.
Sono obbligatori CaseId distinti, uguali estremi geometrici e uguale
larghezza della fascia. Le fasi devono avere identità e campi coerenti.
Mesh differenti sono ammesse; i candidati con piedi differenti della
ricerca d'infissione rimangono distinti.

La griglia usa l'unione dei nodi di tutti i campi, i due limiti in ogni
discontinuità, suddivisioni uniformi e tutte le radici della derivata del
polinomio della grandezza considerata. A ogni quota si selezionano minimo
e massimo fra le osservazioni effettive; ciascuno conserva Caso, Fase,
Sezione, quota/lato e l'intera osservazione N–V–M–u–rotazione–pressioni.
Non si forma una terna artificiale di estremi. I verificatori continuano
a ricevere le azioni concomitanti della fase originaria.

I massimi e minimi esterni globali includono gli estremi interni dei
polinomi. Il disegno fra i punti campionati è un'approssimazione grafica:
non si ricercano tutte le intersezioni fra polinomi di casi differenti,
né si attribuisce una fase unica a tutto un segmento disegnato. La
provenienza è riferita al singolo punto. Le parità esatte sono risolte
per CaseId e StageId ordinali, indipendentemente dall'ordine degli input.

Una storia incompleta rende parziale l'intero inviluppo. Le sole fasi
concluse restano disponibili; i casi senza fasi concluse rimangono nella
diagnostica e non diventano storie soddisfatte. Nessuna convergenza o
inviluppo costituisce una verifica dell'opera. Annullamento cooperativo;
campionamento da 1 a 64 suddivisioni, limite esplicito di un milione di
stazioni complessive per evitare output incontrollati.

## Verifica indipendente e regressione

`tools/ParatieEnvelopeExpected.py` precede l'implementazione e scrive
esclusivamente una fixture nuova. Usa aritmetica razionale per equilibrio
e doppia integrazione della curvatura di una mensola con forze e coppie
concentrate, peso proprio e tre fasi appartenenti a due casi. Con x
positivo dalla testa al piede e coppia C coniugata a du/dx:

`V(x) = somma H_i`, `M(x) = somma [H_i (x-a_i) - C_i]` sulle azioni
sopra la sezione; `u(x) = integrale da x a L di (s-x) M(s)/EI ds`;
`rotazione(x) = -integrale da x a L di M(s)/EI ds`.

Il confronto usa 49 quote, con entrambi i lati interni e concomitanza.
Tolleranze assolute: 2e-8 mm, 2e-11 rad, 2e-7 N su N, 2e-5 N su V,
0,05 Nmm su M; coprono gli arrotondamenti della soluzione della trave
senza mascherare gli errori di unità o i salti imposti. Una prova distinta
usa `M=10000 x-2 x²`: massimo 12,5e6 Nmm a x=2500 mm, fuori dai nodi
e dalla griglia uniforme del disegno, con una sola suddivisione.

Fixture nel formato CRLF del repository, SHA-256:
`C9A7ED23BC4485BFB80FE7203F83CDD6966E378C354E67636FDA662DB249C55D`.

Sette nuovi test, tutti i 67 test EmbeddedWalls e i 24 test Design
delle paratie PASS. Controllati anche
memoria elastoplastica, campi ai punti preesistenti, geometrie incompatibili,
limiti esterni, annullamento, meccanismo e ordine dei casi. Le sette storie
della cattura precedente conservano identici tutti i valori originari:
cambiano solo i metadati additivi Top/Bottom/TributaryWidth e Stages.Elements.
Cattura originale SHA-256:
`7A404044A37A10BBBC5C40A0B9E8D271048808386639D1CF0B711C579A5C9EC3`.

Evidenze locali: `ANTHEA/supporto/artefatti/paratie/incremento-12/`.
Integrazione applicativa, guida globale e set coerente ancora da concludere.
