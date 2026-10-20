# Instabilità non drenata del fondo: primo verificatore locale

Sviluppo sul ramo `codex/paratie-seguito`, dopo il merge dei moduli paratie e
spettri. Il kernel è distinto da UPL/HYD e dalla stabilità globale già presente.
Non è ancora integrato nell'editor, nella persistenza e nei report ANTHEA:
PAR-16 resta aperto, compreso il tampone di fondo.

## Modello e campo

Scavo lungo, piano, in argilla omogenea in condizioni non drenate nell'intero
volume del meccanismo. La conferma di queste ipotesi, delle azioni caratteristiche
e delle fonti è obbligatoria. Non si media automaticamente la resistenza fra
strati. Non vengono aggiunti contributi resistenti di tiranti, puntoni, paratia
o tampone. La verifica non descrive cedimenti, spostamenti o interazione fra
pareti e non sostituisce la verifica globale.

Riferimento meccanico: FHWA-IF-99-015, *Ground Anchors and Anchored Systems*,
1999, §5.8.2, equazioni 34–37, pagine stampate 105–107. Larghezza del blocco
`B' = min(B/sqrt(2), D)`, omettendo D se non è presente uno strato rigido.
Il rapporto con sqrt(2) è verificato nella geometria della figura e
nell'equazione 35; l'estrazione testuale del PDF perde il simbolo di divisione.
Nc è un input esplicito con fonte: il codice non interpola il diagramma di Janbu.

Il bilancio delle pressioni usa:

`p_lorda = gamma*H + q_G2 + psi*q_Q`

`p_taglio = cu*H/B'`, oppure zero se il contributo del piano esterno è trascurato.

`p_netta = p_lorda - p_taglio`, `p_limite = Nc*cu`.

L'aggiunta dei sovraccarichi uniformi deriva dall'equilibrio verticale del
blocco, non viene attribuita come algoritmo proprietario o come formula
testuale del manuale. Sono tensioni totali: non si sottrae la pressione
interstiziale da gamma*H. UPL e HYD richiedono verifiche separate.

Fonte primaria consultata e pagine controllate visivamente:
https://www.fhwa.dot.gov/engineering/geotech/pubs/if99015.pdf
PDF SHA-256 `2501e171a90a1d169d1f3eba581551ebbe5279775a915bb9fd05fa3b95a1a5f2`.

## Coefficienti e unità

Italia, NTC 2018, DM 17/01/2018, §6.5.3.1.2 e tabelle 6.2.I–II: entrambe le
combinazioni A1+M1+R1 e A2+M2+R1, con R1 unitario. La nuova identità
`GeotechnicalCheck.UndrainedBasalHeave` distingue questo stato limite dagli altri.
Il terreno è G1 sfavorevole; i sovraccarichi sono G2 e Q sfavorevoli. Gli
input Q precedono psi, applicato una sola volta. cu viene divisa per gammaM
sia nella capacità sia nel taglio esterno. Non si usano i coefficienti dei
muri a gravità. Profili non implementati o situazioni sismiche danno
`NotEvaluated`; un confronto caratteristico conserva lo stesso esito anche
quando l'utilizzo numerico è inferiore a uno.

Fonte normativa primaria verificata, pagina stampata 197:
https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110007&dgu=2018-02-20

Unità GPC: mm, MPa e N/mm³. Le pressioni non sono forze nodali e non vengono
moltiplicate per l'interasse. Il risultato conserva caso, fase, combinazione,
fonti, coefficienti e tutte le componenti del bilancio. Se p_netta è negativa,
è mostrata come tale, con domanda e utilizzo zero e una diagnostica specifica;
non si produce un fattore di sicurezza infinito. Overflow ed input non finiti
non possono restituire una verifica soddisfatta.

## Evidenze iniziali e seguito

Attesi indipendenti congelati prima del motore nel commit `a85600ed`: quattro
bilanci con Decimal a 50 cifre, in m e kPa, convertiti con `SoilUnits` al confine
del test. Tolleranza 1e-12 MPa assoluta e 1e-11 sull'utilizzo: sola aritmetica,
senza discretizzazione. Sette prove del kernel passate, inclusi dominio,
isolamento dei risultati, scala geometrica e coefficienti applicati una volta;
una prova Model dei profili passata. Non equivalgono al collaudo integrato del
modulo e non sostituiscono le suite complete del treno di rilascio.

Successivo collaudo completo del 19/10/2026: Model `8c1e79a9`, 19 Core e
1249 UnitTest PASS, due test ignorati preesistenti; audit di uguaglianza e API
conclusi senza rimozioni inattese. Checker `56f48103`, 1912 test PASS e i soli
17 fallimenti Steel già ammessi, compatibilità API confermata. EmbeddedWalls
comprende 74 test, di cui sette nuovi. Evidenze nel worktree ANTHEA,
`supporto/artefatti/paratie/incremento-14/{model-full-01,checker-full-01}`.
Non è stato prodotto o installato un set di rilascio di questo incremento.

Da completare: adattatore di fase con geometria/terreni congruenti, selezione
e provenienza del modello, migrazione del documento, editor, risultati,
report/CSV, ricerca dell'infissione e guide globali. La valutazione di Nc resta
esplicita; stratigrafie non omogenee, metodi più generali e tampone rimangono
requisiti aperti.
