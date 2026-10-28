RIFERIMENTI DELLA COLONNA MODELLO - 28/10/2026 (GPCChecker.Concrete 0.27.0.0)

Contenuto
- derive_model_column_oracles.py: riferimento indipendente, solo libreria standard di
  Python (provato con 3.12.14). Non importa DLL, non legge codice né risultati di GPC.
  Uso: python -I derive_model_column_oracles.py [--output model-column-independent-oracles.json]
- model-column-independent-oracles.json: uscita dello script (N, mm, Nmm, 1/mm;
  compressione positiva nel file, |N| nei momenti).
- Le prove sono in ModelColumnTests.cs; i valori attesi sono scritti nelle prove con
  il nome del caso.

Modello dichiarato (lo stesso della pagina docs/metodi/ca.colonna-modello.md)
- Sezione rettangolare b x h, flessione retta attorno all'asse parallelo a b, barre
  puntiformi a strati; sezione netta (la barra interna sottrae il calcestruzzo alla sua
  deformazione), oppure lorda con l'opzione dei casi "-gross".
- Calcestruzzo: parabola-rettangolo di progetto fcd = 0,85 fck/1,5, ec2 = 2 per mille,
  ecu = 3,5 per mille, trazione nulla. Viscosità: deformazioni del legame per (1 + phi_ef),
  comprese ec2 ed ecu (EN 1992-1-1 5.8.6(4)); le barre no.
- Acciaio elastico-perfettamente plastico, fyd = fyk/1,15.
- Deformazioni ultime come il dominio plastico della libreria: ecu' al lembo compresso,
  ec2' al punto C (a (1 - ec2/ecu) h dal lembo compresso), eud = 0,9 euk al bordo esterno
  della barra tesa, euk al bordo esterno della barra compressa (euk = 7,5 per cento).
- Colonna modello: e2 = l0^2/c chi, M2 = |N| e2, M1Rd,max = max sulle chi di
  M(chi) - |N| l0^2/c chi, con chi fra 0 e chi_u.

Algoritmi (diversi da quelli della libreria)
- Calcestruzzo integrato in una dimensione, a pezzi fra le deformazioni 0, ec2', ecu',
  con Gauss-Legendre a 3 punti: esatto per sigma (grado 2) e sigma y (grado 3).
- Equilibrio su N per bisezione su e0 (90 iterazioni) entro i limiti di deformazione;
  chi_u per raddoppio e bisezione (80 iterazioni); massimo con 3000 campioni uniformi e
  sezione aurea (120 iterazioni); equilibrio con M0Ed per scansione e bisezione (100).
- La libreria usa invece l'integrazione analitica sulla mesh, Illinois su e0, 200 passi
  con il primo snervamento inserito e la sezione aurea fra i campioni vicini al massimo.

Attesi esterni (solo nelle prove; nei nomi delle prove e delle guide nessuna citazione)
1. Caso R400-3+3D24-l0-12.
   Fonte: P. Gelfi, VcaSlu 7.1, guida del programma (Università di Brescia,
   https://gelfi.unibs.it/software/Vcaslu-help.pdf), pagina «Colonna modello», file di
   esempio Pilastro40x40.slu. Mensola alta 6 m, l0 = 12 m; 400 x 400, 3Ø24 + 3Ø24 a 40 mm
   dai bordi; C25/30, fcd = 14,17 MPa; NEd = 500 kN; MEd1 = 47,5 + 15 x 6 = 137,5 kNm.
   Valori della fonte: tabella di 11 curvature chi H [per mille] con M [kNm] (sezione
   lorda, legami di progetto), max M1Rd = 157,25 kNm (riga del primo snervamento,
   157 kNm nel testo), e2 = 153 mm, verifica soddisfatta.
   Incongruenza della fonte: il testo dà fyk = 430 MPa e fyd = 374 MPa, ma la tabella è
   coerente con fyd = 450/1,15 = 391,3 MPa (eps_s = 1,957 per mille = 391,3/200000 nella
   riga dello snervamento; con fyd = 374 il massimo sarebbe 153,1 kNm). Le prove usano
   fyk = 450 MPa ed Es = 200000 MPa.
   Riproduzione lorda dello script: scarto massimo +0,07 % sugli 11 momenti della
   tabella, max M1Rd = 157,388 kNm (+0,09 %).
2. Caso R500-6+6D24-N1200.
   Fonte: dispense «Effetti del secondo ordine», Università degli Studi di Trieste
   (moodle2.units.it), esempio del par. 8.2. 500 x 500, 6Ø24 + 6Ø24 a 40 mm dai bordi;
   fck = 35 MPa, fcd = 0,85 fck/1,5; fyk = 430 MPa, fyd = fyk/1,15; Es = 206000 MPa;
   N = 1200 kN; mensole l = 4,0 / 6,5 / 9,0 m (l0 = 2 l).
   Valori della fonte, letti sul grafico: M1,max = 552 / 450 / 300 kNm. Tolleranza 5 %.

Confronti (libreria 0.27.0.0, prova ModelColumnTests; scarti libreria - atteso)
| Caso | Grandezza | Libreria | Riferimento Python (netto) | Fonte |
| R400-3+3D24-l0-12 | M1Rd,max [kNm] | 156,083 | 156,083 (-0,0002 %) | 157,25 (-0,74 %) |
| R400-3+3D24-l0-12 | e2* [mm] | 154,07 | 154,07 (-0,003 %) | 153 (+0,70 %) |
| R400-3+3D24-l0-12 | M agli 11 chi della tabella | | da -0,024 % a +0,000 % | |
| R400-3+3D24-l0-12 | chi_eq [1/mm] con M0Ed = 137,5 kNm | 8,8318e-6 | 8,8318e-6 (0,0000 %) | verificato |
| R400-3+3D24-l0-12 | MEd [kNm] | 201,089 | 201,089 (0,0000 %) | |
| R500-6+6D24-N1200, l0 = 8 m | M1Rd,max [kNm] | 554,124 | 554,124 (0,0000 %) | 552 (+0,38 %) |
| R500-6+6D24-N1200, l0 = 13 m | M1Rd,max [kNm] | 454,845 | 454,845 (-0,0001 %) | 450 (+1,08 %) |
| R500-6+6D24-N1200, l0 = 18 m | M1Rd,max [kNm] | 308,561 | 308,561 (+0,0001 %) | 300 (+2,85 %) |
Gli scarti dalla fonte vengono dalla sezione lorda (R400: -0,8 %) e dalla lettura
grafica (R500). La deviazione sugli 11 momenti viene dall'interpolazione lineare della
prova fra i punti della curva.

Altri casi dello script (stessa sezione R400, netta; libreria entro 0,2 %, 0,5 % a 3200 kN)
- c = 8: 136,824 kNm; phi_ef = 1: 147,532 kNm; phi_ef = 2: 132,482 kNm;
- l0 = 0,5 m: 245,877 kNm, modo rottura della sezione (massimo a chi_u);
- l0 = 1 m: 244,724 kNm, massimo appena prima di chi_u (M(chi) quasi piatto vicino a
  chi_u: con la sezione netta la pendenza è minore di |N| l0^2/c);
- N = 0: 174,198 kNm = M(chi_u); N = 2000 kN, l0 = 6 m: 121,874 kNm;
  N = 3200 kN, l0 = 3 m: 10,796 kNm; NRd,c = 3290,345 kN (rifiuto oltre).

Forme chiuse a mano (EN 1992-1-1, NTC 2018), caso R400
i = 400/√12 = 115,470 mm; lambda = 103,92; nu = 0,22059; omega = 0,46859;
lambda_lim NTC [4.1.41] = 25/√nu = 53,23; EN (5.13N) con A = 1, C = 0,7: 41,49;
imperfezione EN 5.2(7)a con l = 6 m: alpha_h = 2/√6 = 0,8165, theta_i = 4,0825e-3,
e_i = 24,495 mm; l0/400 = 30 mm; NTC l/300 = 20 mm; e0 NTC = max(l0/200; 20) = 60 mm,
EN = max(h/30; 20) = 20 mm; curvatura nominale EN 5.8.8: Kr = 1 (limitato), d = h/2 + is =
360 mm, 1/r0 = 1,20773e-5 1/mm, e2 = 173,91 mm, M2 = 86,957 kNm (Kphi = 1: beta < 0).

Tolleranze (progetto del 28/10/2026, par. 8)
0,2 % su M e su M1Rd,max contro il riferimento; 2 % su chi* ed e2* (massimo piatto);
1,5 % contro la fonte 1 (sezione netta dichiarata); 5 % contro la fonte 2 (lettura
grafica); 1e-6 relativo sulle forme chiuse, 1e-12 sulle imperfezioni.
