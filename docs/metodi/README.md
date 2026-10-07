# Pagine dei metodi di GPCChecker.Concrete

Ogni pagina descrive un metodo di calcolo così come la libreria lo implementa: formule, coefficienti, ipotesi,
limiti, scostamenti rispetto alla norma, un esempio numerico eseguito contro la libreria e i casi di validazione.
La pagina è la revisione tecnica del metodo: descrive ciò che il codice fa, non ciò che la norma consentirebbe.

## Indice

| Id | Titolo | Classi principali | Stato |
| --- | --- | --- | --- |
| [ca.taglio](ca.taglio.md) | Taglio di sezione, con e senza armatura trasversale | `SectionShearCalculator` | bozza |
| [ca.torsione](ca.torsione.md) | Torsione e interazione taglio-torsione | `SectionTorsionCalculator` | bozza |
| [ca.sle-tensioni](ca.sle-tensioni.md) | Limiti tensionali in esercizio | `StressLimitCheck` | bozza |
| [ca.fessurazione](ca.fessurazione.md) | Fessurazione: requisiti, ampiezza delle fessure, aree efficaci, interassi | `SectionCrackCheck`, `CrackWidthCalculator` | bozza |
| [ca.ancoraggi](ca.ancoraggi.md) | Aderenza, ancoraggi e sovrapposizioni delle barre | `AnchorageCalculator` | bozza |
| [ca.dettagli](ca.dettagli.md) | Dettagli costruttivi di travi e pilastri | `MemberDetailingCalculator` | bozza |
| [ca.durabilita-copriferri](ca.durabilita-copriferri.md) | Durabilità, classi di esposizione e copriferri | `ExposureClasses`, `CoverRequirements` | bozza |
| [ca.momento-curvatura](ca.momento-curvatura.md) | Risposta momento-curvatura a sforzo normale costante | `MomentCurvatureAnalysis` | bozza |

La pressoflessione (dominio di rottura e verifica di resistenza della sezione) avrà una pagina con lo stesso
template.

## Convenzioni

- **Nome del file**: `<id>.md`. L'id è stabile e non si riusa: prefisso `ca.` per il calcestruzzo armato, poi il
  nome del metodo in minuscolo con trattini.
- **Front-matter YAML** in testa a ogni pagina, con i campi:
  - `id`: uguale al nome del file;
  - `titolo`;
  - `libreria`: `GPCChecker.Concrete`;
  - `classi`: elenco dei tipi pubblici che implementano il metodo, con il namespace;
  - `versione`: versione dell'assembly descritto (per esempio `0.0.17.0`);
  - `norme`: id del registro normativo riportato sotto;
  - `stato`: `bozza`, `in revisione` o `approvata`.
- **Lingua e numeri**: italiano; virgola decimale nel testo e nelle formule (in LaTeX `0{,}18`).
- **Unità**: N, mm, MPa; momenti in Nmm; angoli in gradi; aree in mm².
- **Segni**: come nel codice, compressione negativa per forze assiali, tensioni e deformazioni. Dove una formula
  normativa usa la compressione positiva (per esempio σcp nel taglio) la pagina lo dichiara accanto alla formula.
- **Formule**: blocchi ` ```math ` in LaTeX, numerati per pagina con una lettera (T taglio, R torsione, E tensioni
  in esercizio, F fessurazione, A ancoraggi, D dettagli, C durabilità e copriferri, M momento-curvatura) e con la
  fonte accanto. Ogni simbolo è definito nella tabella della notazione.
- **Riferimenti al codice** (`file:riga`): solo nella sezione Implementazione, relativi alla radice del repository
  Checker (o del repository Model per le classi di Model e per il verificatore di modello).
- **Esclusi dal testo**: comandi d'interfaccia, cronaca di sviluppo, percorsi di repository fuori dalla sezione
  Implementazione, riferimenti a programmi di terzi, testi di terzi. Sono ammesse le citazioni normative e la
  bibliografia tecnica dei metodi implementati.
- **Riscontro delle fonti**: nella tabella dei riferimenti normativi la colonna "Riscontro" vale
  - `testo`: formula verificata sul testo della norma;
  - `testo NTC 2008`: verificata sul testo del DM 14/01/2008, con la clausola corrispondente di NTC 2018 ancora da
    ripetere sul testo 2018;
  - `fonte secondaria`: verificata su una documentazione tecnica che riporta l'annesso, non sul testo ufficiale;
  - `da riscontrare`: non ancora verificata su una fonte; il valore è quello del codice;
  - `—`: scelta del metodo, senza una formula di norma da riscontrare.

  Una nota tra parentesi può precisare il riscontro (per esempio il riquadro che tratta la differenza).

## Riquadri

Ogni punto in cui il codice si discosta dalla norma, o la interpreta in un modo non univoco, ha un riquadro nella
sezione della formula interessata:

> **Scostamento dichiarato — <codice> <titolo>**
>
> - Norma: clausola e formula della norma.
> - Programma: ciò che il codice calcola.
> - Effetto: segno (a favore o a sfavore di sicurezza) ed entità, con un esempio o una statistica.
> - Stato: `dichiarato` (scelta accettata), `in verifica` (riscontro normativo in corso), `da discutere`
>   (decisione aperta nel registro delle differenze), `da riscontrare` (fonte non ancora verificata) oppure
>   `corretto in <versione>`.

Il codice del riquadro ha il prefisso della pagina (T, R, E, F, A, D, C, M) e un numero; i riquadri delle decisioni
D7 del refactoring riportano anche la lettera della scheda, per esempio "F-1 (D7-a)", e quelli delle voci del
registro delle differenze di ANTHEA il numero della voce, per esempio "T-1 (R4)".

## Template della pagina

Ogni pagina ha queste sezioni, in quest'ordine.

1. **Scopo**: che cosa calcola il metodo e che cosa restituisce.
2. **Campo di applicazione**: tabella con i casi supportati e non supportati e il comportamento del programma
   (calcolo, errore, avviso, esito incompleto).
3. **Riferimenti normativi**: per ogni formula e per ogni profilo normativo la norma, l'edizione, l'appendice
   nazionale, il paragrafo, la tabella o l'equazione e lo stato del riscontro.
4. **Ipotesi**: ipotesi meccaniche e di modello.
5. **Notazione, unità e convenzioni**: tabella dei simboli con unità; segni e assi come nel codice.
6. **Formulazione**: equazioni numerate con la fonte e le differenze tra i profili; riquadri degli scostamenti.
7. **Coefficienti e valori predefiniti**: simbolo, valore, fonte, se è modificabile dal chiamante, dove sta nel codice
   (tipo e metodo).
8. **Implementazione**: classi, metodi, algoritmo, iterazioni e tolleranze, riferimenti `file:riga`.
9. **Limiti e casi non supportati**.
10. **Esempio numerico verificato**: calcolo a mano con i passaggi, valore ottenuto eseguendo la libreria della
    versione indicata, scarto.
11. **Validazione**: casi congelati e test esistenti con i conteggi; benchmark indipendenti.
12. **Bibliografia**.

## Registro degli id normativi

L'id di un riferimento è `<documento>-<paragrafo>`, con il paragrafo come stampato nella norma.

| Prefisso | Documento | Edizione considerata |
| --- | --- | --- |
| `ntc2018` | Norme tecniche per le costruzioni, DM 17 gennaio 2018 | 2018 |
| `circ2019` | Circolare 21 gennaio 2019 n. 7 C.S.LL.PP., istruzioni per l'applicazione delle NTC 2018 | 2019 |
| `en1992-1-1` | EN 1992-1-1, Eurocodice 2, parte 1-1, con valori raccomandati | 2004 + AC:2010 |
| `na-uni-2012` | Appendice nazionale italiana di UNI EN 1992-1-1, DM 31 luglio 2012 | 2012 |
| `na-din` | DIN EN 1992-1-1/NA, appendice nazionale tedesca | da riscontrare (2011 o successiva) |
| `na-dk-2024` | DS/EN 1992-1-1 DK NA, appendice nazionale danese | 2024 |
| `na-ns` | NS-EN 1992-1-1 NA, appendice nazionale norvegese | da riscontrare (2008 o 2010) |
| `mc2010` | fib Model Code for Concrete Structures 2010 | 2013 |
| `cnr-dt200-r1-2013` | CNR-DT 200 R1/2013, rinforzo con FRP | 2013 |
| `cnr-dt204-2006` | CNR-DT 204/2006, calcestruzzo fibrorinforzato | 2006 |
| `uni-en206-1` | UNI EN 206-1, calcestruzzo: specificazione, prestazione, produzione e conformità | 2006 |
| `uni11104` | UNI 11104, specificazioni complementari per l'applicazione della EN 206 | 2016 (prospetto 5) per i valori del codice; 2025 (prospetto 6) in vigore dal 24/07/2025; 2004 ritirata |
