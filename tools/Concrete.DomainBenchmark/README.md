# Benchmark della ricerca sul dominio

Eseguibile diagnostico .NET 8 Release, separato dalle DLL di produzione. Compila una copia dei sorgenti Concrete con contatori di iterazioni e valutazioni; non modifica i sorgenti né gli attesi dei test. Richiede PowerShell 7, SDK .NET 8 e le dipendenze Model, Geometry e Utilities già compilate in Release. Per generare il report occorre Python 3.9 o successivo, senza pacchetti aggiuntivi.

## Stress block: sola mesh

Il confronto del 23 ottobre 2026 usa come baseline il commit `6edf46a7873cf956ef081170b0f3c5e0cfe7d991` (Concrete 0.18.0.1). La candidata 0.18.0.2 integra geometricamente la fascia attiva sulle celle esistenti e abilita il Newton già disponibile. Barre e profili metallici conservano il trattamento precedente.

Preparare tre cartelle **nuove**, usando per tutti la stessa versione di `Program.cs`, le stesse dipendenze e lo stesso catalogo di sezioni. Nell'esempio i percorsi tra parentesi angolari vanno sostituiti con percorsi assoluti locali:

```powershell
./tools/Concrete.DomainBenchmark/Prepare.ps1 -Output '<audit>/before' -Source '<baseline>/GPCChecker.Concrete' -RepositoryRoot '<repository-root>'
./tools/Concrete.DomainBenchmark/Prepare.ps1 -Output '<audit>/after' -RepositoryRoot '<repository-root>'
./tools/Concrete.DomainBenchmark/Prepare.ps1 -Output '<audit>/integration-only' -RepositoryRoot '<repository-root>' -LegacyStressBlockSearch

./tools/Concrete.DomainBenchmark/RunStressBlock.ps1 `
  -Before '<audit>/before/bin/GPCChecker.Test.Concrete.dll' `
  -After '<audit>/after/bin/GPCChecker.Test.Concrete.dll' `
  -IntegrationOnly '<audit>/integration-only/bin/GPCChecker.Test.Concrete.dll' `
  -Output '<audit>/measurements'

py -3.9 tools/Concrete.DomainBenchmark/StressBlockReport.py '<audit>/measurements' '<verify-results>' '<report-directory>'
```

`<baseline>` deve contenere i sorgenti del commit indicato; `<repository-root>` contiene i checkout fratelli Model, Geometry e Utilities. `<verify-results>/summary.json` proviene dalla verifica completa della candidata con `build/Verify.ps1`. L'opzione `-LegacyStressBlockSearch` interviene **solo nella copia diagnostica**: conserva il vecchio algoritmo di ricerca insieme alla nuova integrazione, per distinguere i due contributi al miglioramento.

La campagna esegue in sequenza:

- 240 casi stress block, prima/dopo: sei sezioni, due domini, cinque criteri, una ricerca iniziale e cinque ripetizioni per caso;
- 120 casi plastici in ordine di esecuzione invertito (dopo/prima), tre ripetizioni;
- 72 casi plastici in compressione con `e/D = 0.05`, sei sezioni e dodici direzioni, cinque ripetizioni;
- 720 casi con diagrammi continui, una ripetizione, per controllare l'invarianza numerica;
- 240 casi con sola integrazione geometrica e vecchia ricerca, tre ripetizioni;
- reintegrazione geometrica dei piani restituiti dalla baseline.

Qui `e = sqrt(Mx² + My²) / abs(N)` e `D` è la massima dimensione della sezione (diametro per le sezioni circolari). Il runtime disabilita tiered compilation e riscalda il JIT prima delle misure. La ricerca iniziale include le cache del solver ancora da costruire; le ripetizioni usano la stessa istanza. Non eseguire build, suite o altri benchmark in parallelo.

## Lettura dei risultati

Le mediane sono calcolate sulle ripetizioni di ciascun caso; i percentili interpolati linearmente descrivono la distribuzione di tali mediane tra casi. Il totale pubblicato è la loro somma, non la durata della campagna. Il tempo comprende ricerca, fallback e validazione interna, ma esclude preparazione della sezione e diagnostica esterna.

`accepted` verifica il punto restituito; `planeAccepted` verifica le forze reintegrate dal suo piano con lo stesso metodo. `geometricPlaneAccepted` verifica invece il piano della baseline usando la nuova integrazione: serve a individuare accettazioni dovute all'errore di quadratura o all'interpolazione del fallback. I confronti sui soli casi riusciti e sui soli piani validi sono riportati separatamente.

Le iterazioni comprendono i tentativi dell'algoritmo principale; `bracketIterations` descrive la ricerca di bisezione. Le valutazioni scalari non comprendono la costruzione vettoriale del dominio nel fallback, il cui costo è incluso nel tempo. Forze e momenti del JSONL sono in kN e kNm; i residui mantengono le unità indicate dal validatore (N, Nmm o rad). `deltaN` è in kN e non è una misura di errore per criteri che consentono la variazione di N.

Le tolleranze pubbliche non sono allentate. Il catalogo è strutturato e correlato, non rappresenta una distribuzione statistica dei modelli di produzione. I tempi .NET 8 servono al confronto controllato; la suite completa verifica anche Concrete su .NET Framework 4.7.2.
