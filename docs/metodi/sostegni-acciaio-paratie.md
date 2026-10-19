# Sostegni assiali metallici delle paratie

Il calcolo delle resistenze è in `GPCChecker.Steel`, percorso
`Ntc2018AxialSteel`. Model conserva uno snapshot scalare della membratura,
con proprietà delle sezioni Model e materiale. GPC.Design collega ogni forza
del singolo sostegno alla propria fase e combinazione. Non applica nuovamente
coefficienti alle azioni, interassi o proiezioni trigonometriche.

## Campo e fonte

Italia, NTC 2018, costruzioni ordinarie, SLU statici, aste circolari piene o
tubolari CHS in acciaio strutturale S235/S275/S355/S420/S460. La classe nominale
è distinta dalla resistenza ridotta per lo spessore del prodotto; entrambe
devono essere dichiarate e documentate. Conferma esplicita del carico assiale
centrato. Acciai da precompressione, barre filettate speciali, connessioni,
ancoraggi, bulbi, corrosione e durabilità non sono verificati da questo percorso.
Sisma e altri profili normativi restituiscono Non valutato.

Fonte primaria: [NTC 2018, capitolo 4, Gazzetta Ufficiale](https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110005&dgu=2018-02-20),
§4.2.4.1.2.1-2, §4.2.4.1.3.1, tabelle 4.2.V, VII e VIII; consultata e
controllata graficamente il 19/10/2026. Il profilo GPC StandardNTC2018Steel
fornisce γM0=γM1=1,05, γM2=1,25 e i fattori d'imperfezione. Il profilo
ordinario non viene esteso automaticamente ai ponti.

## Metodo

Unità N, mm, MPa; N positivo a trazione. Snervamento: A fy/γM0.
Se è dichiarata una sezione forata di collegamento ordinario, si controlla
anche 0,9 Anet fu/γM2, con area netta esplicita e fonte. L'area netta non viene
mai sostituita con la lorda. La gerarchia sismica non è implicita.

Compressione: area lorda per le classi 1–3. Le CHS sono classificate con
D/t e limiti 50ε², 70ε², 90ε², ε²=235/fy. Per la classe 4 serve il controllo
locale del guscio: questo incremento restituisce Non valutato.

Per ogni piano con lunghezza libera d'inflessione documentata L0:

    Ncr = π² E I / L0²
    λbar = sqrt(A fy / Ncr)
    Φ = [1 + α (λbar − 0,2) + λbar²] / 2
    χ = min(1, 1 / [Φ + sqrt(Φ² − λbar²)])
    Nb,Rd = χ A fy / γM1

Per λbar≤0,2 si assume χ=1. Non si usa la facoltà di omettere l'instabilità
quando NEd<0,04Ncr. Curve: CHS a caldo `a` (`a0` per grado nominale S460),
CHS a freddo `c`, tondo pieno `c`. Si verificano entrambi i piani, anche per
sezioni circolari: le lunghezze possono differire. Il modo torsionale della
sezione circolare centrata non governa prima dello snervamento; eccentricità e
interazione con flessione richiedono un altro percorso. La snellezza geometrica
e la raccomandazione 200/250 vengono esposte, senza trasformare automaticamente
la raccomandazione in una capacità resistente.

## Recupero delle azioni dei sostegni

La forza costitutiva è positiva a trazione per Anchor e Bilateral, positiva a
compressione per Strut. Il risultato conserva anche N con segno fisico, positivo
a trazione. L'inclinazione è quella dell'asse dalla parete verso l'altro estremo,
positiva in basso. Il contributo allo sforzo normale della fascia di paratia è
N sin(α) b/s: tirante diretto in basso comprime la parete, puntone diretto in
basso la tira verso l'alto. Il caso analitico 20 kN, α=30°, b/s=1,5/2 produce
±7,5 kN. La cattura precedente mostrava il segno del tirante anche per Strut;
correzione registrata con test prima/dopo, senza alterare attesi storici.

## Prove e differenze dal percorso storico

`tools/ParatieSteelExpected.py` usa soltanto la libreria standard Python e
geometria analitica di cerchio/corona; crea nuovi attesi senza sovrascriverli.
Gli attesi precedono il kernel e comprendono trazione con fori, due piani di
instabilità, S460 con fy ridotta e formazione a caldo/freddo, tondo compresso.
Tolleranza relativa 2e-12, assoluta minima 1e-9: formule chiuse in double.
Prove ulteriori: classe 4, dati mancanti, profili modificati, annullamento,
isolamento, cambi di fase, limite plastico, forza per elemento e segno.
Un puntone scarico mantiene i controlli di compressione: N=0 non permette di
nascondere lunghezze libere d'inflessione mancanti o una sezione di classe 4.

Le routine storiche EN1993p11Checker contengono segnaposto per area netta ed
efficace. Non sono state modificate, né sono richiamate passando aree lorde
surrogate. Il nuovo percorso esplicito resta nello stesso assembly Steel,
riusa materiali/sezioni/profili Model e restituisce risultati tipizzati.
I 17 fallimenti storici Steel restano censiti e non risultano risolti da questo
incremento. Le verifiche N–M–V delle sezioni della paratia e le altre famiglie
di sostegni restano aperte nella matrice complessiva.
