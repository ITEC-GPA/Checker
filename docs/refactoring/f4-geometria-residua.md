# F4.13 — geometria residua

## Proposta di taglio

Design 0.19.0.0: nuovo contratto tipizzato ShearGeometryProposal con barre DesignBar già usate dalla ricerca, contorno e indici delle seconde file esplicite. Trasferimento della proposta geometrica ANTHEA; nessuna deduzione di ancoraggio o verifica dei dettagli. Cattura preventiva ANTHEA residual-before.json, SHA ca5700c5e25399b0f69b693e9f48de7b32d380678fd113114999b90a26fc2921, 18 sezioni e 35 casi aggiuntivi separati. Tre prove indipendenti PASS: rettangolo/T, centroidi delle seconde file, sezione cava e anello. Integrazione e confronto applicativo nel passo seguente. Versione incrementata nel secondo componente per la nuova API.

## Riferimento della predalle

CompositeBridge 1.6.0.0: SlabRebarLayout tipizzato, riferimenti soletta/predalle espliciti e due prove indipendenti PASS sui limiti e sulle quote. La predalle resta contenuta nella soletta omogenea. Venti casi prima del port nella stessa cattura ANTHEA; integrazione e confronto nel passo seguente. API aggiuntiva: incremento del secondo componente.

## Stratigrafia dei micropali

Geotechnics 0.3.0.0: MicropileDepthSurvey conserva soltanto spessori verticali e parametri Bustamante–Doix. CalculateFromDepths e l'API precedente convergono sullo stesso motore, senza duplicarlo né inventare peso o resistenza del terreno. Dati copiati e sola lettura. Sei combinazioni di inclinazione/attivazione con curve, azioni, pesi e avvisi identici; conversione indipendente a 60° e rifiuti di dati errati. Diciassette prove PASS incluse regressioni del port e casi limite. Nuova API, incremento del secondo componente. Collegamento ANTHEA ancora da completare.
