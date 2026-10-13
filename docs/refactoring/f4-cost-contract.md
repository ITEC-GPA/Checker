# F4.11b.1 — contratto economico

GPC.Design 0.12.0.0: nuove API per computare quantità indipendenti dai prezzi. Snapshot posseduta di tariffe risolte, revisione e valuta obbligatorie, nessuna dipendenza da cataloghi o documenti. Unità tipizzate; conversioni soltanto kg/t, nessuna interpretazione di unità incompatibili. Importi decimal, nessun arrotondamento intermedio.

Prezzo assente, prezzo zero esplicito, tariffa assente e unità errata sono stati distinti. Una stima incompleta non espone un totale. Le inclusioni si riferiscono alla singola lavorazione padre; armatura di un palo compresa nel suo prezzo non rende gratuita l'armatura del plinto. Cicli, riferimenti inesistenti, duplicazioni e prezzi negativi sono rifiutati. Uno snapshot non può cambiare perché il chiamante modifica l'elenco delle inclusioni.

43 test Design PASS, comprese quattro prove nuove con importi attesi a mano: 2 t × 1.5 €/kg + 3 m³ × 200 €/m³ = 3600 €, maggiorazioni 10% e 20% = 4752 €; secondo listino 4350 €. API compatibili, 1998 firme Design. Nuova funzionalità: secondo componente incrementato. Resta da collegare alle quantità Bridge, gestione dei cataloghi/UI e confronto del ricalcolo.
