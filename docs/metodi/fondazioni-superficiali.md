# Portanza statica delle fondazioni superficiali

Stato 12 ottobre 2026: API in `GPCChecker.Geotechnics/Foundations/ShallowFoundationBearing.cs`, GPCChecker.Geotechnics 0.1.3.0, `ContractVersion` 2. Nessun motore preesistente modificato.

## Contratto e perimetro

`ShallowFoundationBearing.Calculate(ShallowFoundationInput)` usa mm, N, Nmm, MPa, N/mm³ e radianti. Le azioni sono già combinate al piano di posa, inclusi peso della fondazione e ricoprimento. N è efficace nel drenato (sottospinta già sottratta), totale nel non drenato (pressione interstiziale alla base non sottratta). q segue la stessa convenzione. La distinzione è essenziale quando γR agisce sulla resistenza.

Terreno resistente omogeneo, base e terreno orizzontali; forme rettangolare, nastriforme e circolare centrata. L'input della striscia è riferito a un metro. I casi geometrici o meccanici non supportati lanciano eccezioni; nessun risultato parziale viene presentato come verifica. Le azioni oltre il dominio del metodo (risultante esterna alla base, H ≥ N + A′c′ cot φ′, fattore di inclinazione negativo, H > A′cu nell'EC7 non drenato) non sono eccezioni: il risultato ha `DomainExhausted`, resistenza nulla, esito «Non verificata» e il motivo negli avvisi (manuale Califfo §3.4.1: capacità portante nulla).

Profilo NTC 2018: riuso di `StandardNTC2018Geotechnics.Combinations(ShallowFoundationBearing, PersistentTransient)`. M1 e γR=2,3 dalla libreria Model, una sola applicazione. Profilo caratteristico: γR=1, nessun esito normativo. L'allegato D è quello della EN 1997-1:2004/AC:2009, non quello della seconda generazione. Non è implementato un generatore di combinazioni A1.

## Mappa delle funzionalità

| Tema | Codice precedente | Nuovo contributo e limiti |
|---|---|---|
| Portanza statica generale | Assente | Nuova API, cinque famiglie con variante identificata |
| Muri, portanza drenata | `Walls/RetainingWallAnalysis.cs`, fondazione nastriforme c′=0 | Conservato; non trasferito perché contratto e γR differiscono |
| Sis­ma | `ShallowFoundationSeismic.cs`, EC8 2004 F, striscia incoerente | Conservato; non esteso a geometrie o coesioni non documentate |
| Cedimenti | `FoundationSettlement.cs` | Conservato; non fa parte della verifica di portanza |
| NTC, coefficienti | Model, StandardNTC2018Geotechnics | Riutilizzato direttamente |
| Compressibilità | Nessuna API generale specifica individuata | Riduzioni parametri Terzaghi/Vesić; non fattori da indice di rigidezza |

## Fonti e lettura critica

Il PDF fornito `Califfo___Manuale_teorico_26.01.2013.pdf` è stato letto integralmente (24 pagine), comprese appendici, confrontando le formule renderizzate con PDFium. Le immagini di lavoro restano nelle evidenze ANTHEA e non vengono distribuite. Poppler non riproduce correttamente alcuni simboli incorporati nel PDF; il solo testo estratto non è stato usato per decidere indici o segni.

Fonti tecniche:

- USACE EM 1110-1-1905, *Bearing Capacity of Soils*, 30 ottobre 1992, capitolo 4, tabelle 4-1–4-6. Pubblicazione primaria dell'ente, acquisita da [copia integrale](https://vulcanhammer.net/wp-content/uploads/2017/01/em_1110-1-1905.pdf). Il server ufficiale restituiva 403. Le tavole non rendono identiche le varianti degli autori.
- [JRC85029, Eurocode 7 Geotechnical Design, Worked examples, 2013](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2021-12/2013_06_WS_GEOa.pdf), riferimenti alle formule e all'area efficace.
- EN 1997-1:2004/AC:2009, allegato D e §6.5.4; [testo EN 1997-1:2004 ospitato dalla conferenza NGM](https://www.ngm2016.com/uploads/2/1/7/9/21790806/eurocode_7_-_geotechnical_designen.1997.1.2004.pdf).
- [DM 17 gennaio 2018, Gazzetta Ufficiale](https://www.gazzettaufficiale.it/eli/id/2018/02/20/18A00716/sg), capitolo 6, §§6.2.4 e 6.4.2.1, tabella 6.4.I: A1+M1+R3, γR=2,3 per carico limite. Non confondere con il γR dei muri.

Terzaghi Nγ: lacuna della pagina 6 sostituita con interpolazione lineare della tabella USACE 4-1, dichiarata come tale. Nessuna formula interpolante attribuita a Terzaghi senza fonte. Le dimensioni efficaci mancanti a §2.2 sono quelle della EN1997-1, con segni assoluti e controllo di positività. §§3.7–3.8: nessun automatismo per scegliere resistenza di picco/critica e nessuna assunzione generale che Nγ sia trascurabile. Il capitolo sismico incompleto non è stato riempito inventando un'estensione del modello statico.

Hansen: il fattore di forma non drenato inclinato riportato a §3.27 del manuale sembra discontinuo rispetto alla specializzazione a carico normale. Non è stato possibile accertare una correzione sulla pubblicazione originale; questa rimane una questione aperta, non un bug accertato di Checker. Dalla 1.16.0 il ramo inclinato segue il manuale alla lettera, con la discontinuità dichiarata (sezione «Hansen con carico inclinato»). Il ramo normale è additivo. Le correlazioni sc/sq e dc/dq dell'appendice sono verificate algebricamente per una correzione per volta: non provano equivalenza dei prodotti di più fattori. Il nuovo codice dichiara la variante correlata, non modifica formule altrui come se l'equivalenza fosse universale.

Meyerhof 0°–10°: interpolazione degli incrementi di forma/profondità q e γ prevista nella tabella USACE 4-3. Carichi inclinati ammessi solo per striscia per evitare combinazioni non riscontrate con fattori di forma. Meyerhof non drenato con carico inclinato: ic = iq = (1 − θ/90°)² anche sul termine del sovraccarico, qlim = cu Nc sc dc ic + q iq (Meyerhof 1963; USACE tab. 4-3, «ic = iq» per ogni φ; con φ = 0 valgono solo Nq = sq = dq = 1). Hansen/Vesić: k passa da D/B ad atan(D/B) oltre uno, come nella formulazione prescelta; il salto empirico non è nascosto con un raccordo numerico.

## Cattura e prove

Nessuna API di portanza generale esisteva: il risultato precedente del nuovo modulo è «funzionalità assente», non un numero da sovrascrivere. I sorgenti di portanza dei muri, sisma e cedimenti non sono modificati. La suite geotecnica esistente passa 108/108 casi con i suoi attesi invariati. Le evidenze sono in `ANTHEA/supporto/artefatti/fondazioni-superficiali/checker/`.

Nuovi test: `tests/ShallowFoundation.Tests/BearingTests.cs`; attesi indipendenti in `independent.py`, che verifica costanti congelate e non importa l'assembly. Tabelle pubblicate arrotondate: tolleranza assoluta 0,011 per coprire l'arrotondamento a due decimali; casi analitici: tolleranza relativa 1e-10, valori prossimi a φ=0 confrontati con il limite con tolleranza 1e-7. Controlli di inclinazione, scambio assi, eccentricità, falda drenata/non drenata, singolarità, overflow e campi dei metodi. Le routine `Log1p`/`Expm1` evitano cancellazione; non cambiano dominio o segno.

Errori accertati del codice preesistente: nessuno. Difetti trovati e corretti durante lo sviluppo della nuova API: confronto della grande eccentricità con il lato sbagliato dopo lo scambio degli assi; mancato controllo di overflow area/resistenza; convenzione iniziale efficace nel non drenato sostituita con la convenzione a tensioni totali di D.3 prima di qualsiasi rilascio. Non sono regressioni pubblicate.

## Rilascio ancora da eseguire

Assegnazione versione da parte dell'utente, commit e push autorizzato, tutte le suite previste dal treno e creazione del set coerente con `build/Release-Set.ps1`. Solo quel set può aggiornare ANTHEA. La nuova suite è inclusa nel runner `build/Verify.ps1`. Nessun riferimento a build locali deve entrare nello snapshot distribuito.

## Profili comuni e rappresentazioni

L'input opzionale SoilProfile è direttamente GPC.Model.Geotechnics.SoilProfile. Soil e SoilLayer sono le classi di Model già usate da AxialPileSurvey; nessun dominio terreno duplicato. Si integra il ricoprimento con VerticalTotalStress/VerticalEffectiveStress, si considera il livello inferiore all'interfaccia della base e si richiedono parametri omogenei sotto di essa, con ipotesi esplicita di semispazio. Il profilo non è alterato; i vecchi input scalari restano utilizzabili senza profilo. La falda del profilo è autoritativa.

FoundationGraphics calcola il contatto convenzionale p=max(0,a+bx+cy) equilibrando N, My e Mx. Coordinate e pressioni normalizzate; integrali esatti sul poligono, Newton con ricerca del passo, residuo <1e-11. Non convergenza: avviso senza grafico, nessuna soluzione forzata. La pressione non è una soluzione elastica del semispazio né una verifica di cedimento; l'area compressa è distinta dall'area efficace di Meyerhof. Per il cerchio centrato p è uniforme. Esempi analitici: ex=0.5 m su 2×3 m e N=600 kN danno area compressa 4.5 m² e pmax=266.666667 kPa, contro A′=3 m²; con ex=0.75 ed ey=1.125 m il triangolo ha area 0.75 m² e pmax=2400 kPa.

La rappresentazione di Prandtl è solo un riferimento per terreno privo di peso: α=π/4+φ/2, β=π/4−φ/2, r0=B′/(2cosα), r=r0 exp(θ tanφ), 0≤θ≤π/2. Le due sezioni non compongono una superficie tridimensionale. Niente ottimizzazione critica né effetti di H, D o γ nel disegno. Fonte primaria sul limite della cinematica rispetto al termine di peso: Hjiaj, Lyamin e Sloan, Numerical limit analysis solutions for the bearing capacity factor Nγ, IJSS 42 (2005) 1681–1704, DOI 10.1016/j.ijsolstr.2004.08.002, §1 e §5, figura 6. Copia dell'autore: https://www.newcastle.edu.au/__data/assets/pdf_file/0006/22596/72_Numerical-limit-analysis-solutions-for-the-bearing-capacity-factor-N-gamma.pdf . Le figure distribuite sono originali e non riproducono quelle della pubblicazione.

Verifiche aggiunte: GraphicsTests (7 casi indipendenti) e SoilProfileTests (6 casi: integrale drenato/non drenato, riferimento altimetrico, interfaccia, proprietà eterogenee, dominio e identità del caso omogeneo), oltre ai 10 casi iniziali. Nessuna modifica degli attesi originari o dei motori legacy. Output ANTHEA in supporto/artefatti/fondazioni-superficiali/visualizzazioni.

## Controllo con il manuale Califfo (12 ottobre 2026)

Confronto formula per formula e numerico con il manuale. `tests/ShallowFoundation.Tests/califfo_reference.py` ricava gli attesi dalle formule del manuale, senza importare il motore, e li congela in `califfo-reference.json` (426 casi; 471 dalla 1.16.0 con Hansen inclinato); `CaliffoReferenceTests` li confronta con tolleranza relativa 1e-9. Le formule implementate coincidono con il manuale, salvo le convenzioni elencate sotto; nessun errore a sfavore di sicurezza. Il 15/10/2026 la revisione dei bug ha trovato che il termine q del Meyerhof non drenato inclinato seguiva il manuale ma non Meyerhof/USACE (sovrastima): corretto per decisione dell'utente (GEO-1).

Decisioni dell'utente, applicate con `ContractVersion` "2":

- azioni oltre il dominio: «Non verificata» con resistenza nulla, invece dell'eccezione (vedi Contratto e perimetro);
- Hansen non drenato: le correzioni additive stanno in `FoundationTerm.AdditiveShape`, `AdditiveDepth` e `AdditiveInclination` (`Additive` vero, `AdditiveFactor` = 1 + s′c + d′c − i′c); `Shape`, `Depth` e `Inclination` restano 1. Per ogni termine vale Pressure = resistenza × N × Shape × Depth × Inclination × AdditiveFactor;
- Meyerhof sotto 10°: interpolazione mantenuta. Verificata sulla tabella 4-3 di USACE EM 1110-1-1905, che la prescrive per 0 < φ ≤ 10°; il manuale lascia indefiniti i fattori. Il risultato lo dichiara negli avvisi.
- Meyerhof non drenato con carico inclinato (decisione del 15/10/2026, correzione GEO-1): si segue Meyerhof (1963)/USACE e non il manuale. Il termine del sovraccarico è q iq con iq = ic = (1 − θ/90°)² (`Surcharge.Inclination`), come nel ramo drenato; il manuale (3.16) scrive + q e la (3.14) dà ic = iq solo per φ > 0. Prima il motore seguiva il manuale e risultava discontinuo con il drenato a φ′ → 0 e c′ = cu, con una sovrastima di q (1 − iq). Esempio: striscia B = 2 m, D = 3 m, q = 54 kPa, cu = 75 kPa, N = 300 kN/m, H = 150 kN/m: qlim = 249,04 + 26,83 = 275,87 kPa invece di 303,04 kPa (−9,0%); con H = 0 nulla cambia. Gli altri metodi non drenati restano con + q (EC7 D.3, Vesić, Hansen, Terzaghi).

Convenzioni del motore non prescritte dal manuale, registrate nel riferimento con il valore letterale e il verso:

- quando l'eccentricità scambia B′ e L′, i fattori di profondità usano il lato geometrico allineato a B′ invece della B geometrica: −3/−6% sulla capacità, a favore di sicurezza;
- EC7 su pianta quadrata eccentrica: fattori di forma da B′/L′ invece della formula della quadrata, che il manuale applica alla geometria reale: −3/−4%, a favore di sicurezza;
- Meyerhof con θ > φ′: iγ = 0 (USACE tab. 4-3), dove il manuale scrive solo (1 − θ/φ′)²: a favore di sicurezza;
- Meyerhof non drenato con H > 0: q iq invece di + q (decisione GEO-1 sopra): a favore di sicurezza. In `califfo_reference.py` è una convenzione con il valore letterale del manuale e il verso «le»; `califfo-reference.json` è stato rigenerato dallo script (cambiano solo i casi 387, 389 e 391, Meyerhof non drenato a striscia con Hx = 40, 150 e 200 kN);
- Hansen inclinato su pianta circolare (1.16.0): H allineata a B′ (HB = |H|, HL = 0), perché il cerchio non ha un asse privilegiato; il manuale non dà regole per il cerchio e il valore letterale del riferimento scompone H su x e y. Casi 434, 448 e 467: −2,8%, −3,7% e −0,8%, a favore di sicurezza.

Non implementati rispetto al manuale, sempre con eccezione esplicita: fattori di inclinazione della base e del pendio (b, g), Meyerhof inclinato su pianta non nastriforme, fattori di compressibilità di Vesić da indice di rigidezza (4.3–4.6), Nγ di Salgado, adesione ca di Bowles, Terzaghi con D > B.

Lavori in coda dal 12/10: Hansen con carico inclinato, drenato (3.23–3.25, calcolo nelle due direzioni con il minore) e non drenato (3.27). Fatto nella 1.16.0: vedi sotto.

## Hansen con carico inclinato (29 ottobre 2026, 1.16.0)

Prima `ShallowFoundationBearing` rifiutava Brinch Hansen con H > 0 (`NotSupportedException`). Ora segue il manuale alla lettera (pagine 10–13, lette sulle pagine renderizzate con PDFium). Fonte secondaria di riscontro: USACE EM 1110-1-1905 (1992), tab. 4-5, «Data from Hansen 1970», con gli stessi fattori di inclinazione drenati (esponente 5, coefficienti 0,5 e 0,7 − η/450) e non drenati (½ − ½ √(1 − H/(A′ca)), con ca ≤ c). La pubblicazione originale (Brinch Hansen 1970, Danish Geotechnical Institute, Bulletin 28) non era disponibile; Bowles (*Foundation Analysis and Design*) non è stato consultato. I punti che dipendono solo dall'originale sono **da riscontrare sul testo**.

Drenato, (3.23)–(3.25), base e piano campagna orizzontali (η = 0), x = H/(N + A′c′ cot φ′):

- iqB = (1 − 0,5 xB)⁵, iγB = (1 − 0,7 xB)⁵, icB = iqB − (1 − iqB)/(Nq − 1); nella direzione di L′ gli stessi con HL;
- sqB = 1 + iqB (B′/L′) sin φ′, scB dalla legge di correlazione, sγB = 1 − 0,4 iγB B′/(iγL L′), comunque ≥ 0,6; dqB e dcB come nel carico normale, con D/B;
- direzione di L′: sqL = 1 + iqL (L′/B′) sin φ′, sγL = 1 − 0,4 iγL L′/(iγB B′) ≥ 0,6, dqL con D/L′. Nel termine di peso resta 0,5 γ B′ Nγ: il manuale fa variare solo i pedici. USACE §4-3d(3) usa invece la lunghezza quando H è parallela a L: non adottato, sarebbe a sfavore di sicurezza;
- B′L′ del manuale è l'area efficace A′ (π B²/4 per il cerchio).

Non drenato, (3.26), (3.27), (3.27′), (3.27″), correzioni additive (`FoundationTerm.AdditiveShape`, `AdditiveDepth`, `AdditiveInclination`):

- i′cB = 0,5 − 0,5 √(1 − HB/(A′cu)), s′cB = 0,2 i′cB B′/L′, d′cB = 0,4 k(D/B);
- direzione di L′: i′cL con HL, s′cL = 0,2 i′cL L′/B′, d′cL = 0,4 k(D/L′);
- qlim = (π + 2) cu (1 + s′c + d′c − i′c) + q, come il carico normale (3.26).

Regole comuni:

- con H > 0 la capacità si calcola per rottura nella direzione di B′ e in quella di L′ e si adotta la minore (§3.4.1: «il calcolo … dovrà in tal caso essere effettuato 2 volte»). La direzione di L′ si calcola anche con HL = 0, lettura letterale e più cautelativa; la nastriforme ha solo la direzione di B′. HB e HL sono le componenti parallele a B′ e L′ dopo l'eventuale scambio degli assi; sul cerchio H è allineata a B′ (convenzione sopra);
- dominio: un fattore di inclinazione negativo (drenato) oppure HB o HL oltre A′cu (non drenato) danno capacità nulla, `DomainExhausted` (§3.4.1). Nel drenato decide il segno dei fattori, senza il limite H ≥ N + A′c′ cot φ′ di EC7 e Vesić: con c′ = 0 e φ′ = 30° ic diventa negativo per H > 0,88 N circa;
- gli avvisi del risultato riportano qlim nelle due direzioni, la direzione che governa, qlim con H = 0 e la discontinuità. Nessun campo nuovo nel risultato: documenti salvati e impronte di ANTHEA cambiano solo per la versione della libreria. Con H = 0 risultati e `Reference` restano quelli di prima.

Discontinuità con il carico normale (dichiarate, a favore di sicurezza, non risolte):

1. **Forma non drenata.** Il manuale scrive s′cB = 0,2 i′cB B′/L′ con l'i′c additivo, nullo per H = 0, e nota che con il carico normale vale invece (3.28) s′c = 0,2 B′/L′. Per H → 0 la correzione di forma passa da 0,2 B′/L′ a zero. USACE tab. 4-5 dà s′c = 0,2 B′/W′ senza fattori di inclinazione, ma esclude l'uso congiunto dei fattori di forma e di inclinazione: con H > 0 la forma vale «unity», cioè s′c = 0, valore ancora più basso del manuale. Una variante continua (s′c = 0,2 B′/L′ anche con H > 0) non ha fonte che la sostenga ed è a sfavore di sicurezza rispetto al manuale (fino al +17% nell'esempio sotto): non adottata, resta una domanda per l'utente.
2. **Direzione di L′ con HL = 0.** Senza inclinazione, con forma L′/B′ (sγL limitato a 0,6) e profondità D/L′, non tende a (3.19)–(3.20) per H → 0. Governa nel non drenato appena H > 0 (1 + d′cL < 1 + d′cB) e nel drenato quando prevale il termine di peso (fondazione superficiale su sabbia). Limitarla ai casi con HL > 0 sarebbe meno cautelativo e non è adottato.

Esempi (rettangolo B = 2 m, L = 3 m, D = 1 m, N = 600 kN, γ = 18 kN/m³, confronto caratteristico):

| Caso | H = 0 | H lungo B | Direzione di B′ | Direzione di L′ | Adottato |
|---|---|---|---|---|---|
| drenato, φ′ = 30°, c′ = 0 | 704,29 kPa | 60 kN | 522,63 | 798,16 | 522,63 |
| drenato, D = 0 | 198,92 kPa | 6 kN | 194,47 | 162,75 | 162,75 (−18,2%) |
| non drenato, cu = 75 kPa | 532,16 kPa | 1 N | 480,74 | 455,04 | 455,04 (−14,5%) |
| non drenato, cu = 75 kPa | 532,16 kPa | 60 kN | 469,20 | 455,04 | 455,04 |

Prove: `CaliffoReferenceTests` con 45 casi nuovi (426–470) calcolati da `califfo_reference.py` senza importare il motore; i 426 casi esistenti sono invariati. `BearingTests.HansenWithAnInclinedLoad`: striscia e H → 0, simmetria di quadrato e cerchio, scambio degli assi, le due discontinuità, dominio.
