# Capsula: armature per tratti e dettagli di pilastro

La geometria `Stadium` accetta facoltativamente `StadiumReinforcement`: numero
e diametro per semicirconferenza, numero e diametro delle barre interne per
rettilineo. I due estremi di tangenza appartengono alla semicirconferenza e
non vengono duplicati. L'orientamento segue il lato maggiore. Le barre curve
sono radiali rispetto al centro del rispettivo arco; copriferro e staffe
usano lo stesso contorno poligonale della sezione. I controlli di interferenza
e copriferro restano quelli del generatore.

Il valore nullo mantiene la disposizione radiale precedente. Il confronto
binario delle nove tipologie preesistenti conserva contorni, fori, barre,
staffe e fibre senza differenze. Non si modifica DelaunayMesh.

`CalculateColumnOnPerimeter` applica le regole NTC del pilastro già esistenti,
sostituendo soltanto il riconoscimento dell'interasse longitudinale: ascissa
curvilinea sul percorso chiuso degli assi barra, compreso il tratto di
chiusura. Gli archi seguono il contorno discretizzato. Una barra manuale
fuori dal percorso lascia il solo interasse da verificare; gli altri
controlli restano disponibili. Il percorso deve essere coerente con
l'armatura effettiva.

La staffa perimetrale generata non dimostra il trattenimento delle barre:
la conferma di staffe e legature rimane richiesta. Non sono introdotte
verifiche di confinamento sismico delle pile, piegatura o ancoraggio.

Prove mirate: 9 superate, inclusi 5000 × 2500 mm, rotazione, diametri
32/25 mm, conteggi 17/8 per tratto, interasse curvo confrontato con arco
analitico, tratto rettilineo senza barre, disposizione radiale precedente,
barre manuali fuori percorso e input non validi.
Le catture indipendenti e il confronto binario sono conservati in ANTHEA,
`supporto/artefatti/capsula-pilastro/`. Il collaudo completo del set è separato.

Versioni: Design 0.30.0.0 e Concrete 0.13.0.0 per le nuove API.
Steel 2.2.0.7, Geotechnics 0.13.0.3, CompositeBridge 1.7.0.16,
Masonry 0.1.0.19 e ModelChecker 2.1.0.24 incrementano il componente finale
per la ricompilazione coerente con la nuova revisione sorgente.
