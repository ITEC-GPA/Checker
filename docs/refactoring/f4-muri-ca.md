# F4.7a — sezioni dei muri in Concrete/Walls

13 ottobre 2026. Prima API additiva, senza attivazione in ANTHEA.

`WallStrip` descrive una striscia rettangolare in mm, con facce e membro
tipizzati, armature indipendenti, copriferro netto e input immutabile.
Le aree sono mm² della striscia, le azioni future N/Nmm totali sulla larghezza:
nessuna conversione implicita da valori per metro. Geometrie impossibili,
valori non finiti e membri/facce sconosciuti vengono rifiutati.

`WallStripDetailing` porta i controlli longitudinali minimi/massimi esistenti.
La policy storica del muro usa il maggiore d per entrambe le facce e metà
del minimo verticale sul fusto; è dichiarata, non sostituita implicitamente
dal controllo generico per faccia. I coefficienti comuni sono estratti in
`LongitudinalReinforcementLimits` e riusati da `MemberDetailingCalculator`.
Nessuna duplicazione della formula della percentuale minima.

Gli esiti separano domanda/capacità/rapporto, metodo, unità, disponibilità,
esecuzione e giudizio con gli enum di Model. Non certificano ancoraggi,
sovrapposizioni, armature orizzontali o legature trasversali.

## Confronti

Fixture osservata `tests/Concrete.Checks/Fixtures/wall-detailing-f4.csv`:
576 sezioni estratte dai file immutabili F4 di ANTHEA, con SHA dei sorgenti
verificati contro `tests/fixtures/f4/reference.json`. Cattura originaria
5cb01170c7ae3f4874e73a23584a91818bfebb9a; nessun atteso è prodotto dal nuovo codice.
SHA-256 CSV: 0B7F0DCAE45F0F3ED9FFF434751912927EF71DA54B2E142A63B4ED9B84DFCA3A.
Tolleranza nuova fissata prima della prova: 1e-12 relativa (scala minima 1 mm²),
per il solo confronto delle aree. Esiti esatti; tutti i 576 casi equivalenti.

Quattro nuovi test: corpus, vettore indipendente asimmetrico, minimo fusto/
fondazione, input invalidi e immutabilità. Suite mirata con i controlli
preesistenti di dettagli/ancoraggi/piastre: **23 PASS**, nessun fallimento.
Artefatti ANTHEA `supporto/artefatti/refactoring/f4/wall-strip-a`.
Concrete candidata **0.3.0.0**: secondo componente incrementato per API nuova.

Restano F4.7b (N–M, taglio, SLE/curvature), F4.7c (confronto del percorso
completo e adattatore) e F4.9 per il set coerente e l'attivazione.
La formula locale del taglio in ANTHEA non è ancora cambiata (F4-03).

## F4.7b.1 — N–M e SLE equivalenti

`WallSectionSession` possiede una sezione preparata e le sue tre analisi
(GPC non lineare ULS, lineare senza trazione e lineare integra richiesta solo
quando serve). Il chiamante ne trasferisce la proprietà esclusiva. La mesh
non viene ricostruita; geometria, barre, materiale e profilo sono verificati.
Azioni immutabili in N/Nmm, N negativo a compressione. Cache di capacità
per N esatto e segno; annullamento prima/dopo il solutore, chiamate serializzate
per sessione. Materiali/profilo non vanno mutati dopo il trasferimento.

Riuso di LegacySectionCalculation, DomainPointAxialTolerance, StressLimitCheck
e SectionCrackCheck; nessun nuovo solutore o formula di fessurazione. Gli
stati limite sono enum. La prova integrativa di sezione non fessurata considera
solo i motivi tipizzati previsti, senza interpretare testi tradotti. L'errore
del solutore conserva Data=Ready quando gli input erano disponibili.

Il banco ANTHEA `baseline/f4-wall-port` è opzionale con `-F4CandidateConcrete`:
crea una copia isolata dei binari di test, registra SHA della candidata e usa
le API nuove via reflection solo nel test. Non sostituisce DLL in lib/Checker
né nei binari applicativi. I riferimenti catturati sono verificati con SHA
prima di ogni confronto; nessuna modifica di attesi o tolleranze.

Confronto: 26 muri, 576 sezioni, 286 combinazioni e 62568 quantità confrontate
(domande, capacità, rapporti, disponibilità e curvature); nessuna differenza
oltre la tolleranza già in uso 1e-9 assoluta + 1e-9 relativa. Scarto massimo
7.105427357601002e-15. Il corpus non esercita la prova integrativa non fessurata:
non se ne dichiara una copertura numerica completa. I dieci nuovi test di
sessione e quelli esistenti di dettagli danno **33 PASS**.

Le prove iniziali che pretendevano antisimettria numerica esatta dai solutori
iterativi sono state corrette: entrambi i versi devono essere validi e vengono
confrontati separatamente con i valori congelati, senza allargare tolleranze.
Concrete candidata **0.4.0.0**, incremento del secondo componente per la nuova
API di sessione. Percorso runtime ANTHEA ancora invariato.

### Difetto emerso: curvatura con viscosità

Il muro legacy usa il gradiente di StrainPlane senza il fattore 1+phi.
`StressAnalysisResult.GetVerticeStrain(point, phi)` include invece tale fattore
per le deformazioni totali. Esempio C30, striscia 1000×400 mm, 6phi16/faccia,
copriferro 40 mm, N=-20 kN, M=20 kNm: gradiente equivalente 3.0544466e-7/mm
con phi=2, contro 7.5335540e-7/mm con phi=0. La curvatura totale del primo caso
è tre volte il gradiente, coerente con la differenza di deformazione/altezza.

In questo commit si conserva la curvatura equivalente, dichiarata nell'API,
per dimostrare il port. Correzione separata F4.7b.3, con atteso indipendente e
quantificazione sugli spostamenti; non si modifica la tolleranza per nascondere
il difetto. Il taglio con sigma_cp resta il passo separato F4.7b.2.

## F4.7b.2 — taglio con compressione assiale

`CheckShear` passa alla API esistente SectionShearCalculator lo sforzo normale
concomitante (N negativo in compressione), Ac e l'armatura della faccia tesa.
Le resistenze del materiale sono passate come valori assoluti: Fck nel modello
materiale è negativo, nel contratto di taglio è positivo. Profili, limiti e
rifiuto della trazione provengono da GPC, senza altra formula nel servizio.
La resistenza di sezione non certifica ancoraggi o applicabilità senza staffe.

Confronto con il file Python indipendente F4.3b, protetto da SHA-256:
**12512 verifiche**, tutte corrispondenti; 4192 capacità cambiano rispetto al
legacy, fino a +4.301578125 kN/m (+2.80197%), senza cambiamenti di giudizio nel
corpus. Il banco completo ora confronta 87592 quantità, zero differenze non
ammesse. I cinque attesi sintetici precedenti restano invariati; **22 test
mirati PASS**, compresi compressione, limite, trazione e annullamento.
Artefatti ANTHEA `wall-shear-native-final` e runner `20261013-f4-wall-shear-final`.

Concrete candidata **0.4.0.1**: incremento dell'ultimo componente per la
correzione F4-03, distinta dal port equivalente. L'attivazione in ANTHEA è
ancora F4.9; i risultati dell'applicazione corrente non sono cambiati.
