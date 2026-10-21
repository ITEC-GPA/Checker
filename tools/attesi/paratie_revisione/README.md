# Attesi indipendenti della revisione paratie (ottobre 2026)

Script Python scritti durante la revisione del modulo paratie. Il codice C# non è stato usato per produrre i valori attesi,
salvo dove indicato come "port". Esecuzione: `py -I <script>.py`, Python 3.13 con numpy.

| Cartella | Script | Uso nei test |
|---|---|---|
| `..\paratie_indipendente.py` | risolutore a molle concentrate, mesh 50/25 mm e valore di Richardson; equilibrio limite del caso tirantato | `independent-expected.json`, `IndependentVerificationTests`, `ReviewFindingsTests` |
| `stabilita_globale` | `circle_check.py`: Bishop con 400 000 conci; `port_check.py`: port del codice C# (solo per prevederne l'esito); `test_cases.py` stampa tutti i valori | `GlobalStabilityIndependentTests`, `ReviewFindingsTests.GlobalStabilityHonoursPrescribedPorePressures` |
| `filtrazione` | `wallfe.py`: FEM P1 indipendente della mesh paratia; `harr.py`: confronto con Harr; `symmetry.py`, `symmetry_refine.py`: asse di simmetria a flusso nullo; `testcases.py` stampa tutti i valori | `SeepageIndependentTests`, `ReviewFindingsTests.SymmetricExcavationAxisCarriesNoFlow` |
| `sezioni` | `test_cases.py`, `check_sections.py`: formule NTC 2018 e integrazione a fibre | `EmbeddedWallReviewTests` (Design.Checks) |
| `anthea` | conversioni e casi del modulo ANTHEA (pretensione fra le fasi) | casi elencati nel resoconto, da tradurre nei test di ANTHEA |

Resoconto: `docs/verifiche/paratie-revisione-2026-10.md`.
