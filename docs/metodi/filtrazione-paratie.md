# Filtrazione stazionaria 2D e collegamento alle paratie

Revisione del 19 ottobre 2026. Metodo proprio, verificato con soluzioni
indipendenti; non è una riproduzione di un solutore commerciale.

## Modello e unità

`SeepageModel` descrive un dominio piano con triangoli lineari conformi,
materiali per elemento e condizioni al contorno. Coordinate e carico totale h
sono in mm, z cresce verso l'alto. Il tensore simmetrico K ha unità mm/s; la
pressione è u = γw(h − z), in MPa, con γw da `SoilUnits`. La velocità di Darcy è
q = −K∇h in mm/s. Le portate sono **per unità di lunghezza fuori piano**, mm²/s:
non sono forze né portate volumetriche già moltiplicate per l'interasse.

Il problema stazionario è div(K∇h)=0. Sono ammessi carichi piezometrici imposti,
flussi normali assegnati positivi in ingresso e superfici di filtrazione
atmosferiche. Un bordo non assegnato è impermeabile. Ogni componente connessa
deve avere un carico imposto. Il tensore deve essere positivo definito.

La superficie drenante soddisfa h≤z e q uscente≥0: quando è attiva h=z,
altrimenti il flusso è nullo. Non introduce acqua da un bordo atmosferico.
Non si modella il transitorio, la consolidazione, il trasporto o l'erosione.

## Non saturazione e metodo numerico

Con curva non satura: ψ=h−z, m=1−1/n, α>0 in 1/mm, n>1. Per ψ<0:

Se = [1 + (α|ψ|)^n]^(−m)

kr = Se^(1/2) [1 − (1 − Se^(1/m))^m]^2.

Per ψ≥0, Se=kr=1. Il tensore è moltiplicato per kr. Parametri e fonte sono
assegnati; nessuna permeabilità minima nascosta. Se la conducibilità si annulla
numericamente, il risultato è mal condizionato e non pubblica pressioni. La
legge non include isteresi. Senza curva K è costante: analisi satura confinata,
non una ricostruzione automatica della superficie libera.

Galerkin P1: Kij = ∫∇Ni·K∇Nj dA. Tre punti simmetrici integrano la conducibilità
non satura; un flusso uniforme su un bordo contribuisce qL/2 a ciascun estremo.
Picard con ricerca della riduzione del residuo e insieme attivo dei nodi
drenanti; gradiente coniugato con precondizionamento diagonale per i sistemi
lineari. Nessuna penalizzazione dei carichi imposti. La convergenza richiede
residuo nodale, bilancio globale e variazione del carico entro le tolleranze.
Il limite di iterazione o un sistema mal condizionato produce un esito esplicito
con campo delle pressioni vuoto. L'annullamento propaga il token.

Tolleranze di default: variazione h 1e−5 mm, portata assoluta 1e−9 mm²/s e
relativa 1e−8 rispetto alle portate assegnate e alle reazioni. Sono controlli
algebrici, non una stima dell'errore di discretizzazione. I gradienti agli spigoli
possono essere singolari: la verifica di un gradiente di uscita richiede un
percorso/volume fisico esplicito e una prova di sensibilità alla mesh.

## Dominio della paratia e trasferimento

`EmbeddedWallSeepageMesh` genera un dominio a due colonne stratificate con
parete verticale impermeabile di spessore finito. Lati esterni a carico imposto,
fondo impermeabile, terreno esposto sommerso o drenante. Le distanze dei bordi
e la quota inferiore sono input ingegneristici da controllare. La parete deve
attraversare entrambi i piani campagna e non raggiungere il fondo del dominio.
Interfacce, scavo, piede e livelli d'acqua sono linee di mesh. Non usa Gmsh.

`SeepageWallCoupling.Profile` estrae un profilo lineare a tratti sul lato scelto,
con tutte le intersezioni della mesh e gli attraversamenti di pressione nulla.
Non estrapola fuori dal dominio o attraverso vuoti e rifiuta punti idraulicamente
ambigui. Per la legge meccanica delle paratie la suzione può essere trascurata
solo con scelta esplicita: il FEM conserva il valore negativo, mentre il profilo
trasferito usa zero. Non viene attribuita resistenza per suzione al terreno.
Per i pesi naturali/saturi si può conservare la quota di falda oppure attivare
esplicitamente `SaturationFromPorePressures`, con peso saturo nei tratti a
pressione positiva; ipotesi e prove nelle [verifiche idrauliche](verifiche-idrauliche-paratie.md).
Non è una simulazione accoppiata di consolidazione. Le fasi hanno campi idraulici separati;
una filtrazione fallita non può fornire pressioni alla fase meccanica.

## Prove indipendenti

`tests/EmbeddedWalls.Tests/SeepageTests.cs`, corsa `paratie/seepage-03`:
12 prove idrauliche, insieme alle 11 regressioni meccaniche, tutte passate.

- Darcy confinato: Δh=1000 mm, L=1000 mm, K=0,01 mm/s, q=0,01 mm/s.
- Tensore ruotato e campo affine: controllo delle due componenti di q.
- Due strati in serie: q=1000/(500/0,01+500/0,04)=0,016 mm/s;
  carico all'interfaccia 1200 mm.
- Flusso di Neumann e reazioni, integrazione delle lunghezze del bordo.
- Equilibrio idrostatico non saturo, pressione negativa conservata e valore
  indipendente della curva con α|ψ|=1, n=2.
- Flusso gravitazionale a ψ costante, anche da inizializzazione diversa.
- Bordo atmosferico drenante e asciutto; isolamento fra due analisi.
- Campo armonico quadratico, errore del gradiente P1 che si dimezza su tre mesh.
- Due fasi idrauliche trasferite alla mensola: M al piede 353160000 e 44145000
  Nmm, spostamento iniziale in testa 12,71376 mm, attesi analitici.
- Paratia impermeabile: bilancio, ostacolo al flusso, portata ridotta aumentando
  l'infissione e assenza di flusso con carichi identici.
- Rifiuto di tensori non ammissibili, domini flottanti, flussi interni,
  annullamento e mancata convergenza senza campo pubblicato.

Le tolleranze delle prove affini sono più strette di quelle applicative perché
il campo appartiene allo spazio degli elementi; la prova armonica misura invece
l'errore di discretizzazione. Nessun atteso è ricavato dal codice sotto prova.

## Fonti tecniche

- USACE, EM 1110-2-1901, *Seepage Analysis and Control for Dams*, 30 settembre
  1986: [documento ufficiale](https://www.publications.usace.army.mil/portals/76/publications/engineermanuals/em_1110-2-1901.pdf).
  Principi di Darcy, pressioni e filtrazione; non è assunto come profilo normativo italiano.
- M.Th. van Genuchten, 1980, *A closed-form equation for predicting the hydraulic
  conductivity of unsaturated soils*, SSSAJ 44(5), 892–898. Formulazione
  verificata anche nella [documentazione tecnica ufficiale USGS SUTRA](https://water.usgs.gov/nrp/gwsoftware/sour/special/unsat/unsat.htm),
  consultata il 19 ottobre 2026. La pagina usa α riferito alla pressione: qui α
  è riferito al carico di pressione in mm, come dichiarato nel contratto.
