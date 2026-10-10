BUILD E VERIFICHE DI CHECKER

GPCModelChecker (adattatore tra GPC Model e i motori di verifica) e i suoi test stanno
in questo repository dal 10/10/2026: cartelle ModelChecker e ModelChecker.Tests, spostate
dal repository Model (revisione di architettura, punto R3). Il ciclo tra i due repository
non esiste piu': Model fissa solo Geometry e Utilities, Checker usa GPCModel compilato nel
checkout fratello ../Model come le altre librerie. Namespace (GPC.Model.Checker), nome
dell'assembly e versione di GPCModelChecker sono invariati.

VERIFICA: ./build/Verify.ps1 [-ModelRoot <checkout Model>] [-Suites all|modelchecker] [-Results <cartella>]
Prepara il bundle fissato di Model e compila GPCModel, GPCModelData e GPC.Model.Persistence
in Release; poi tutte le suite di Checker: Test.Concrete e Test.Steel net472 (restore
packages.config, MSBuild senza riferimenti di progetto, vstest x64), CompositeBridge,
Geotechnics, Calculation e ModelChecker.Tests; infine le API pubbliche di GPCModelChecker e
GPCChecker.Concrete contro build/api (baseline e type-renames.tsv spostati da Model/build/api;
lo strumento resta Model/tools/ApiSurface). Un fallimento e' ammesso solo se elencato per
nome in build/known-failures.json (oggi i 17 casi storici di Steel). Scrive summary.json.
Con -Suites modelchecker esegue solo ModelChecker.Tests e il controllo delle API.

RILASCIO: build/RILASCIO.txt (treno unico, versioni assegnate dall'utente) e
./build/Release-Set.ps1 -Out <cartella nuova> [-Root <checkout fratelli>] [-Previous <set>] [-Candidate]
che esegue le verifiche di Model e Checker e produce le dodici DLL con release-set.json.

Build deterministiche: in Release (Directory.Build.props) ContinuousIntegrationBuild mappa
la radice del checkout su /_/, quindi lo SHA-256 di una DLL dipende dai sorgenti, dal commit
e dalle DLL referenziate, non dalla cartella di compilazione (provato il 10/10/2026: nove
DLL identiche compilate in due cartelle diverse). I progetti di test net472 (Test.Concrete,
Test.Steel) non sono progetti SDK e non cambiano.
