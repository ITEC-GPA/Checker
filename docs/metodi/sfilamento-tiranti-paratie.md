# Sfilamento dei tiranti delle paratie

Calcolo in Geotechnics, contratti e profilo normativo in Model, trasferimento
per fase in Design. Le forze sono in N, le distanze lungo l'asse in mm, le
pressioni in MPa. La trazione è positiva e riferita al singolo tirante.

## Predizione Bustamante–Doix

`GroundAnchorPullout.Predict` richiama le letture degli abachi già disponibili
in `BustamanteDoix.UnitShaftResistance`. Per ciascun tratto cementato:

    Ds = alpha D
    Ra,c,i = pi Ds Li qs(pl)
    Ra,c = somma dei contributi dello stesso profilo

Il tratto libero è escluso; il profilo deve coprire tutto il bulbo senza lacune
né sovrapposizioni. Non si aggiunge portanza di punta, non si estrapolano gli
abachi e non si applicano coefficienti normativi alla predizione.

`pl` è la pressione limite pressiometrica del terreno, con fonte per strato.
`pi` è la pressione d'iniezione: resta un dato tecnologico distinto. Dal
riferimento Viggiani già conservato in ANTHEA, pp.392–396, si controllano i
campi IRS `pi >= pl`, IGU `0.5 pl <= pi <= pl` e l'intervallo di alpha della
tabella 13.12. Le curve sono le digitalizzazioni storiche delle fig.13.16–19:
le precisioni numeriche dei test non attestano una pari precisione empirica.

Il percorso corrente è limitato a un bulbo isolato e confinato, con almeno
5 m di ricoprimento da tutte le superfici libere e almeno 4 m di tratto
cementato. Sono **limiti espliciti dell'implementazione**, non una ricostruzione
integrale dei limiti del metodo originale. Confinamento, zona stabile,
isolamento e applicabilità alle fasi devono avere una fonte; esecuzione,
quantità iniettate e qualità della miscela richiedono documentazione distinta.
Non si assegna automaticamente un incremento di resistenza aumentando pi.

Riferimenti: C. Viggiani, *Fondazioni*, §13.1.6, pp.392–396, eq.13.21,
tab.13.12–13.13 e fig.13.16–19; M. Bustamante e B. Doix, *Une méthode pour le
calcul des tirants et des micropieux injectés*, BLPC 140, 1985, pp.75–92.
La scheda bibliografica dell'editore è stata individuata; l'accesso al testo
integrale tramite IFSTTAR non risponde e il collegamento Unife reindirizza.
Non si dichiara una nuova verifica completa del testo originale o di tutti i
suoi casi: i limiti superficiali e le estensioni restano aperti.

## Resistenza di progetto NTC

Fonte primaria verificata: [NTC 2018 capitolo 6, Gazzetta Ufficiale](https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110007&dgu=2018-02-20),
§6.6.1–2 e tabelle 6.6.I–III, pagine stampate 198–199, controllate anche
graficamente il 19/10/2026. Italia, edizione 2018, norma nazionale; SLU statici
A1+M1+R3. Le azioni devono provenire da una storia non lineare indipendente con
questa base; non si moltiplicano reazioni già ottenute da altre combinazioni.

Il profilo Model distingue capacità calcolate per profili completi da capacità
misurate in prove preliminari. Per i profili, xi medio/minimo vale rispettivamente
1.80/1.80, 1.75/1.70, 1.70/1.65, 1.65/1.60, 1.60/1.55 per n=1,2,3,4,>=5.
Per le prove: 1.5/1.5, 1.4/1.3, 1.3/1.2 per n=1,2,>2. GammaR vale 1.1
per temporanei e 1.2 per permanenti. Non sono coefficienti dei pali.

    Rak = min(media(Ra)/xi_medio, minimo(Ra)/xi_minimo)
    Rad = Rak/gammaR
    utilizzo = Ed/Rad

Ogni campione è la capacità totale di un singolo profilo/prova documentato.
Non si raccolgono i minimi dei singoli strati da indagini differenti e non si
mescolano le due popolazioni. Nessun ulteriore coefficiente M viene applicato.
Sisma o norma non disponibile producono Non valutato. Senza conferma delle
prove in sito richiesta dalle NTC, una capacità analitica sufficiente resta
preliminare/Non valutata; una capacità insufficiente rimane Non soddisfatta.
Questa è la sola verifica terreno–bulbo: acciaio, aderenza acciaio–miscela,
testa, gruppo, stabilità globale, durabilità e collaudo sono distinti.

## Incongruenza storica individuata

Il percorso `AxialPileCapacity` dei micropali passa `pile.Pressure` a
`BustamanteDoix.Segments`; il parametro è descritto come pressione d'iniezione
assunta uguale a pl. La fonte conservata distingue le due grandezze. Il nuovo
percorso per tiranti usa quindi `UnitShaftResistance` con pl esplicita per
strato. Il percorso storico e i suoi attesi sono conservati; la revisione del
contratto storico dei micropali deve essere tracciata separatamente, con
migrazione dei dati e confronto prima/dopo. Nessuna correzione silenziosa.

## Attesi e prove

`tools/ParatieAnchorExpected.py` ha creato gli attesi prima del kernel, con
sola aritmetica e pi e senza sovrascrittura. Bulbo di 6 m, D=200 mm: 2 m in
SG1 con pl=2 MPa, alpha=1.5 e 4 m in AL1 con pl=1 MPa, alpha=1.8. Contributi
471238.898038469 N e 814300.815810475 N. Due profili danno Rad permanente
490196.078431373 N e utilizzo 0.816 a 400 kN. Tre prove danno Rak=750 kN e
Rad temporaneo=681.818181818 kN. Tolleranza 2e-12 relativa, minimo 1e-9 assoluto
per aritmetica double su ordinate fissate, non per la validità empirica.

Prove ulteriori: pl distinta da pi, conteggi e fattori, applicabilità,
profili incompleti, campioni duplicati, impossibilità di estrapolare, input
invalidi, copia degli input, annullamento e isolamento. Design conserva fase,
caso e forza individuale anche con interasse/fascia/inclinazione non unitari;
la rimozione del tirante elimina il relativo controllo della fase.

Evidenze nell'albero ANTHEA `supporto/artefatti/paratie`: `anchor-kernel-01`
ha individuato due API non disponibili in netstandard2.0; sostituite da
controlli equivalenti senza cambiare framework. `anchor-kernel-02`: sette
nuovi test superati; `anchor-kernel-03`: 50 test paratie superati;
`anchor-design-01`: due nuovi test Design superati. UI/persistenza/report e
set coerente dell'incremento sono ancora da integrare e collaudare.
