# Azioni sismiche: compatibilità CSLP e isolamento

GPC.Design 0.22.0.0 aggiunge `GPC.Design.Actions`: nuova funzionalità, incremento del secondo componente secondo AGENTS.md. Non cambia i motori esistenti.

## Provenienza e riproducibilità

- Riferimento: CSLP, Spettri-NTC 1.0.3, foglio NTC 2008, https://cslp.mit.gov.it/pareri/spettri-ntc-ver-103.
- Reticolo: 10751 nodi, nove tempi di ritorno, 99 ordinate tabulate per nodo; 8100 comuni dell'edizione originale.
- SHA-256 dei file originali e della copia di analisi: `tests/Design.Checks/Fixtures/spettri-sources.json`.
- Gli originali rimangono negli artefatti ANTHEA; le risorse incorporate contengono solo dati numerici e nomi geografici. Nessuna macro, immagine o UI Excel viene distribuita.
- `tools/spettri/extract_reference.py` produce le risorse e la parte compilata di `Cslp103Grid`. Identificativi delle celle e formule originali sono conservati per audit; nessuna formula letta da un documento viene eseguita a runtime.
- Le ricerche MIN/MAX/MATCH sulle distanze sono sostituite da una scansione del reticolo con lo stesso criterio sui pari merito. Le operazioni geometriche locali conservano le formule del foglio, incluse le due interpolazioni.

## Contratto numerico

Accelerazioni in g, periodi in s, longitudine/latitudine in gradi decimali arrotondati a cinque cifre come Excel. Le ordinate di ag nel reticolo originale sono divise per 10. La categoria speciale di sottosuolo S1/S2 non è ricondotta ad A–E.

La modalità compatibile conserva VR minimo 35 anni, soglia 30.11 nel confronto inferiore di SLV, limite 2474.9 per SLC, interpolazione temporale logaritmica, 45 campioni per spettro, soglia 0.2 ag negli SLU e convenzione agv del file. Non è una dichiarazione di aggiornamento del programma CSLP alle NTC 2018.

I parametri assegnati per i quattro stati limite sono un ingresso esplicito opzionale: non sostituiscono i dati della fase 1, e la provenienza va esposta dall'applicazione.

## Isolamento

NTC Italia 2018, §7.10.5.3.2: per T ≥ 0.8 Tis si adotta lo smorzamento dell'isolamento. Sistema equivalente traslazionale, massa in kN·s²/m, rigidezze in kN/mm; conversione 1000 nel periodo, g=9.81 m/s² nelle forze. La rigidezza effettiva SLE/SLU interviene anche nello spostamento.

L'esempio utente ha due etichette dei periodi invertite e usa VLOOKUP approssimato sulla tabella degli spettri. `ExampleSamples` mantiene i 45 campioni dell'esempio solo per confronto, mentre il risultato ingegneristico valuta Sa analiticamente a Tis. La soglia non ha il vuoto di 0.0001 s della tabella. Una ricerca oltre 4 s è dichiarata indisponibile, non estrapolata come un campione costante. La verifica di dispositivi, torsione, eccentricità, regolarità, effetti verticali e applicabilità dell'analisi strutturale resta esterna a questa API.

Il pendolo usa N, μ, d e R assegnati; g=9806 mm/s² come nell'esempio, esplicito e modificabile nell'API. Non si deduce il raggio da un catalogo commerciale.

## Verifiche

`SpectraTests` nella suite `Design.Checks` usa attesi acquisiti da Excel con macro disabilitate e congelati separatamente dal motore. 20 casi, entrambe le interpolazioni, quattro stati limite, sottosuoli A–E, topografie T1–T4, diversi smorzamenti/fattori q e tempi di riferimento; 17796 confronti, massimo scarto assoluto osservato 1.3322676295501878e-14. Tolleranza dichiarata 1e-10 × max(1, |atteso|), per differenze floating point fra Excel e .NET, molto inferiore alla precisione mostrata.

Il secondo atteso conserva i valori e gli spettri dell'Excel utente: confronto delle ordinate e del pendolo, formula chiusa indipendente per Sa(Tis), equilibrio delle forze e controllo dimensionale dello spostamento. Ulteriori prove coprono input non validi, limiti analitici e calcolo concorrente. Le fixture non si rigenerano per inseguire risultati del codice.
