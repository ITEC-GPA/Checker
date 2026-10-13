# F4.11a.1 — BridgeConcept in GPC.Design

Port iniziale equivalente della geometria, analisi a trave prismatica, stime delle strutture speciali, fondazioni, quantità, computo e ricerca discreta. Ingressi immutabili BridgeInput/BridgeAssumptions/BridgeRates: proprietà richieste e validate, nessun JSON, documento o WPF. Liste del catalogo e risultati esposti in sola lettura; ogni ricerca possiede il proprio stato e supporta cancellazione/progressi.

La selezione mantiene tentativi, motivi di esclusione, Pareto, ordinamento e cambiamenti. Identità delle geometrie per valori e sequenze, senza serializzazioni JSON. Tutti i 14 tipi restano predimensionamenti parametrici, non verifiche normative.

Provenienza: ANTHEA ec284cf2, cattura immutabile prima del port in tests/fixtures/f4/bridge-reference.json. Copia compressa identica nei valori in tests/Design.Checks/Fixtures/bridge-f4.json.gz, SHA nel test. 31 calcoli e 17 griglie: 39265 e 121757 numeri confrontati, proposte e giudizi invariati; tolleranza numerica 1e-10, dati discreti/testi esatti. 38 test Design PASS; prove indipendenti di trave appoggiata, rifiuti, annullamento e risultati posseduti.

Nessun servizio GPC esistente per la ricerca tipologica o trave continua a tre momenti è stato trovato; BeamAnalysisProfile descrive risultati di un modello e non risolve questo problema. Il modulo E e la densità sono stati prima raccolti in un solo punto mantenendo l'ordine aritmetico catturato. Il riuso immediato di Model ha rivelato un difetto preesistente nel campionamento: una differenza di un ulp può aggiungere un punto praticamente coincidente e spostare gli indici del diagramma. Unificazione dei materiali e campionamento stabile sono il passo distinto successivo, senza sovrascrivere gli attesi.

Design 0.11.0.0, incremento del secondo componente per nuove API. Collegamento ANTHEA, cataloghi sostituibili, traffico D7-f e rilascio coerente ancora aperti. Il ramo legacy resta disponibile in ANTHEA per confronti.

## Campionamento — 0.11.0.1

Corretto il doppio punto all'estremo analitico praticamente coincidente con una stazione della griglia (distanza <= L × 1e-12). Ogni punto legacy rimosso nel confronto deve avere anche tutti i valori fisici equivalenti con la tolleranza originale; fixture SHA immutata. Prova indipendente su due campate uguali e quattro carichi quasi uguali: 41 punti per campata. 39 test PASS, `bridge-native-sampling`. Nessuna modifica delle soglie di confronto. Versione fix, ultimo componente.

Il tentativo di riuso immediato Ecm/densità da Model non è incluso: cambia l'ordine delle operazioni e produce scarti assoluti di circa 1.2e-10 presso valori nulli del diagramma. Anche la variante a carico unitario ha mostrato tali scarti. La proposta di ampliare la tolleranza è stata respinta dalla revisione automatica; soglie conservate e varianti ritirate. Rimane da risolvere il contratto dei materiali senza duplicazioni nel rilascio finale; il port attuale conserva l'aritmetica già catturata.
