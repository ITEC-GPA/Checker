# F4.12 — ricerca delle armature delle sezioni

## Quantità e selezione (primo passo)

SectionDesignQuantities e SectionDesignSelection isolano quantità per metro, obiettivi tipizzati, graduatoria/Pareto, campionamento con budget, rete ortogonale e riserva torsionale. Input senza JSON/WPF, geometria delle barre copiata, densità canonica da Model. Riutilizzate le regole elementari PlateDetailingRules; la stessa geometria preparata continua a essere usata nei solutori.

Cattura prima del port: ANTHEA tests/fixtures/f4/reference.json protegge nove casi section-design, compresi rettangolare/circolare, uno/quattro worker, budget, rifiuti e assenza di soluzioni. Gli attesi non sono riscritti. Collegamento e confronto del percorso completo ancora da completare; il primo passo aggiunge servizi alle librerie senza sostituire il motore pubblico di ricerca.

Design 0.7.0.0 per nuove API. Ventisette test Design PASS, API compatibili. Attesi indipendenti per massa longitudinale e sviluppo di staffe, conteggio pezzi, obiettivo pesato, ripartizione torsionale su un rettangolo, rete secondaria, graduatoria con parità e frontiera, budget che campiona l'intero intervallo. Validati numeri non finiti, cataloghi vuoti e geometria degenere. Nessuna certificazione di una proposta priva di verifiche.
