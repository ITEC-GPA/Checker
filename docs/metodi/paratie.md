# Paratie: motore per fasi in sviluppo

## Campo del nucleo meccanico

Trave verticale di Eulero-Bernoulli a piccoli spostamenti, con rigidezza EI per
tratti e due letti indipendenti di molle distribuite. Ogni modello rappresenta
la larghezza dichiarata: le forze sui singoli tiranti sono scalate mediante
larghezza/interasse. Unità N, mm, MPa, N/mm³, rad; quote verso l'alto.

Il nucleo non simula ancora deformazioni assiali, trasferimento assiale al
terreno, secondo ordine, plasticità della sezione o interazione con altre paratie.
La forza normale riportata è l'equilibrio dei carichi verticali assegnati,
del peso lineare assegnato e delle componenti dei sostegni. Il peso lineare
deve già rappresentare la situazione di progetto dichiarata; non si aggiunge
automaticamente galleggiamento. Un risultato convergente non è una verifica
completa dell'opera.

## Pressioni

Strati orizzontali, paramento verticale liscio e sovraccarico uniforme. Si riusano
i coefficienti Rankine di `Walls.EarthPressure`. Nel caso drenato:

    pa = max(0, Ka sigma'v - 2 c' sqrt(Ka))
    pp = Kp sigma'v + 2 c' sqrt(Kp)
    p0 = K0 sigma'v

La pressione dell'acqua si aggiunge una sola volta alle pressioni efficaci;
il battente sopra il terreno entra anche nella tensione totale verticale.
Nel caso non drenato in tensioni totali, phi=0 e cu esplicito, i limiti sono
max(0,sigma_v-2cu) e sigma_v+2cu. La pressione iniziale è K0 sigma'v+u e
l'acqua non viene riaggiunta alla legge totale. Non si simula la generazione
o dissipazione delle sovrappressioni neutre.

Legge elastoplastica perfetta, con spostamento entrante v e offset plastico vp:

    p_trial = p0 + kh (v-vp)
    p = clamp(p_trial, pa, pp)
    vp_new = v - (p-p0)/kh    solo quando si supera un limite

Il modulo kh è assegnato con provenienza; non è ricavato automaticamente da E.
Scarico e ricarico usano il medesimo kh. Non si dichiara una legge costitutiva
avanzata dipendente dalla storia geologica o dalla deformazione a piccole ampiezze.

## Fasi e iterazione

Ogni fase è una configurazione completa. Le pressioni iniziali, i limiti,
le rigidezze e i carichi sono interpolati fra le configurazioni mediante
rilascio/applicazione incrementale: non è una simulazione del fronte di scavo
continuamente mobile. La prima fase rappresenta l'installazione ideale nel
terreno e la costruzione dello stato iniziale. Le fasi successive conservano
gli offset plastici dei punti di integrazione ancora attivi. Un riporto nuovo
assume la deformata già raggiunta come riferimento locale.

Un sostegno installato assume lo spostamento della fase precedente come
riferimento, con forza di bloccaggio applicata progressivamente. Un puntone o
un tirante che si scarica conserva il punto di forza nulla e riprende carico
quando lo spostamento vi ritorna; solo il raggiungimento della capacità sposta
il riferimento (allungamento permanente). Sostegni
rimossi perdono progressivamente il contributo e cancellano la propria storia;
una reinstallazione è nuova. Una fase deve separare installazione e scavo quando
il loro ordine fisico influenza il risultato.

Elementi Hermite, quattro punti di Gauss, carichi consistenti. Rotazioni scalate
per lunghezza e matrice a banda riequilibrata prima di Cholesky. Newton con
ricerca lineare di Armijo sull'energia potenziale, riduzione adattiva
dell'incremento, stato di prova distinto da quello confermato. Con molle e
sostegni elasto-plastici l'energia è convessa e derivabile una volta: ogni
direzione di Newton con tangente definita positiva la riduce, mentre il massimo
modulo del residuo non è una misura adatta nei punti angolosi della plasticità.
La differenza di energia si somma termine per termine; la riduzione della norma
2 del residuo copre solo le differenze perse per arrotondamento vicino alla
convergenza. La tolleranza relativa si riferisce al massimo fra carichi,
reazioni del terreno e dei sostegni e forze interne di flessione, perché
l'arrotondamento dei termini EI cresce con EI/h³.
Non si introducono rigidezze fittizie contro i meccanismi.
Fallimento di una fase: rollback completo della fase, restano solo quelle concluse.
Annullamento: eccezione cooperativa, nessun risultato nuovo pubblicato.

Le azioni di sezione sono recuperate dall'equilibrio dei carichi interpolati
ai punti di Gauss. Si conservano i due lati dei nodi e gli zeri del taglio
per localizzare gli estremi del momento. Le pressioni fra punti di Gauss
sono interpolazioni della discretizzazione, non nuove soluzioni locali della legge.

## Norma: prima estensione verificata

NTC 2018, Italia, DM 17/01/2018, §6.5.3.1.2, G.U. 20/02/2018 p.197:
`EmbeddedWallResistance` ha A1+M1+R1 e A2+M2+R1, gammaR=1.
La stabilità globale rimane la verifica esistente A2+M2+R2. UPL/HYD sono
distinte. Il nuovo controllo non usa i coefficienti dei muri a gravità.
Il generatore sismico non è ancora definito per questo controllo e restituisce
nessuna combinazione; non viene usato il profilo dei muri come ripiego.

Fonte primaria: https://www.gazzettaufficiale.it/eli/id/2018/02/20/18A00716/sg
(allegato, capitolo 6). La conformità del modulo completo resta aperta nella
matrice ANTHEA `docs/paratie/requisiti.md`.

## Verifiche indipendenti

In `tests/EmbeddedWalls.Tests`: H L³/(3EI), q L⁴/(8EI), equilibrio V/M,
traslazione uniforme q/(2kh b), supporto aggiunto alla deformata, ciclo plastico
con valori aritmetici, tensioni efficaci con falda e conservazione degli input.
Per il fronte plastico dello scavo si confrontano mesh 500/250/125 mm e si
richiedono correzioni decrescenti e ultima variazione inferiore a 0.02 mm.
Non sono attesi rigenerati dal solver e non costituiscono confronto con altri prodotti.

Revisione del 21/10/2026 (`docs/verifiche/paratie-revisione-2026-10.md`): un
risolutore indipendente a molle concentrate (`tools/attesi/paratie_indipendente.py`,
mesh 50/25 mm e valore di Richardson) scrive `independent-expected.json`; mensola
in sabbia, argilla non drenata e paratia tirantata con falda coincidono entro lo
0,1 %. Geotechnics 0.14.0.1 corregge il sostegno monolatero che seguiva la parete
staccandosi, la ricerca lineare e la scala della tolleranza (`ReviewFixesTests`).

## Collegamento Concrete del primo incremento

`GPC.Design.EmbeddedWalls` costruisce sessioni indipendenti di `WallSectionSession`
per sezioni rettangolari ordinarie NTC 2018 senza staffe. Non modifica la mesh delle
sezioni esistenti. Le azioni sono totali sulla stessa larghezza della fascia:
nessun ridimensionamento implicito. N passa da compressione positiva a negativa;
M cambia segno perché la curvatura positiva della trave tende la faccia a sinistra
(retro), rappresentata dalla faccia Top di Concrete. La faccia Front è Bottom.
Il test con armatura asimmetrica confronta la terna del piede con Concrete diretto.
Fase, caso, quota e lato del nodo restano associati a ogni controllo. Nessuna
verifica viene eseguita su un'analisi non convergente. I minimi dei muri a gravità
non vengono presentati come dettagli completi di diaframmi o pali.

K0 è obbligatorio e positivo. Una tensione iniziale fuori dai limiti attivo/passivo
viene rifiutata esplicitamente, senza correzioni silenziose.

Versioni del primo incremento: Model 5.6.0.0 (contratti e profilo paratie),
Geotechnics 0.5.0.0 (nuovo motore), Design 0.22.0.0 (coordinamento Concrete).
Sono nuove funzionalità: incremento del secondo componente secondo AGENTS.md.
Il set candidato serve ai collaudi; non è un rilascio ufficiale.
