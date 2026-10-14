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

## Strutture superiori, 0.18.0.0

Archi e reticolari: carico globale equivalente al massimo momento della campata appoggiata, reazioni da inviluppo esatto delle campate indipendenti. Per la componente verticale di archi/diagonali e cavi si usa l'equivalente al taglio, senza sottostimarlo con quello al momento. Pendini, montanti e stralli: carico locale sul pannello e ripartizione a leva fra i due piani, con corsie addossate al piano esterno; un tandem completo resta concentrato. La combinazione di massimi globali e locali è cautelativa nel modello ideale.

Strallato: reazione limite della metà tributaria a ciascuna antenna con un solo tandem completo per estremo. Sospeso: limiti distinti di H, carico verticale e reazione/sollevamento, non concomitanti, dell'equilibrio ideale. I blocchi usano la reazione MINIMA per il sollevamento e le fondazioni la massima per la compressione. Non sono linee di influenza del ponte a cavi reale: la nota tipizzata e gli avvisi lo dichiarano. Nessun diagramma fittizio da trave per le strutture superiori. Schemi 6/grandi luci e verifiche globali/locali restano fuori dal modello parametrico.

traffic-upper-a: 51 test Design PASS, vecchi 31 modelli/17 ricerche invariati. Nuove otto configurazioni superiori finite, quantità geometriche impalcato invariate, masse/costi diversi per i carichi corretti, estremi e provenienza presenti. Atteso manuale del tandem locale: W=10.5 m, carreggiata 9.5 m, pannello 8 m: 908.428571 kN sul piano esterno, confermato. La forza calcolata dei pendini dell'arco riproduce il bilancio indipendente con permanente e tandem, senza spalmatura sulla luce. Griglia estesa 648 candidati, 566 ammissibili: 203 ms nella prima prova (precedente ricerca di 7 candidati 21 ms freddo / 2 ms caldo); confronto diretto completo registrato nella corsa traffic-upper-b. Tolleranze/attesi precedenti invariati.
