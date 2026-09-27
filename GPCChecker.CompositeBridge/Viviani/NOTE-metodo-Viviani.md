# Metodo Viviani (programma DT-NTC2008 "SezioneComposta")

Calcolo della sezione composta acciaio-calcestruzzo da ponte del prof. Viviani, dal programma decompilato
`Repository/SezioneComposta` (VB.NET per .NET 2.0, calcolo in `Form1.Button1_Click`). È conservato nella libreria
come **storico dei risultati** e come termine di **confronto** con il calcolo lineare di `HBridgeSection`.

## Cosa contiene la libreria

| File | Contenuto |
|---|---|
| `VivianiModels.cs` | Dati (`VivianiInput`) e risultati (`VivianiResult`) nelle unità del programma: cm, cm², kN, kNm, MPa. I colori dei campi sono parte dei risultati (`VivianiColor`). |
| `VivianiMethod.cs` | Il calcolo riscritto in modo leggibile. Formule, ordine delle operazioni e rami sono quelli del programma; i nomi originali delle variabili sono nei commenti. |
| `VivianiComparison.cs` | Conversione dei dati della libreria (mm, fasi) nei dati del metodo e confronto delle tensioni ai 6 punti; momento plastico con l'integrazione esatta della libreria. |

Nei test (`GPCChecker.Test.CompositeBridge/Viviani`):

- `VivianiOriginal.cs` è l'**estrazione meccanica** del calcolo originale, generata dallo script `extract-viviani.ps1`: il codice è quello
  decompilato, con le caselle di testo sostituite da campi.
- `VivianiEquivalenceTests` confronta **bit per bit** riscrittura e originale su 5000 casi casuali e su 18 casi scelti per percorrere tutti
  i rami: tutte le 59 uscite e i 6 colori coincidono.
- `VivianiHistoryTests` congela dati e risultati di 21 casi in `Validation/viviani-storico.json`: il file è lo **storico** e ogni
  modifica che cambia un risultato fa fallire il test.
- `VivianiComparisonTests` confronta il metodo con la libreria (vedi sotto).

Il comportamento del programma **non è stato corretto**: gli errori elencati qui sotto sono riprodotti fedelmente.

## Il metodo in breve

- **Punti**: 1 estradosso soletta (calcestruzzo), 2 armatura, 3 estradosso acciaio, 4 sommità anima, 5 piede anima, 6 intradosso acciaio.
- **Azioni**: tre gruppi, con N, T e M:
  - G1 sulla sola carpenteria (n = ∞);
  - G2 con n∞ (lunga durata);
  - Q con n0 (breve durata).
- **Coefficienti γ**:
  - le tensioni elastiche li usano solo con "Elast.";
  - il momento plastico li usa sempre.
- **Anima efficace** (EN 1993-1-5 §4.4, solo per l'anima):
  - due strisce, be1 dall'alto e be2 dal basso;
  - il numero di iterazioni è fisso (4 per default).
- **Soletta fessurata**: se l'estradosso della soletta è teso sotto G2 + Q, per G2 e Q si usa la sezione fessurata (acciaio + armatura).
- **Momento plastico positivo o negativo**:
  - la classe dell'anima si ricava dall'asse neutro plastico;
  - con T Ed > 0,5 Vpl il fyd dell'anima è ridotto (EN 1993-1-1 §6.2.8).

## Errori (cambiano i risultati)

1. **M Rd con asse neutro plastico nella piattabanda inferiore (M > 0): manca la divisione per 1000.** In questo ramo il momento resta in
   MPa·cm³ invece che in kNm. Il risultato è M Rd 1000 volte maggiore e η = M Ed / M Rd 1000 volte minore: la verifica risulta sempre
   soddisfatta.
   - Il ramo si presenta con una piattabanda inferiore molto più resistente del resto della sezione.
   - Test: `PlasticMomentInTheBottomFlangeIsThousandTimesLarger`. Sui casi casuali è verificato in 87 casi su 87.
2. **M < 0 con tutta la carpenteria compressa (armatura più resistente dell'intera trave).** Il programma pone l'asse neutro alla quota
   dell'estradosso dell'acciaio. Come forza dell'armatura prende nr = (Af fyd + Nacciaio)/2 invece della forza di equilibrio Nacciaio, quindi
   M Rd è sovrastimato di (Af fyd − Nacciaio)/2 · (H − R). È un caso limite poco realistico (7 casi su 20000 casuali).
3. **ε = √(235/fyd) invece di √(235/fy)** in tutte le classificazioni e nella snellezza λp. Con fyd = fy/γM0 = 355/1,05, ε aumenta di
   √1,05 = 2,5%. I limiti c/t delle classi risultano più larghi del 2,5% e λp più piccolo, quindi ρ più grande. Entrambi gli effetti sono a
   sfavore di sicurezza.
4. **η = 1,00 quando M Rd non è calcolato.** Succede per anima di classe 3 o 4 nella verifica plastica, anima compressa troppo snella
   (classe −1) oppure T Ed > Vpl. Il campo mostra "1,00" in nero, che si può leggere come "sfruttamento 100%" invece di "non calcolato".
5. **Con T Ed > Vpl il programma scrive Tw = 0 nella casella dello spessore d'anima.** Un secondo "COMPLETAMENTO" senza riscrivere Tw fa
   i calcoli con Tw = 0 (divisioni per zero, NaN/∞). La riscrittura non modifica i dati: lo segnala con `ShownWebThickness` e
   `ShearExceedsWebResistance`.

## Imprecisioni e limiti del modello

6. **Taglio.**
   - τ = T / ((be1 + be2) tw) usa l'anima **efficace** (ridotta in classe 4), non quella lorda: τ risulta sovrastimato di Hw/(be1 + be2).
     È a favore di sicurezza ma non segue EN 1993-1-5.
   - Vpl = Hw tw fyd/√3 è calcolato senza η e **senza la verifica di instabilità a taglio** (χw, EN 1993-1-5 §5). Per anime snelle
     (Hw/tw > 72ε/η) la resistenza a taglio può essere molto minore.
7. **Classificazione solo dell'anima.**
   - Le piattabande non sono mai classificate né ridotte: una piattabanda superiore in classe 4 (c/t > 14ε) è considerata interamente
     efficace.
   - Manca anche la verifica della piattabanda superiore compressa in fase di getto (G1).
8. **Iterazioni in numero fisso** (4), senza controllo di convergenza.
   - Se a un'iterazione la classe torna 3, il ciclo si ferma e restano le strisce ridotte dell'iterazione precedente.
   - A parità esatta il limite di classe 3 usa "≥" in tre rami e ">" in quello con il piede compresso (differenza solo formale).
9. **Soletta fessurata.**
   - Il criterio guarda solo la fibra superiore della soletta.
   - Con l'estradosso compresso e l'intradosso teso, la soletta è considerata interamente reagente.
   - Una volta fessurata, la sezione fessurata vale per tutte le azioni sulla composta, anche quelle che da sole la comprimerebbero.
10. **Armature.**
    - Sono un solo strato.
    - La loro area si somma alla soletta senza togliere il calcestruzzo occupato (A = Ac/n + As).
    - Hanno il modulo dell'acciaio da carpenteria (Es = Ea): la tensione nelle barre risulta Ea/Es = 1,05 volte quella con Es = 200000 MPa.
11. **Soletta.** La larghezza collaborante (shear lag) non è calcolata: B deve essere già la larghezza efficace.
12. **Sforzo normale.**
    - Il momento plastico è calcolato con N = 0: gli sforzi normali delle tre fasi entrano solo nelle tensioni elastiche.
    - Per le sezioni di classe 3 non c'è una verifica elastica allo SLU: le tensioni sono mostrate ma non confrontate con i limiti.
13. **Dati degeneri** (per esempio senza anima) danno NaN o ∞. Lo storico li conserva così come sono.

## Confronto con il calcolo lineare della libreria

Trave di ponte stradale (soletta 3500×250, anima 1700×16, piattabande 600×30 e 800×40, C35/45, S355, Ø20/100).
Azioni: G1 = 5300 kNm, G2 = 1650 kNm con φ = 2 e ψL = 1,1, Q = 7800 kNm. Tensioni in MPa; "libreria" = `HBridgeSection` sezione lorda.

**Con le stesse ipotesi** (senza armature): i due calcoli coincidono. Il residuo è l'integrazione delle piastre sulla linea media del
solutore Checker, che non considera l'inerzia propria delle piattabande (circa 10⁻⁵ di J).

| Punto | Viviani | Libreria | Differenza |
|---|---:|---:|---:|
| 1 soletta estradosso | −7,008 | −7,008 | 0,000 |
| 3 acciaio estradosso | −162,809 | −162,811 | −0,002 |
| 4 anima sommità | −156,324 | −156,326 | −0,002 |
| 5 anima piede | 211,154 | 211,161 | 0,007 |
| 6 acciaio intradosso | 219,801 | 219,808 | 0,007 |

**Con le armature** (B450C): differenze sotto lo 0,5% sull'acciaio. La tensione nelle barre cambia del 3,4%, quasi tutto per il rapporto
Es/Ea (punto 10).

| Punto | Viviani | Libreria | Differenza |
|---|---:|---:|---:|
| 1 soletta estradosso | −6,483 | −6,572 | −0,089 |
| 2 armatura | −45,903 | −44,367 | 1,537 |
| 3 acciaio estradosso | −158,542 | −159,135 | −0,594 |
| 4 anima sommità | −152,149 | −152,730 | −0,580 |
| 5 anima piede | 210,068 | 210,236 | 0,169 |
| 6 acciaio intradosso | 218,591 | 218,777 | 0,186 |

**Momento negativo**:
- Il metodo fessura la soletta da solo, mentre la libreria segue il tipo di fase.
- Con fasi "Composta" la libreria dà la soletta tesa (5,05 MPa) e tensioni molto diverse.
- Con le fasi G2 e Q impostate come "Soletta esclusa" la libreria coincide con il metodo entro l'1%.

**Classe 4** (anima 2200×12):
- Il metodo riduce solo l'anima, con ε da fyd, in 4 iterazioni.
- La libreria riduce anche le piattabande, con ε da fy, fino a convergenza.
- Differenze sulle tensioni dell'acciaio entro l'1%.

**Momento plastico**: su 20000 casi casuali il valore del metodo coincide con l'integrazione esatta a blocchi rettangolari della
libreria (`BridgeBendingShear.PlasticMoment`, calcestruzzo solo per M > 0 come il metodo) in 7918 casi su tutti i rami (asse neutro in
soletta, all'armatura, in piattabanda superiore, in anima, in piattabanda inferiore con M < 0). Le eccezioni sono i due rami degli errori
1 e 2.
