# Regole per gli agenti (librerie GPC: Checker)

- **Versioni**: le assegna l'utente. Non cambiare `AssemblyVersion`, `Version` o `FileVersion` senza averglielo chiesto; sui branch di lavoro le versioni restano quelle di master.
- **Rilascio**: un solo treno per Utilities, Geometry, Model e Checker, descritto in `build/RILASCIO.txt`. Il set coerente lo produce `build/Release-Set.ps1`; nessun rilascio senza tutte le suite, comprese quelle net472 di Concrete e Steel (`build/Verify.ps1`). Fallimenti ammessi solo per nome in `build/known-failures.json`.
- **Dipendenze**: Checker sta a valle di Model. `GPCModelChecker` (cartelle `ModelChecker` e `ModelChecker.Tests`) usa GPCModel dal checkout fratello `../Model`; Model non referenzia Checker.
- **Gmsh** (GMsh.Net, UnsafeEx, `gmsh-*.dll`, GMesh) solo negli unit test, mai nelle DLL delle librerie.
- **Attesi e fixture**: non rigenerare mai un atteso o una fixture di regressione per far passare un test, in particolare il contratto K0 di Concrete (`Fixtures/model-checker-contract.json`), senza l'ok dell'utente. Segnalare i bug invece di correggerli in silenzio.
- **Git**: ogni lavoro parte da master aggiornato su un branch dedicato; merge su master e push solo con l'ok dell'utente.
