---
id: ca.ancoraggi
titolo: Aderenza, ancoraggi e sovrapposizioni delle barre
libreria: GPCChecker.Concrete
classi:
  - GPC.Checkers.Concrete.Detailing.AnchorageCalculator
  - GPC.Checkers.Concrete.Detailing.AnchorageInput
  - GPC.Checkers.Concrete.Detailing.AnchorageResult
  - GPC.Checkers.Concrete.Detailing.DetailingProfiles
  - GPC.Checkers.Concrete.Detailing.DetailingProfile
versione: 0.0.17.0
norme:
  - ntc2018-4.1.2.1.1.4
  - ntc2018-4.1.2.3.10
  - ntc2018-4.1.6.1.4
  - circ2019-C4.1.6.1.4
  - en1992-1-1-3.1.6
  - en1992-1-1-8.4.2
  - en1992-1-1-8.4.3
  - en1992-1-1-8.4.4
  - en1992-1-1-8.7.2
  - en1992-1-1-8.7.3
  - na-uni-2012-3.1.6
  - na-dk-2024-8.4.2
  - cnr-dt200-r1-2013
stato: bozza
---

# Aderenza, ancoraggi e sovrapposizioni delle barre

## 1. Scopo

Il metodo calcola, per una barra ad aderenza migliorata:

- la resistenza tangenziale di aderenza di progetto fbd;
- la lunghezza di ancoraggio di base lb,rqd;
- la lunghezza richiesta di ancoraggio rettilineo lbd oppure di sovrapposizione l0.

Confronta poi la lunghezza richiesta con quella disponibile. Per le sovrapposizioni controlla anche l'interferro
fra le barre sovrapposte: con NTC 2018 come limite, con la famiglia Eurocodice come allungamento della
sovrapposizione.

Il risultato è un esito di dettaglio della singola zona di ancoraggio o di giunzione. Il metodo è una funzione della
libreria: i dati della zona (barra, estremo, lunghezza disponibile, percentuale sovrapposta) sono del chiamante,
perché il modello non li descrive.

## 2. Campo di applicazione

| Caso | Stato | Comportamento del programma |
| --- | --- | --- |
| Barra rettilinea ad aderenza migliorata, tesa | supportato | calcolo di fbd, lb,rqd, lbd e dell'esito sulla lunghezza disponibile |
| Barra rettilinea compressa | supportato | stessa formula delle barre tese; con α1…α5 = 1 il minimo di compressione di EN (8.7) non governa mai, quindi il risultato coincide con la regola corretta (6.3) |
| Sovrapposizione rettilinea in trazione | supportato | α6 dalla percentuale ρ1 di barre sovrapposte; NTC: interferro ≤ 4Ø come verifica; Eurocodice: allungamento della sovrapposizione oltre min(4Ø; 50 mm) |
| Condizioni di aderenza non buone | supportato | η1 = 0,7 su indicazione del chiamante |
| Barre di diametro superiore a 32 mm | supportato | η2 = (132 − Ø)/100; Ø ≥ 132 mm è rifiutato (`ArgumentException`) |
| Barre lisce | non supportato | errore (`NotSupportedException`) |
| Uncini, piegature, barre trasversali saldate, confinamento, pressione trasversale (α1…α5 < 1) | non supportato | coefficienti assunti pari a 1, a favore di sicurezza |
| Fasci di barre, reti elettrosaldate (diametro equivalente) | non supportato | nessun trattamento specifico |
| Armature da precompressione | non supportato | fuori dal metodo |
| Armatura trasversale lungo la sovrapposizione (EN 8.7.4); distanza fra sovrapposizioni adiacenti e interferro fra sovrapposizioni adiacenti (EN 8.7.2(3)) | non supportato | non verificati |
| Limite di fctk,0,05 alla classe C60/75 (EN 8.4.2(2)) | dato del chiamante | il metodo usa il valore ricevuto (riquadro A-3) |
| NTC 2018; CNR-DT 200 R1/2013 (membratura in c.a. secondo NTC) | supportato | ramo NTC |
| EN 1992-1-1 (valori raccomandati); UNI EN 1992-1-1 con DM 31/07/2012; DS/EN 1992-1-1 con DK NA | supportato | ramo Eurocodice; αct è un dato (1 raccomandato e nel DM 31/07/2012) |
| DIN EN 1992-1-1, NS-EN 1992-1-1, Model Code 2010, CNR-DT 204 | non supportato | `DetailingProfiles.Resolve` solleva `NotSupportedException` con il motivo |
| CS-TR34 | non applicabile | motivo restituito da `DetailingProfiles.NotApplicableReason` |
| ACI 318, AASHTO | non supportato | implementazione futura |
| Classi normative derivate da quelle elencate | non supportato | risoluzione per tipo esatto, senza ripiego su un'altra norma |

## 3. Riferimenti normativi

| Formula o grandezza | Norma | Edizione e appendice | Paragrafo | Eq. o tabella | Riscontro |
| --- | --- | --- | --- | --- | --- |
| fbd = fbk/γc, γc = 1,5; fbk = 2,25 η1 η2 fctk; η1 = 1,0 o 0,7; η2 = 1,0 per Ø ≤ 32 mm, (132 − Ø)/100 oltre; rinvio alla sezione 8 di UNI EN 1992-1-1:2015 | NTC | 2018 | 4.1.2.1.1.4 | [4.1.6], [4.1.7] | testo |
| Ancoraggio senza uncini non minore di 20Ø e di 150 mm | NTC | 2018 | 4.1.2.3.10 | — | testo |
| Sovrapposizione non minore di quanto prescritto al §4.1.2.3.10; interferro nella sovrapposizione non maggiore di 4Ø; cautele per Ø > 32 mm | NTC | 2018 | 4.1.6.1.4 | — | testo |
| Sovrapposizione con lo sforzo di entrambe le barre e la percentuale di barre sovrapposte | Circolare | 2019 | C4.1.6.1.4 | — | testo |
| fctm = 0,30 fck^(2/3) fino a C50/60; fctk = 0,7 fctm (dato del chiamante) | NTC | 2018 | 11.2.10.2 | [11.2.3a] | testo |
| fctd = αct fctk,0,05/γc | EN 1992-1-1 | 2004 + AC:2010 | 3.1.6(2)P | (3.16) | testo |
| fbd = 2,25 η1 η2 fctd; limite di fctk,0,05 a C60/75 | EN 1992-1-1 | 2004 + AC:2010 | 8.4.2(2) | (8.2) | testo |
| lb,rqd = (Ø/4)(σsd/fbd) | EN 1992-1-1 | 2004 + AC:2010 | 8.4.3(2) | (8.3) | testo |
| lbd = α1…α5 lb,rqd ≥ lb,min; lb,min in trazione e in compressione | EN 1992-1-1 | 2004 + AC:2010 | 8.4.4 | (8.4), (8.6), (8.7) | testo |
| Interferro delle barre sovrapposte oltre 4Ø o 50 mm: sovrapposizione allungata della differenza | EN 1992-1-1 | 2004 + AC:2010 | 8.7.2(3) | — | testo (riquadro A-2) |
| l0 = α1 α2 α3 α5 α6 lb,rqd ≥ l0,min; α6 = (ρ1/25)^0,5 fra 1 e 1,5 | EN 1992-1-1 | 2004 + AC:2010 | 8.7.3 | (8.10), (8.11), prospetto 8.3 | testo |
| αct = 1,0; sezione 8 con i valori raccomandati | UNI EN 1992-1-1 | DM 31/07/2012 | 3.1.6(2)P | — | testo |
| Regole di aderenza non applicabili alle barre lisce | DS/EN 1992-1-1 | DK NA:2024 | 8.4.2(2) | — | testo |
| Membratura in c.a. secondo NTC: nessun contributo del rinforzo FRP all'ancoraggio delle barre | CNR-DT 200 | R1/2013 | — | — | da riscontrare |
| Aderenza (non implementata) | fib MC2010 | 2013 | 6.1.3 | — | — |

## 4. Ipotesi

- Tensione di aderenza costante pari a fbd lungo tutta la lunghezza di ancoraggio (EN 8.4.3(2)).
- Barra rettilinea e nervata. I coefficienti α1, α2, α3, α4 e α5 di EN 8.4.4 valgono 1: nessun effetto
  favorevole di forma, copriferro, confinamento, barre trasversali saldate o pressione trasversale.
- σsd è la tensione di progetto della barra nella sezione da cui si misura l'ancoraggio. È un dato in valore
  assoluto, non superiore a fyd per costruzione del chiamante.
- Le condizioni di aderenza (buone o non buone) sono un dato. Il metodo non le ricava dalla posizione della
  barra nel getto (EN figura 8.2).
- fctk,0,05 e γc sono dati. Ramo NTC: αct = 1, come in [4.1.6]-[4.1.7]. Ramo Eurocodice: αct è un dato, 1 per valore
  raccomandato e per DM 31/07/2012.
- α6 si calcola dalla percentuale ρ1 di barre sovrapposte con l'espressione continua di EN 8.7.3, anche nel profilo
  NTC: NTC §4.1.2.1.1.4 rimanda alla sezione 8 di UNI EN 1992-1-1 e la Circolare C4.1.6.1.4 chiede di considerare la
  percentuale delle barre sovrapposte. Il prospetto 8.3 ammette l'interpolazione; l'espressione continua dà 1,15 per
  33% e 1,41 per 50%, contro 1,15 e 1,4 del prospetto.

## 5. Notazione, unità e convenzioni

Unità: mm, MPa (N/mm²). Tutte le grandezze sono moduli positivi: il metodo non distingue il segno della tensione
della barra.

| Simbolo | Significato | Unità | Nel codice |
| --- | --- | --- | --- |
| Ø | diametro nominale della barra | mm | `AnchorageInput.Diameter` |
| σsd | tensione di progetto della barra all'inizio dell'ancoraggio | MPa | `AnchorageInput.Stress` |
| fctk,0,05 | resistenza caratteristica a trazione (frattile 5%) | MPa | `AnchorageInput.Fctk05` |
| γc | coefficiente parziale del calcestruzzo | — | `AnchorageInput.GammaC` |
| αct | coefficiente degli effetti di lunga durata sulla resistenza a trazione | — | `AnchorageInput.AlphaCt` |
| η1 | coefficiente delle condizioni di aderenza | — | `AnchorageResult.Eta1` |
| η2 | coefficiente del diametro | — | `AnchorageResult.Eta2` |
| fbd | resistenza tangenziale di aderenza di progetto | MPa | `AnchorageResult.Fbd` |
| lb,rqd | lunghezza di ancoraggio di base | mm | `AnchorageResult.BasicLength` |
| lb,min, l0,min | lunghezze minime di norma | mm | `AnchorageResult.MinimumLength` |
| lbd, l0 | lunghezza richiesta di ancoraggio o di sovrapposizione | mm | `AnchorageResult.RequiredLength` |
| ρ1 | percentuale di barre sovrapposte nella sezione di giunzione, in (0; 100] | % | `AnchorageInput.LapPercent` |
| α6 | coefficiente della percentuale sovrapposta | — | `AnchorageResult.Alpha6` |
| s | interferro netto fra le barre sovrapposte | mm | `AnchorageInput.LapClearDistance` |
| slim | limite (NTC) o soglia di allungamento (Eurocodice) dell'interferro | mm | `AnchorageResult.MaximumLapClearDistance` |
| ldisp | lunghezza disponibile | mm | `AnchorageInput.AvailableLength` |

## 6. Formulazione

### 6.1 Aderenza e lunghezza di base

Coefficiente del diametro, comune ai due rami (NTC [4.1.7]; EN 8.4.2(2)):

```math
\eta_2 = \begin{cases} 1 & \varnothing \le 32\ \text{mm} \\ \dfrac{132 - \varnothing}{100} & \varnothing > 32\ \text{mm} \end{cases} \qquad \text{(A.1)}
```

Resistenza di aderenza di progetto (NTC [4.1.6]-[4.1.7]; EN (8.2) con (3.16)); nel ramo NTC αct = 1:

```math
f_{bd} = \frac{2{,}25\,\eta_1\,\eta_2\,\alpha_{ct}\,f_{ctk,0{,}05}}{\gamma_c}, \qquad \eta_1 = 1{,}0\ \text{(aderenza buona)},\ 0{,}7\ \text{(altrimenti)} \qquad \text{(A.2)}
```

Lunghezza di ancoraggio di base (EN (8.3)):

```math
l_{b,rqd} = \frac{\varnothing\,\sigma_{sd}}{4\,f_{bd}} \qquad \text{(A.3)}
```

Coefficiente della percentuale sovrapposta (EN 8.7.3(1)); per gli ancoraggi vale 1:

```math
\alpha_6 = \min\left[1{,}5;\ \max\left(1;\ \sqrt{\rho_1/25}\right)\right] \qquad \text{(A.4)}
```

### 6.2 Ramo NTC 2018 e CNR-DT 200

Ancoraggio (NTC §4.1.2.3.10, con lb,rqd da EN e α1…α5 = 1):

```math
l_{bd} = \max\left(l_{b,rqd};\ 20\,\varnothing;\ 150\ \text{mm}\right) \qquad \text{(A.5)}
```

Sovrapposizione (NTC §4.1.6.1.4 con EN (8.10) e (8.11)) e interferro (NTC §4.1.6.1.4):

```math
l_0 = \max\left(\alpha_6\,l_{b,rqd};\ 0{,}3\,\alpha_6\,l_{b,rqd};\ 20\,\varnothing;\ 200\ \text{mm}\right), \qquad s \le s_{lim} = 4\,\varnothing \qquad \text{(A.6)}
```

Il termine 0,3 α6 lb,rqd non governa mai, perché α6 lb,rqd ≥ 0,3 α6 lb,rqd.

> **Scostamento dichiarato — A-1 (R13) Minimo della sovrapposizione NTC**
>
> - Norma: NTC 2018 §4.1.6.1.4 richiede una sovrapposizione non minore dell'ancoraggio di §4.1.2.3.10, cioè di 20Ø e
>   di 150 mm.
> - Programma: minimo di 200 mm, il valore di EN (8.11).
> - Effetto: a favore di sicurezza, solo quando α6 lb,rqd < 200 mm e 20Ø < 200 mm, cioè per Ø < 10 mm. Con Ø8 la
>   sovrapposizione minima passa da 160 a 200 mm (+40 mm); con Ø6 da 150 a 200 mm.
> - Stato: dichiarato (voce R13 del registro delle differenze).

### 6.3 Ramo Eurocodice (EN, UNI, DS)

Ancoraggio (EN (8.4) con α1…α5 = 1 e (8.6)):

```math
l_{b,min} = \max\left(0{,}3\,l_{b,rqd};\ 10\,\varnothing;\ 100\ \text{mm}\right), \qquad l_{bd} = \max\left(l_{b,rqd};\ l_{b,min}\right) \qquad \text{(A.7)}
```

Per le barre compresse EN (8.7) porta il primo termine a 0,6 lb,rqd. Con α1…α5 = 1 risulta comunque
lbd = max(lb,rqd; 10Ø; 100 mm) in entrambi i casi, perciò la distinzione non è necessaria.

Sovrapposizione (EN (8.10), (8.11) e 8.7.2(3)):

```math
l_{0,min} = \max\left(0{,}3\,\alpha_6\,l_{b,rqd};\ 15\,\varnothing;\ 200\ \text{mm}\right), \qquad l_0 = \max\left(\alpha_6\,l_{b,rqd};\ l_{0,min}\right) + \max\left(0;\ s - s_{lim}\right), \qquad s_{lim} = \min\left(4\,\varnothing;\ 50\ \text{mm}\right) \qquad \text{(A.8)}
```

> **Scostamento dichiarato — A-2 Soglia dell'interferro nella famiglia Eurocodice**
>
> - Norma: EN 8.7.2(3) indica un interferro non maggiore di «4Ø o 50 mm» senza dire quale dei due valori prevalga.
> - Programma: usa il minore, min(4Ø; 50 mm).
> - Effetto: a favore di sicurezza, solo per Ø > 12,5 mm e interferro compreso fra 50 mm e 4Ø. In quell'intervallo la
>   sovrapposizione si allunga di s − 50 mm: con Ø20 e s = 70 mm, +20 mm.
> - Stato: dichiarato.

### 6.4 Esito

```math
\text{LengthPassed} = \left(l_{disp} \ge l_{req}\right), \qquad \text{Passed} = \text{LengthPassed} \wedge \left(s \le s_{lim}\right)_{\text{solo NTC, solo sovrapposizioni}} \qquad \text{(A.9)}
```

Qui lreq è lbd o l0. Nel ramo Eurocodice il controllo dell'interferro è sempre soddisfatto, perché l'interferro
eccedente è già sommato a l0.

> **Scostamento dichiarato — A-3 (R10) Limite di fctk,0,05 a C60/75 non applicato dal metodo**
>
> - Norma: EN 8.4.2(2) limita fctk,0,05 al valore di C60/75 (3,1 MPa), salvo dimostrazione.
> - Programma: usa il valore ricevuto; il limite spetta al chiamante.
> - Effetto: se il chiamante passa fctk,0,05 di classi superiori, fbd è sovrastimata e le lunghezze sono sottostimate
>   (a sfavore di sicurezza): con C90/105 (fctk,0,05 = 3,5 MPa) lb,rqd risulta l'11% più corta del valore di norma.
> - Stato: da discutere (voce R10 del registro delle differenze).

## 7. Coefficienti e valori predefiniti

| Simbolo | Valore | Fonte | Modificabile | Dove nel codice |
| --- | --- | --- | --- | --- |
| coefficiente di fbd | 2,25 | NTC [4.1.7]; EN (8.2) | no | `AnchorageCalculator.BondStrength` |
| η1 | 1,0 / 0,7 | NTC §4.1.2.1.1.4; EN 8.4.2(2) | tramite il dato `GoodBond` | `AnchorageCalculator.Calculate` |
| η2 | 1 fino a 32 mm, poi (132 − Ø)/100 | NTC [4.1.7]; EN 8.4.2(2) | no | `BondStrength`, `Calculate` |
| αct | 1 (NTC, fisso); dato, predefinito 1 (Eurocodice) | NTC [4.1.6]; EN 3.1.6(2)P; DM 31/07/2012 | sì, ramo Eurocodice (`AlphaCt`) | `AnchorageInput`, `Calculate` |
| γc | dato | NTC §4.1.2.1.1.4 (1,5); EN 2.4.2.4 | sì (`GammaC`) | `AnchorageInput` |
| α1…α5 | 1 | assunzione del metodo | no | `Calculate` |
| α6 | (ρ1/25)^0,5 in [1; 1,5]; 1 per gli ancoraggi | EN 8.7.3(1) | tramite `LapPercent` | `Calculate` |
| minimi NTC dell'ancoraggio | 20Ø; 150 mm | NTC §4.1.2.3.10 | no | `Calculate` |
| minimi NTC della sovrapposizione | 0,3 α6 lb,rqd; 20Ø; 200 mm | NTC §4.1.6.1.4; EN (8.11) (200 mm, riquadro A-1) | no | `Calculate` |
| interferro NTC | 4Ø | NTC §4.1.6.1.4 | no | `Calculate` |
| lb,min Eurocodice | 0,3 lb,rqd; 10Ø; 100 mm | EN (8.6) | no | `Calculate` |
| l0,min Eurocodice | 0,3 α6 lb,rqd; 15Ø; 200 mm | EN (8.11) | no | `Calculate` |
| soglia dell'interferro Eurocodice | min(4Ø; 50 mm) | EN 8.7.2(3) (riquadro A-2) | no | `Calculate` |
| percentuale sovrapposta predefinita | 100% | assunzione del metodo | sì (`LapPercent`) | `AnchorageInput` |

## 8. Implementazione

Percorsi relativi alla radice del repository Checker (namespace `GPC.Checkers.Concrete.Detailing`).

- `DetailingProfiles` (`GPCChecker.Concrete/Detailing/DetailingProfiles.cs`): risoluzione per tipo esatto
  (`TryResolve`, righe 17-29; `Resolve`, righe 45-52); motivi di non applicabilità (righe 31-32) e di non supporto
  (righe 34-43); `IsNtc` (riga 54) raggruppa NTC 2018 e CNR-DT 200; `Reference` (righe 56-67) restituisce il testo di
  riferimento del risultato.
- `AnchorageInput` (`GPCChecker.Concrete/Detailing/AnchorageCalculator.cs:10-34`) e `AnchorageResult` (righe 36-58).
- `AnchorageCalculator.BondStrength` (righe 69-75): (A.1)-(A.2) per qualsiasi η1 e αct; rifiuta valori non finiti o
  non positivi e Ø ≥ 132 mm.
- `AnchorageCalculator.Calculate` (righe 77-117):
  1. controlla i dati (righe 79-84) e η2 (righe 85-86);
  2. rifiuta le barre lisce (riga 88);
  3. calcola η1 (riga 89) e fbd con αct = 1 nel ramo NTC (riga 90);
  4. calcola lb,rqd (riga 91) e α6 (riga 92);
  5. applica il ramo NTC (righe 95-101) o il ramo Eurocodice (righe 102-110), compreso l'allungamento per
     l'interferro (righe 106-107);
  6. compone il risultato con l'espressione applicata e il riferimento normativo (righe 111-116).

Il metodo non ha stato e non alloca risorse condivise: può essere chiamato in parallelo.

## 9. Limiti e casi non supportati

- Solo barre rettilinee nervate. Uncini, piegature, barre saldate, fasci, reti e confinamento non sono
  considerati: α1…α5 = 1 dà lunghezze a favore di sicurezza.
- Nessuna verifica dell'armatura trasversale lungo le sovrapposizioni (EN 8.7.4), dello sfalsamento
  longitudinale (0,3 l0) o dell'interferro fra sovrapposizioni adiacenti (2Ø o 20 mm, EN 8.7.2(3)).
- Barre con Ø > 32 mm: le «particolari cautele» di NTC §4.1.6.1.4 non sono valutate; vale solo η2.
- La percentuale di barre sovrapposte e le condizioni di aderenza sono dati: il metodo non conosce la
  disposizione delle giunzioni né la posizione della barra nel getto.
- Il limite di fctk,0,05 a C60/75 è a carico del chiamante (riquadro A-3).
- DIN, NS, Model Code 2010 e CNR-DT 204 non sono supportati; CS-TR34 non è applicabile.

## 10. Esempio numerico verificato

Dati comuni:

- barra Ø16 in B450C, con σsd = fyd = 450/1,15 = 391,30 MPa;
- calcestruzzo C25/30: fctm = 0,30 · 25^(2/3) = 2,5650 MPa e fctk,0,05 = 0,7 · 2,5650 = 1,7955 MPa;
- γc = 1,5, αct = 1, aderenza buona.

Calcolo a mano:

1. η2 = 1 (Ø ≤ 32 mm); fbd = 2,25 · 1 · 1 · 1,7955/1,5 = 2,6932 MPa.
2. lb,rqd = 16 · 391,30/(4 · 2,6932) = 581,17 mm.
3. Caso A, ancoraggio con ldisp = 600 mm:
   - NTC: lbd = max(581,17; 320; 150) = 581,17 mm ≤ 600 mm, soddisfatto;
   - EN: lb,min = max(174,35; 160; 100) = 174,35 mm, quindi lbd = 581,17 mm.
4. Caso B, sovrapposizione del 100% con s = 30 mm e ldisp = 900 mm:
   - α6 = min(1,5; max(1; √(100/25) = 2)) = 1,5;
   - NTC: l0 = max(871,76; 261,53; 320; 200) = 871,76 mm ≤ 900 mm; s = 30 mm ≤ 4Ø = 64 mm, soddisfatto.
5. Caso C, come B con s = 70 mm:
   - NTC: 70 mm > 64 mm, interferro non soddisfatto;
   - EN: slim = min(64; 50) = 50 mm, l0 = 871,76 + 20 = 891,76 mm ≤ 900 mm, soddisfatto.
6. Caso D, aderenza non buona: fbd = 0,7 · 2,6932 = 1,8852 MPa; lb,rqd = 830,24 mm > 600 mm, non
   soddisfatto.
7. Caso E, sovrapposizione del 50%: α6 = √2 = 1,4142; l0 = 821,90 mm.
8. Caso F, Ø8 con σsd = 100 MPa, sovrapposizione del 100%:
   - lb,rqd = 8 · 100/(4 · 2,6932) = 74,26 mm, α6 lb,rqd = 111,39 mm;
   - NTC: l0 = max(111,39; 33,42; 160; 200) = 200 mm (il testo NTC darebbe 160 mm, riquadro A-1);
   - EN: l0 = max(111,39; max(33,42; 120; 200)) = 200 mm.
9. Caso G, Ø40 con EN: η2 = 0,92; fbd = 2,4778 MPa; lb,rqd = 1579,27 mm.

Valori della libreria (GPCChecker.Concrete 0.0.17.0, eseguita):

| Caso | Grandezza | A mano | Libreria | Scarto |
| --- | --- | --- | --- | --- |
| A | fbd [MPa] | 2,69321 | 2,693212116015797 | < 10⁻¹² |
| A | lb,rqd = lbd [mm] | 581,171 | 581,1712274708805 | < 10⁻¹² |
| A (EN) | lb,min [mm] | 174,351 | 174,35136824126414 | < 10⁻¹² |
| B (NTC) | α6; l0 [mm] | 1,5; 871,757 | 1,5; 871,7568412063207 | < 10⁻¹² |
| C (NTC) | esito dell'interferro | non soddisfatto (70 > 64) | `LapClearDistancePassed` = false | — |
| C (EN) | l0 [mm] | 891,757 | 891,7568412063207 | < 10⁻¹² |
| D | fbd [MPa]; lb,rqd [mm] | 1,88525; 830,245 | 1,8852484812110581; 830,2446106726862 | < 10⁻¹² |
| E | α6; l0 [mm] | 1,41421; 821,900 | 1,4142135623730951; 821,9002319503383 | < 10⁻¹² |
| F (NTC ed EN) | l0 [mm] | 200 | 200 | 0 |
| G (EN) | η2; lb,rqd [mm] | 0,92; 1579,27 | 0,92; 1579,2696398665228 | < 10⁻¹² |

Gli scarti sono al livello dell'arrotondamento in doppia precisione.

## 11. Validazione

- **Casi congelati del motore precedente** (`anchorage-legacy.csv` del progetto di test): 468 righe NTC, cioè 445
  ancoraggi e sovrapposizioni con esito (griglia, valori limite e 300 casi casuali), 5 dati rifiutati e 18
  resistenze di aderenza. Sono confrontati fbd, lb,rqd, la lunghezza richiesta, η1, η2, α6, il limite di 4Ø e i tre
  esiti, con tolleranza relativa 10⁻⁹.
- **Test** di `DetailingMigrationTests`:
  - `LegacyAnchorageAndBondAreReproduced` (i casi congelati);
  - `EurocodeAnchorageAndLapMatchHandCalculation`: calcoli a mano con EN, Ø20, σsd = 391,3 MPa, fctk,0,05 = 2,0 MPa,
    quindi fbd = 3,0 MPa, lb,rqd = 652,2 mm, l0 = 1,5 lb,rqd + 20 mm; anche il minimo NTC di 20Ø con Ø8, η1 = 0,7
    con DS, η2 = 0,92 con UNI e il rifiuto delle barre lisce;
  - `ProfilesAreResolvedByExactTypeWithoutFallback` (risoluzione per tipo esatto e motivi di non supporto).
- **Esempio di questa pagina**: eseguito con la libreria 0.0.17.0 e ricalcolato in modo indipendente, scarto
  inferiore a 10⁻¹².
- **Benchmark indipendenti pubblicati**: nessuno, per ora.

## 12. Bibliografia

- DM 17 gennaio 2018, *Aggiornamento delle «Norme tecniche per le costruzioni»*, §4.1.2.1.1.4, §4.1.2.3.10,
  §4.1.6.1.4.
- Circolare 21 gennaio 2019 n. 7 C.S.LL.PP., §C4.1.6.1.4.
- EN 1992-1-1:2004 + AC:2010, *Eurocode 2: Design of concrete structures — Part 1-1: General rules and rules for
  buildings*, §3.1.6, §8.4, §8.7.
- UNI EN 1992-1-1, appendice nazionale: DM 31 luglio 2012.
- DS/EN 1992-1-1 DK NA:2024, §8.4.2.
- CNR-DT 200 R1/2013, *Istruzioni per la progettazione, l'esecuzione ed il controllo di interventi di
  consolidamento statico mediante l'utilizzo di compositi fibrorinforzati*.
- fib, *fib Model Code for Concrete Structures 2010*, Ernst & Sohn, 2013, §6.1.3.
