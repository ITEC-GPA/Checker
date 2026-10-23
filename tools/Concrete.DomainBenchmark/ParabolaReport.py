"""Paired timing and numerical evidence for the analytic-parabola experiment."""
import csv
import hashlib
import html
import json
import math
import statistics
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

measurements, verification, baseline_verification, destination = map(Path, sys.argv[1:5])
destination.mkdir(parents=True, exist_ok=True)


def read(name):
    meta = json.loads((measurements / (name + '.jsonl.meta.json')).read_text(encoding='utf-8-sig'))
    groups = {}
    for line in (measurements / (name + '.jsonl')).read_text(encoding='utf-8-sig').splitlines():
        row = json.loads(line)
        groups.setdefault(row['Id'], []).append(row)
    assert len(groups) == meta['cases'], name
    out = {}
    for key, rows in groups.items():
        assert [r['run'] for r in rows] == list(range(meta['repeats'] + 1)), (name, key)
        warm = [r for r in rows if not r['cold']]
        result = dict(warm[0], ms=statistics.median(r['ms'] for r in warm), coldMs=rows[0]['ms'])
        for flag in ('accepted', 'planeAccepted'):
            result[flag] = all(r[flag] for r in rows)
        result['unstable'] = len({json.dumps([r.get(k) for k in ('n', 'mx', 'my', 'accepted', 'planeAccepted', 'iterations', 'evaluations', 'path')]) for r in rows}) != 1
        out[key] = result
    return out


def replay(name):
    return {r['Id']: r for r in map(json.loads, (measurements / (name + '.jsonl')).read_text(encoding='utf-8-sig').splitlines())}


def percentile(values, p):
    values = sorted(values)
    if not values:
        return None
    x = (len(values) - 1) * p
    lo, hi = math.floor(x), math.ceil(x)
    return values[lo] + (values[hi] - values[lo]) * (x - lo)


def stats(rows):
    def vals(k): return [r[k] for r in rows]
    times = vals('ms')
    return dict(cases=len(rows), accepted=sum(vals('accepted')), planeAccepted=sum(vals('planeAccepted')),
                sumSeconds=sum(times) / 1000, meanMs=statistics.mean(times) if times else None,
                sumPreparationSeconds=sum(vals('prepareMs')) / 1000, sumFirstSolveSeconds=sum(vals('coldMs')) / 1000,
                sumOneShotSeconds=sum(r['prepareMs'] + r['coldMs'] for r in rows) / 1000,
                p95OneShotMs=percentile([r['prepareMs'] + r['coldMs'] for r in rows], .95),
                **{'p' + str(p) + 'Ms': percentile(times, p / 100) for p in (50, 90, 95, 99)}, maxMs=max(times) if times else None,
                coldP95Ms=percentile(vals('coldMs'), .95), meanIterations=statistics.mean(vals('iterations')) if rows else None,
                p95Iterations=percentile(vals('iterations'), .95), meanEvaluations=statistics.mean(vals('evaluations')) if rows else None,
                fallback=sum(r['path'] != 'iterative' for r in rows), unstable=sum(vals('unstable')))


sets = {name: read(name) for name in ('before', 'after', 'gauss', 'focus-before', 'focus-after', 'focus-gauss',
        'reverse-before', 'reverse-after', 'mesh-before', 'mesh-after', 'mesh-gauss', 'unchanged-before', 'unchanged-after')}
reintegrated = {name: replay(name + '-reintegrated') for name in ('before', 'gauss', 'focus-before', 'focus-gauss', 'mesh-before', 'mesh-gauss')}
groups = {}


def cohort(name, prefix='', select=lambda r: True, common=False, valid_planes=False, exact_planes=False):
    b, a = sets[prefix + 'before'], sets[prefix + 'after']
    assert set(b) == set(a)
    keys = [k for k in b if select(b[k]) and (not common or b[k]['accepted'] and a[k]['accepted'])
            and (not valid_planes or b[k]['planeAccepted'] and a[k]['planeAccepted'])
            and (not exact_planes or reintegrated[prefix + 'before'].get(k, {}).get('geometricPlaneAccepted') and a[k]['planeAccepted'])]
    result = {'before': stats([b[k] for k in keys]), 'after': stats([a[k] for k in keys])}
    if prefix + 'gauss' in sets:
        result['gauss'] = stats([sets[prefix + 'gauss'][k] for k in keys])
    result['speedup'] = result['before']['sumSeconds'] / result['after']['sumSeconds'] if keys else None
    groups[name] = result


cohort('All')
cohort('Common', common=True)
cohort('CommonPlanes', common=True, valid_planes=True)
cohort('CommonExactPlanes', common=True, exact_planes=True)
cohort('Focus', 'focus-')
cohort('FocusCommon', 'focus-', common=True)
cohort('FocusCommonPlanes', 'focus-', common=True, valid_planes=True)
cohort('FocusCommonExactPlanes', 'focus-', common=True, exact_planes=True)
cohort('Reverse', 'reverse-')
cohort('Mesh', 'mesh-')
cohort('MeshCommon', 'mesh-', common=True)
for state in ('Plastic', 'Elastic'):
    cohort(state, select=lambda r: r['state'] == state)
    cohort('Focus' + state, 'focus-', select=lambda r: r['state'] == state)
    for criterion in ('ConstantN', 'ConstantEccentricity'):
        cohort('Focus' + state + criterion, 'focus-', select=lambda r: r['state'] == state and r['criterion'] == criterion, common=True)
    cohort('Small' + state, 'focus-', select=lambda r: r['state'] == state and '/ConstantEccentricity/-1/0.05/' in r['Id'], common=True)
for divisions in (4, 12, 24):
    cohort('Mesh' + str(divisions), 'mesh-', select=lambda r: int(r['Id'].split('/')[-2]) == divisions, common=True)

changed = {}
precision = {}
drift = {}
slow = {}
for prefix in ('', 'focus-', 'mesh-'):
    b, a = sets[prefix + 'before'], sets[prefix + 'after']
    changed[prefix or 'main'] = dict(lost=[k for k in b if b[k]['accepted'] and not a[k]['accepted']],
        gained=[k for k in b if not b[k]['accepted'] and a[k]['accepted']],
        failedAfter=[k for k in a if not a[k]['accepted']])
    common = [k for k in b if b[k]['accepted'] and a[k]['accepted']]
    slow[prefix or 'main'] = [dict(Id=k, beforeMs=b[k]['ms'], afterMs=a[k]['ms'], ratio=a[k]['ms'] / b[k]['ms'])
        for k in sorted(common, key=lambda k: a[k]['ms'] - b[k]['ms'], reverse=True)
        if a[k]['ms'] > 1.1 * b[k]['ms'] and a[k]['ms'] - b[k]['ms'] > .02]
    if prefix == 'focus-':
        for row in slow[prefix]:
            k = row['Id']
            rb, ra = sets['reverse-before'][k]['ms'], sets['reverse-after'][k]['ms']
            row.update(reverseBeforeMs=rb, reverseAfterMs=ra, reverseRatio=ra / rb,
                       confirmedInReverse=ra > 1.1 * rb and ra - rb > .02)
    for criterion, quantity in (('ConstantN', 'N'), ('ConstantN', 'SearchAngle'), ('ConstantEccentricity', 'SearchAngle')):
        keys = [k for k in common if b[k]['criterion'] == criterion]
        entry = dict(cases=len(keys), unit='N' if quantity == 'N' else 'rad')
        for side, data in (('before', b), ('after', a)):
            values = [abs(v['Residual']) for k in keys for v in data[k]['planeResiduals'] if v['Quantity'] == quantity]
            entry[side] = dict(p50=percentile(values, .5), p95=percentile(values, .95), maximum=max(values) if values else None)
        precision[prefix + criterion + '/' + quantity] = entry
    values = []
    for k in common:
        old = math.hypot(b[k]['mx'], b[k]['my'])
        if old >= 1 and b[k]['criterion'] == 'ConstantN':
            values.append((100 * (math.hypot(a[k]['mx'], a[k]['my']) / old - 1), k))
    drift[prefix or 'main'] = dict(cases=len(values), p50AbsPercent=percentile([abs(v) for v, _ in values], .5),
        p95AbsPercent=percentile([abs(v) for v, _ in values], .95), largest=sorted(values, key=lambda v: abs(v[0]), reverse=True)[:10])

unchanged_fields = ('accepted', 'planeAccepted', 'n', 'mx', 'my', 'path', 'iterations', 'evaluations', 'bracketIterations')
assert set(sets['unchanged-before']) == set(sets['unchanged-after'])
unchanged_differences = [k for k in sets['unchanged-before'] if any(sets['unchanged-before'][k].get(f) != sets['unchanged-after'][k].get(f) for f in unchanged_fields)]
gauss_same_plane = replay('before-analytic-gauss')
mesh_variation = {}
for side in ('before', 'gauss', 'after'):
    families = {}
    for key, row in sets['mesh-' + side].items():
        family = '/'.join(key.split('/')[:-2] + key.split('/')[-1:])
        families.setdefault(family, []).append(row)
    changes = []
    for key, rows in families.items():
        moments = [math.hypot(r['mx'], r['my']) for r in rows]
        changes.append(dict(Id=key, rangePercent=100 * (max(moments) - min(moments)) / max(moments)))
    mesh_variation[side] = dict(families=len(changes), p95RangePercent=percentile([r['rangePercent'] for r in changes], .95),
        maximum=max(changes, key=lambda r: r['rangePercent']))
decomposition = []
for k, b in sets['before'].items():
    if not b['accepted'] or k not in gauss_same_plane or k not in reintegrated['before']:
        continue
    g, a = gauss_same_plane[k], reintegrated['before'][k]
    decomposition.append(dict(Id=k, lawDeltaN_N=1000 * (g['n'] - b['n'] - b['planeN']),
        integrationDeltaN_N=1000 * (a['n'] - g['n']),
        lawDeltaMoment_kNm=math.hypot(g['mx'] - b['mx'] - b['planeMx'], g['my'] - b['my'] - b['planeMy']),
        integrationDeltaMoment_kNm=math.hypot(a['mx'] - g['mx'], a['my'] - g['my'])))
tests = json.loads((verification / 'summary.json').read_text(encoding='utf-8-sig'))
old_tests = json.loads((baseline_verification / 'summary.json').read_text(encoding='utf-8-sig'))
new_failures = sorted(set(tests['unexpected']) - set(old_tests['unexpected']))
resolved_failures = sorted(set(old_tests['unexpected']) - set(tests['unexpected']))
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
failure_details = []
analytic_tests = []
for p in verification.glob('*.trx'):
    document = ET.parse(p).getroot()
    analytic_ids = {test.attrib['id'] for test in document.findall('.//t:UnitTest', ns)
                    if 'AnalyticParabolaIntegrationTests' in test.find('t:TestMethod', ns).attrib.get('className', '')}
    for result in document.findall('.//t:UnitTestResult', ns):
        if result.attrib.get('testId') in analytic_ids:
            analytic_tests.append(dict(name=result.attrib['testName'], outcome=result.attrib['outcome'],
                duration=result.attrib.get('duration'), output=result.findtext('.//t:StdOut', '', ns)))
        if result.attrib['outcome'] == 'Failed':
            failure_details.append(dict(suite=p.stem, name=result.attrib['testName'], message=result.findtext('.//t:Message', '', ns)))
cross_session_path = measurements / 'cross-session.json'
cross_session = json.loads(cross_session_path.read_text(encoding='utf-8')) if cross_session_path.exists() else None
report = dict(baseline='aa262e21 / Concrete 0.18.0.2', candidate='Concrete 0.19.0.0, analytic n=2 European parabola, unchanged public tolerances',
    cohorts=groups, changed=changed, precisionOfReintegratedPlanes=precision, momentChangesConstantN=drift,
    slowerCases=slow, samePlaneDecomposition=decomposition, unchangedCases=len(sets['unchanged-before']), unchangedDifferences=unchanged_differences,
    meshSensitivity=mesh_variation,
    tests=tests, analyticTests=analytic_tests, newFailures=new_failures, resolvedFailures=resolved_failures, failureDetails=failure_details,
    crossSession=cross_session)
(destination / 'riepilogo.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
with (destination / 'casi-prima-dopo.csv').open('w', encoding='utf-8-sig', newline='') as stream:
    fields = ['catalogue', 'Id', 'state', 'criterion', 'before_ms', 'gauss_ms', 'after_ms', 'before_iterations', 'after_iterations',
              'before_accepted', 'after_accepted', 'before_planeAccepted', 'after_planeAccepted', 'old_plane_exact_accepted', 'meshElements',
              'before_n', 'after_n', 'before_mx', 'after_mx', 'before_my', 'after_my']
    writer = csv.DictWriter(stream, fields, delimiter=';'); writer.writeheader()
    for prefix in ('', 'focus-', 'mesh-'):
        b, a, g = (sets[prefix + suffix] for suffix in ('before', 'after', 'gauss'))
        for k in b:
            row = dict(catalogue=prefix or 'main', Id=k, state=b[k]['state'], criterion=b[k]['criterion'],
                gauss_ms=g[k]['ms'], meshElements=a[k]['meshElements'], old_plane_exact_accepted=reintegrated[prefix + 'before'].get(k, {}).get('geometricPlaneAccepted'))
            for side, data in (('before', b), ('after', a)):
                row.update({side + '_' + key: data[k][key] for key in ('ms', 'iterations', 'accepted', 'planeAccepted', 'n', 'mx', 'my')})
            writer.writerow(row)


def fmt(v, digits=3):
    if v is None: return '—'
    if isinstance(v, int): return str(v)
    return f'{v:,.{digits}f}'.replace(',', '_').replace('.', ',').replace('_', '.')


def table(headers, rows):
    return '<div class="scroll"><table><thead><tr>' + ''.join('<th>' + html.escape(str(v)) + '</th>' for v in headers) + '</tr></thead><tbody>' + ''.join('<tr>' + ''.join('<td>' + str(v) + '</td>' for v in row) + '</tr>' for row in rows) + '</tbody></table></div>'


def pairs(names):
    return table(['Campione', 'Casi', 'Riusciti prima → dopo', 'Totale [s]', 'P50 [ms]', 'P95 [ms]', 'Accelerazione'], [[label,
        groups[key]['before']['cases'], f"{groups[key]['before']['accepted']} → {groups[key]['after']['accepted']}",
        *[fmt(groups[key]['before'][v]) + ' → ' + fmt(groups[key]['after'][v]) for v in ('sumSeconds', 'p50Ms', 'p95Ms')], fmt(groups[key]['speedup'], 2) + '×'] for key, label in names])


parts = ['<!doctype html><html lang="it"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Parabola analitica — confronto prima e dopo</title>',
    '<style>body{font:16px/1.55 system-ui,sans-serif;color:#16302c;background:#f2f5f2;margin:0}main{max-width:1250px;margin:auto;padding:36px 28px}h1{font-size:34px;line-height:1.15}h2{margin-top:34px}table{border-collapse:collapse;background:white;width:100%;font-size:14px}td,th{padding:10px;border-bottom:1px solid #dbe3df;text-align:right;white-space:nowrap}td:first-child,th:first-child{text-align:left}th{background:#dfece5}.scroll{overflow:auto}a{color:#076b56}.box{background:white;border-left:5px solid #168166;padding:16px 22px}.warning{border-color:#be7519}li{margin:7px 0}.muted{color:#50625d}pre{white-space:pre-wrap;font-size:13px}</style><main>',
    '<p>Checker Concrete · candidata 0.19.0.0 · 23 ottobre 2026</p><h1>Parabola analitica e taglio della mesh</h1>',
    '<p>Confronto con la candidata stress block 0.18.0.2. Il nuovo percorso riguarda il parabola-rettangolo europeo standard con n=2, fck ≤ 50 MPa e calcestruzzo teso ordinario escluso. Le tabelle personalizzate, le alte resistenze e gli altri diagrammi conservano il percorso precedente. L’opzione tension stiffening mantiene la propria legge in trazione; la compressione usa la nuova parabola e in quel percorso misto resta la quadratura di Gauss.</p>',
    '<div class="box">La mesh è preparata una volta. Si tagliano soltanto le celle attraversate dall’asse neutro e dall’inizio del plateau; i piani di prova oltre la deformazione ultima rispettano anche tale limite. Le risultanti della parabola n=2 sono integrate esattamente sulla geometria poligonale, salvo arrotondamenti. Le barre restano puntuali e usano la stessa legge analitica per il calcestruzzo sottratto.</div>',
    '<h2>Tempi e confronti su campioni omogenei</h2>', pairs([('All', '240 casi originali'), ('Common', 'Originali riusciti in entrambi'), ('CommonPlanes', 'Originali con piani validi in entrambi'), ('Focus', 'Campione esteso dei due criteri principali'), ('FocusCommon', 'Estesi riusciti in entrambi'), ('FocusCommonPlanes', 'Estesi con piani validi in entrambi'), ('FocusCommonExactPlanes', 'Estesi con entrambi i piani validi sulla parabola esatta')]),
    '<p class="muted">Totale = somma delle mediane per caso; non è la durata della campagna. I due cataloghi hanno 24 casi in comune e non vanno sommati. “Piano valido” significa che le forze reintegrate rispettano il criterio, non soltanto quelle dichiarate nel punto interpolato dal fallback.</p>',
    '<h2>Da dove viene il guadagno?</h2>']
for key, label in (('All', '240 casi originali'), ('Focus', 'Campione esteso')):
    parts.append('<h3>' + label + '</h3>' + table(['Variante', 'Totale [s]', 'P95 [ms]', 'Iterazioni medie', 'Piani validi', 'Fallback'], [[title,
        fmt(groups[key][variant]['sumSeconds']), fmt(groups[key][variant]['p95Ms']), fmt(groups[key][variant]['meanIterations'], 2),
        groups[key][variant]['planeAccepted'], groups[key][variant]['fallback']] for variant, title in
        (('before', 'Spezzata + Gauss'), ('gauss', 'Parabola analitica + Gauss'), ('after', 'Parabola analitica + taglio esatto'))]))
parts.extend(['<h2>Preparazione e prima ricerca</h2>',
    '<p>Il guadagno della ricerca non coincide con quello dell’intera elaborazione quando la sezione viene ricostruita per ogni azione. Questa tabella include costruzione della sezione, preparazione del solver e prima ricerca. La preparazione è misurata una volta per caso, senza cinque ripetizioni dedicate.</p>',
    table(['Campione', 'Preparazione prima → dopo [s]', 'Prima ricerca prima → dopo [s]', 'Totale prima → dopo [s]', 'P95 totale prima → dopo [ms]'], [
        [label] + [fmt(groups[key]['before'][field]) + ' → ' + fmt(groups[key]['after'][field]) for field in
            ('sumPreparationSeconds', 'sumFirstSolveSeconds', 'sumOneShotSeconds', 'p95OneShotMs')]
        for key, label in (('All', 'Originale'), ('FocusCommon', 'Esteso: casi comuni'))])])
parts.extend(['<h2>ConstantN, ConstantEccentricity e piccole eccentricità</h2>', pairs([
    ('FocusPlasticConstantN', 'Plastic / ConstantN / casi comuni'), ('FocusPlasticConstantEccentricity', 'Plastic / ConstantEccentricity / casi comuni'),
    ('FocusElasticConstantN', 'Elastic / ConstantN / casi comuni'), ('FocusElasticConstantEccentricity', 'Elastic / ConstantEccentricity / casi comuni'),
    ('SmallPlastic', 'Compressione Plastic / e/D=0,05'), ('SmallElastic', 'Compressione Elastic / e/D=0,05'), ('Reverse', 'Controllo esteso in ordine dopo/prima')]),
    '<h2>Distribuzione dei tempi e iterazioni</h2>'])
for key, label in (('All', 'Tutti i casi originali, inclusi i non riusciti'),
                   ('Common', 'Originali riusciti in entrambi'),
                   ('Focus', 'Tutti i casi estesi, inclusi i non riusciti'),
                   ('FocusCommon', 'Estesi riusciti in entrambi')):
    parts.append('<h3>' + label + '</h3>' + table(['Misura', 'Prima', 'Dopo'], [[title, fmt(groups[key]['before'][field]), fmt(groups[key]['after'][field])] for field, title in
        (('meanMs', 'Media [ms]'), ('p50Ms', 'P50 [ms]'), ('p90Ms', 'P90 [ms]'), ('p95Ms', 'P95 [ms]'), ('p99Ms', 'P99 [ms]'), ('maxMs', 'Massimo [ms]'),
         ('coldP95Ms', 'P95 prima ricerca [ms]'), ('meanIterations', 'Iterazioni medie'), ('p95Iterations', 'P95 iterazioni'), ('meanEvaluations', 'Valutazioni scalari medie'), ('fallback', 'Fallback'), ('unstable', 'Casi non ripetibili'))]))
parts.extend(['<h2>Precisione e variazione delle resistenze</h2>',
    '<p>Residui dei piani reintegrati sui casi estesi riusciti in entrambi: le due colonne usano la rispettiva legge di calcolo. I residui non misurano da soli l’errore fisico; i controlli analitici indipendenti verificano l’integrazione della nuova legge.</p>',
    table(['Residuo', 'Casi', 'P95 prima → dopo', 'Massimo prima → dopo'], [[label + ' [' + entry['unit'] + ']', entry['cases']] + [
        f"{entry['before'][metric]:.6g} → {entry['after'][metric]:.6g}" for metric in ('p95', 'maximum')] for label, entry in precision.items() if label.startswith('focus-')]),
    '<p>La differenza di momento resistente a N costante è una variazione del risultato, non un errore rispetto a un riferimento esatto. Sono esclusi i casi con momento precedente inferiore a 1 kNm, per evitare percentuali fuorvianti vicino a zero.</p>',
    table(['Campione', 'Casi', 'P50 variazione assoluta [%]', 'P95 variazione assoluta [%]', 'Massima variazione [%]'], [[label, drift[key]['cases'], fmt(drift[key]['p50AbsPercent'], 5), fmt(drift[key]['p95AbsPercent'], 5), fmt(drift[key]['largest'][0][0], 5) if drift[key]['largest'] else '—'] for key, label in (('main', 'Originale'), ('focus-', 'Esteso'))]),
    '<p>I test indipendenti comprendono primitive monodimensionali, tagli obliqui, soglie e scale, resistenza analitica di una sezione semplicemente armata, coerenza delle tensioni riportate e sottrazione alle barre. L’equilibrio non lineare è controllato anche con 600.000 fibre rettangolari che valutano direttamente la parabola, senza usare il nuovo integratore. Vicino alla compressione centrata si integra separatamente il piccolo deficit rispetto al plateau, evitando cancellazioni tra grandi momenti.</p>',
    '<p>La variazione del legame non è trascurabile in ogni regime: con otto corde uniformi, la pendenza della prima corda è 1,875·fcd/εc2, mentre la tangente iniziale della parabola è 2·fcd/εc2 (+6,67%). La differenza massima di tensione tra parabola e interpolazione è fcd/256, pari allo 0,390625% di fcd. Questo spiega perché alcune curvature a bassi carichi possono variare più delle resistenze ultime.</p>',
    '<h2>Sensibilità alla mesh</h2>', pairs([('Mesh4', 'Dimensione elemento D/4, casi comuni'), ('Mesh12', 'Dimensione elemento D/12, casi comuni'), ('Mesh24', 'Dimensione elemento D/24, casi comuni')]),
    '<p>Quattro sezioni rettangolari, due domini, tre direzioni e due criteri. D è la massima dimensione della sezione. La geometria esterna resta identica; il numero effettivo di celle è nel CSV. L’invarianza dell’integrazione rispetto alla suddivisione è verificata anche con primitive indipendenti nei test.</p>',
    table(['Variante', 'Famiglie di casi', 'P95 escursione del momento [%]', 'Massima escursione [%]'], [
        [label, mesh_variation[side]['families'], f"{mesh_variation[side]['p95RangePercent']:.6g}", f"{mesh_variation[side]['maximum']['rangePercent']:.6g}"]
        for side, label in (('before', 'Spezzata + Gauss'), ('gauss', 'Parabola + Gauss'), ('after', 'Parabola + taglio'))]),
    '<p>Escursione = (massimo − minimo)/massimo del modulo del momento resistente tra le tre mesh, a parità di sezione, domanda, criterio e dominio. Include anche la tolleranza della ricerca inversa.</p>',
    '<h2>Cosa peggiora e quali casi cambiano esito</h2>'])
for key, label in (('main', 'Originali'), ('focus-', 'Estesi'), ('mesh-', 'Mesh')):
    parts.append('<h3>' + label + '</h3><p>Persi: ' + str(len(changed[key]['lost'])) + '; guadagnati: ' + str(len(changed[key]['gained'])) + '; non riusciti dopo: ' + str(len(changed[key]['failedAfter'])) +
        '. Rallentamenti oltre il 10% e 0,02 ms sui casi comuni: ' + str(len(slow[key])) + '.</p>')
    parts.append(table(['Caso', 'Prima [ms]', 'Dopo [ms]', 'Rapporto dopo/prima'], [[html.escape(r['Id']), fmt(r['beforeMs']), fmt(r['afterMs']), fmt(r['ratio'], 2)] for r in slow[key][:10]]))
    if key == 'focus-':
        parts.append('<p>Rallentamenti che superano entrambe le soglie anche nel controllo in ordine inverso: ' + str(sum(r['confirmedInReverse'] for r in slow[key])) + '. Le differenze su decine di microsecondi non sono automaticamente regressioni riproducibili.</p>')
    parts.append('<details><summary>Casi persi e nuovi riusciti</summary><pre>' + html.escape(json.dumps(changed[key], ensure_ascii=False, indent=2)) + '</pre></details>')
if cross_session:
    observed = cross_session['previousSlowCase']
    rows = []
    for session, label in (('measurements', 'Sessione precedente'), ('measurements-aligned', 'Sessione finale')):
        for prefix, order in (('focus-', 'prima/dopo'), ('reverse-', 'dopo/prima')):
            b, a = (observed['sessions'][session][prefix + side]['ms'] for side in ('before', 'after'))
            rows.append([label, order, fmt(b), fmt(a), fmt(100 * (a / b - 1), 2)])
    parts.extend(['<h3>Piccolo rallentamento osservato nella sessione precedente</h3>',
        '<p>Caso ' + html.escape(observed['Id']) + '. La prima sessione mostrava un rallentamento ripetuto anche in ordine inverso; la sessione finale non lo conferma oltre entrambe le soglie. Non si conclude che ogni singolo caso sia sempre più veloce: su queste durate conta anche la variabilità dell’host. I campioni individuali sono conservati in cross-session.json nell’archivio.</p>',
        table(['Sessione', 'Ordine', 'Prima [ms]', 'Dopo [ms]', 'Variazione [%]'], rows),
        '<p>Le due sessioni hanno risultati numerici, esiti e contatori identici in tutti i cataloghi confrontati. Nella campagna finale le tre varianti usano DLL dipendenti identiche per hash; la baseline è stata ricompilata dopo un aggiornamento esterno di Model. Le impronte dei sorgenti e delle DLL sono conservate in campaign.json.</p>'])
parts.extend(['<h2>Regressioni e stato della candidata</h2>',
    '<p>Nuovi test analitici: ' + str(sum(t['outcome'] == 'Passed' for t in analytic_tests)) + '/' + str(len(analytic_tests)) + ' superati. Gli output numerici dei controlli indipendenti sono nel riepilogo JSON.</p>',
    '<p>Altri diagrammi: ' + str(len(sets['unchanged-before'])) + ' casi, ' + str(len(unchanged_differences)) + ' differenze in forze, esiti, percorsi e contatori. API compatibili: ' + str(tests['apiCompatible']) + '.</p>',
    table(['Suite', 'Superati', 'Falliti'], [[s['name'], s['passed'], s['failed']] for s in tests['results']]),
    '<div class="box warning">Gli attesi e le fixture storiche non sono stati rigenerati. La candidata modifica il legame di calcolo rispetto alla spezzata e non è un rilascio ufficiale.</div>',
    '<p>Nuovi fallimenti rispetto alla 0.18.0.2:</p><ul>' + ''.join('<li>' + html.escape(x) + '</li>' for x in new_failures) + '</ul>',
    '<p>I controlli storici di equilibrio e compressione uniforme richiamano il materiale Model, che continua a interpolare la spezzata. Il riferimento a fibre del tension stiffening dichiara esplicitamente otto corde. Le fotografie di tensioni e curve conservano i valori precedenti. Queste differenze vanno rivalidate rispetto alla nuova legge; non sono state nascoste ampliando le tolleranze. Il caso di convergenza vicino al polo, emerso nella prima prova, è stato corretto e i relativi test sono rieseguiti.</p>',
    '<details><summary>Messaggi completi dei test falliti</summary><pre>' + html.escape(json.dumps(failure_details, ensure_ascii=False, indent=2)) + '</pre></details>',
    '<h2>Metodo e limiti</h2><ul><li>.NET 8 Release x64, tiered compilation disabilitata, esecuzioni sequenziali su host non dedicato. Suite complete anche su .NET Framework 4.7.2.</li><li>JIT riscaldato; una prima ricerca e cinque ripetizioni per caso. Tre ripetizioni per il controllo in ordine inverso e per Gauss nei cataloghi esteso e mesh. Una ripetizione per la regressione degli altri diagrammi.</li><li>Percentili interpolati sulle mediane per caso. Tempo di ricerca comprensivo dei fallback; preparazione, diagnostica esterna e scrittura escluse. Le valutazioni scalari non contano la costruzione vettoriale del fallback, inclusa invece nei tempi.</li><li>Cataloghi strutturati, non campione casuale dei modelli reali. Precisione esatta riferita alla geometria poligonale e alla legge n=2; non elimina l’approssimazione geometrica di contorni curvi né quella delle barre puntuali.</li><li>Le funzioni materiali di Model e le tabelle salvate restano quelle precedenti: la candidata applica la parabola analitica nei percorsi Checker supportati. Un eventuale rilascio deve rendere esplicita questa scelta anche per i consumatori esterni.</li></ul>',
    '<p><a href="casi-prima-dopo.csv">Dati caso per caso</a> · <a href="riepilogo.json">Riepilogo completo</a> · <a href="misure.zip">Misure grezze</a> · <a href="impronte.json">Impronte</a></p></main></html>'])
(destination / 'report-prima-dopo.html').write_text('\n'.join(parts), encoding='utf-8')
raw = sorted(p for p in measurements.iterdir() if p.is_file())
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
with zipfile.ZipFile(destination / 'misure.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for p in raw: archive.write(p, p.name)
    archive.write(verification / 'summary.json', 'verification-summary.json')
evidence = dict(baseline=report['baseline'], candidate=report['candidate'], dataSha256={p.name: digest(p) for p in raw},
    verificationSha256=digest(verification / 'summary.json'), reportScriptSha256=digest(Path(__file__)), archiveSha256=digest(destination / 'misure.zip'))
(destination / 'impronte.json').write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k: report[k] for k in ('cohorts', 'changed', 'newFailures', 'unchangedDifferences')}, indent=2))
