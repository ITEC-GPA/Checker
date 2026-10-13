# F4.12e — proiezioni delle domande di progetto

SectionDesignDemand raccoglie il passaggio da N/tensione con segno a compressione positiva, la selezione della massima compressione, il rapporto domanda/capacità e la capacità richiesta per l'obiettivo. I limiti sono già risolti dai servizi Concrete: non vengono ripetute formule o coefficienti normativi. Nello stesso rapporto le unità devono coincidere.

Ricerca preventiva: StressLimitCheck valuta l'intera risposta tensoriale e i limiti normativi, AnchorageCalculator produce lunghezze richieste, MemberDetailingCalculator riceve NEd positivo. Nessuna API esistente combina le righe di azione del progetto e il suo obiettivo: questo strato appartiene al coordinatore Design. Restano riusati tutti i servizi meccanici esistenti.

Attesi manuali: compressione massima di [-100,-6000,+9000] = 6000; 12/20 = 0.6; 300/400 = 0.75; 400/500 = 0.8; 400/0.8 = 500. Capacità mancanti/nonpositive, non finiti e overflow non producono un rapporto positivo. Nessuna azione significa soltanto minimo geometrico, comportamento preesistente.

Design 0.10.0.0: API aggiuntiva, incremento del secondo componente. Collegamento ANTHEA e confronto sul corpus immutato F4.12/F4.20 nel passo successivo; nessun rilascio isolato.
