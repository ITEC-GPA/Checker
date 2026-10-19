# Sezioni circolari in c.a. nelle paratie

Model definisce una sezione fisica circolare con una corona uniforme di barre
longitudinali e staffe singole. Dimensioni in mm, tensioni in MPa. Copriferro
libero esterno alla staffa; raggio della corona R−c−φst−φL/2. Numero pari di
barre, almeno quattro, senza sovrapposizioni e interamente dentro il contorno
numerico. Ogni sessione crea materiali, sezione e solutori propri.

Proprietà lorde analitiche del cerchio per EI. Contorno di integrazione
esplicitamente poligonale, 64 lati predefiniti, modificabile da 32 a 256 in
multipli di quattro. Nessuna modifica alle mesh Delaunay storiche o alle loro
opzioni. La sensibilità 64→128 è verificata separatamente. Calcestruzzo
ordinario C16–C50, diagramma parabola-rettangolo; acciaio per armatura con
diagramma elastico perfettamente plastico e deformazione ultima assegnata.

## Verificatori riutilizzati

`CircularRcSectionSession` in Concrete coordina `PileReinforcement`,
`SectionCheckerModelCode2010`, `StressLimitCheck` e `SectionCrackCheck`.
Profilo italiano NTC 2018, SLU statici e combinazioni SLE caratteristica,
frequente e quasi permanente. La verifica N–M mantiene N assegnato e segno
del momento. Taglio con staffe singole e modello circolare equivalente:
serve conferma esplicita di applicabilità e z/d. Una spirale non è
automaticamente equiparata a staffe singole. Capacità/risposte non ottenute
o non applicabili restano Non valutato anche con domanda nulla.

La verifica di fessurazione usa la geometria circolare e l'interasse della
corona del verificatore esistente, copriferro delle barre longitudinali
c+φst. Nessuna nuova formula normativa in Design o ANTHEA. Controlli
costruttivi separati dalla resistenza nominale; il comportamento trave o
pilastro è una scelta esplicita del progettista. I minimi sismici dei pali
di fondazione non sono applicati automaticamente alle paratie. Ancoraggi,
giunti, instabilità e dettagli dissipativi richiedono verifiche ulteriori.

Fonte primaria consultata: Italia, D.M.17/01/2018, NTC §4.1,
[G.U.20/02/2018, capitolo 4](https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110005&dgu=2018-02-20).
Coefficienti e procedure vengono dai profili Concrete già presenti; nessun
annesso nazionale europeo viene sovrapposto al profilo NTC.

## Interazione con la paratia

Design fornisce EI_fascia=Ec I b/s per una fila di pali identici, fascia b e
interasse s. Tutte le componenti N–M–V dello stesso taglio sono trasferite
con s/b una sola volta, conservando caso, fase, quota, sezione e lato.
N e M sono convertiti nelle convenzioni Concrete. La ripartizione uniforme
e il trasferimento delle pressioni attraverso il fronte devono essere
documentati; più file, pali secanti ed effetto arco non sono dedotti da questa
equivalenza. La risposta strutturale globale resta elastica nel motore attuale.

## Attesi e prove

`tools/ParatieRcPileExpected.py` ha creato la fixture prima del codice
di produzione. Fibre orizzontali indipendenti sul poligono, integrazione
con 131072 strisce e confronto con 65536, scarto relativo sotto 1e-7.
Conglomerato spostato dalle barre sottratto; equilibrio dello sforzo normale
con ricerca indipendente dell'asse neutro. D=800 mm, 12φ20, c=50 mm,
staffe φ10, C30/B450: per N=0, MRd=461314867.0936418 Nmm (64 lati).
Tre valori di N, entrambi i segni di M, due discretizzazioni. Tolleranza
0.3% verso il solutore nativo, comprensiva della discretizzazione delle
tensioni e del suo criterio di equilibrio assiale già collaudato.

Ulteriori prove: EI e mensola analitica, tensione uniforme su sezione
omogeneizzata, SLE in compressione/flessione, trasferimento completo delle
azioni, dati invalidi, incompleta convergenza, annullamento e isolamento.
La prima compilazione ha segnalato un namespace mancante. Il primo test
SLE richiedeva erroneamente due controlli anche in combinazione frequente:
corretto il conteggio dopo riscontro del profilo, che non assegna limiti
tensionali a tale combinazione. Nessun atteso numerico indipendente o
storico modificato. `rc-pile-design-03`: cinque test PASS;
`rc-pile-design-04`: sei test PASS, aggiunti isolamento concorrente e N
fuori dominio. Integrazione applicativa ancora in corso al momento di
questo documento.
