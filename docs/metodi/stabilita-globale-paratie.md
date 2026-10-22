# Stabilità globale delle paratie e pressioni FEM

`EmbeddedWallGlobalStability` prepara la sezione del motore `Slopes` esistente:
non duplica la ricerca circolare né l'equilibrio di Bishop. Il primo campo
supportato comprende pareti verticali continue rettangolari, terreni orizzontali
alle due facce e strati orizzontali. Le superfici globali passano sotto la parete;
il suo volume sostituisce il terreno, con peso derivato dai segmenti e dalla
larghezza tributaria. Pali discontinui, sostegni esterni e vincoli assegnati
richiedono geometrie e leggi aggiuntive: questo adattatore li rifiuta.

## Coordinate e azioni

Quote della paratia positive verso l'alto, millimetri; datum della sezione globale
alla testa. Il motore globale ha valle a sinistra: la coordinata fisica orizzontale
si riflette quando lo scavo è a destra. Sono conservati i segni delle forze e
delle coppie, con test speculari. Le azioni della fascia vengono divise per la
larghezza tributaria: N/mm per forze concentrate, N·mm/mm per coppie. Il carico
uniforme sul terreno resta una pressione MPa. La reazione delle molle locali
non è un'azione esterna del sistema globale. Le forze dei tiranti non possono
essere trasferite senza distinguere ancoraggi interni ed esterni alla superficie.

I parametri e le azioni in ingresso devono essere caratteristici, con conferma
e fonte esplicite. La combinazione globale statica deriva da Model; la selezione
di categoria G1/G2/Q, favorevolezza e fattore di combinazione psi è esplicita per
ogni azione. Il fattore parziale e psi sono applicati una sola volta. Le selezioni
permanenti richiedono psi=1. Non sono aggiunte combinazioni implicite.

Per NTC 2018: §6.5.3.1.2, stabilità globale A2+M2+R2, distinta dalle due
combinazioni della resistenza locale. Coefficienti delle azioni da Tab.6.2.I,
materiali da Tab.6.2.II e resistenza globale da Tab.6.8.I, come nel profilo Model.
Fonte primaria consultata il 19/10/2026: [D.M.17/01/2018, G.U.20/02/2018,
capitolo 6](https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110007&dgu=2018-02-20).
Nessun nuovo supporto sismico o di altri annessi nazionali è dichiarato.

## Pressioni e pesi dal campo stazionario

`ISlopeHydraulicField` aggiunge al motore preesistente una pressione alla base
dei conci e intervalli di saturazione per le colonne di integrazione del peso.
Il costruttore originario di `SlopeSection` resta disponibile e il percorso senza
campo FEM mantiene le operazioni precedenti. `SeepageSlopeField` campiona solo
un risultato convergente: trasformazione affine di coordinate, interpolazione
P1 del carico idraulico, pressione con il peso specifico dell'acqua di GPC.
Niente extrapolazioni fuori mesh o attraverso lacune. I corpi impermeabili sono
esclusi dalle colonne prima del campionamento; il loro peso è aggiunto a parte.

Le intersezioni con gli elementi e con u=0 separano gamma naturale e gamma saturo.
Non è una legge di peso in funzione della saturazione né una frangia capillare.
La suzione alla superficie di scorrimento richiede la scelta esplicita di
trascurarla; non aumenta implicitamente la resistenza. Nell'analisi non drenata
la pressione alla base è zero nel bilancio in tensioni totali, mentre i pesi
rimangono coerenti con il campo. Acqua sopra il piano campagna e sue forze sulle
frontiere richiedono un'estensione specifica; il solo campo interstiziale non basta.
Un profilo di pressioni interstiziali assegnato alla parete (`PorePressures`) non
determina la pressione sulle basi dei conci lontano dalla parete e sotto il piede:
senza profilo piezometrico globale o campo FEM la verifica si rifiuta esplicitamente
(prima la sezione veniva calcolata asciutta).

Con φ = 0 la resistenza di un concio è c·l e non dipende da N': un concio con N' < 0
non fa più scartare il cerchio. Il rifiuto resta per i conci con attrito, che non
hanno resistenza a trazione.

## Esiti e prove

Restano i controlli del motore esistente: geometria ammissibile, equilibrio,
convergenza di Bishop, minimo al bordo della ricerca, confronto con numero doppio
di conci. Un rapporto inferiore a uno di una ricerca non conclusiva non dimostra
la verifica. Il risultato conserva dominio, combinazione, fase e diagnostica.

Prove nuove: conversioni di geometria/peso/azioni, simmetria, coefficienti NTC,
pressioni idrostatiche confrontate con il percorso originario, campo affine,
volume saturo analitico, foro impermeabile, non drenato, dominio incompleto,
suzione e annullamento. La cattura `tools/ParatieSlopeCapture` usa CreateNew e
non sovrascrive attesi: nove casi originari asciutti, con falda e con corpi/carichi,
statici, non drenati e sismici conservano identici byte prima/dopo l'estensione.
