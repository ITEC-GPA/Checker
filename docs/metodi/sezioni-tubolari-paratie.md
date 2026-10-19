# Sezioni tubolari della paratia: resistenza N–M–V

Percorso additivo in Checker Steel, coordinato da Design. Campo corrente:
CHS a spessore costante, integre, classi 1–2, acciaio strutturale S235–S460,
costruzioni ordinarie e SLU statici NTC 2018. N, mm, Nmm, MPa. Non è una
verifica completa della membratura o della paratia: instabilità, collegamenti,
pressioni radiali/ovalizzazione, introduttori di carico, torsione e fenomeni
tridimensionali richiedono percorsi distinti. Classi 3–4 non valutate da questa
implementazione plastica; non si attribuisce loro la capacità lorda.

## Fonte e criterio

Italia, D.M.17/01/2018, NTC §4.2.4.1.2 e tab.4.2.V/VII;
G.U.20/02/2018, supplemento 8, capitolo 4:
https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110005&dgu=2018-02-20

Classificazione D/t con soglie 50ε²/70ε²/90ε², ε²=235/fyk. Profilo
`StandardNTC2018Steel` non modificato, γM0=1.05. La verifica plastica usa una
distribuzione di tensioni in equilibrio con N e M; non usa l'approssimazione
di un programma commerciale né la formula riservata alle sezioni I/H.

Per il taglio V: Av=2A/π e Vrd=Av fyk/(√3 γM0), [4.2.17,4.2.23]. Fino a
|V|/Vrd=0.5 la riduzione è nulla; oltre tale soglia ρ=(2|V|/Vrd−1)²,
[4.2.40]. Il presente percorso applica **cautelativamente la riduzione
(1−ρ) a tutta la sezione**, quindi non pretende di trovare la massima capacità
N–M–V ottenibile riducendo soltanto Av. Per |V|≥Vrd la capacità N–M adottata
è zero, con superamento del taglio esplicito se |V|>Vrd.

Posti R=D/2, r=R−t, f=(1−ρ)fyk/γM0 e h distanza assoluta dell'asse neutro
plastico dal centro, l'area della calotta di raggio a sopra h è

`C(a,h)=a²[acos(h/a)−(h/a)√(1−h²/a²)]`, nulla se h≥a.

L'equilibrio assiale è `|N|=f[A−2(C(R,h)−C(r,h))]`. La bisezione su [0,R]
usa 80 iterazioni e controllo residuo ≤max(1e-8 N,1e-10 Nrd). Il momento
resistente si ottiene dai momenti statici delle calotte:

`Mnr=(4/3)f[(R²−h²)^(3/2)−max(0,r²−h²)^(3/2)]`.

Per |N|≥fA, Mnr=0; il controllo assiale rimane distinto e segnala il
superamento. La simmetria dell'anello rende la capacità indipendente dai segni
di N e M; i risultati conservano comunque le azioni firmate concomitanti.
N positivo a trazione. Il successo locale non certifica l'instabilità.

## Fascia equivalente e azioni

Model conserva uno snapshot scalare delle proprietà geometriche esatte di
`SectionCHS` e del materiale. Per una fila di tubi identici, interasse s e
fascia b, Design fornisce `EI_fascia=E I b/s`. I carichi distribuiti e le
pressioni agiscono sulla fascia b. Le tre azioni sulla sezione fisica sono
tutte moltiplicate per s/b, una sola volta, nello stesso taglio/fase/caso.
Il segno N della trave, positivo a compressione, viene invertito.

Fila unica, ripartizione uniforme e trasferimento delle pressioni attraverso
il fronte di sostegno richiedono una giustificazione esplicita. Non si
modellano implicitamente più file, effetto arco o coperture del fronte fra
tubi. Interasse inferiore al diametro rifiutato. Il modulo resta elastico
strutturalmente: il controllo plastico di resistenza non introduce una cerniera
né modifica la storia delle molle geotecniche.

## Prove e stato

Prima del kernel, `tools/ParatieTubeExpected.py` ha creato una nuova fixture
con integrazione numerica Simpson delle calotte in coordinate angolari a
16384 intervalli, per asse neutro assegnato (nessuna inversione di N, nessun
import GPC). Sei casi: flessione pura, N–M con taglio basso/alla soglia/elevato,
quasi limite assiale e classe 2. La produzione inverte N con formule analitiche.
Confronto relativo 2e-10, assoluto minimo 1e-7 nelle rispettive unità, coerente
con quadratura indipendente e double; nessun atteso storico modificato.

`tube-steel-02`: quattro test PASS, comprendenti tutte le combinazioni di
segni dei sei casi, capacità pure/limite, dati non supportati, isolamento e
annullamento. La prima compilazione ha segnalato un conflitto di nome locale,
corretto prima della riesecuzione. `tube-design-01`: due test PASS, mensola
analitica e confronto di tutti i tagli/fasi con chiamata diretta a Steel,
larghezza 1.5 m e interasse 2 m. Il primo comando Model indicava un nome di
progetto inesistente; la compilazione corretta è `tube-model-build-02`.

Set coerente, integrazione ANTHEA e interfaccia di questo incremento ancora
da collaudare. Le altre famiglie di sezione e verifiche della matrice restano
aperte; nessuna esclusione dell'incarico complessivo.
