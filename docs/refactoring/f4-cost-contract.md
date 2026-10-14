# F4.11b.1 — contratto economico

GPC.Design 0.12.0.0: nuove API per computare quantità indipendenti dai prezzi. Snapshot posseduta di tariffe risolte, revisione e valuta obbligatorie, nessuna dipendenza da cataloghi o documenti. Unità tipizzate; conversioni soltanto kg/t, nessuna interpretazione di unità incompatibili. Importi decimal, nessun arrotondamento intermedio.

Prezzo assente, prezzo zero esplicito, tariffa assente e unità errata sono stati distinti. Una stima incompleta non espone un totale. Le inclusioni si riferiscono alla singola lavorazione padre; armatura di un palo compresa nel suo prezzo non rende gratuita l'armatura del plinto. Cicli, riferimenti inesistenti, duplicazioni e prezzi negativi sono rifiutati. Uno snapshot non può cambiare perché il chiamante modifica l'elenco delle inclusioni.

43 test Design PASS, comprese quattro prove nuove con importi attesi a mano: 2 t × 1.5 €/kg + 3 m³ × 200 €/m³ = 3600 €, maggiorazioni 10% e 20% = 4752 €; secondo listino 4350 €. API compatibili, 1998 firme Design. Nuova funzionalità: secondo componente incrementato. Resta da collegare alle quantità Bridge, gestione dei cataloghi/UI e confronto del ricalcolo.

## F4.11b.2 — quantità Bridge e rivalutazione

Design 0.13.0.0 espone quantità misurate con codice lavorazione e unità, separate dal risultato storico. Il codice nasce nello stesso punto in cui viene prodotta la quantità, senza ricostruzione dalle etichette italiane. L'armatura delle fondazioni è separata in plinti/ancoraggi e pali, con relazione di inclusione soltanto al proprio prezzo del palo. Le quantità sono possedute e immutabili. Reprice non modifica né ricalcola la struttura; risultati storici privi delle quantità tipizzate richiedono un nuovo calcolo invece di produrre un costo nullo.

44 test Design PASS: 31 casi di tutte le famiglie conservano costi e quantità, prezzi raddoppiati danno costo doppio, inclusioni detraggono esattamente la sola armatura dei pali. Conservati gli attesi originali, tolleranza 1e-10; la traccia strutturale e CO₂ resta protetta dal corpus iniziale. Le nuove quantità sono escluse dalla serializzazione storica del risultato; il nuovo documento di prezzi ne conserva lo snapshot esplicito.

## F4.11b.3 — prezzi nell'ottimizzazione

Design 0.14.0.0: BridgeModel può ricevere uno snapshot risolto; tutti i candidati e le copie ne conservano la stessa revisione. I risultati con listino esplicito espongono quantità e costi delle quote distinte, inclusioni evidenti e totale rivalutato. Calcolo e ricerca rifiutano una stima incompleta, senza attribuire prezzi zero alle voci mancanti. Valuta EUR esplicita: nessun cambio implicito. Null conserva esattamente il percorso degli archivi legacy.

45 test PASS, comprese graduatoria e tentativi con prezzi raddoppiati, riproduzione delle alternative, revisione comune, quadratura del computo e rifiuto dei listini incompleti. Il corpus iniziale è invariato. Versione con secondo componente incrementato per il nuovo ingresso del motore.

Fix 0.15.0.1: Bridge rifiuta inclusioni non coperte dalla propria scomposizione invece di ignorarle. Al momento rebar in pile_1/pile_15 è la relazione implementata; le altre richiedono quantità figlie esplicite. Test su montaggio incluso nella carpenteria: rifiuto, nessuno sconto implicito. Contratto generale CostEstimator invariato.
