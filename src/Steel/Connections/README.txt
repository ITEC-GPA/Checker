CONNESSIONI STANDARD - INDICE DELLA LIBRERIA
==========================================
Versione sorgenteSteel2.73.0.0. Solo librerie, nessuna interfacciaANTHEA.
Stati aggiornati per sottofase: PLAN_PHASES_1_4.txt; esecuzioni: VALIDATION.txt.

COMPONENTI
- BoltStrength/BoltBearing/BoltPunching/BoltSingleShearAssembly: PHASE_1B.txt.
- BoltSlip: precarico e scorrimento, azioniSLE/SLU distinte; PHASE_1B_SLIP.txt.
- ElasticBoltGroup: riparto/equilibrio, non resistenza; PHASE_1C.txt.
- FilletWeldResistance/FilletWeldGroup: cordoni e gruppi; PHASE_1D.txt.
- TStubTension/TStubBoltRows: modi resistenti, leva, file singole e tutti i
  gruppi contigui; PHASE_4A_TSTUB.txt. Forze concomitanti fornite dal chiamante.
- TStubEffectiveGeometry: lunghezze da flangia di pilastro non irrigidita e
  singola fila esterna di piastra estesa; PHASE_4A_GEOMETRY.txt. CreateInput
  collega geometria e resistenza senza sommare capacita delle singole file.
- TStubInternalEndPlateLengths: file interne e tutti i gruppi, alpha esplicito
  qualificato sul grafico EN; PHASE_4A_INTERNAL_END_PLATE.txt. La letturaSCI
  del grafico resta distinta dall'approssimazione matematica AppendiceG.
- ColumnWebComponents: pannello a taglio, anima in trazione/compressione,
  interazioni e diffusione dalla piastra; PHASE_4B_COLUMN_WEB.txt.
- GussetLapCompression: meccanismo fuori piano del tab/fazzoletto a semplice
  sovrapposizione compresso, SCI P358; PHASE_3B_GUSSET_COMPRESSION.txt.
- BraceAngleTension: lordo/netto efficace dell'angolare collegato su un lato,
  lato lungo/corto fisico esplicito; PHASE_3A_ANGLE_NET.txt.
- BraceAngleModelAdapter.FromSingle/FromPair: snapshot Model L/2L identici,
  lato fisico e riparto esplicito, equilibrio e momenti alla linea bulloni;
  PHASE_3C_MODEL_ADAPTER.txt. Nessuna qualifica implicita dell'intero nodo.

NODI E COMPONENTI ASSEMBLATI
- DoubleCoverSplice.Verify: doppio coprigiunto rettangolare in trazione,
  dettagli e chiamata riproducibile nel seguito di questo documento.
- FinPlate.Verify: piastra corta su anima di pilastro, PHASE_2B_SPEC.txt.
- GussetTwinFlatTension.Verify: gruppo unico, fazzoletto rettangolare centrale
  e due piatti uguali tesi; PHASE_3A_TWIN_FLAT.txt. Attacco al supporto escluso.
- FlexibleEndPlate.Verify/VerifyPair: piastra parziale a taglio ordinario,
  anche due travi contrapposte sul medesimo supporto; PHASE_2D_SPEC.txt.
- DoubleAngleCleatComponents.Verify: solo trave/squadrette/bulloni;
  supporto non qualificato e nessun Passed globale del nodo, PHASE_2C_SPEC.txt.
- IBeamSpliceForces.Calculate: riparto N-V-M e sei gruppi sui due lati.
- IBeamSpliceBolts.VerifyStrength/VerifySlip: soli componenti bullonati, con
  azioni SLU/SLE esplicite; PHASE_2E_SPEC.txt include un esempio C# eseguibile.
- IBeamSplicePlates.VerifyBearing/VerifyFlanges/VerifyWebSections: rifollamento,
  piatti/instabilita longitudinale dei coprigiunti d'ala, sezioni d'anima N-V-M.
- IBeamSpliceWebLongitudinal.Verify: inviluppo reale dei tagli fra colonne di
  bulloni e modo di instabilita longitudinale dei coprigiunti d'anima;
  PHASE_2E_WEB_LONGITUDINAL.txt. Compreso nel percorso unico dei componenti.
- IBeamSpliceComponents.Verify: inviluppo dei componenti con coerenza della
  geometria/norma e stati limite richiesti. CanQualifyWholeJoint=false;
  le verifiche ancora mancanti sono esposte in MissingWholeJointChecks.
  Restano meccanismi locali d'anima, membro/modello globale e qualifica del nodo.

CONTRATTI
Ogni metodo dichiara Scope, controlli, tracce e limiti. IsComplete riguarda
quel campo, non tutta la struttura. Fuori campo =>NotSupported e nessun
rapporto favorevole. DatiN/mm/MPa; azioni gia combinate e fattorizzate.
Gli assi non si deducono dal nome: SteelActions, FinPlateActions e
PlanarConnectionActions hanno convenzioni esplicite nei rispettivi contratti.
I test omonimi in tests/Steel.Members.Tests contengono esempi eseguibili.

PRIMO CASO QUALIFICATO: DOPPIO COPRIGIUNTO RETTANGOLARE
==================================================

API: GPC.Checkers.Steel.Connections.DoubleCoverSplice.Verify(input)
Metodo: Steel.Connection.DoubleCoverSplice.Tension

CAMPO DEL METODO
Giunto piano tra due piatti rettangolari identici, con due coprigiunti identici,
uno per faccia, disposti simmetricamente rispetto al piano medio. Trasmissione
per taglio/rifollamento, senza precarico, imbotiture, saldature o contatto di testa.
Carico statico di sola trazione centrata. I bulloni non sono tesi lungo il gambo.
Due colonne trasversali di bulloni, Rows file longitudinali per ciascun lato
della giunzione. Fori tondi normali, non svasati; classi 8.8 o 10.9, acciaio al
carbonio ordinario con fy <= 460 MPa. Resistenze del materiale assegnate dal
chiamante per il prodotto e lo spessore effettivi. Non acciaio patinabile.

Non e' un giunto completo trave/colonna a I: eventuali ali/anima, ripartizioni
fra componenti e verifiche della membratura vanno trattate dal relativo nodo.
Nessuna modifica ad ANTHEA, persistenza, converter o Model in questa fase.

GEOMETRIA E AZIONI
Unita': mm, mm2, MPa=N/mm2, N, Nmm. N positivo a trazione lungo asse locale 3.
V1 e' nel piano dei piatti, V2 fuori piano; T attorno a 3, M1/M2 attorno a 1/2,
con la convenzione di SteelActions. Le sei azioni concomitanti sono obbligatorie
e gia' fattorizzate dal chiamante. Il motore non genera combinazioni.

Vista longitudinale (ciascuna 'o' rappresenta due bulloni distanti Gauge):

     bordo coprigiunto                                  bordo coprigiunto
       |--CoverEnd--o--Pitch--o--MainEnd--| |--MainEnd--o--Pitch--o--CoverEnd--|
    <- N          piatto sinistro       | |       piatto destro           N ->
                                      gap
       <---------------- due coprigiunti continui identici ---------------->

Rows=2 nel disegno; per Rows generico Lj=(Rows-1)*Pitch.
CoverLength=2*(CoverEnd+Lj+MainEnd)+ButtGap.
Ogni gruppo ha 2*Rows bulloni: il totale e' 4*Rows. Ogni lato trasferisce N.
Le file sono numerate dal giunto verso l'esterno: Row1 adiacente alla testa del
piatto; RowRows adiacente al bordo libero del coprigiunto. I due gruppi sono
speculari. Gauge e' centrato: e2=(Width-Gauge)/2 per ciascun tipo di piatto.
Il coprigiunto non puo' essere piu' largo del piatto centrale.

Riparto simmetrico: ciascun coprigiunto trasferisce N/2, ciascun bullone N/(2*Rows)
su due piani di taglio, ciascun piano N/(4*Rows). Nessuna eccentricita' secondaria
da singolo coprigiunto. Entrambi i piani hanno la medesima condizione di filettatura.
Le due lamiere principali sono identiche; i controlli 'Main' valgono per entrambe.
Gli esiti 'EachCoverBearing' valgono separatamente per ognuno dei due coprigiunti.

VERIFICHE SLU
- Taglio di ogni bullone sui due piani, inclusa riduzione per giunti lunghi.
- Rifollamento della lamiera centrale e di ogni coprigiunto, distinguendo bulloni
  di estremita' e interni lungo N. Trasversalmente le due colonne sono di bordo.
- Snervamento lordo e rottura netta di ogni tipo di piatto.
- Block tearing: percorsi simmetrici interno ed esterno; si considera il peggiore.
  Anv=2*t*(e1+Lj-(Rows-0.5)*d0); Ant interno=t*(Gauge-d0);
  Ant esterno=t*(2*e2-d0). Non vengono sommate resistenze di percorsi alternativi.

Si impone un riparto uguale della forza fra bulloni e si verifica ciascun bullone:
non si usa la maggiore capacita' ottenibile con redistribuzione plastica del gruppo.
Per la sezione netta viene cautelativamente utilizzata la forza intera del piatto.
Minimi geometrici: e1,e2 >= 1.2*d0; p1 >= 2.2*d0; Gauge >= 2.4*d0.
Massimo qualificato per entrambi i passi: min(14*t_min,200 mm), anche quando la
norma consente geometrie piu' ampie per una specifica disposizione. Per esposizione
corrosiva: bordi <= 4*t_min+40 mm. t_min e' il minore spessore collegato.
Per una sola fila Pitch non entra nei controlli fisici o nelle resistenze.

NORME E COEFFICIENTI
EC: EN 1993-1-8:2005 (corrigenda della copia consultata) e EN 1993-1-1:2005.
NTC: D.M. 17/01/2018, Capitolo 4.2. Non si mescolano edizioni con gli Eurocodici
di seconda generazione. Il metodo NTC usa EN 1993-1-8 per giunti lunghi e block
tearing non esplicitati dalle formule NTC del paragrafo bullonature; i riferimenti
integrativi rimangono visibili nel risultato.

ConnectionDesignCode.Ntc2018(): gammaM0=1.05, gammaM2 piatti=1.25,
gammaM2 bulloni/rifollamento=1.25. La formula NTC di rifollamento per bullone di
bordo non contiene il termine trasversale di passo presente nella Tabella3.4 EC3.
ConnectionDesignCode.Eurocode3(gammaM0,gammaM2Plate,gammaM2Connection,parameterSet):
tutti i fattori sono espliciti, senza selezione implicita dell'annesso nazionale.
GammaM2Plate governa rottura netta e termine a trazione del block tearing;
GammaM2Connection governa taglio bulloni e rifollamento. SCI P358 usa rispettivamente
1.1 e 1.25 nel contesto UK: questi valori sono riprodotti solo nel suo benchmark.
ParameterSet documenta la provenienza dichiarata; non certifica i valori forniti.

Diametri qualificati 12..36 mm. Gioco diametrale massimo qualificato: 1 mm per
d <= 14, 2 mm per 14 < d <= 24, 3 mm oltre. M12/M14 con gioco 2 mm sono esclusi:
richiedono le condizioni aggiuntive e il fattore 0.85 di EN1993-1-8 3.6.1(5).
NTC: se il gioco supera 1 mm per d<=20 o 1.5 mm per d>20, occorre la provenienza
di una verifica SLE dei possibili assestamenti, HoleClearanceServiceabilitySource.
Questa evidenza e' fornita dal chiamante, conservata nell'input, non calcolata qui.
La presenza di un testo non equivale a una verifica SLE eseguita dal motore.

ESITI E TRACCIABILITA'
Input immutabili, risultato immutabile, controlli con ID stabili, azione, resistenza,
utilizzazione e riferimento; traccia di aree, fattori e riparto con unita'.
Passed/Failed riguardano solo le verifiche SLU nel campo dichiarato in Scope.
IsComplete significa che tale insieme e' stato valutato, non che l'intera struttura
sia verificata. NotSupported non contiene controlli parziali favorevoli o rapporto
globale: azioni anche minime fuori campo non vengono trascurate.
Dimensioni non positive, NaN/infinito e materiali incoerenti sollevano eccezioni
di input; non generano un esito positivo. Fy/Fu sono valori caratteristici forniti,
non ricavati automaticamente da un nome di acciaio.

ESEMPIO DI CHIAMATA (C#, SENZA INTERFACCIA)
using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;

var input = new DoubleCoverSpliceInput(
    ConnectionDesignCode.Ntc2018(),
    new ConnectionPlate(200, 16, 275, 430),
    new ConnectionPlate(160, 10, 275, 430),
    new ConnectionBolt(20, 21, 245, ConnectionBoltGrade.Class8_8, true),
    new SteelActions(100000, 0, 0, 0, 0, 0),
    rows: 2, pitch: 60, gauge: 80, mainEnd: 40, coverEnd: 50,
    buttGap: 10, exposed: false);
var result = DoubleCoverSplice.Verify(input);

Questo esempio e' coperto da CompleteSpliceMatchesIndependentHandCalculation:
Passed; controllo governante Main.BlockInside; resistenza 656190.04 N,
utilizzazione 0.1523948763. Leggere sempre Status/Scope prima dei valori.

FUORI CAMPO
Compressione, momenti/tagli globali, fatica, duttilita' sismica, rigidezza/rotazione,
scorrimento e deformabilita' SLE, bulloni precaricati/svasati, fori asolati o
maggiorati, imbotiture, saldature, coprigiunti diversi, passi non uniformi,
acciaio patinabile, gruppi non centrati. Sono sviluppi separati, non implicitamente
coperti da una verifica del solo bullone o piatto.

FONTI CONTROLLATE IL 23/10/2026
1. EN1993-1-8:2005, Tabella3.4 p27 (PDF29), 3.6.1(5) p25 (PDF27), 3.7/3.8
   p30 (PDF32), 3.10.2 p32 (PDF34). Copia del testo normativo:
   https://www.phd.eng.br/wp-content/uploads/2015/12/en.1993.1.8.2005-1.pdf
2. NTC2018, Tabella4.2.XVIII e prescrizione fori p110; formule4.2.63-71 p111.
   Testo D.M.17/01/2018, Supplemento ordinario n8 alla G.U. n42 del20/02/2018.
   https://www.gazzettaufficiale.it/eli/id/2018/02/20/18A00716/sg
3. SCI/BCSA P358, ristampa2014, cap6: p179 procedura piatto teso e block tearing;
   cap6.9 esempio3 foglio9/9 p225 (PDF235), aree e resistenze del coprigiunto.
   https://www.steelconstruction.info/images/a/a9/SCI_P358.pdf

PROVE E LIMITI DEL CONFRONTO INDIPENDENTE
DoubleCoverSpliceTests: esempio SCI per un componente, calcolo manuale del giunto
rettangolare completo (EC e NTC), riparto, materiali, piani di taglio, percorsi,
giunti lunghi, geometria limite e rifiuto dei casi fuori campo. SCI non pubblica
qui questo esatto doppio coprigiunto rettangolare: non viene presentato come tale.
Le aree del benchmark SCI sono 3000,2376,888,4776 mm2; resistenze pubblicate
825 e797 kN, con tolleranza 0.5 kN sul valore arrotondato. Gli altri attesi sono
formule indipendenti nei test, senza chiamare il motore per costruire gli attesi.

PASSI SUCCESSIVI
Vedere PLAN_PHASES_1_4.txt per il piano effettivo: supporto delle doppieL,
continuitaI, fazzoletti/controventi, T-stub e nodi a momento, rigidezza/rotazione.
Piastre di base e nodi tubolari appartengono alle fasi successive5/6.
I risultati restano consumabili dalle librerie; integrazioneANTHEA rinviata.

- DoubleAngleCleatColumnFlangeBearing: rifollamento della flangia reale del
  supporto;14test, altri modi locali aperti. PHASE_2C_SUPPORT_TENSION_REVIEW.txt.

- EndPlateMomentComponents: assemblaggio resistenza a momento a filo/estesa,
  tutti i gruppi e compressione/pannello; PHASE_4CDE_MOMENT_COMPONENTS.txt.

- PackingFaceActionAdapter: assi fisici e sei azioni delle due facce delle
  imbottiture Model2L/2C; PHASE_3D_PACKING_FACE_ACTIONS.txt.

- EndPlateRotationalStiffness: molle delle file e componenti comuni, rigidezza
  iniziale/secante; benchmark JRC inPHASE_4F_JRC_STIFFNESS_REVIEW.txt.

- GussetTwinFlatWeldedSupport: percorso bullonato e attacco aT a due cordoni,
  con domanda sul supporto distinta; PHASE_3A_WELDED_SUPPORT.txt.

- EndPlateBoltComponents: bulloni con leva, V-T, rifollamento/punzonamento
  su entrambe le lamiere e giunti lunghi; PHASE_4CDE_BOLT_COMPONENTS.txt.

- EndPlateWeldComponents: inviluppo locale delle saldature per piastra estesa,
  tutto il taglio nell'anima; PHASE_4CDE_WELD_COMPONENTS.txt.

- JointStiffnessClassification, JointStrengthClassification ed
  EndPlateRotationCapacity: classi e criteri sufficienti distinti, con
  evidenze esterne esplicite e nessuna capacita rad inventata; PHASE_4F_CLASSIFICATION_ROTATION_IMPLEMENTATION.txt.

- IBeamSplicePlates.VerifyFlangeNetPlastic: netto delle ali categoriaC,
  integrato in ogni casoSLU dichiarato; PHASE_2E_FLANGE_NET_PLASTIC.txt.

- FlexibleEndPlateTyingComponents.Verify: politicaSCIaccidentale esplicita,
  piastra/bulloni e anima trave; PHASE_2D_TYING_COMPONENT_IMPLEMENTATION.txt.
