# Sezioni composte da ponte

Libreria `netstandard2.0`, namespace `GPC.Checkers.CompositeBridge`. Contiene il
calcolo trasferito da ANTHEA e i controlli prima in `GPCChecker.Steel/CompositeBridges`.
Non dipende da ANTHEA, JSON, WPF o CheckerUI. Geometrie e materiali nativi restano
quelli di Model; nel metodo cumulativo le tensioni composte usano il solutore
lineare di Checker.Concrete.

È disponibile anche il **nuovo metodo separato con storico delle deformazioni**:
[`History/HBridgeHistoryAnalysis`](History/README.md). Usa attivazioni dei materiali,
memoria delle deformazioni, ritiri incrementali e leggi lineari/non lineari con
stato; non ha un limite prefissato di fasi. La pagina dedicata spiega le differenze
di formulazione, il campo di applicazione e i test. L'interfaccia ANTHEA permette
di scegliere fra cumulativo, storico lineare e storico non lineare.

Le [curve della sezione](History/SECTION_RESPONSE.md) hanno un'API separata:
M–κ a N costante e N–ε a κ costante, con tensioni e deformazioni delle fibre.
Possono partire dalla sezione vergine o riprendere uno stato dello storico.

## API e unità

`HBridgeSection.Calculate(HBridgeInput, CancellationToken)` restituisce
`HBridgeAnalysisResult`: geometria, materiali, situazioni cumulative, contributi
di fase, proprietà efficaci, tensioni, residui, taglio, pioli, irrigidimenti,
appoggi, saldature, interazione N–M–V, soletta trasversale e fatica.

`HBridgeInput` raggruppa geometria, due file opzionali di barre, fasi, opzioni e
accessori. Materiali: `BridgeMaterialSet` con oggetti Model. Le opzioni sono enum;
le etichette italiane nei risultati mantengono la compatibilità dei report esistenti.
Dimensioni in **mm**, tensioni/moduli in **MPa**, carichi di fase in **kN/kNm**,
ritiro in **microdeformazioni**, flusso aggiuntivo in **N/mm**. I metodi elementari
di `Checks/` e il solutore nativo usano **N, mm, MPa**.

`Phi` e `PsiL` producono `n = Ea/Ecm (1 + PsiL*Phi)`; in alternativa si assegna
`N` con `HomogenizationSource = ModularRatio`. Quest'ultimo `N` è il rapporto
modulare, distinto da `ForceKN`. I carichi sono già combinati: i coefficienti dei
materiali non moltiplicano nuovamente le azioni.

Gli ingressi sono record a proprietà `init`; l'analisi copia le fasi. Le istanze
Model dei materiali sono condivise: non modificarle durante una chiamata.
Costruire una nuova richiesta per ciascuna variazione. I risultati sono un'istantanea
per il chiamante; le liste di risultati mantengono la forma dell'API precedente.

Esempio con materiali ottenuti dai cataloghi Model del chiamante:

```csharp
var input = new HBridgeInput {
    Materials = new(concrete, structuralSteel, rebarSteel),
    Geometry = new() {
        SlabWidth = 3000, SlabHeight = 250, WebHeight = 1800, WebThickness = 14,
        TopWidth = 500, TopThickness = 25, BottomWidth = 700, BottomThickness = 30
    },
    // Barre disabilitate per default; se abilitate richiedono diametro, passo e quota dell'asse.
    Phases = new[] {
        new BridgePhase { Name = "G1", Kind = BridgePhaseKind.SteelOnly, MomentKNm = 1500 },
        new BridgePhase { Name = "G2", MomentKNm = 2000, Phi = 2, PsiL = 1.1 },
        new BridgePhase { Name = "Ritiro", Kind = BridgePhaseKind.Shrinkage,
            ShrinkageMicrostrain = -200, Phi = 2, PsiL = .55 }
    }
};
var result = HBridgeSection.Calculate(input, cancellationToken);
```

## Separazione per sezioni future

- `Analysis/CumulativePhaseAnalysis`: algoritmo generico su stato e contributi
  forniti dall'adattatore. Nessun numero fisso di piastre o anime. Ogni situazione
  riparte dalla sezione lorda e ricalcola tutti gli incrementi sulla medesima
  geometria efficace; restituisce lo stato effettivamente usato per le tensioni.
- `CompositeLinearStressSolver`: accetta una `ReinforcedConcreteSection` Model
  con più parti di acciaio. Restituisce un campo lineare di tensione e deformazione
  fisica dell'acciaio. Campo attuale: N–Mx, assi principali paralleli a x/y,
  unico modulo elastico della carpenteria. Non usa `StrainPlane` per ricostruire
  le deformazioni; rimangono i limiti nativi documentati nell'audit.
- `EffectivePlateReduction`: pannelli interni o sbalzi, indipendenti dalla loro
  posizione; tensioni negative in compressione.
- `CompositeSectionProperties` e `CompositeHomogenization`: composizione delle
  proprietà e conversione φ/n comuni alle diverse topologie.
- `Checks/`: funzioni elementari per resistenze e dettagli, trasferite senza
  duplicati da Steel.
- `HSections/`: costruzione del profilo ad H, sostituzione delle due piastre
  inferiori, assemblaggio delle zone efficaci, ritiro e controlli specifici.

- `Box/BoxDistortion`: torsione e distorsione di una cella singola, senza dipendenze
  dalla sezione ad H.
  - `BoxCell`: cella di Bredt, A0, J = 4A0²/Σ(ℓ/t), q = T/(2A0).
  - `BoxDistortion.Mode`: modo distorsivo con scorrimento nullo delle pareti (Σℓ·V = 0),
    ingobbamento ortogonale a N, Mx e My, I_Dw, rigidezza a telaio K con nodi rigidi,
    momenti d'angolo e carico generalizzato di un torcente applicato come coppia verticale
    alla sommità delle anime. Normalizzazione: media dei valori assoluti delle variazioni
    degli angoli (γ del rettangolo).
  - `PlateDiaphragmStiffness`/`PlateDiaphragmStresses` (piastra con i bordi mossi dalle
    pareti, mesh di elementi piani) e `BracingStiffness`/`BracingForces` (diagonali a X).
  - `Envelope` e `BeamOnFoundation`: trave su suolo elastico con elementi di Hermite, molle
    dei diaframmi, carico distribuito e carico concentrato mobile.

## Cassoncino: torsione, distorsione e diaframmi (1.4)

`BridgeSteelSectionType.Box` con `HBridgeInput.Box.Enabled` attiva i controlli; i torcenti
delle fasi sono `BridgePhase.TorsionKNm` (kNm) e devono essere nulli per H e anima inclinata,
che restano in flessione retta. `BridgeStage.Torsion` contiene flussi per fase, distorsione,
controlli e valori.

- Fasi composte: cella chiusa dalla soletta al piano medio (anime prolungate); J con hc/nG,
  nG = n(1+νc)/(1+νa), dimezzata con soletta esclusa (EN 1994-2 §§5.4.2.2(11), 5.4.2.3(6)).
  Fasi di solo acciaio: cella chiusa dal controvento superiore di spessore equivalente t*.
- q entra nel taglio dell'anima più caricata (EN 1993-1-1 §6.2.7(9)), nell'inviluppo elastico,
  nel fondo (tensione equivalente, imbozzamento, EN 1993-1-5 §7.1(5)), nei pioli di una
  piattabanda, nelle superfici a–a interne e b–b e nell'armatura longitudinale della soletta
  (EN 1992-1-1 §6.3.2(3)).
- Distorsione sulla campata appoggiata con diaframmi d'estremità rigidi: σdw, momenti
  trasversali ai nodi, forze nei diaframmi; σdw oltre il 10% della flessione entra nel
  fondo (EN 1993-2 §6.2.7(3)). Diaframma d'appoggio a taglio e coppia T/e_b degli apparecchi.
- Esclusi: accoppiamento della distorsione con l'ingobbamento torsionale, torsione non
  uniforme del cassone aperto, aste del controvento, irrigidimenti longitudinali del fondo.

Test (`BoxTorsionTests`): rettangolo in forma chiusa (I_Dw = t(b+h)b²h²/96,
K = 24/(b/Dh + h/Dv), K_D = G t b h e 2EA b²h²/L³, carico T/2), trapezio con modello a
telaio indipendente e contro Yoo et al. (SSRC 2015), Hetényi per la trave infinita e
appoggiata, flussi e verifiche calcolati a mano, H invariata.

## Metodo mantenuto e limiti noti

Questa è una migrazione del metodo cumulativo esistente. Conservati 120 tentativi,
tolleranza `1e-7`, rilassamento `0.55`, riferimenti fissi/iterativi di N, calcolo
del ritiro e controlli di equilibrio. Non è stato introdotto il secondo metodo
con storia costruttiva e redistribuzione nel tempo **all'interno di questo metodo
cumulativo**. Il nuovo metodo con storico costruttivo è separato nella cartella
`History/`; l'evoluzione viscosa automatica nel tempo resta da sviluppare.

I difetti già segnalati restano aperti, incluso il metadato `SolverInertia` a
carico nullo nell'adattatore H. Si vedano [audit](../docs/audit-sezioni-miste-ponte.md)
e [approfondimento](../docs/approfondimento-solver-lineare-ponte.md).

## Build e test

Dalla radice Checker, con le dipendenze native nelle cartelle `bin/Release`
già usate dagli altri progetti:

```powershell
dotnet build GPCChecker.CompositeBridge/GPCChecker.CompositeBridge.csproj -c Release
dotnet test GPCChecker.Test.CompositeBridge/GPCChecker.Test.CompositeBridge.csproj -c Release
```

Per provare una distribuzione specifica, passare a entrambi i comandi
`-p:BridgeLibraryDir=<cartella-assoluta-DLL>`. Non copiare le dipendenze native
ricompilate sopra una distribuzione diversa. ANTHEA usa soltanto la nuova DLL
CompositeBridge compilata contro il proprio gruppo di dipendenze, rimasto invariato.

`GPCChecker.Test.CompositeBridge` verifica la libreria senza riferimenti all'app;
`GPCChecker.Test.BridgeAudit` mantiene il controllo integrato degli archivi e
degli otto risultati completi acquisiti prima dello spostamento.
