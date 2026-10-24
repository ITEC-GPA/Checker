MIGRAZIONE APPROVATA DEI CONTRATTI DI DOMINIO — 24/10/2026

L'utente ha approvato la migrazione dei cinque contratti storici, conservando le
fixture. Questo documento riguarda i due contratti Concrete di dominio e S1.
Nessun JSON/CSV/XML storico, allowlist o valore atteso storico è stato modificato.

Mapping dei test:
DomainPointContractOf0017IsUnchanged
 -> HistoricalStressBlockAndContinuousPhysicalContractsArePreserved
 -> ContinuousDomainResistancesMatchIndependentStripOracles (30 riferimenti)
LargeSectionPointBeyondTheStoppingTestIsAccepted
 -> LargeSectionFallbackMaintainsScaleAndExposesItsPlaneLimit

CONTRATTO STORICO E COPERTURA
Il JSON completo è preservato e verificato mediante SHA256 UTF-8/newline LF:
1f61a7891eddc10b01f5694062a589ec74780a15241e6c947d3a334f5c047491.
La cattura esistente continua a produrre tutti i 1024 punti effettivi nel file
*.actual per diagnosi; non sostituisce la fixture.
I 256 StressBlock rimangono confrontati in modo esatto, campo per campo/testo,
togliendo soltanto la virgola JSON esterna di separazione.
AGGIORNAMENTO 24/10/2026, decisione dell'utente "2A": il confronto esatto dei
256 StressBlock con 0.0.17 e la cattura *.actual sono ritirati. Dopo aa262e21
(integrazione dello stress block corretta) cambiano 254/256 punti StressBlock,
con qualunque metodo della parabola. Restano l'hash del JSON storico, il
confronto esatto con la fixture attuale (DomainPointContractWithAnalyticDefault-
IsUnchanged, StressBlock compreso) e le verifiche fisiche dei diagrammi continui.

Tutti i 768 input continui sono ancora eseguiti:
- 720 devono produrre Completed, senza deroga generica null/NotConverged.
  Si verificano campo, verso, vincoli di ricerca sul piano reintegrato, limiti
  deformativi dei vertici del calcestruzzo e delle fibre esterne delle barre.
  Gli scarti tra forze esportate e reintegrate devono rispettare le tolleranze
  pubbliche del solver, senza nuovi limiti ingegneristici più larghi.
- 24 R1 a -5 MN: limite indipendente assoluto Ac,net*fcd+As*fyd; devono essere
  AxialForceOutsideDomain, con utilizzo > 1 e capacità entro quel limite.
- 18 R3 a +180 kN, Mx-/My±: unico strato di acciaio a y=-200 mm. Da equilibrio,
  C <= As*fyd-N e yc >= -250 mm danno
  Mx >= 200N-50(As*fyd-N) = 26.560217 kNm > 0.
  È un'esclusione analitica delle domande Mx<=0; sono obbligatori il rifiuto
  esplicito e l'assenza di capacità esportate.
- 6 R3 a -2 MN, Mx+/My±: Bilinear/Elastic e NonLinear/Plastic.
  Sono RAMI NON QUALIFICATI con evidenza numerica indipendente, non un teorema
  di impossibilità fisica. Devono essere respinti esplicitamente senza capacità.
  La scansione indipendente a 1° su 0..180°, con simmetria nell'altro semicerchio,
  dà max Mx=-15.6197 kNm e -4.16428 kNm rispettivamente. Il raffinamento locale
  a 0.01° è documentato nel JSON; non costituisce un bound angolare globale.

CORREZIONE DELL'ASSERT SUL CAMPO
Il primo controllo del nuovo catalogo confondeva GetFailureZonesDiscretization
con l'insieme di tutti i campi fisici ammessi. La lista di campionamento Elastic
omette F3B, mentre la bisezione percorre legittimamente anche quel campo.
R3/ParabolaRectangle/Elastic/-500/Mx- è F3B sia nella fixture storica sia nel
risultato attuale: non è stato cambiato il campo di produzione o l'atteso.
Il controllo corretto valida l'enum e ricostruisce il piano dalla parametrizzazione
del campo: curvatura affine per F1/F2A/F2B/F3B/F4, profondità compressa (1/chi)
affine per F3A. Il parametro è proiettato sul campo chiuso [0,1]; il piano deve
coincidere su tutti i vertici e i centri delle barre entro 1e-12 di deformazione,
la stessa tolleranza aritmetica assoluta già usata per i limiti deformativi.
Questo è un controllo di coerenza della classificazione, non un oracolo numerico
indipendente. L'indipendenza è fornita dalle prove statiche e dagli integrali
separati descritti di seguito.

ORACOLI INDIPENDENTI E RIPRODUCIBILITÀ
Directory distribuita:
GPCChecker.Test.Concrete/ReferenceCalculations/domain-migration/
File: derive_domain_migration_oracles.py e domain_migration_oracles.json.
Il derivatore richiede Python >= 3.10 e soltanto la libreria standard.

Per rigenerare senza sovrascrivere i riferimenti nel repository, copiare lo script
in una cartella temporanea vuota ed eseguire da quella cartella:
  python -X utf8 derive_domain_migration_oracles.py
Lo script scrive accanto a sé domain_migration_oracles.json e
DomainPointIndependentData.cs. Confrontare quest'ultimo byte per byte con
GPCChecker.Test.Concrete/DomainPointIndependentData.cs: UTF-8 senza BOM, CRLF.
Gli utensili prepare_domain_patch.py e prepare_s1_patch.py sono solo strumenti
locali di preparazione e non fanno parte del pacchetto di riferimento.

Il derivatore ricostruisce i vincoli affini di deformazione in (epsilon0,curvatura),
senza campi F1-F4, ricerche Newton/bisezione del core o risultati Checker. Integra
esattamente i poligoni tagliati dalle soglie delle spezzate costitutive mediante
momenti geometrici; somma barre puntuali elastiche/plastiche, sottraendo il
calcestruzzo sostituito. Le tabelle derivano dalle leggi dichiarate di input:
parabola in 10 punti, bilineare, nonlineare con 14 voci nel core e 13 nodi distinti
nell'oracolo (l'ultimo nodo del core è duplicato). fck<=40 MPa, alphaCC=0.85,
gammaC=1.5, gammaS=1.15. Si confrontano le stesse spezzate costitutive, senza
confonderle con la parabola continua esatta.

Limiti acciaio: trazione plastica 0.075*0.9; compressione caratteristica 0.075
nel core. L'oracolo usa 0.0675 anche come limite conservativo di compressione;
questo limite non è attivo in alcuno dei 30 casi o dei due inviluppi R3, nei quali
la compressione resta inferiore a 0.0035.

I 30 punti N=0, Mx+ coprono R1, R2, R3, C1 (72 lati), C2 (144 lati), ciascuno
per 3 diagrammi e 2 stati. DomainPointIndependentData.cs deriva esclusivamente
da questi integrali. La tolleranza del momento, 2 per mille, è stata fissata prima
di eseguire i nuovi test .NET: è un requisito di accuratezza per quadratura Gauss
fissa contro integrazione esatta a strisce, non un bound matematico della
quadratura. Non è stata tarata sui risultati di produzione.
Il confronto a posteriori con le catture già disponibili ha rilevato uno scarto
massimo di 0.021505% (R3/Bilinear/Plastic), entro il requisito prefissato 0.2%.
L'oracolo è analitico/numerico indipendente, non una prova sperimentale o una
nuova prescrizione normativa.

S1: LIMITE APERTO E CONTRATTO FUTURO
Geometrie, azioni, scalatura 1:4 e tolleranze storiche restano invariate. Non si
impongono più zero fallback o un errore N maggiore della tolleranza di ricerca.
Un risultato typed Completed deve essere verificato reintegrando il SUO piano,
anche se in futuro sarà diverso dal primo candidato raw: vincoli di ricerca,
equilibrio N, confronto delle risultanti e limiti deformativi dei materiali.
NotConverged è ammesso soltanto con candidato raw presente e forze convergenti,
ma piano reintegrato respinto esclusivamente per l'errore angolare oltre limite,
con direzione presente e stesso semipiano. Diagnostica coerente e assenza di
capacità esportate sono obbligatorie; non è una deroga generica alle regressioni.
Resta verificata la scalatura geometrica N~L² e M~L³ del candidato raw.
Il difetto fisico S1 resta aperto: la guardia impedisce una falsa qualifica, non
ha risolto la discontinuità del fallback.

RESPONSABILITÀ E VERIFICA
Preparazione dei test, derivazione indipendente e diagnosi: agente phase4a_tstub.
Review indipendente: agente phase3a_tension_gusset.
Installazione, build e test .NET centralizzati: agente root.
Root ha verificato con esito positivo il catalogo completo dopo la correzione
sul campo e i 30 oracoli indipendenti. La precedente versione S1 con i controlli
direzione/semipiano ha superato 6/6 test insieme alle regressioni della guardia.
La verifica finale della variante typed-first e dell'intera suite è registrata
nel rapporto di validazione centrale del rilascio.
