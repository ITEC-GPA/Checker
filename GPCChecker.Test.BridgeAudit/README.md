# Audit delle sezioni miste da ponte

## Aggiornamento 28 settembre 2026 (CompositeBridge 1.4.0.0)

Le otto baseline sono state riacquisite dopo l'introduzione della torsione del cassoncino.
Rispetto alle precedenti cambiano soltanto il testo del campo di validità (`Scope`), le nuove
chiavi di ingresso del cassoncino (tutte inattive) con `T = 0` nelle fasi e il campo
`Torsion` nullo delle situazioni: nessun valore numerico diverso. Esito: **302 superati**.

## Stato al 27 settembre 2026 (Checker.Concrete 0.0.13.0)

I tre difetti del solutore descritti nelle sezioni storiche sono corretti e i test
relativi non hanno più `Ignore` né la categoria `KnownBug`:

- **Assi di riferimento delle forze ruotati.** Il solutore integra negli assi −X, −Y
  (Mx = −Σσ(y − y0), My = Σσ(x − x0)). Prima confrontava queste risultanti con le
  componenti negli assi scelti dall'utente. Ora le forze sono convertite negli assi
  del solutore e i punti del dominio sono riportati negli assi dell'utente.
  Con gli assi −X, −Y (ANTHEA, CompositeBridge, PileChecker) i risultati non cambiano.
- **Profilo inglobato nel calcestruzzo, analisi lineare.** Il calcestruzzo spostato
  dal profilo era sottratto con la tensione di progetto del diagramma non lineare.
  Ora è sottratto con quella lineare Ec·ε, come per le barre.
- **`StrainPlane.GetNeutralAxis`.** L'asse restituito non considerava il punto di
  riferimento del piano, quindi non stava su ε = 0. Ora ci sta. In più, con ε0 = 0
  la vecchia costruzione per intercette dava una direzione NaN; ora non più.

Esito: **302 superati, nessuno ignorato**, con le DLL ANTHEA aggiornate.

## Migrazione in CompositeBridge — stato attuale

Il motore è ora in [GPCChecker.CompositeBridge](../GPCChecker.CompositeBridge/README.md).
I riferimenti alle vecchie posizioni ANTHEA/Steel nelle sezioni storiche sottostanti
descrivono le precedenti esecuzioni. Questo progetto verifica l'adattatore archivio
ANTHEA e la DLL distribuita; la suite `GPCChecker.Test.CompositeBridge` chiama
direttamente la libreria senza dipendere dall'app.

- Suite ordinaria: **267 superati**, inclusi 8 confronti di risultati completi
  acquisiti prima della migrazione (`Baselines/bridge-*.json`).
- Audit completo sulle DLL distribuite: **282 casi, 267 superati, 5 falliti,
  10 ignorati**. I cinque fallimenti sono i quattro costruttori con H nullo
  nelle DLL Model precedenti alla correzione e il metadato di inerzia a carico
  nullo. Gli attributi `Ignore` dei dieci casi nativi erano già presenti e non
  sono stati cambiati durante la migrazione. Non è una suite completa verde.
- Nuova suite autonoma: **28 superati**, sia sulle dipendenze distribuite sia
  sulle build sorgenti. Include più anime/pannelli, proprietà del rettangolo cavo,
  riferimenti di N, φ/n, ritiro, armature opzionali e annullamento.

Il filtro ordinario resta `TestCategory!=KnownBug&TestCategory!=ConstructorRegression`.
Le istantanee congelano il comportamento precedente, comprese le limitazioni;
non sostituiscono gli oracoli indipendenti dei test meccanici.

Suite aggiunta il 25/09/2026. Il solo costruttore Model con H nullo è stato corretto
su richiesta dell'utente; i solver Checker e ANTHEA non sono modificati.
Tutti i nuovi test di calcolo e il [rapporto con i rilievi](../docs/audit-sezioni-miste-ponte.md)
sono nel repository Checker. Il risultato aggiornato è nell'
[approfondimento del solver](../docs/approfondimento-solver-lineare-ponte.md).

## Due esecuzioni complementari

- `GPCChecker.Test.Concrete/BridgeElasticStagesTests.cs` e
  `BridgeLinearSolverAuditTests.cs`: 86 casi delle API native,
  integrati anche nel progetto storico .NET Framework 4.7.2. Il file è collegato,
  senza duplicazioni, nel presente progetto.
- `BridgeModuleStagesTests.cs`: 65 casi delle fasi e delle larghezze efficaci del
  modulo ANTHEA. Il riferimento a `../../ANTHEA/X.Core/X.Core.csproj` permette di
  provare il codice effettivo senza copiarlo o spostarlo durante l'audit.

Il runner .NET 8 usa le DLL `../../ANTHEA/lib/Checker`, ossia il gruppo distribuito
con l'app. Richiede i repository Checker e ANTHEA affiancati. Il progetto storico
usa invece i riferimenti già presenti nel suo `.csproj`; non sostituire DLL
singole per far coincidere due ambienti. I test nativi registrano percorso e
SHA-256 delle librerie caricate nel TRX.

## Esecuzione da questa cartella

```powershell
dotnet test GPCChecker.Test.BridgeAudit.csproj -c Release --logger trx --results-directory ../TestResults/BridgeAuditSnapshot
```

Esito aggiornato sulle DLL ANTHEA: **151 test, 136 passati, 15 falliti, nessuno ignorato**.
Undici fallimenti sono marcati `KnownBug` (tre difetti Checker e il metadato ANTHEA
a carico nullo). Quattro sono `ConstructorRegression`: la correzione è nel sorgente
Model, mentre questo runner usa le vecchie DLL ANTHEA, volutamente non sostituite.
I test contengono il comportamento atteso,
senza `Ignore` e senza trasformare l'eccezione errata in un test verde.
Per rieseguire solo i difetti:

```powershell
dotnet test GPCChecker.Test.BridgeAudit.csproj -c Release --filter TestCategory=KnownBug
```

Per il controllo dei casi attualmente corretti sulle DLL ANTHEA, escludendo
esplicitamente i difetti aperti e la correzione non ancora distribuita
(non equivale a una suite completa verde):

```powershell
dotnet test GPCChecker.Test.BridgeAudit.csproj -c Release --filter 'TestCategory!=KnownBug&TestCategory!=ConstructorRegression'
```

Progetto storico, dalla radice Checker, con MSBuild e VSTest di Visual Studio 2022.
Compilare prima Model per includere il costruttore corretto:

```powershell
dotnet build ../Model/Model/GPCModel.csproj -c Release
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe' GPCChecker.Test.Concrete/GPCChecker.Test.Concrete.csproj /t:Build /p:Configuration=Release /v:minimal /nologo
& 'C:/Program Files/Microsoft Visual Studio/2022/Community/Common7/IDE/Extensions/TestPlatform/vstest.console.exe' GPCChecker.Test.Concrete/bin/Release/GPCChecker.Test.Concrete.dll /Platform:x64 '/TestCaseFilter:TestCategory=Bridge|TestCategory=BridgeStages' /Logger:trx /ResultsDirectory:TestResults/BridgeSourceAudit
```

Esito sorgente: **102 casi, 92 passati e 10 falliti**. Sono 86 casi nativi e 16
Bridge preesistenti. I quattro casi del profilo nullo passano; i dieci fallimenti
riproducono i tre nuovi difetti del solver non corretti.
Non sommare le due esecuzioni come test distinti: gli 86 casi nativi sono condivisi.

## Criteri dei test

Unità interne N, mm, MPa; nell'adattatore ANTHEA gli ingressi sono kN e kNm.
Gli oracoli ricavano area, baricentro, inerzia e tensioni da rettangoli e aree
concentrate, mantenendo distinti Es ed Ea e sottraendo il CLS sostituito dalle barre.
Il confronto con Checker usa l'integrazione delle pareti sulla linea media,
non l'inerzia geometrica completa dei rettangoli. Le tolleranze native sono
relative 1E-5, con termine assoluto 1E-7; le proprietà geometriche usano 1E-10.
Il benchmark JRC usa le tolleranze coerenti con i valori pubblicati arrotondati.

`ApiCharacterization` e `ApproximationCharacterization` documentano scelte
esistenti, con valori scritti nel TRX: non ne attestano la validità per ogni
applicazione. Le prove di classe 4 includono coefficienti tabulati, un esempio
indipendente JRC, inversione dei bordi, zona tesa, convergenza ed equilibrio.
Non costituiscono validazione completa di un ponte, né verifiche di connettori,
fatica, taglio, instabilità globale o redistribuzione nel tempo.


## Taglio e connessione da norma

`BridgeShearConnectionTests.cs` aggiunge 44 casi derivati da NTC/EC: coefficienti di instabilità, pioli, irrigidimenti, interazione M–V nel campo N=0/fy≤355, scorrimento NTC/EC, fasi e completezza del report. La suite ordinaria ora passa 220 test; i casi KnownBug e ConstructorRegression mantengono i filtri già documentati.

I metodi nuovi sono in `GPCChecker.Steel/CompositeBridges`. Formule, fonti primarie, limiti e due difetti preesistenti a taglio sono descritti in [revisione normativa ANTHEA](../../ANTHEA/supporto/docs/taglio-pioli-fonti-e-metodo.md). Non è stata modificata l’implementazione preesistente dei checker.

## Irrigidimenti, appoggi e connessione

`BridgeLocalDetailsTests.cs` aggiunge 39 casi: geometria asimmetrica, eccentricità,
secondo ordine, instabilità, pannelli, terminali, saldature, armatura trasversale,
ancoraggi NTC/EC, fatica e inviluppo elastico N–M–V. Sono inclusi oracoli indipendenti,
invarianza delle fasi, dati opzionali e comportamento del report.

La suite ordinaria aggiornata passa **259 test**, mantenendo le esclusioni esplicite
`KnownBug` e `ConstructorRegression` già illustrate. Non è una dichiarazione di
correzione dei difetti storici. Metodi e campo:
[dettagli locali ANTHEA](../../ANTHEA/supporto/docs/irrigidimenti-appoggi-connessione.md).
