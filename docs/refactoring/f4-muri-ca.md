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
