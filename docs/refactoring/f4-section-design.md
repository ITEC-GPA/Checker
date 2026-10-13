# F4.12 — ricerca delle armature delle sezioni

## Quantità e selezione (primo passo)

SectionDesignQuantities e SectionDesignSelection isolano quantità per metro, obiettivi tipizzati, graduatoria/Pareto, campionamento con budget, rete ortogonale e riserva torsionale. Input senza JSON/WPF, geometria delle barre copiata, densità canonica da Model. Riutilizzate le regole elementari PlateDetailingRules; la stessa geometria preparata continua a essere usata nei solutori.

Cattura prima del port: ANTHEA tests/fixtures/f4/reference.json protegge nove casi section-design, compresi rettangolare/circolare, uno/quattro worker, budget, rifiuti e assenza di soluzioni. Gli attesi non sono riscritti. Collegamento e confronto del percorso completo ancora da completare; il primo passo aggiunge servizi alle librerie senza sostituire il motore pubblico di ricerca.

Design 0.7.0.0 per nuove API. Ventisette test Design PASS, API compatibili. Attesi indipendenti per massa longitudinale e sviluppo di staffe, conteggio pezzi, obiettivo pesato, ripartizione torsionale su un rettangolo, rete secondaria, graduatoria con parità e frontiera, budget che campiona l'intero intervallo. Validati numeri non finiti, cataloghi vuoti e geometria degenere. Nessuna certificazione di una proposta priva di verifiche.


## Catalogo e coordinamento (secondo passo)

SectionDesignCatalogue genera la griglia e la priorità approssimata; SectionDesignSearch gestisce preparazione, gruppi longitudinali, prove iniziali, vicini, completamento, budget, annullamento, progressi, esclusioni e graduatoria. I payload generici sono privati per candidato e opachi al motore; i callback collegano i servizi Concrete già esistenti. Il motore decide l'ammissibilità dagli esiti, non dalle etichette; conserva il primo esempio escluso per identità stabile. Il criterio Plastico/Elastico resta nei contratti delle verifiche, separato dallo stato limite.

Design 0.8.0.0: API nuova di catalogo/coordinamento. Trenta test Design PASS: conteggi manuali della griglia, ordine, stessa graduatoria ed esclusioni con uno/quattro worker, una sola valutazione per gruppo longitudinale compatibile, budget e annullamento senza dichiarare completa una ricerca parziale. Empty checks non certificano una proposta. Adattatore e collaudo completo ancora da collegare nel passo seguente.
