MIGRAZIONE CONTRATTI CURVATURA - 24/10/2026

Perimetro autorizzato
Sono migrati solo i contratti fisici nativi delle cinque curve, dipendenti dal
nuovo solver. I file curvature-legacy.csv, curvature-contract-0.0.17.csv e
 detailing-sections.xml restano intatti.
I 64 casi analitici dell'algoritmo momento-curvatura conservano identicamente
risultati, campioni, errori, testi, culture e header: il selettore rimuove solo
le 5 righe R con prefisso esatto "R\tfixture " e i rispettivi P successivi.
Il default Compare senza selettore resta invariato per gli altri contratti.

Nuovo contratto fisico
I cinque DataRow leggono solo le colonne INPUT 0..14 del CSV storico. Non usano
le colonne dei risultati archiviati. Il controllo verifica a ogni campione:
- N costante e momenti concomitanti nella direzione assegnata;
- ricostruzione del piano epsilon=epsilon0+gx(x-Ox)+gy(y-Oy), unità 1/mm;
- epsilon0 riferita al polo reale, assi solver -X/-Y, N positivo a trazione;
- equilibrio M1=-integrale sigma(y-Oy)dA e M2=integrale sigma(x-Ox)dA;
- max|epsilon_s| sui centri delle barre, snervamento fyd/Es, primo campione
  snervato e raffinamento, ammissibilità delle deformazioni e limite attivo;
- una sola estremità ultima, ramo crescente, assenza di interruzioni;
- N del piano ultimo entro min(tolleranza richiesta curva, tolleranza dominio),
  direzione dei momenti entro il limite angolare pubblico.
Le barre sono puntuali, con area pi*d²/4 e sottrazione del calcestruzzo occupato.
Le proprietà adottate sono assertate contro gli input: E=200000 MPa, fy=fu=450 MPa,
predeformazione nulla, epsilon_su=0.1, fyd=450/gammaS. Cls senza trazione,
fcd=alphaCC*|fck|/gammaC, epsilon_c2=-0.002, epsilon_cu=-0.0035.
La parabola-rettangolo dell'input è rappresentata da 8 tratti lineari prima del
plateau, come il modello dichiarato, non dalla parabola continua ideale.

Due integratori separati
I: clipping esatto del poligono sui tratti affini della legge. Per ogni triangolo,
regola baricentrica a 3 punti esatta fino al grado2: sigma è affine, sigma*x e
sigma*y sono quadratiche. I poligoni sono quelli reali dell'input. C400 è il
poligono a32 lati, non il cerchio ideale. La T è decomposta in2 rettangoli disgiunti.
Q: somma indipendente di sigma(epsilon) sugli stessi nodi/pesi Quad400/Tri79 del
modello discreto. GaussIntegration è usata solo per estrarre i dati numerici:
non si chiamano l'integratore o la legge materiali di Model/Checker, né si
confrontano due wrapper del medesimo motore.

Tolleranze e limite della prova
Per Q ai campioni ordinari si usa il criterio di arresto esistente 1e-5 sulle
scale b*h*fck, b*h²*fck, b²*h*fck. Al limite si usano i limiti pubblici N/direzione
ed il budget dimensionale del dominio; l'effetto della proiezione del momento
sulla direzione richiesta è limitato da |M|*sin(tolleranza_angolare). Il margine
aritmetico è 1e-10 della scala, non ricavato dagli scarti osservati.
Per I il budget è arresto + |I-Q|, con |I-Q| misurato indipendentemente e loggato
per ogni componente con l'indice del massimo. Questa disuguaglianza triangolare
NON dimostra accuratezza o convergenza della mesh: dimostra l'equilibrio del
modello discreto e rende visibile il divario dalla soluzione continua.
Non è stata aggiunta una percentuale di accettazione su Mlim/chiU/My/chiY: non
esiste ancora una motivazione a priori per tale errore di discretizzazione.

Indagine dei due rossi iniziali (nessuna correzione di produzione)
La prima versione di Q prolungava erroneamente il plateau oltre epsilon_cu.
StressStrainTable.GetStress restituisce invece 0 oltre l'ultimo punto.
Tri79 ha anche nodi esterni ai triangoli (coordinate baricentriche negative):
la deformazione di quei nodi può oltrepassare epsilon_cu pur restando ammissibile
in tutti i punti fisici della sezione. Le diagnostiche temporanee hanno mostrato:
case1 R300x500: 9 nodi oltre ecu, min epsilon=-0.003546416; differenza correggibile
con deltaN=+409.920069N, deltaM1=+102651.245482Nmm;
case4 T1200x800: 15 nodi oltre ecu, min epsilon=-0.003597417; deltaN=+5016.736300N,
deltaM1=-1482068.054301Nmm.
Dopo aver escluso il plateau oltre ecu, Q coincide con la reintegrazione core
entro roundoff. Il core coincideva già con i risultati esportati: il sospetto
iniziale sul fallback raw è stato smentito da queste prove.
Il finale corregge quindi la legge indipendente e il clipping, aggiunge 4 prove
analitiche sul supporto finito, e rimuove tutte le chiamate diagnostiche al core.
Restano loggati i nodi oltre ecu: è una proprietà della quadratura che limita
l'accuratezza continua e può introdurre discontinuità numeriche, non un nuovo
campo di validità materiale. Non viene corretta la quadratura in questa task.

Riferimenti sorgente verificati
Model/Materials/StressStrainTable.cs: GetStress, ritorno0 oltre l'ultimo punto.
Model/Numerics/GaussIntegration/TriangleGaussPoints.cs: Tri79, nodi esterni e pesi.
Model/Materials/Concrete/ConcreteMaterialEuropeanCommon.cs: diagramma e fattori.
Checker/GPCChecker.Concrete/SectionSolvers/SectionSolver.cs: integra barre,
calcestruzzo, scale adimensionali, trasporto delle risultanti e arresto.
Checker/GPCChecker.Concrete/Response/MomentCurvatureAnalysis.cs: campionamento,
proiezione, epsilon0, ricerca del primo snervamento e limite nativo.

Derivatore indipendente (Python stdlib)
Installare derive_curvature_oracles_final.py col nome derive_curvature_oracles.py
in GPCChecker.Test.Concrete/ReferenceCalculations/curvature/.
Da quella posizione il default cerca ../../Fixtures; --fixtures lo sovrascrive.
--output seleziona la destinazione; default curvature-independent-oracles.json.
Legge geometrie/materiali/input archiviati e valida le ipotesi. Nessun import DLL.
Risolve N e direzione M più un vincolo attivo (ecu per l'ultimo, fyd/Es per il
primo snervamento) con Newton indipendente3x3, finite differenze e line search;
integra I esattamente, senza la mesh di Checker. L'output conserva residui,
iterazioni, piani, risultati e SHA256 degli input. Non è un nuovo expected storico.
I 10 stati sono stati ricalcolati ed il derivatore termina senza errori.

Oracoli continui indipendenti finali (Nmm; 1/mm)
0 R300x500: Mlim=192034426.86121461; chiU=3.4268801131829574e-05; My=180687773.81253779; chiY=7.325635146009984e-06
1 R300x500: Mlim=69354449.653580308; chiU=7.6392983527127833e-05; My=65672184.755101837; chiY=5.4479849275102199e-06
2 R400x400: Mlim=149606147.55616361; chiU=1.0624195322529862e-05; My=128753709.46089791; chiY=6.9234449183534153e-06
3 C400: Mlim=125224112.87386745; chiU=2.1970980651526205e-05; My=108715129.52778411; chiY=1.1791842884407145e-05
4 T1200x800: Mlim=789978634.40024209; chiU=4.6635973153880663e-05; My=712719993.78629589; chiY=3.6060709649430426e-06

Verifica eseguita
Python 3.12.14 (solo standard library), finale eseguito direttamente in staging,
5 limiti + 5 snervamenti convergenti. Il percorso delle fixture e dell’output sono
parametri CLI; non rimane alcun percorso assoluto di macchina nel derivatore.
Nessun build/test.NET/install/Git eseguito da questo agente. Root ha eseguito i
12 casi iniziali (10 verdi/2 rossi) e le due diagnostiche. Root ha eseguito i 16 casi finali: 16/16 superati.

Evidenza centrale finale (letta dal log e dal TRX)
closure-curvature-physical-green.log/.trx: 16 test eseguiti, 16 superati;
VSTest 17.14.0 x64. I 2 rossi iniziali non erano un difetto del solver: le prove
limit-diagnostic e cutoff-diagnostic hanno isolato il prolungamento della legge
nell'oracolo Q. Nessuna tolleranza è stata allargata; N è stata resa più stringente.

Misure massime |I-Q| (N; Nmm), non limiti di accettazione della mesh:
case0 R300x500: N449.331791 (p41), M1 108915.676317 (p41), M2 116.007431 (p23).
case1 R300x500: N133.535382 (p41), M1 46048.841085 (p41), M2 0.115023 (p40).
case2 R400x400: N20.872580 (p32), M1 3787.316355 (p33), M2 1494.172228 (p39).
case3 C400: N93.451363 (p41), M1 5004.231740 (p41), M2 1287.784464 (p31).
case4 T1200x800: N3749.384656 (p41), M1 1236282.084933 (p41), M2 84.950398 (p40).
In particolare il budget continuo della T è maggiore della tolleranza N della
richiesta: il test NON certifica che il piano soddisfi N entro 1000 N nell'integrale
continuo; certifica il modello discreto richiesto e ne misura questa differenza.
Una successiva qualifica di accuratezza continua richiederà un criterio autonomo
sulla mesh/quadratura, non l'allargamento del criterio di equilibrio del solver.
