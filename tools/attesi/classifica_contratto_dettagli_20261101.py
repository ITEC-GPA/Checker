"""Classificazione riga per riga del contratto dei dettagli (detailing-contract-0.0.17.csv) dopo le decisioni dell'utente del 1/11/2026.

    py -3 -I tools/attesi/classifica_contratto_dettagli_20261101.py <prima.csv> <dopo.csv>

Ogni riga cambiata deve essere spiegata da una delle due correzioni dichiarate, altrimenti lo script si ferma:

1. EN 1992-1-1 8.7.2(3), lettura letterale (domanda 3): nelle righe A dei profili della famiglia Eurocodice (EN1992p11, UniEN1992p11,
   DsEN1992p11) con sovrapposizione, l'espressione dice «clear distance when it exceeds» invece di «clear distance beyond»; se l'interferro s
   supera slim = min(4Ø; 50 mm), RequiredLength = prima + slim (prima l0 + s − slim, ora l0 + s), LengthPassed = disponibile ≥ richiesta,
   Passed = LengthPassed e LapClearDistancePassed. Nessun altro campo cambia.
2. EN 1992-1-1 8.7.2(4) e 8.8 (domanda 4): nei casi M della famiglia Eurocodice i controlli di prima restano identici, nello stesso ordine;
   in coda si aggiungono LapPercentage (in sospeso, senza la disposizione delle sovrapposizioni) se e solo se il caso ha la zona di
   sovrapposizione, e i controlli LargeBar* se e solo se una barra supera 32 mm; nell'intestazione cambiano solo il numero dei controlli e
   l'esito complessivo, ricalcolato da tutti i controlli (false se uno fallisce, null se uno è in sospeso, true altrimenti).

Solo libreria standard. Stampa i conteggi per categoria.
"""
import sys
from collections import Counter

EURO = {'EN1992p11', 'UniEN1992p11', 'DsEN1992p11'}
NEW_KEYS = ['LapPercentage', 'LargeBarAnchorage', 'LargeBarCrackControl', 'LargeBarLap']


def read(path):
    with open(path, encoding='utf-8') as f:
        return [line.rstrip('\n').rstrip('\r') for line in f]


def blocks(lines):
    """Lista di (testa, [righe C]) per i casi M; righe singole per A, B e commenti, con la chiave della testa."""
    out, i = [], 0
    while i < len(lines):
        line = lines[i]
        if line.startswith('M\t'):
            j = i + 1
            while j < len(lines) and lines[j].startswith('C\t'):
                j += 1
            out.append(('M', line, lines[i + 1:j]))
            i = j
        else:
            out.append((line.split('\t')[0], line, []))
            i += 1
    return out


def key(kind, head):
    f = head.split('\t')
    if kind == 'M':
        return tuple(f[:4])
    if kind == 'A':
        return tuple(f[:f.index('ok')] if 'ok' in f else f)
    return (head,)


def overall(checks):
    passed = [c.split('\t')[6] for c in checks]
    return 'false' if 'false' in passed else 'null' if 'null' in passed else 'true'


def main(before_path, after_path):
    before, after = blocks(read(before_path)), blocks(read(after_path))
    assert len(before) == len(after), (len(before), len(after))
    counts = Counter()
    for (kb, hb, cb), (ka, ha, ca) in zip(before, after):
        assert kb == ka and key(kb, hb) == key(ka, ha), (hb[:120], ha[:120])
        if hb == ha and cb == ca:
            counts['identiche ' + kb] += 1
            continue
        fb, fa = hb.split('\t'), ha.split('\t')
        if kb == 'A':
            ok = fb.index('ok')
            profile = fb[2]
            inputs = fb[ok - 11:ok]
            lap, s, phi, available = inputs[6] == 'true', float(inputs[8]), float(inputs[0]), float(inputs[5])
            assert profile in EURO and lap, ('riga A non spiegata', hb)
            ob, oa = fb[ok + 1:], fa[ok + 1:]
            # Profile Fbd Eta1 Eta2 BasicLength Alpha6 MinimumLength RequiredLength AvailableLength LengthPassed MaximumLap LapClearPassed Passed Expression Reference
            assert ob[:7] == oa[:7] and ob[8] == oa[8] and ob[10:12] == oa[10:12] and ob[14] == oa[14], ('campi non attesi', hb, ha)
            assert oa[13] == ob[13].replace('clear distance beyond min(4Ø; 50 mm)', 'clear distance when it exceeds min(4Ø; 50 mm)'), ('espressione', ob[13], oa[13])
            limit = float(ob[10])
            assert limit == min(4 * phi, 50)
            rb, ra = float(ob[7]), float(oa[7])
            if s > limit:
                assert abs(ra - (rb + limit)) <= 1e-9 * ra, ('lunghezza', rb, ra, limit)
                counts['A: interferro oltre la soglia, l0 + slim'] += 1
            else:
                assert ra == rb
                counts['A: solo il testo dell\'espressione'] += 1
            assert oa[9] == ('true' if available >= ra else 'false'), ('LengthPassed', oa[9])
            assert oa[12] == ('true' if oa[9] == 'true' and oa[11] == 'true' else 'false'), ('Passed', oa[12])
            if oa[9] != ob[9]:
                counts['A: esito della lunghezza da true a false'] += 1
            continue
        assert kb == 'M', ('riga non spiegata', hb, ha)
        profile, ok = fb[3], fb.index('ok')
        assert profile in EURO, ('caso M non spiegato', hb)
        assert fb[:ok + 3] == fa[:ok + 3] and fb[ok + 4] == fa[ok + 4], ('intestazione', hb, ha)
        assert ca[:len(cb)] == cb, ('controlli esistenti cambiati', hb)
        added = [c.split('\t')[2] for c in ca[len(cb):]]
        lap = fb[ok - 4] == 'true'
        expected = (['LapPercentage'] if lap else [])
        assert added[:len(expected)] == expected and all(k.startswith('LargeBar') for k in added[len(expected):]), ('controlli aggiunti', hb, added)
        for i, c in enumerate(ca[len(cb):]):
            f = c.split('\t')
            assert f[1] == str(len(cb) + i) and f[6] == 'null' and f[9] == 'false', ('controllo aggiunto non in sospeso', c)
        assert int(fa[ok + 5]) == len(ca) == int(fb[ok + 5]) + len(added), 'conteggio'
        assert fa[ok + 3] == overall(ca), ('esito complessivo', fa[ok + 3], overall(ca))
        counts['M: aggiunti ' + '+'.join(added)] += 1
        counts['M: esito complessivo %s -> %s' % (fb[ok + 3], fa[ok + 3])] += 1
    # ogni caso Eurocodice con la zona di sovrapposizione ha LapPercentage (nessun caso dimenticato)
    for kind, head, checks in after:
        if kind == 'M':
            f = head.split('\t')
            if 'ok' in f and f[3] in EURO and f[f.index('ok') - 4] == 'true':
                assert any(c.split('\t')[2] == 'LapPercentage' for c in checks), ('LapPercentage mancante', head)
                counts['controllo: casi Eurocodice in zona di sovrapposizione'] += 1
    for k in sorted(counts):
        print('%6d  %s' % (counts[k], k))


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
