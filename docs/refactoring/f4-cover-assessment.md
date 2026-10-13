# F4.15 — controllo condiviso del copriferro

CoverAssessment combina due requisiti già calcolati da CoverRequirements: quello della scheda materiali e quello per le barre effettive. Nessuna formula normativa duplicata. Contratto in mm, valori finiti e non negativi, esiti comuni Outcome/Execution/Data.

Il requisito più gravoso governa. Se le barre effettive non sono disponibili, un copriferro inferiore al minimo noto resta non soddisfatto; un valore superiore non viene certificato e resta non valutato. È la politica già usata da ConcreteCoverAnalysis e ProjectValidation, ora in un unico servizio.

Concrete 0.7.0.0: nuova API, incremento del secondo componente. Tre prove indipendenti PASS (18 configurazioni/condizioni): minimo materiali/barre, soglia esatta, valore nullo, barre mancanti e valori non finiti/negativi. API compatibili. L'adattatore Application, la cattura/confronto dei testi di progetto e il collaudo distribuito restano da completare prima della chiusura F4.15.
