# F4.11 D7-f — traffico mobile

## Servizio indipendente

Design 0.16.0.0: aggiunta funzionale di RoadTraffic e BeamTrafficEnvelope. NTC 2018 §5.1.3.3, Tabelle 5.1.I/II, gamma Q sfavorevole 1.35 da Tabella 5.1.V; residuo 2.5 kN/m² da EN 1991-2 Tabella 4.2. Fonte primaria consultata: https://www.gazzettaufficiale.it/eli/gu/2018/02/20/42/so/8/sg/pdf/ (pagine PDF 157/160). Nessuna riduzione dei tandem o incremento dinamico aggiuntivo.

Tre momenti risolti nei coefficienti della posizione del carico. Linee di influenza cubiche per campata, divise in corrispondenza della sezione (salto del taglio). Integrali analitici sulle zone di segno sfavorevole; radici isolate su tratti monotoni, bisezione fino alla precisione macchina. Estremi del tandem completo di due assi distanti 1.2 m tramite derivata quadratica su tutti i tratti fra discontinuità traslate. Ogni estremo è indipendente: sommare reazioni massime non è un caso fisico. Trave unica prismatica, EI costante; nessuna ripartizione trasversale, torsione o verifica locale.

41 sezioni per campata per i diagrammi, API At per una sezione assegnata. L'inviluppo in ogni sezione è analitico; il massimo fra sezioni ha la risoluzione del campionamento longitudinale. Pendenza positiva di freccia verso il basso, EI kN m², spostamenti in mm.

## Attesi e prove

Fixture estratta dai risultati dello studio Python preesistente, senza ricalcolo né sostituzione: ANTHEA/supporto/artefatti/refactoring/ricerca-f/risultati/effetti.csv, SHA256 c8e85ed520bb80f05531e4bfbef8fa32cfa5b51a2fb451c2b76f555491d48615. Venti valori per luci 20/30 m, una/due/tre campate, appoggiate e continue; fixture traffic-research.json SHA256 7bb22b105c47e700d477b0ff5b50ac620151c61c19657f2073e34e51289d5335. Soglia 0.2% dichiarata prima del test per la griglia Python 0.05 m/carico e 0.10 m/sezione e le 41 sezioni della UI; tutti passati alla prima corsa. Controlli indipendenti esatti su tandem, M_B = -qL²/8, R_B = 1.25qL, freccia 5qL⁴/(384EI), corsie e integrazione del carico uniforme su tutti gli schemi con 1/2/3/5 campate (1e-8/1e-9 assoluti).

Corsa traffic-a: 3 test PASS, involucro completo tre campate/123 sezioni in 3.776 ms a caldo. L'integrazione con BridgeConcept e il confronto delle ricerche sono il passo successivo; nessuna modifica degli archivi o del calcolo Bridge in questo commit.

## Carreggiate diseguali

0.16.0.1: correzione della numerazione globale: la corsia 1 si colloca sulla corsia convenzionale più larga. Scambiare l’ordine delle carreggiate 5.4/8.8 m non cambia il carico. Rilevata in revisione prima dell’integrazione; nessun impatto sui venti attesi precedenti (carreggiate uguali).

## Collegamento alle travate, 0.17.0.0

Standard e travate avanzate (incorporate/ortotrope) usano G più estremi Q separati. Risultati espongono min/max M/V/freccia e reazione minima; la ricerca controlla anche il sollevamento nell'inviluppo minimo. Avviso appoggi dal massimo per dispositivo/appoggio, senza somma non fisica dei massimi. Dati di traffico e coefficienti adottati nel risultato. Cache limitata a 128 geometrie indipendente da EI, con risultati immutabili; costi e geometrie differenti riusano le stesse linee di influenza. Vecchi archivi/API restano LegacyUniform, ancora nessun cambio ai documenti.

traffic-integration-a: 50 test Design PASS, corpus precedente invariato. Sette candidati: legacy 1.36 ms, Schema 1 20.09 ms a freddo / 2.71 ms a caldo. Campo nuovo per le strutture superiori temporaneamente rifiutato (integrazione successiva), mai reinterpretato come uniforme. Versione funzionale 0.17.0.0.
