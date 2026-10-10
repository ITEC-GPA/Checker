BUILD E VERIFICHE DI CHECKER

GPCModelChecker (adattatore tra GPC Model e i motori di verifica) e i suoi test stanno
in questo repository dal 10/10/2026: cartelle ModelChecker e ModelChecker.Tests, spostate
dal repository Model (revisione di architettura, punto R3). Il ciclo tra i due repository
non esiste piu': Model fissa solo Geometry e Utilities, Checker usa GPCModel compilato nel
checkout fratello ../Model come le altre librerie. Namespace (GPC.Model.Checker), nome
dell'assembly e versione di GPCModelChecker sono invariati.

Eseguire: ./build/Verify.ps1 [-ModelRoot <checkout Model>]
Prepara il bundle fissato di Model, compila GPCModel, GPCModelData e GPC.Model.Persistence
in Release, esegue ModelChecker.Tests e confronta le API pubbliche di GPCModelChecker e
GPCChecker.Concrete con build/api (baseline e type-renames.tsv spostati da Model/build/api;
lo strumento resta Model/tools/ApiSurface).

Build deterministiche: in Release (Directory.Build.props) ContinuousIntegrationBuild mappa
la radice del checkout su /_/, quindi lo SHA-256 di una DLL dipende dai sorgenti, dal commit
e dalle DLL referenziate, non dalla cartella di compilazione. I progetti di test net472
(Test.Concrete, Test.Steel) non sono progetti SDK e non cambiano.
