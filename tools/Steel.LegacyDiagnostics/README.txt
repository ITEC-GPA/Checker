DIAGNOSTICA DEI CONFRONTI EC3 LEGACY
20 ottobre 2026 - passo 1

Aggiornamento: il censimento sotto e' storico (commit 3c646d65).
CONFIGURATION_STUDY.txt/.json documentano l'indagine successiva sui quattro
confronti di configurazione: tre SAP riallineati sotto ipotesi ColdFormed
non confermata, HInteraction2 ancora aperto.
RHS_CLASSIFICATION_STUDY.txt documenta la correzione successiva dei due test RHS,
con attesi ricalcolati indipendentemente per gli input a spigoli vivi.
La suite legacy corrente ha 111 successi e 11 fallimenti.
H_LTB_STUDY.txt documenta i due confronti IPE300: materiale/metodo della
fonte ancora da confermare e fix della coerenza del report LTB (Steel 2.4.0.2).
I due legacy rimangono aperti; input, attesi e tolleranze sono invariati.
L'indagine successiva su SectionHBuckling1 e i benchmark SCI/CIC sono in
../../tests/Steel.Members.Tests/BUCKLING_BENCHMARKS.txt.
La ricostruzione indipendente conferma il calcolo S355 del checker; S275
spiega numericamente gli attesi legacy, ma manca la pagina originale.
Il fix delle curve I/H (Steel 2.4.0.1) non chiude alcuno degli 11 fallimenti.
Usare -SourceRevision 55ac7438 per riprodurre gli input precedenti al
riallineamento, senza modificare il checkout. -Scenario consente Baseline,
HollowColdFormed, HollowWelded, HollowAlpha049Only e InteractionGamma105.
Solo Baseline conserva gli input; gli altri scenari applicano la variazione
dichiarata prima della verifica, mantenendo invariate asserzioni e tolleranze.

Scopo
Riprodurre gli input dei 29 metodi EN1993p11Test e registrare anche le
asserzioni successive al primo errore. Non e' una suite di qualificazione:
la suite originale net472 rimane il controllo di regressione.
Run.ps1 strumenta soltanto una copia in obj. Il file originale, le sue
asserzioni, gli input e gli attesi non vengono modificati.
Un'eccezione interrompe il singolo caso, non il censimento degli altri.

Uso dalla radice del repository (PowerShell, .NET SDK 8):
  ./tools/Steel.LegacyDiagnostics/Run.ps1 -OutputPath <percorso-assoluto-json>
Per una coppia di worktree aggiungere -ModelRoot <percorso-Model>.
Le dipendenze Geometry/Utilities mantengono i percorsi fratelli del progetto.
Il JSON riporta hash sorgente/DLL, opzioni, materiale, geometria, risultati,
errori intercettati dal checker, asserzioni ed eventuale eccezione.
Le grandezze usano N, mm, Nmm, MPa; N positivo indica trazione.

Esecuzione di riferimento
Checker 3c646d65 / Steel 2.3.0.1; Model 7da2b9b7 / 5.15.0.2.
Sorgente EN1993p11Test.cs SHA256:
D0ACEA606A2B841FD04F02061024692DBFD71D683240A83E51CFDAD9731994FD
29 casi, 140 asserzioni raggiunte, 16 casi con discrepanze.
Gli stessi 16 nomi falliscono nella suite originale net472:
106 superati / 16 falliti, compresi i test delle altre classi.
La corrispondenza dei nomi non sostituisce la verifica numerica delle formule.

CENSIMENTO: EVIDENZE E QUESTIONI ANCORA APERTE
I dati seguenti descrivono gli scostamenti; non giustificano un cambiamento
automatico degli attesi. I riferimenti Cordova non sono stati consultati:
pagina e valori sono quelli dichiarati dai commenti dei test.

1. Sap_SectionRHSTest1
   Le capacita' confrontate passano. Phi1=0.4940173 contro 0.480.
   Il test costruisce Rolled/HotFinished (default di SteelSection);
   il report SAP incorporato indica Rolled: No e curva c.
   Il codice seleziona curva a per il modello hot-finished: input non allineati.
2. Sap_SectionRHSTest2
   Stessa geometria e tipo del caso 1; falliscono Phi1 e Phi2:
   0.4940173/0.5064541 contro 0.480/0.500. Ricostruire il tipo produttivo.
3. Sap_SectionCHSTest1
   Capacita' e altri confronti passano; Phi=0.4934643 contro 0.479.
   Modello Rolled/HotFinished. Verificare curva/produzione del confronto SAP
   prima di cambiare la selezione normativa.
4. SectionRHSClassification
   Una sola stazione creata da un array di sei azioni.
   Vengono interrogati indici 0..4; l'indice 4 deve essere sia classe 4 sia 1.
   Classe uniforme calcolata 4; il primo atteso e' 1. Il replay poi rileva
   IndexOutOfRangeException. Test internamente contraddittorio.
5. SectionRHSClasstification2
   Una sola stazione creata da un array di cinque azioni; asserzioni 0..4.
   Classe uniforme calcolata 3, primo atteso 1, poi indice fuori intervallo.
   Va esplicitato se si intende classificazione alla resistenza o una
   particolare deroga basata sull'utilizzazione; il test non la dichiara.
6. Sap_SectionHAsymmetricTest1
   Mb=414.5085 kNm contro 484.759; discrepanze anche in chi e kij.
   Nessun errore esplicito del checker. Da ricostruire classificazione,
   proprieta' mono-simmetriche, Mcr e curve; il confronto non e' qualificato.
7. Sap_SectionHAsymmetricTest2
   Errore esplicito: interazione plastica MN,Rd non implementata per questa
   geometria asimmetrica. Capacita' non pubblicate (NaN). Funzione mancante,
   non discrepanza risolvibile cambiando una tolleranza.
8. Sap_SectionLTest
   Errore esplicito: McrLT non calcolabile nel percorso senza simmetria.
   Le capacita' rimangono NaN. Serve un percorso appropriato per gli angolari.
9. Sap_SectionTTest
   Mc1=519.9032 e Mc2=124.960 kNm contro 923.000/227.300.
   Classe calcolata 3; il report SAP usa capacita' plastiche. La classificazione
   attuale degli sbalzi non considera adeguatamente il gradiente tensionale.
   Restano separatamente da verificare Mcr, chiLT e interazioni.
10. Sap_SectionTTest2
    Capacita' di sezione confrontate passano. ChiLT=0.6500128 circa contro
    0.511; scostamenti PhiLT/kij. Proseguire su Mcr e mono-simmetria.
11. SectionHBuckling1
    Nb1=1638.5145 e Nb2=1859.2232 kN contro 1522.500/1667.400.
    L=6500 mm, fattori efficaci 2/1: lunghezze 13000/6500 mm.
    Verificare proprieta' del profilo raccordato, curve e dati Cordova p.149.
12. SectionHLateralTorsionalBuckling1
    Mc1=223.0697 contro 172.830 kNm; Mb=93.7322 contro 78.435.
    Geometria IPE300, S355, carico uniforme applicato in sommita'.
    Lo scostamento esiste gia' nella resistenza di sezione: verificare prima
    materiale/proprieta' del confronto, poi Mcr (Cordova p.190).
13. SectionHLateralTorsionalBuckling2
    Mb=124.3745 contro 106.890 kNm; stessa sezione/materiale del caso 12.
    L=8000 mm, carico concentrato, fattore LTB=0.5.
    Qualificare geometria/materiale e significato dei vincoli nel confronto.
14. SectionCLateralTorsionalBuckling2
    Mc1=230.1450 contro 173.775 kNm; Mb=57.0372 contro 100.380.
    S355; Constant indica carico distribuito, applicato in sommita'.
    Occorrono profilo/materiale reali e schema di Cordova p.211,
    oltre alla verifica delle proprieta' torsionali/ingobbamento del C.
15. SectionHInteraction2
    Rapporti 1.077567/1.269394 contro 1.000/0.920.
    StandardUNIEN1993p11 usa gammaM0=1.05 e gammaM1=1.10;
    i commenti del test dichiarano gammaM1=1.05 nel riferimento.
    Tale differenza non dimostra da sola la causa di tutti gli scostamenti:
    verificare separatamente kij e metodo semplificato citato nel test.
16. SectionHInteraction3
    Rapporti 0.826401/0.824577 contro 0.770/0.730.
    StandardEN1993p11, gammaM0=gammaM1=1.0. Ricostruire kij e diagrammi
    dei momenti per il carico distribuito prima di attribuire la discrepanza.

Problemi di codice ulteriori individuati durante il censimento
- Nei rami C di flessione/combinazione restano larghezze non depurate da
  anima/raccordi; la precedente correzione non copriva tutti i rami.
- La classificazione N-M deriva il gradiente da N senza usare i momenti.
- La selezione della curva per RHS saldati confronta t/b con 40 o 30:
  richiede verifica diretta della tabella, non adattamento ai report SAP.

Ordine successivo
Correggere e verificare le grandezze di classificazione con nuovi casi
analitici; mantenere separati i problemi di configurazione dei confronti.
Poi resistenza/interazioni di sezione, Mcr/stabilita' e qualificazione NTC.
Non modificare gli attesi legacy per mascherare gli scostamenti.
