# Azioni climatiche — contratto e verifica

Implementazione aggiuntiva in `GPC.Design.Actions.Climate`, ingressi e risultati in
`GPC.Model.Actions.Climate`. Nessun codice di verifica esistente è spostato o modificato.
Le quantità restituiscono simbolo, descrizione, unità, formula e riferimento; i casi
alternativi non vengono sommati. I risultati sono caratteristici, senza γ o ψ globali.

## Edizioni effettivamente implementate

- Italia: DM 17 gennaio 2018, NTC §§3.3–3.5 e 5.1.3; Circolare 21 gennaio 2019 n.7, C3.3.8.
- EN 1991-1-3:2003 + AC:2009: §§5.2–5.3, valori raccomandati. Il modulo **non dichiara** l’adozione dell’emendamento A1:2015 o della seconda generazione.
- EN 1991-1-4:2005 + A1:2010 + AC:2010: §§4–7, tabelle 7.1–7.4.
- EN 1991-1-5:2003 + AC:2009: §5 e appendice D; §6.1, Approccio 1, figura 6.1 corretta, tabelle 6.1–6.2.
- CNR-DT 207 R1/2018: G.11.1–G.11.2, equazioni G.25–G.27 e riferimento 3.14.

Le edizioni EN sono di **prima generazione**. La scelta EN richiede paese e fonte
dei dati nazionali: non è implementato un archivio di appendici nazionali e non
si dichiara conformità automatica a una specifica AN. sk, vb,0 e Tmin/Tmax EN
sono dati assegnati. I coefficienti tabellari sono quelli raccomandati EN.
L’eventuale impiego di coefficienti EN a complemento NTC è dichiarato nel risultato.

Testi primari consultati (non redistribuiti):

- [NTC — Gazzetta Ufficiale](https://www.gazzettaufficiale.it/eli/id/2018/02/20/18A00716/sg).
- [Circolare — Gazzetta Ufficiale](https://www.gazzettaufficiale.it/eli/id/2019/02/11/19A00855/sg).
- [CNR DT207 R1](https://www.cnr.it/it/node/9550).
- Testi CEN/BS degli EN sopra indicati, copie PDF acquisite per consultazione;
  i titoli e le correzioni sono stati letti dal frontespizio, le tabelle dal PDF renderizzato.

## Ambito e limiti vincolanti

**Neve:** quattro zone NTC fino a 1500 m; oltre, dato locale non inferiore al
valore a 1500 m. Copertura piana, monofalda, due falde, falde multiple, cilindrica
senza fermaneve, copertura inferiore presso salto. Per quest’ultima μs è assegnato
quando la falda superiore supera 15°; distribuzione troncata se b2<ls.
Le raccomandazioni EN sugli accumuli ordinari non coprono l’appendice B,
aggetti di neve, ostacoli e carichi sui dispositivi fermaneve.

**Vento edifici:** profili NTC/EN entro 200 m dal suolo, edifici rettangolari
con h/d≤5, coperture piane a spigoli vivi/con parapetto hp/h≤0,1, monofalda
e due falde simmetriche 5°–75°. Coefficienti locali per area A mediante
interpolazione logaritmica fra 1 e 10 m². Casi ±cpi o apertura dominante;
combinazioni dei rami di pressione coerenti su ciascuna falda. Pressioni
interne valutate alla propria quota. Il coefficiente strutturale EN non
scala la pressione interna. Niente somma automatica di pressioni locali
come risultante globale, né riduzione della correlazione sopravento/sottovento.
Tettoie, concavità, bordi raccordati e interferenze richiedono modelli diversi.

**Vento ponti:** CNR G.11 preliminare per impalcato ordinario costante, lunghezza
complessiva ≤200 m. Quota di riferimento: centro impalcato + htot/2. Dimensione
di riferimento d anche nella forza orizzontale; momento con d². Quattro segni
simultanei Y/M; impalcati affiancati isolati, indipendenti o collegati e azioni
per le pile comuni. Per d/htot<2 cfX deve essere documentato e assegnato.
La verifica dinamica e aeroelastica non è sostituita dal calcolo statico.

**Temperatura edifici:** metodo semplificato NTC soltanto dopo conferma del
campo del §3.5.5. Profilo lineare di superficie assegnato oppure trasmissione
stazionaria monostrato con Rsi, h/λ, Rse (EN appendice D). Nessuna ipotesi
implicita di incastro o trasformazione in sforzi. Dilatazioni e curvature libere.

**Temperatura ponti:** Approccio 1; temperature uniformi per tipologia,
gradienti verticali e ksur tabellati, simultaneità ωN/ωM, gradiente orizzontale
assegnato, movimenti per appoggi/giunti con maggiorazione 10/20 °C.
Per spessori diversi da 50/100/150 mm assegnare i gradienti documentati;
non si inventa un’interpolazione della pavimentazione. Non comprende
Approccio 2, interazione binario–ponte o analisi dei vincoli.

## Riproduzione delle prove

`dotnet test tests/Design.Checks/Design.Checks.csproj -c Release -f net8.0 --filter FullyQualifiedName~ClimateActionsTests`

Stessi test su `-f net472`. Comprendono casi a mano tabellari, soglie,
geometrie escluse, unità e segni, determinismo parallelo. L’oracolo
`Oracles/climate_independent.py` usa direttamente la formula di esposizione
NTC, indipendentemente dal percorso vm/Iv del motore: 80 punti, tolleranza
assoluta 1e-10 kN/m² (ben inferiore alla precisione ingegneristica dei dati).
Lo script rifiuta di sovrascrivere il file atteso. Nessuna fixture storica modificata.
