# Verifiche locali UPL e HYD

19 ottobre 2026. Implementazione in `src/Geotechnics/Seepage/HydraulicSafety.cs`;
contratti e profilo normativo in Model. Non comprende instabilità non drenata
del fondo, erosione interna o stabilità globale del complesso terreno–opera.

## Profilo e unità

Profilo nazionale italiano NTC 2018, §6.2.4.2 e Tab.6.2.III, verificato sulla
[Gazzetta Ufficiale, capitolo 6, pagina 187](https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110007&dgu=2018-02-20).
È il testo nazionale, senza sovrapporre un annesso Eurocodice. Situazione
persistente/transitoria; altri profili o situazioni restituiscono
`NotEvaluated`, non i coefficienti dei muri a gravità.

Forze N, lunghezze mm, pressioni MPa, pesi per volume N/mm³. Quote positive in
alto. I coefficienti sono forniti da `StandardGeotechnical.HydraulicProfile`;
le formule e l'interpretazione fisica sono in Geotechnics. I fattori idraulici
non modificano gli insiemi di azioni serializzati dei profili esistenti.

## Sollevamento UPL

Input: risultanti caratteristiche firmate (positive verso l'alto), categoria
G1/G2/Q, identificativo e fonte. Pesi totali, senza sottrarre preventivamente la
spinta d'acqua se questa è già inclusa fra le azioni. Le pressioni correlate
sulle due facce dello stesso corpo devono formare una risultante netta unica.
Le combinazioni delle azioni variabili e la selezione dei livelli piezometrici
sfavorevoli spettano al coordinamento delle analisi; questa API verifica una
combinazione caratteristica esplicitamente assegnata.

Il controllo confronta ΣγF·F↑ con ΣγF·|F↓| + Rd. I coefficienti sfavorevoli / 
favorevoli sono 1,1/0,9 per G1, 1,5/0,8 per G2 e 1,5/0 per Q.
Rd è una resistenza **già di progetto**, ammessa soltanto con una fonte che
documenti metodo e trattamento M2, oltre agli eventuali fattori di pali o
ancoraggi. Non viene ridotta una seconda volta. Sono restituiti anche i singoli
contributi caratteristici, i fattori e le azioni di progetto.

La capacità nulla con domanda positiva produce `NotSatisfied`, utilizzo non
disponibile e diagnostica esplicita, senza infinito serializzato.

## Sifonamento HYD

I criteri implementati richiedono terreno saturo e flusso prevalentemente
verticale, con motivazione assegnata. Il campo bidimensionale non dimostra da
solo tale applicabilità.

Con efflusso libero: ic=(γsat−γw)/γw e i↑≤ic/γR. γR vale 3 per il gradiente
medio e 2 per il gradiente di efflusso. Il peso saturo deve superare quello
dell'acqua; la resistenza coesiva non viene introdotta in questa formula.

Con frontiera caricata: max(0,u−u_idrostatica)≤σ′v,assenza_filtrazione/2.
Pressioni e tensione efficace iniziale sono riferite allo stesso punto e alla
stessa combinazione; quest'ultima comprende il carico applicato alla frontiera.

Fuori da queste condizioni l'esito è `NotEvaluated`: occorre un metodo diverso
con margine di sicurezza giustificato. Flusso discendente dà domanda ascendente
nulla, non una verifica di tutti gli altri possibili fenomeni idraulici.

## Campionamento FEM e pesi meccanici

`SeepageSampling` conserva le intersezioni con gli elementi, controlla anche
l'interno degli intervalli e non estrapola attraverso vuoti. Il gradiente medio
verticale usa la differenza di carico alle estremità. Nel punto di efflusso
sono restituiti i gradienti degli elementi incidenti con il massimo ascendente;
nessuna media nasconde il salto attraverso un'interfaccia. La classificazione
della frontiera e la scelta del terreno rappresentativo restano esplicite.

L'integrale di pressione lungo una faccia orizzontale usa i tratti P1 esatti e
la larghezza rappresentata. La pressione superiore assegnata è uniforme.
Se richiesto, la suzione viene esclusa inserendo analiticamente lo zero di ogni
tratto; per default il segno resta conservato.

`EmbeddedWallGround.SaturationFromPorePressures` abilita un modello binario del
peso del terreno: γsat dove u>0, γnaturale nei tratti con u=0. Richiede il
profilo completo dalla superficie e tutti gli attraversamenti di pressione
nulla, conservati da `SeepageWallCoupling`. Non descrive saturazione capillare
o variazione continua del peso con il contenuto d'acqua. Il comportamento
precedente con quota di falda esplicita resta disponibile con flag falso.

## Collaudo indipendente

`tests/EmbeddedWalls.Tests/HydraulicSafetyTests.cs`, corsa `hyd-safety-02`:
7 prove nuove, 30 PASS complessivi con le regressioni meccaniche/idrauliche.

- UPL: domanda 18000 N, capacità 23400 N da somma manuale; Rd non rifattorizzata,
  variabile favorevole esclusa e capacità nulla riconosciuta.
- γsat=2γw: ic=1. Gradiente 0,4 dà utilizzo 1,2 (medio) e 0,8 (efflusso).
- u=0,03 MPa, u0=0,01 MPa, σ′0=0,03 MPa: utilizzo 4/3 sotto frontiera caricata.
- Campo affine h=2000−0,4x−0,5z: gradiente ascendente 0,5 e risultante netta
  25506 N su faccia lunga 1000 mm, larga 2000 mm, pressione superiore γw·500 mm.
- Pressione con cambio di segno: integrale firmato nullo, parte positiva
  triangolare 2452,5 N su larghezza 1000 mm.
- Colonna 5 m con 1 m a γ=18 e 4 m a γsat=20 kN/m³: σv=98 kPa,
  u=39,24 kPa, σ′v=58,76 kPa; flag conservato nello snapshot del solutore.
- Annullamento richiesto nell'avanzamento: nessun campo pubblicato anche se
  l'iterazione iniziale era già in equilibrio.

I due test normativi Model dedicati passano in `hyd-safety-model-01`. Gli attesi
derivano dalle formule esposte; nessuna baseline è stata sovrascritta.
