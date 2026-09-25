# Curve della sezione

`BridgeSectionResponseAnalysis.Calculate` è un solutore separato dai metodi per
fasi. Accetta una `HistorySection` generica, una sequenza ordinata di valori
assoluti di controllo, le opzioni e un eventuale `HistoryStageResult` iniziale.
Non impone un numero massimo di punti. Sono consentiti inversioni e cicli.

## Controlli e unità

- `MomentCurvature`: κ imposta, N costante, ε₀ risolta per equilibrio; M è il risultato.
- `AxialForceStrain`: ε alla quota `ReferenceY` imposta, κ costante; N e M sono
  risultanti. M è anche la reazione necessaria a mantenere κ, non è imposto nullo.
- Unità dell'API: N, mm, MPa, deformazione adimensionale, curvatura in 1/mm.
- Convenzioni: ε(y) = ε₀ − κy; M alla quota y = M all'origine + N·y.
- `AxialForce = null` mantiene N iniziale; `FixedCurvature = null` mantiene κ
  iniziale. Per una sezione vergine sono zero. Non assegnare il parametro
  relativo all'altro tipo di controllo.

Il solutore conserva deformazione di nascita, deformazioni imposte, deformazione
meccanica e stato plastico di ogni fibra. Non modifica lo stato iniziale passato
dal chiamante. Eventuali componenti inattivi rimangono inattivi. La curva non
introduce nuovi getti o ritiri: questi vanno definiti nello storico di partenza.

Si riusano le leggi costitutive dello storico. Nel controllo M–κ l'equilibrio
scalare di N usa predittore elastico, Newton con tangente, ricerca del passo e
suddivisione adattiva. N–ε integra direttamente il piano prescritto. Solo gli
stati accettati aggiornano la memoria; una prova fallita non la modifica.
Se necessario si equilibra prima la nuova N costante o la nuova κ costante.

La sezione della curva è lorda, con componenti e aree fissi: vengono rifiutati
modelli di area efficace o stati iniziali con area ridotta. Le tangenti sono
algoritmiche; alle cuspidi dipendono dal ramo. Il raggiungimento di un limite
costitutivo o la mancata convergenza non sono una certificazione di capacità.

## Adattatore ad H

`HBridgeSectionResponse.Calculate` costruisce la sezione e accetta valori di
controllo assoluti; `Trace` genera una rampa uniforme dall'ordinata iniziale.
Il suo parametro `increment` è l'escursione complessiva firmata, non il passo.

```csharp
var gross = input with { Options = input.Options with { Class4 = false } };
var options = new SectionResponseOptions {
    Control = SectionResponseControl.MomentCurvature,
    ReferenceY = 0,
    AxialForce = -100_000, // N
    SubstepsPerTarget = 4
};
var historyOptions = new HBridgeHistoryOptions {
    MaterialMode = HistoryMaterialMode.Nonlinear,
    InstantaneousConcrete = true
};
var curve = HBridgeSectionResponse.Trace(
    gross, 0.003 / 1000, 100, options, historyOptions, startPhaseIndex: null);
```

`startPhaseIndex = null` indica sezione vergine interamente composta.
L'indice zero-based non nullo si riferisce alle sole fasi attive: il metodo
riesegue la storia fino a quella fase con le leggi richieste. Non converte uno
stato lineare/cumulativo in uno stato plastico. Restano valide le regole dello
storico per φ/n: la modalità istantanea deve essere scelta esplicitamente.

Per N–ε usare `AxialForceStrain`, `FixedCurvature` e valori di ε adimensionali.
`Trace(..., -0.002, 100, ...)` prescrive un'escursione totale di −2000 µε.

## Risultato e verifiche

`SectionResponseResult` include lo stato iniziale e gli stati accettati,
l'esito `Completed`, `MaterialDomain` o `EquilibriumNotFound`, il primo indice
obiettivo fallito e il messaggio. I punti riportano il raggiungimento del valore
richiesto, piano totale, N/M, tensioni/deformazioni delle fibre e tangenti.
Di default si conservano gli obiettivi e l'ultimo stato valido in caso di arresto;
`IncludeSubsteps` conserva anche i sottopassi.

23 test di comportamento coprono soluzioni elastiche/plastiche analitiche,
scarico/inversione, ripresa dallo storico, getti/ritiri, stato immutabile,
dominio materiale e input errati. Altri 6 test confrontano 35 stati con
OpenSees: [dati, tolleranze e riproducibilità](../../GPCChecker.Test.CompositeBridge/Validation/README.md).
Il dossier di validazione aggiunge 4 casi per i cinque metodi e riferimenti
pubblicati. La suite CompositeBridge completa corrente contiene 154 casi.
