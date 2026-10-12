# Portanza statica delle fondazioni superficiali

Stato 12 ottobre 2026: API sorgente in `GPCChecker.Geotechnics/Foundations/ShallowFoundationBearing.cs`, non ancora in un release set approvato. Nessun cambio di versione. Nessun motore preesistente modificato.

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

Hansen: il fattore di forma non drenato inclinato riportato a §3.27 del manuale sembra discontinuo rispetto alla specializzazione a carico normale. Non è stato possibile accertare una correzione sulla pubblicazione originale; questa rimane una questione aperta, non un bug accertato di Checker. Il ramo inclinato è esplicitamente escluso. Il ramo normale è additivo. Le correlazioni sc/sq e dc/dq dell'appendice sono verificate algebricamente per una correzione per volta: non provano equivalenza dei prodotti di più fattori. Il nuovo codice dichiara la variante correlata, non modifica formule altrui come se l'equivalenza fosse universale.

Meyerhof 0°–10°: interpolazione degli incrementi di forma/profondità q e γ prevista nella tabella USACE 4-3. Carichi inclinati ammessi solo per striscia per evitare combinazioni non riscontrate con fattori di forma. Hansen/Vesić: k passa da D/B ad atan(D/B) oltre uno, come nella formulazione prescelta; il salto empirico non è nascosto con un raccordo numerico.

## Cattura e prove

Nessuna API di portanza generale esisteva: il risultato precedente del nuovo modulo è «funzionalità assente», non un numero da sovrascrivere. I sorgenti di portanza dei muri, sisma e cedimenti non sono modificati. La suite geotecnica esistente passa 108/108 casi con i suoi attesi invariati. Le evidenze sono in `ANTHEA/supporto/artefatti/fondazioni-superficiali/checker/`.

Nuovi test: `tests/ShallowFoundation.Tests/BearingTests.cs`; attesi indipendenti in `independent.py`, che verifica costanti congelate e non importa l'assembly. Tabelle pubblicate arrotondate: tolleranza assoluta 0,011 per coprire l'arrotondamento a due decimali; casi analitici: tolleranza relativa 1e-10, valori prossimi a φ=0 confrontati con il limite con tolleranza 1e-7. Controlli di inclinazione, scambio assi, eccentricità, falda drenata/non drenata, singolarità, overflow e campi dei metodi. Le routine `Log1p`/`Expm1` evitano cancellazione; non cambiano dominio o segno.

Errori accertati del codice preesistente: nessuno. Difetti trovati e corretti durante lo sviluppo della nuova API: confronto della grande eccentricità con il lato sbagliato dopo lo scambio degli assi; mancato controllo di overflow area/resistenza; convenzione iniziale efficace nel non drenato sostituita con la convenzione a tensioni totali di D.3 prima di qualsiasi rilascio. Non sono regressioni pubblicate.

## Rilascio ancora da eseguire

Assegnazione versione da parte dell'utente, commit e push autorizzato, tutte le suite previste dal treno e creazione del set coerente con `build/Release-Set.ps1`. Solo quel set può aggiornare ANTHEA. La nuova suite è inclusa nel runner `build/Verify.ps1`. Nessun riferimento a build locali deve entrare nello snapshot distribuito.

## Controllo con il manuale Califfo (12 ottobre 2026)

Confronto formula per formula e numerico con il manuale. `tests/ShallowFoundation.Tests/califfo_reference.py` ricava gli attesi dalle formule del manuale, senza importare il motore, e li congela in `califfo-reference.json` (426 casi); `CaliffoReferenceTests` li confronta con tolleranza relativa 1e-9. Le formule implementate coincidono con il manuale; nessun errore a sfavore di sicurezza.

Decisioni dell'utente, applicate con `ContractVersion` "2":

- azioni oltre il dominio: «Non verificata» con resistenza nulla, invece dell'eccezione (vedi Contratto e perimetro);
- Hansen non drenato: le correzioni additive stanno in `FoundationTerm.AdditiveShape`, `AdditiveDepth` e `AdditiveInclination` (`Additive` vero, `AdditiveFactor` = 1 + s′c + d′c − i′c); `Shape`, `Depth` e `Inclination` restano 1. Per ogni termine vale Pressure = resistenza × N × Shape × Depth × Inclination × AdditiveFactor;
- Meyerhof sotto 10°: interpolazione mantenuta. Verificata sulla tabella 4-3 di USACE EM 1110-1-1905, che la prescrive per 0 < φ ≤ 10°; il manuale lascia indefiniti i fattori. Il risultato lo dichiara negli avvisi.

Convenzioni del motore non prescritte dal manuale, registrate nel riferimento con il valore letterale e il verso:

- quando l'eccentricità scambia B′ e L′, i fattori di profondità usano il lato geometrico allineato a B′ invece della B geometrica: −3/−6% sulla capacità, a favore di sicurezza;
- EC7 su pianta quadrata eccentrica: fattori di forma da B′/L′ invece della formula della quadrata, che il manuale applica alla geometria reale: −3/−4%, a favore di sicurezza;
- Meyerhof con θ > φ′: iγ = 0 (USACE tab. 4-3), dove il manuale scrive solo (1 − θ/φ′)²: a favore di sicurezza.

Non implementati rispetto al manuale, sempre con eccezione esplicita: fattori di inclinazione della base e del pendio (b, g), Meyerhof inclinato su pianta non nastriforme, fattori di compressibilità di Vesić da indice di rigidezza (4.3–4.6), Nγ di Salgado, adesione ca di Bowles, Terzaghi con D > B.

Lavori in coda: Hansen con carico inclinato, drenato (3.23–3.25, calcolo nelle due direzioni con il minore) e non drenato (3.27).
