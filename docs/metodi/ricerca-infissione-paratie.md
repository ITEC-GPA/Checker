# Ricerca dell'infissione — sviluppo e attesi

Obiettivo: minimo fra profondità costruttive discrete assegnate. Ogni
candidato deve ricostruire e calcolare tutte le storie non lineari e tutte
le verifiche richieste. Nessuna sovrapposizione di risultati e nessuna
ipotesi di monotonia. L'intervallo esplorato, il passo e il riferimento
altimetrico sono parte del risultato.

Un esito non convergente o non valutabile non dimostra l'inammissibilità
del candidato: se precede una soluzione ammissibile, il minimo non è
certificato. L'annullamento conserva soltanto le valutazioni complete e
non produce un esito definitivo. La ricerca non fornisce automaticamente
le verifiche non richieste e non sostituisce il progetto completo.

## Atteso indipendente preparato prima del coordinatore

`tools/ParatieEmbedmentExpected.py`: soluzione chiusa di
EI u'''' + k u = 0, EI=2e14 Nmm², k=2 N/mm², H=10000 N in testa;
momento nullo a entrambe le estremità e taglio nullo al piede. Base
cosh(βx)cos(βx), cosh(βx)sin(βx), sinh(βx)cos(βx), sinh(βx)sin(βx),
β=(k/(4EI))^(1/4). Sistema di quattro condizioni al contorno risolto
con Decimal, 60 e 80 cifre; stessi valori dopo conversione in double.
Equilibrio verificato separatamente con Simpson a 8192 intervalli:
errore delle forze <1e−6 N e dei momenti <0.001 Nmm.

Quattordici lunghezze 3500…10000 mm, passo 500, tredici sezioni ciascuna.
Per il limite di spostamento in testa 3.25 mm, il primo candidato della
griglia atteso è L=6500 mm: u(6000)=3.43397078639164 mm,
u(6500)=3.2038458451501377 mm. Questo è un benchmark elastico matematico,
non una paratia reale verificata. Il generatore rifiuta la sovrascrittura
dell'atteso; nessun risultato GPC è stato usato per costruirlo.

Il primo generatore proposto richiedeva mpmath, non disponibile nel
runtime: prima di produrre qualunque fixture è stato sostituito con un
calcolo in sola libreria standard. Nessuna dipendenza di produzione nuova.

## Geometria dei candidati

Il modello più lungo definisce tutto il materiale attraversato. La
riduzione della quota di piede taglia i tratti strutturali, conserva il
terreno sottostante e non elimina sostegni o carichi incompatibili:
questi producono errore di input. Nessun vincolo artificiale al piede.
Le pressioni FEM dipendenti dalla geometria vanno ricalcolate; un profilo
piezometrico assegnato può essere conservato solo con conferma esplicita
che sia indipendente dal candidato. Non si estrapolano terreni o sezioni.

## Collaudo del coordinatore, 19 ottobre 2026

Dieci nuove prove, 24 prove Design paratie complessive: PASS nella corsa
`ANTHEA/supporto/artefatti/paratie/incremento-11/gpc-design`.
Fixture SHA-256 `A6EE11220A0A1E064D9EC657F53746835EA1DEE4CD592676F01D5D5898D8767A`.
Due storie indipendenti con H=10/15 kN e limiti 3.25/4.5 mm selezionano
7500 mm. Provati anche meccanismi, griglia non monotona, dati mancanti,
geometrie errate, annullamento, immutabilità e aggregazione dei verificatori.

Confronto analitico delle 14 lunghezze: tolleranza u=2e−6 mm; per le
tre sezioni di studio L=3500/6500/10000 mm, θ=2e−9 rad, M=5 Nmm,
V=0.01 N. Equilibrio di ogni fase entro 0.01 N e 100 Nmm. Sono limiti
assoluti piccoli rispetto alle azioni del benchmark, non limiti di progetto.
L'inversione H→−H/2 conserva la risposta elastica della storia.

Il primo test richiedeva una riduzione monotona dell'errore passando da
12 a 48 elementi anche sulle azioni recuperate: non superato per L=3500,
M (0.00187→0.01533 Nmm), valori già dominati dall'arrotondamento.
Il test separa ora convergenza da 6 a 24 elementi (riduzione >4 per tutte
le quattro grandezze) e accuratezza assoluta con 96 elementi. Nessuna
modifica ad attesi, tolleranze assolute o motore. Con 96 elementi,
massimo errore osservato nelle tre lunghezze: u=7.24e−8 mm,
θ=1.97e−11 rad, M=0.0481 Nmm, V=0.000197 N.

Nuove API: Model 5.15.0.0 (contratto ricerca), Geotechnics 0.12.0.0
(taglio indipendente del modello), Design 0.27.0.0 (ricerca e aggregazione).
Le altre librerie del treno aumentano la revisione per la ricompilazione
contro Model e la nuova identità dei sorgenti; nessuna modifica alle formule.
Integrazione ANTHEA e rilascio complessivo ancora in corso.
