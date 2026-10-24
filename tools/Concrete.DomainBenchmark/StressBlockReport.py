"""Report paired stress-block measurements; no numerical fixtures are regenerated."""
import csv
import hashlib
import html
import json
import math
import statistics
import sys
import zipfile
from pathlib import Path

measurements, verification, destination = map(Path, sys.argv[1:4])
destination.mkdir(parents=True, exist_ok=True)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(name):
    meta = json.loads((measurements / (name + '.jsonl.meta.json')).read_text(encoding='utf-8-sig'))
    groups = {}
    for line in (measurements / (name + '.jsonl')).read_text(encoding='utf-8-sig').splitlines():
        row = json.loads(line)
        groups.setdefault(row['Id'], []).append(row)
    assert len(groups) == meta['cases'], (name, 'Incomplete catalogue')
    result = {}
    for key, rows in groups.items():
        assert [r['run'] for r in rows] == list(range(meta['repeats'] + 1)), (name, key, 'Incomplete repeats')
        warm = [r for r in rows if not r['cold']]
        r = dict(warm[0])
        r['ms'] = statistics.median(x['ms'] for x in warm)
        r['coldMs'] = rows[0]['ms']
        r['accepted'] = all(x['accepted'] for x in rows)
        r['planeAccepted'] = all(x['planeAccepted'] for x in rows)
        r['unstable'] = len({json.dumps([x.get(k) for k in ('accepted', 'planeAccepted', 'n', 'mx', 'my', 'iterations', 'evaluations', 'bracketIterations', 'path')]) for x in rows}) > 1
        result[key] = r
    return result


def percentile(values, p):
    if not values:
        return None
    s = sorted(values)
    pos = (len(s) - 1) * p
    lo = math.floor(pos)
    hi = math.ceil(pos)
    return s[lo] + (s[hi] - s[lo]) * (pos - lo)


def stats(rows):
    def column(key):
        return [r[key] for r in rows if r.get(key) is not None]
    times = column('ms')
    return dict(cases=len(rows), accepted=sum(r['accepted'] for r in rows), planeAccepted=sum(r['planeAccepted'] for r in rows),
                sumSeconds=sum(times) / 1000, meanMs=statistics.mean(times) if times else None,
                **{'p' + str(p) + 'Ms': percentile(times, p / 100) for p in (50, 90, 95, 99)},
                maxMs=max(times) if times else None, coldP95Ms=percentile(column('coldMs'), .95),
                meanIterations=statistics.mean(column('iterations')) if rows else None,
                p95Iterations=percentile(column('iterations'), .95),
                meanScalarEvaluations=statistics.mean(column('evaluations')) if rows else None,
                fallback=sum(r['path'] != 'iterative' for r in rows), unstable=sum(r['unstable'] for r in rows))


before, after = read('before'), read('after')
assert set(before) == set(after) and len(before) == 240
replay = {r['Id']: r for r in map(json.loads, (measurements / 'before-planes-reintegrated.jsonl').read_text().splitlines())}
cohorts = {}


def pair(label, keys, b=before, a=after):
    keys = list(keys)
    x, y = stats([b[k] for k in keys]), stats([a[k] for k in keys])
    cohorts[label] = dict(before=x, after=y, speedup=x['sumSeconds'] / y['sumSeconds'] if y['sumSeconds'] else None)


for state in (None, 'Plastic', 'Elastic'):
    keys = [k for k, r in before.items() if state is None or r['state'] == state]
    name = state or 'All'
    pair(name, keys)
    common = [k for k in keys if before[k]['accepted'] and after[k]['accepted']]
    pair(name + 'Common', common)
    pair(name + 'CommonPlanes', [k for k in common if before[k]['planeAccepted'] and after[k]['planeAccepted']])
    pair(name + 'CommonGeometric', [k for k in common if replay.get(k, {}).get('geometricPlaneAccepted') and after[k]['planeAccepted']])
for criterion in ('ConstantN', 'ConstantEccentricity'):
    pair('Plastic' + criterion, [k for k, r in before.items() if r['state'] == 'Plastic' and r['criterion'] == criterion])
    pair('Plastic' + criterion + 'Common', [k for k, r in before.items() if r['state'] == 'Plastic' and r['criterion'] == criterion and r['accepted'] and after[k]['accepted']])

reverse_before, reverse_after = read('before-reverse'), read('after-reverse')
pair('PlasticReverse', reverse_before, reverse_before, reverse_after)
low_before, low_after = read('low-before'), read('low-after')
assert len(low_before) == 72 and set(low_before) == set(low_after)
pair('SmallEccentricity', low_before, low_before, low_after)
pair('SmallEccentricityCommon', [k for k in low_before if low_before[k]['accepted'] and low_after[k]['accepted']], low_before, low_after)
integration_only = read('integration-only')
pair('IntegrationOnly', before, before, integration_only)
pair('PlasticIntegrationOnly', [k for k in before if before[k]['state'] == 'Plastic'], before, integration_only)

continuous_before, continuous_after = read('continuous-before'), read('continuous-after')
assert set(continuous_before) == set(continuous_after) and len(continuous_before) == 720
comparison_keys = ('accepted', 'planeAccepted', 'n', 'mx', 'my', 'path', 'iterations', 'evaluations', 'bracketIterations')
continuous_changes = [k for k in continuous_before if any(continuous_before[k].get(v) != continuous_after[k].get(v) for v in comparison_keys)]
lost = [k for k in before if before[k]['accepted'] and not after[k]['accepted']]
gained = [k for k in before if not before[k]['accepted'] and after[k]['accepted']]
tests = json.loads((verification / 'summary.json').read_text(encoding='utf-8-sig'))
precision = {}
for criterion, quantity in (('ConstantN', 'N'), ('ConstantN', 'SearchAngle'), ('ConstantEccentricity', 'SearchAngle')):
    label = criterion + '/' + quantity
    precision[label] = {}
    for side, data in (('before', before), ('after', after)):
        rows = [data[k] for k in before if before[k]['state'] == 'Plastic' and before[k]['criterion'] == criterion and before[k]['accepted'] and after[k]['accepted']]
        residuals = [abs(v['Residual']) for r in rows for v in r['residuals'] if v['Quantity'] == quantity]
        precision[label][side] = dict(quantity=quantity, unit='N' if quantity == 'N' else 'rad',
                                         cases=len(rows), p50=percentile(residuals, .5), p95=percentile(residuals, .95), maximum=max(residuals))

geometric_precision = {}
for criterion, quantity in (('ConstantN', 'N'), ('ConstantN', 'SearchAngle'), ('ConstantEccentricity', 'SearchAngle')):
    keys = [k for k in before if before[k]['state'] == 'Plastic' and before[k]['criterion'] == criterion
            and before[k]['accepted'] and after[k]['accepted'] and replay.get(k, {}).get('geometricPlaneAccepted') and after[k]['planeAccepted']]
    entry = {'cases': len(keys), 'unit': 'N' if quantity == 'N' else 'rad'}
    for side in ('before', 'after'):
        residuals = [abs(v['Residual']) for k in keys for v in (replay[k]['residuals'] if side == 'before' else after[k]['planeResiduals']) if v['Quantity'] == quantity]
        entry[side] = {'p95': percentile(residuals, .95), 'maximum': max(residuals) if residuals else None}
    geometric_precision[criterion + '/' + quantity] = entry

report = dict(baseline='6edf46a7 / Concrete 0.18.0.1', candidate='Concrete 0.18.0.2, geometric concrete integration; bars unchanged',
              primaryScope='StressBlock + Plastic. Elastic cases are diagnostic only and excluded from primary performance conclusions.',
              cohorts=cohorts, precision=precision, geometricPlanePrecision=geometric_precision, lost=lost, gained=gained, lostPlaneReplay=[replay.get(k) for k in lost],
              continuousCases=len(continuous_before), continuousChanges=continuous_changes, tests=tests,
              method='Each case: cold solve plus five warm solves. Percentiles across per-case warm medians. Reverse plastic control and integration-only diagnostic: three warm solves. Continuous-diagram control: one warm solve. Sequential, .NET 8 Release, tiered compilation disabled.')
raw_files = sorted(p for p in measurements.iterdir() if p.is_file())
with zipfile.ZipFile(destination / 'misure.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for path in raw_files:
        archive.write(path, path.name)
    archive.write(verification / 'summary.json', 'verification-summary.json')
evidence = dict(baseline=report['baseline'], candidate=report['candidate'], primaryScope=report['primaryScope'],
                dataSha256={p.name: digest(p) for p in raw_files},
                verificationSummarySha256=digest(verification / 'summary.json'), reportScriptSha256=digest(Path(__file__)),
                archiveSha256=digest(destination / 'misure.zip'))
campaign = measurements / 'campaign.json'
if campaign.exists():
    evidence['campaign'] = json.loads(campaign.read_text(encoding='utf-8-sig'))
(destination / 'impronte.json').write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
(destination / 'riepilogo.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
fields = ['Id', 'state', 'criterion', 'before_ms', 'after_ms', 'before_iterations', 'after_iterations', 'before_evaluations', 'after_evaluations',
          'before_accepted', 'after_accepted', 'before_planeAccepted', 'after_planeAccepted', 'before_geometricPlaneAccepted',
          'before_path', 'after_path', 'before_n', 'after_n', 'before_mx', 'after_mx', 'before_my', 'after_my',
          'before_deltaN', 'after_deltaN', 'before_angle', 'after_angle']
with (destination / 'casi-prima-dopo.csv').open('w', encoding='utf-8-sig', newline='') as stream:
    writer = csv.DictWriter(stream, fields, delimiter=';'); writer.writeheader()
    for key in before:
        row = dict(Id=key, state=before[key]['state'], criterion=before[key]['criterion'], before_geometricPlaneAccepted=replay.get(key, {}).get('geometricPlaneAccepted'))
        for side, data in (('before', before), ('after', after)):
            row.update({side + '_' + f: data[key][f] for f in ('ms', 'iterations', 'evaluations', 'accepted', 'planeAccepted', 'path', 'n', 'mx', 'my', 'deltaN', 'angle')})
        writer.writerow(row)


def fmt(value, digits=3):
    if value is None:
        return '—'
    if isinstance(value, int):
        return str(value)
    return f'{value:,.{digits}f}'.replace(',', '_').replace('.', ',').replace('_', '.')


def table(headers, rows):
    return '<div class="scroll"><table><thead><tr>' + ''.join('<th>' + html.escape(str(h)) + '</th>' for h in headers) + '</tr></thead><tbody>' + ''.join('<tr>' + ''.join('<td>' + str(c) + '</td>' for c in row) + '</tr>' for row in rows) + '</tbody></table></div>'


def performance(names):
    rows = []
    for key, title in names:
        group = cohorts[key]; b, a = group['before'], group['after']
        rows.append([title, b['cases'], f"{b['accepted']} → {a['accepted']}", f"{fmt(b['sumSeconds'])} → {fmt(a['sumSeconds'])}",
                     f"{fmt(b['p50Ms'])} → {fmt(a['p50Ms'])}", f"{fmt(b['p95Ms'])} → {fmt(a['p95Ms'])}", fmt(group['speedup'], 1) + '×'])
    return table(['Campione', 'Casi', 'Riusciti', 'Totale [s]', 'P50 [ms]', 'P95 [ms]', 'Accelerazione'], rows)


parts = ['<!doctype html><html lang="it"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Stress block — integrazione geometrica della mesh</title>',
         '<style>body{font:16px/1.55 system-ui,sans-serif;color:#16302c;background:#f2f5f2;margin:0}main{max-width:1180px;margin:auto;padding:36px 28px}h1{font-size:34px;line-height:1.15}h2{margin-top:36px}p{max-width:1000px}.tag{color:#476b60;font-weight:700}.box{background:white;border-left:5px solid #168166;padding:16px 22px}.warning{border-color:#be7519}table{border-collapse:collapse;background:white;width:100%;font-size:14px}td,th{padding:10px 12px;border-bottom:1px solid #dbe3df;text-align:right;white-space:nowrap}td:first-child,th:first-child{text-align:left}th{background:#dfece5}.scroll{overflow:auto}a{color:#076b56}code{font-size:.9em}li{margin:6px 0}.muted{color:#50625d}footer{margin-top:36px;font-size:13px}</style><main>',
         '<p class="tag">Checker Concrete · candidata 0.18.0.2 · 23 ottobre 2026</p><h1>Stress block: taglio della sola mesh</h1>',
         '<p>Confronto con la versione 0.18.0.1. La geometria della mesh è preparata una volta; a ogni valutazione si ritagliano le celle sulla fascia di deformazioni in cui il blocco è attivo e si integrano area e momenti statici. Barre e profili metallici conservano il trattamento precedente.</p>',
         '<div class="box"><strong>Dominio plastico:</strong> ' + str(cohorts['Plastic']['after']['accepted']) + '/120 casi riusciti, ' + str(cohorts['Plastic']['after']['fallback']) + ' fallback. Tempo totale ' + fmt(cohorts['Plastic']['before']['sumSeconds']) + ' → ' + fmt(cohorts['Plastic']['after']['sumSeconds']) + ' s.</div>',
         '<p>Il confronto principale riguarda <strong>StressBlock + Plastic</strong>. L’associazione tra stress block e limite elastico non descrive il comportamento elastico del calcestruzzo: i relativi casi restano soltanto come diagnostica in appendice e sono esclusi dai numeri principali.</p>',
         '<h2>Prestazioni e campioni omogenei — dominio plastico</h2>', performance([('Plastic', 'Tutti i casi plastici'), ('PlasticCommon', 'Riusciti in entrambi'), ('PlasticCommonPlanes', 'Riusciti + piani validi in entrambi'), ('PlasticCommonGeometric', 'Riusciti + piani verificati geometricamente')]),
         '<p class="muted">Il totale è la somma delle mediane per caso, non la durata della campagna. Il sottoinsieme geometrico reintegra con il nuovo metodo anche i piani della versione precedente; può essere più piccolo perché elimina punti affetti dall’errore di quadratura.</p>',
         '<h2>Distribuzione completa dei tempi</h2>']
for key, title in [('Plastic', 'Tutti i 120 casi plastici'), ('PlasticCommon', 'Soli casi plastici riusciti in entrambi')]:
    group = cohorts[key]
    parts.append('<h3>' + title + '</h3>' + table(['Misura', 'Prima', 'Dopo'], [[label, fmt(group['before'][field]), fmt(group['after'][field])] for field, label in
        [('sumSeconds', 'Totale [s]'), ('meanMs', 'Media per caso [ms]'), ('p50Ms', 'P50 [ms]'), ('p90Ms', 'P90 [ms]'), ('p95Ms', 'P95 [ms]'), ('p99Ms', 'P99 [ms]'), ('maxMs', 'Massimo [ms]'), ('coldP95Ms', 'P95 prima ricerca [ms]')]]))
parts.extend(['<h2>ConstantN, ConstantEccentricity e piccola eccentricità</h2>', performance([('PlasticConstantNCommon', 'ConstantN plastico, casi comuni'), ('PlasticConstantEccentricityCommon', 'ConstantEccentricity plastico, casi comuni'), ('SmallEccentricity', 'Compressione plastica e/D=0,05'), ('SmallEccentricityCommon', 'e/D=0,05: casi comuni'), ('PlasticReverse', 'Plastico: controllo in ordine inverso')]),
              '<h2>Iterazioni, valutazioni e precisione</h2>'])
parts.append(table(['Campione', 'Iterazioni medie prima → dopo', 'P95 iterazioni', 'Valutazioni scalari medie', 'Fallback'], [
    [label, f"{fmt(cohorts[key]['before']['meanIterations'], 2)} → {fmt(cohorts[key]['after']['meanIterations'], 2)}",
     f"{fmt(cohorts[key]['before']['p95Iterations'], 1)} → {fmt(cohorts[key]['after']['p95Iterations'], 1)}",
     f"{fmt(cohorts[key]['before']['meanScalarEvaluations'], 1)} → {fmt(cohorts[key]['after']['meanScalarEvaluations'], 1)}",
     f"{cohorts[key]['before']['fallback']} → {cohorts[key]['after']['fallback']}"] for key, label in [('Plastic', 'Intero campione plastico'), ('PlasticCommon', 'Plastico, casi comuni'), ('SmallEccentricity', 'e/D=0,05')]]))
parts.append(table(['Residuo sui casi plastici comuni', 'P50 prima → dopo', 'P95 prima → dopo', 'Massimo prima → dopo'], [
    [label + ' [' + precision[label]['before']['unit'] + ']'] + [f"{precision[label]['before'][metric]:.6g} → {precision[label]['after'][metric]:.6g}" for metric in ('p50', 'p95', 'maximum')] for label in precision]))
parts.append('<p>La tabella precedente riguarda i punti dichiarati dal solver. Il controllo più restrittivo seguente confronta invece entrambi i piani con l’integrazione geometrica, sui soli casi plastici per cui entrambi risultano validi.</p>')
parts.append(table(['Residuo dei piani reintegrati geometricamente', 'Casi', 'P95 prima → dopo', 'Massimo prima → dopo'], [
    [label + ' [' + entry['unit'] + ']', entry['cases']] + [
        ('—' if entry['before'][metric] is None else f"{entry['before'][metric]:.6g} → {entry['after'][metric]:.6g}") for metric in ('p95', 'maximum')]
    for label, entry in geometric_precision.items()]))
parts.extend(['<p>Le tolleranze pubbliche restano invariate. La precisione geometrica è verificata con tagli rettangolari e obliqui, celle concave, fori, scale e traslazioni, deformazione uniforme e una sezione rettangolare semplicemente armata con equilibrio e momento resistente in forma chiusa. L’integrazione è esatta, entro l’arrotondamento, rispetto alla geometria poligonale della mesh e alla legge a gradino supportata. La sottrazione puntuale del calcestruzzo alle barre rimane invariata e può ancora introdurre salti.</p>',
              '<p>Il test della sezione rettangolare semplicemente armata verifica direttamente N entro 10⁻⁶ N e il momento entro 0,01 Nmm rispetto alla forma chiusa; la ricerca inversa del momento resistente rientra in 2·10⁻⁵ relativi. Sono tolleranze del test analitico, distinte da quelle generali del solver.</p>',
              '<h2>Quanto vale il solo taglio della mesh?</h2>', performance([('PlasticIntegrationOnly', 'Mesh geometrica + vecchia ricerca, plastico')]),
              '<p>Questa variante diagnostica mantiene il vecchio algoritmo iterativo. La candidata finale usa il Newton già introdotto per i diagrammi continui anche quando lo stress block ha integrazione geometrica. Il confronto isola quindi l’effetto dell’integrazione da quello della ricerca.</p>',
              '<h2>Appendice diagnostica — include anche il dominio elastico</h2><details><summary>Apri risultati completi e casi che cambiano esito</summary>',
              performance([('All', 'Intero campione misto'), ('Elastic', 'Elastico: solo diagnostica'), ('AllCommon', 'Campione misto riuscito in entrambi'), ('IntegrationOnly', 'Sola integrazione, campione misto')]),
              '<div class="box warning"><strong>Persi rispetto alla vecchia accettazione:</strong> ' + str(len(lost)) + '. <strong>Nuovi riusciti:</strong> ' + str(len(gained)) + '.</div>',
              table(['Caso prima riuscito', 'Piano già valido con vecchia integrazione', 'Piano valido con integrazione geometrica', 'Residui geometrici'], [[html.escape(k), replay.get(k, {}).get('oldPlaneAccepted'), replay.get(k, {}).get('geometricPlaneAccepted'), '<br>'.join(html.escape(f"{r['Quantity']}: {r['Residual']:.6g} / {r['Tolerance']:.6g}") for r in replay.get(k, {}).get('residuals', []))] for k in lost]),
              '<p>I tre casi R3 del dominio elastico sono elencati senza nasconderli nel totale. Nei due casi a eccentricità costante il piano restituito era già fuori tolleranza nella versione precedente. Nel caso N=0, direzione 30°, la reintegrazione geometrica annulla la forza artificiale del calcestruzzo alla soglia di attivazione e mostra un errore angolare di circa 0,151 rad (8,66°). Questo controllo invalida quei piani precedenti; non costituisce, da solo, una prova di inesistenza di qualsiasi altra soluzione.</p>',
              '<p>Nuovi riusciti: ' + ', '.join(html.escape(k) for k in gained) + '.</p></details>',
              '<h2>Regressioni e stato della candidata</h2>',
              '<p>Controllo dei diagrammi continui: ' + str(len(continuous_before)) + ' casi, ' + str(len(continuous_changes)) + ' differenze nelle forze, negli esiti, nei percorsi e nei contatori. Casi stress block con esiti, forze o contatori variabili tra ripetizioni: ' + str(cohorts['All']['before']['unstable']) + ' prima, ' + str(cohorts['All']['after']['unstable']) + ' dopo. Suite complete:</p>',
              table(['Suite', 'Superati', 'Falliti'], [[html.escape(s['name']), s['passed'], s['failed']] for s in tests['results']]),
              '<p>API compatibili: ' + str(tests['apiCompatible']) + '. I cinque fallimenti storici sono gli stessi già presenti nella 0.18.0.1; attesi e fixture non sono stati aggiornati. La candidata non è un rilascio ufficiale.</p>',
              '<h2>Metodo e riproducibilità</h2><ul><li>.NET 8 Release x64, tiered compilation disabilitata; misure sequenziali, senza altre nostre build o suite in parallelo. Host non dedicato.</li><li>Una prima ricerca a freddo e cinque ripetizioni a regime per caso. Ordine inverso sui 120 casi plastici e variante di sola integrazione: tre ripetizioni. Controllo dei diagrammi continui: una ripetizione, usata per la regressione numerica.</li><li>Tempo comprensivo di ricerca, fallback e validazione interna; preparazione, scrittura dei dati e reintegrazione diagnostica esterna esclusi. I contatori delle valutazioni scalari non comprendono la costruzione vettoriale del dominio di fallback, il cui tempo è invece incluso.</li><li>240 casi originali, sei geometrie, due domini e cinque criteri. Campione separato di 72 compressioni plastiche a e/D=0,05. I cataloghi sono strutturati e correlati, non un campione casuale della produzione.</li></ul>',
              '<p><a href="casi-prima-dopo.csv">Dati caso per caso</a> · <a href="riepilogo.json">Riepilogo completo</a> · <a href="misure.zip">Misure grezze</a> · <a href="impronte.json">Impronte e provenienza</a></p>',
              '<footer>Riferimento geometrico: <a href="https://doc.cgal.org/4.3/Polygon/group__PkgPolygon2Functions.html">area di poligoni orientati</a>. Implementazione interna C#, senza nuove dipendenze. Codice e istruzioni riproducibili in tools/Concrete.DomainBenchmark.</footer></main></html>'])
(destination / 'report-prima-dopo.html').write_text('\n'.join(parts), encoding='utf-8')
print(json.dumps(dict(cohorts=cohorts, lost=lost, gained=gained, continuousChanges=continuous_changes, precision=precision), ensure_ascii=False, indent=2))
