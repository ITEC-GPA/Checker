"""Build the paired timing/iteration/precision report with Python's standard library.
Usage: py -3.9 Report.py <measurement-directory> <test-result-directory> <report-directory>
"""
import csv
import hashlib
import html
import json
import math
from pathlib import Path
import statistics as st
import sys
import xml.etree.ElementTree as ET
from FocusReport import build as build_focus

measure, tests, dest = map(Path, sys.argv[1:4])
dest.mkdir(parents=True, exist_ok=True)

def load(name):
    rows = [json.loads(line) for line in (measure / (name + '.jsonl')).read_text(encoding='utf-8-sig').splitlines()]
    grouped = {}
    for row in rows:
        grouped.setdefault(row['Id'], []).append(row)
    result = {}
    for key, group in grouped.items():
        warm = [r for r in group if r['run'] > 0]
        r = dict(warm[0])
        r['ms'] = st.median(x['ms'] for x in warm)
        r['coldMs'] = next(x['ms'] for x in group if x['cold'])
        r['warmMinMs'] = min(x['ms'] for x in warm)
        r['warmMaxMs'] = max(x['ms'] for x in warm)
        r['stableResults'] = all(all(x[k] == warm[0][k] for k in ('n','mx','my','accepted','iterations','path')) for x in warm)
        result[key] = r
    return result

before, after = load('before'), load('after')
assert before.keys() == after.keys()
rev_before, rev_after = load('before-reverse'), load('after-reverse')
meta = json.loads((measure / 'before.jsonl.meta.json').read_text())

def q(values, p):
    a = sorted(v for v in values if v is not None and math.isfinite(v))
    if not a: return None
    i = (len(a)-1)*p
    lo = int(i); hi = min(lo+1,len(a)-1)
    return a[lo]+(a[hi]-a[lo])*(i-lo)

def stats(data):
    rows = list(data)
    return dict(cases=len(rows), accepted=sum(r['accepted'] for r in rows), fallback=sum(r['path']!='iterative' for r in rows),
        medianMs=q([r['ms'] for r in rows],.5), p95Ms=q([r['ms'] for r in rows],.95), maxMs=max(r['ms'] for r in rows),
        sumSeconds=sum(r['ms'] for r in rows)/1000, coldMedianMs=q([r['coldMs'] for r in rows],.5),
        coldP95Ms=q([r['coldMs'] for r in rows],.95), preparationMedianMs=q([r['prepareMs'] for r in rows],.5),
        meanIterations=st.mean(r['iterations'] for r in rows), medianIterations=q([r['iterations'] for r in rows],.5),
        p95Iterations=q([r['iterations'] for r in rows],.95), maxIterations=max(r['iterations'] for r in rows),
        meanEvaluations=st.mean(r['evaluations'] for r in rows), p95Evaluations=q([r['evaluations'] for r in rows],.95),
        meanBracketIterations=st.mean(r['bracketIterations'] for r in rows), unstable=sum(not r['stableResults'] for r in rows))

groups = {'Diagrammi continui': lambda r:r['diagram']!='StressBlock',
          'Continui accettati da entrambe':lambda r:r['diagram']!='StressBlock' and before[r['Id']]['accepted'] and after[r['Id']]['accepted'],
          'Stress block invariato':lambda r:r['diagram']=='StressBlock', 'Intero campione':lambda r:True}
summary = {}
for name,predicate in groups.items():
    summary[name]={'before':stats(r for r in before.values() if predicate(r)), 'after':stats(r for r in after.values() if predicate(r))}
    summary[name]['speedup']=summary[name]['before']['sumSeconds']/summary[name]['after']['sumSeconds']

def residual(r, name):
    found=[abs(x['Residual']) for x in r['residuals'] if x['Quantity']==name]
    return found[0] if found else None

common=[key for key in before if before[key]['accepted'] and after[key]['accepted'] and before[key]['diagram']!='StressBlock']
precision=[]
for label,name,factor in [('Scarto N imposto [N]','N',1),('Scarto M1 imposto [kNm]','M1',1e-6),
                          ('Scarto M2 imposto [kNm]','M2',1e-6),('Scarto direzione [gradi]','SearchAngle',180/math.pi)]:
    keys=[k for k in common if residual(before[k],name) is not None]
    for percentile in (.5,.95,1):
        precision.append((label+(' — mediana' if percentile==.5 else ' — P95' if percentile==.95 else ' — massimo'),
            len(keys),q([residual(before[k],name)*factor for k in keys],percentile),q([residual(after[k],name)*factor for k in keys],percentile)))

columns=['Id','diagram','state','criterion','before_ms','after_ms','speedup','before_cold_ms','after_cold_ms',
         'before_iterations','after_iterations','before_scalar_evaluations','after_scalar_evaluations','before_bracket_iterations','after_bracket_iterations',
         'before_path','after_path','before_accepted','after_accepted','before_N_error_N','after_N_error_N',
         'before_angle_rad','after_angle_rad','before_NRd_kN','after_NRd_kN','before_MxRd_kNm','after_MxRd_kNm',
         'before_MyRd_kNm','after_MyRd_kNm','before_plane_N_error_kN','after_plane_N_error_kN',
         'before_plane_Mx_error_kNm','after_plane_Mx_error_kNm','before_plane_My_error_kNm','after_plane_My_error_kNm']
with (dest/'casi-prima-dopo.csv').open('w',encoding='utf-8-sig',newline='') as stream:
    writer=csv.writer(stream,delimiter=';');writer.writerow(columns)
    for key,b in before.items():
        a=after[key]
        writer.writerow([key,b['diagram'],b['state'],b['criterion'],b['ms'],a['ms'],b['ms']/a['ms'],b['coldMs'],a['coldMs'],
            b['iterations'],a['iterations'],b['evaluations'],a['evaluations'],b['bracketIterations'],a['bracketIterations'],
            b['path'],a['path'],b['accepted'],a['accepted'],residual(b,'N'),residual(a,'N'),b['angle'],a['angle'],
            b['n'],a['n'],b['mx'],a['mx'],b['my'],a['my'],b['planeN'],a['planeN'],b['planeMx'],a['planeMx'],b['planeMy'],a['planeMy']])

ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
test_summary=[]; failures=[]
for file in ['baseline.trx','ecc-concrete.trx','ecc-GPCChecker.Test.CompositeBridge.trx','ecc-Design.Checks.trx','ecc-ModelChecker.Tests.trx']:
    tree=ET.parse(tests/file)
    results=tree.findall('.//t:UnitTestResult',ns)
    test_summary.append([file,len(results),sum(x.attrib['outcome']=='Passed' for x in results),sum(x.attrib['outcome']=='Failed' for x in results)])
    for x in results:
        if x.attrib['outcome']=='Failed':
            failures.append({'suite':file,'test':x.attrib['testName'],'message':x.findtext('.//t:Message','',ns)})
(dest/'test-da-aggiornare.txt').write_text('\n\n'.join(x['suite']+' / '+x['test']+'\n'+x['message'].strip() for x in failures),encoding='utf-8')

snapshot_before=json.loads((Path(__file__).resolve().parents[2]/'GPCChecker.Test.Concrete/Fixtures/domain-point-contract.json').read_text(encoding='utf-8-sig'))['points']
snapshot_after=json.loads((Path(__file__).resolve().parents[2]/'GPCChecker.Test.Concrete/bin/Release/domain-point-contract.actual.json').read_text(encoding='utf-8-sig'))['points']
stress_snapshot=sum(x==y for x,y in zip(snapshot_before,snapshot_after) if x['diagram']=='StressBlock')
improved=[k for k in before if not before[k]['accepted'] and after[k]['accepted']]
regressed=[k for k in before if before[k]['accepted'] and not after[k]['accepted']]
reverse={'before':stats(rev_before.values()),'after':stats(rev_after.values())}
reverse['speedup']=reverse['before']['sumSeconds']/reverse['after']['sumSeconds']
stress_differences=[k for k in before if before[k]['diagram']=='StressBlock' and any(before[k][f]!=after[k][f] for f in ('n','mx','my','accepted','iterations','evaluations','path'))]
summary.update(metadata=meta,precision=precision,reverse=reverse,improved=improved,regressed=regressed,
               stressBlockDifferences=stress_differences,tests=test_summary,failures=failures)
focus_html, focus_summary = build_focus(measure, dest)
summary['focusParabolaRectangle'] = focus_summary
(dest/'riepilogo.json').write_text(json.dumps(summary,indent=2,ensure_ascii=False),encoding='utf-8')

def fmt(x):
    if x is None:return '—'
    if isinstance(x,str):return html.escape(x)
    if isinstance(x,int):return str(x)
    if x==0:return '0'
    return (f'{x:.4g}' if abs(x)<.001 else f'{x:,.3f}').replace(',',' ').replace('.',',')

def table(headers,rows):
    return '<table><thead><tr>'+''.join('<th>'+html.escape(h)+'</th>' for h in headers)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+fmt(x)+'</td>' for x in row)+'</tr>' for row in rows)+'</tbody></table>'

body=[]
def p(text):body.append('<p>'+text+'</p>')
def h(text):body.append('<h2>'+html.escape(text)+'</h2>')
body.append('<h1>Ricerca del punto resistente<br>Confronto prima / dopo</h1>')
p('23 ottobre 2026 · GPCChecker.Concrete 0.18.0.0 → 0.18.0.1 · branch <code>codex/concrete-domain-iteration</code>')
body.append(focus_html)
h('Confronto generale: catalogo originario dei 960 casi')
s=summary['Diagrammi continui']; b=s['before'];a=s['after']
p(f'<strong>Sui {a["cases"]} casi con diagrammi continui, il tempo complessivo a regime passa da {fmt(b["sumSeconds"])} s a {fmt(a["sumSeconds"])} s: accelerazione {fmt(s["speedup"])}×.</strong> '
  f'I risultati accettati passano da {b["accepted"]} a {a["accepted"]}. Le valutazioni scalari medie delle risultanti per ricerca passano da {fmt(b["meanEvaluations"])} a {fmt(a["meanEvaluations"])}.')
p('Misure sul percorso applicativo completo di ricerca e validazione, compresi gli eventuali fallback. Nessun rilascio o merge su master: i contratti numerici storici restano da approvare e aggiornare.')
h('Modifiche e perimetro')
p('Per i diagrammi diversi dallo stress block: residui adimensionali della linea di carico, Newton su due parametri, soluzione 2×2 con controllo di quasi-singolarità, '
  'backtracking, primo tentativo basato anche sul rapporto dimensionale della sezione e sul ramo di N per i momenti costanti, sequenza coerente dei campi, differenze finite con passo locale adattivo e riuso dei punti di rotazione. '
  'A eccentricità costante il primo tentativo dipende dal rapporto adimensionale M/N e non dal modulo del carico. '
  'Il criterio di arresto controlla le risultanti effettivamente restituite e mira a 1/10 delle tolleranze di arresto; se il raffinamento si arresta, conserva un eventuale piano valutato entro tutte le tolleranze pubbliche, che non cambiano. '
  'I metodi di ripiego esistenti sono conservati. Nessuno stato di primo tentativo è condiviso tra chiamate concorrenti.')
p(f'<strong>Stress block invariato:</strong> stesso metodo iterativo e stessa integrazione. Verifica separata su {stress_snapshot}/256 righe del contratto storico identiche bit per bit. '
  f'Nel benchmark, casi stress block con differenze nelle risultanti, nel percorso o nei contatori: {len(stress_differences)}.')
h('Protocollo riproducibile')
p('960 casi: 6 sezioni, 4 diagrammi, 2 domini (plastico/elastico), 5 criteri. Sezioni R1 300×500 con armatura asimmetrica, R2 400×600, R3 300×500 con armatura su un lato, '
  'C1 circolare D600 (72 lati), C2 D800 (144 lati), S1 1000×200 con 5+5Ø12. Dati completi e sollecitazioni nel catalogo di <code>Program.cs</code>; unità interne N e Nmm. '
  'Materiali e fattori identici, NTC 2018 con αcc=0,85, γc=1,5 e γs=1,15, acciaio B450C elastico perfettamente plastico. Il confronto non modifica leggi costitutive, discretizzazione o fattori.')
p(f'Runtime {html.escape(meta["runtime"])} x64, {html.escape(meta["cpu"])}, {meta["processors"]} processori logici; {html.escape(meta["os"])}. '
  'Compilazione Release, tiered compilation disattivata, warm-up iniziale, una prima ricerca e cinque ricerche successive sullo stesso solver per caso. '
  'Le due versioni sono eseguite in sequenza, senza nostre compilazioni o test concorrenti durante le misure. Il computer non è un banco a carico esclusivo. '
  'Controllo aggiuntivo con ordine invertito (dopo, prima), tre ripetizioni sui 720 casi continui.')
p('Per ciascun caso si usa la mediana delle cinque ricerche successive. P50/P95/massimo nelle tabelle descrivono la distribuzione di queste mediane tra i casi, non il jitter di una singola chiamata. '
  'Il tempo di campione è la somma delle mediane. Preparazione della sezione, post-elaborazione del report e reintegrazione diagnostica sono fuori dal timer di ricerca. '
  'La prima ricerca comprende eventuale costruzione del dominio di fallback; le ricerche successive ne riusano la cache. '
  'I contatori sono inseriti soltanto nelle copie di benchmark di entrambe le versioni; il costo di misura è quindi presente in entrambi i confronti.')
h('Tempi a regime')
body.append(table(['Gruppo','Casi','Mediana prima [ms]','Mediana dopo [ms]','P95 prima [ms]','P95 dopo [ms]','Tempo prima [s]','Tempo dopo [s]','Fattore'],[
    [name,s['before']['cases'],s['before']['medianMs'],s['after']['medianMs'],s['before']['p95Ms'],s['after']['p95Ms'],s['before']['sumSeconds'],s['after']['sumSeconds'],s['speedup']]
    for name,s in summary.items() if name in groups]))
body.append(table(['Diagramma','Mediana prima [ms]','Mediana dopo [ms]','P95 prima [ms]','P95 dopo [ms]'],[
    [diagram,stats(r for r in before.values() if r['diagram']==diagram)['medianMs'],stats(r for r in after.values() if r['diagram']==diagram)['medianMs'],
     stats(r for r in before.values() if r['diagram']==diagram)['p95Ms'],stats(r for r in after.values() if r['diagram']==diagram)['p95Ms']]
    for diagram in sorted(set(r['diagram'] for r in before.values()))]))
body.append(table(['Criterio (diagrammi continui)','Mediana prima [ms]','Mediana dopo [ms]','P95 prima [ms]','P95 dopo [ms]'],[
    [criterion,stats(r for r in before.values() if r['diagram']!='StressBlock' and r['criterion']==criterion)['medianMs'],
     stats(r for r in after.values() if r['diagram']!='StressBlock' and r['criterion']==criterion)['medianMs'],
     stats(r for r in before.values() if r['diagram']!='StressBlock' and r['criterion']==criterion)['p95Ms'],
     stats(r for r in after.values() if r['diagram']!='StressBlock' and r['criterion']==criterion)['p95Ms']]
    for criterion in sorted(set(r['criterion'] for r in before.values()))]))
p(f'Controllo in ordine inverso sui continui: {fmt(reverse["before"]["sumSeconds"])} s prima, {fmt(reverse["after"]["sumSeconds"])} s dopo, fattore {fmt(reverse["speedup"])}×. '
  'I risultati numerici e i contatori sono controllabili nei file grezzi di entrambe le esecuzioni.')
h('Preparazione e prima ricerca')
body.append(table(['Gruppo','Preparazione P50 prima [ms]','Preparazione P50 dopo [ms]','Prima ricerca P50 prima [ms]','Prima ricerca P50 dopo [ms]','Prima ricerca P95 prima [ms]','Prima ricerca P95 dopo [ms]'],[
    [name,s['before']['preparationMedianMs'],s['after']['preparationMedianMs'],s['before']['coldMedianMs'],s['after']['coldMedianMs'],s['before']['coldP95Ms'],s['after']['coldP95Ms']]
    for name,s in summary.items() if name in groups]))
h('Iterazioni, valutazioni delle risultanti e convergenza')
body.append(table(['Gruppo','Iterazioni medie prima','Iterazioni medie dopo','Iterazioni P95 prima','Iterazioni P95 dopo','Valutazioni scalari medie prima','Valutazioni scalari medie dopo','Accettati prima/dopo','Fallback prima/dopo'],[
    [name,s['before']['meanIterations'],s['after']['meanIterations'],s['before']['p95Iterations'],s['after']['p95Iterations'],s['before']['meanEvaluations'],s['after']['meanEvaluations'],
     f'{s["before"]["accepted"]}/{s["after"]["accepted"]}',f'{s["before"]["fallback"]}/{s["after"]["fallback"]}'] for name,s in summary.items() if name in groups]))
p('Iterazioni = aggiornamenti tentati dal metodo principale, inclusi riavvii e tentativi falliti. Le iterazioni di raffinamento del fallback Illinois sono registrate separatamente nel CSV. '
  'Valutazioni scalari = chiamate a CalculateForceResultantForDomain per un piano, comprese differenze finite, backtracking, fallback e validazione interna. La costruzione vettoriale di un intero dominio non è inclusa nel contatore; il suo costo è comunque incluso nei tempi. '
  '“Accettato” significa rispetto dei vincoli numerici del criterio, non verifica strutturale soddisfatta. Un aumento dei fallback può indicare che il metodo rifiuta un candidato impreciso prima accettato.')
p(f'Casi prima respinti e ora accettati: {len(improved)}. Casi prima accettati e ora respinti: {len(regressed)}. '
  f'Casi con risultati o contatori variabili tra le ripetizioni: {summary["Intero campione"]["before"]["unstable"]} prima, {summary["Intero campione"]["after"]["unstable"]} dopo.')
h('Precisione numerica')
p(f'Confronto sui {len(common)} casi continui accettati da entrambe le versioni. Si includono solo le componenti effettivamente vincolate dal criterio: '
  'N in ConstantN/ConstantNMx/ConstantNMy; momenti assegnati nelle altre modalità pertinenti; angolo per ConstantN e ConstantEccentricity. '
  'Questi sono residui rispetto alla richiesta, non errori rispetto a una soluzione esatta della struttura.')
body.append(table(['Indicatore','Casi','Prima','Dopo'],precision))
plane=[]
for label,field,factor in [('Coerenza N punto/piano [N]','planeN',1000),('Coerenza Mx punto/piano [kNm]','planeMx',1),('Coerenza My punto/piano [kNm]','planeMy',1)]:
    keys=[k for k in common if before[k][field] is not None and after[k][field] is not None]
    plane.append([label+' — P95',q([abs(before[k][field])*factor for k in keys],.95),q([abs(after[k][field])*factor for k in keys],.95)])
    plane.append([label+' — massimo',q([abs(before[k][field])*factor for k in keys],1),q([abs(after[k][field])*factor for k in keys],1)])
body.append(table(['Reintegrazione del piano restituito','Prima','Dopo'],plane))
moment_changes=[]
for key in common:
    if before[key]['criterion']=='ConstantN':
        bm=math.hypot(before[key]['mx'],before[key]['my']); am=math.hypot(after[key]['mx'],after[key]['my'])
        if bm>1e-9:moment_changes.append(abs(am-bm)/bm*100)
body.append(table(['Variazione del modulo del momento resistente a N imposto','Valore'],[
    ['Mediana di |M dopo − M prima| / |M prima| [%]',q(moment_changes,.5)],
    ['P95 [%]',q(moment_changes,.95)],['Massimo [%]',q(moment_changes,1)]]))
p('La variazione del momento misura la differenza tra due risultati numerici, non l’errore rispetto a un riferimento indipendente.')
fixed_moments=[k for k in common if before[k]['criterion']=='ConstantMxMy']
axial_changes=[abs(after[k]['n']-before[k]['n'])/abs(before[k]['n'])*100 for k in fixed_moments if abs(before[k]['n'])>1e-9]
body.append(table(['Variazione della resistenza assiale a momenti imposti','Valore'],[
    ['Mediana di |N dopo − N prima| / |N prima| [%]',q(axial_changes,.5)],['P95 [%]',q(axial_changes,.95)],['Massimo [%]',q(axial_changes,1)]]))
p(f'Nei {len(fixed_moments)} casi continui a momenti costanti accettati da entrambe le versioni, i fallback passano da '
  f'{sum(before[k]["path"]!="iterative" for k in fixed_moments)} a {sum(after[k]["path"]!="iterative" for k in fixed_moments)}. '
  'Il primo tentativo distingue correttamente il ramo di compressione da quello di trazione, senza trattare il modulo di N come un vincolo assegnato.')
p('La ricerca Newton restituisce le risultanti del proprio piano deformativo. I fallback preesistenti possono interpolare risultanti: per questo il report misura anche lo scarto dalla reintegrazione del piano. '
  'Nel caso S1 in trazione a N=221,277 kN si osserva un salto della direzione dei momenti nella valutazione esistente della superficie vicino a θ=0,00971427 rad; '
  'il nuovo metodo può rifiutare la falsa convergenza e demandare la ricerca al fallback. Questa discontinuità e l’integrazione non sono modificate.')
h('Casi più lenti e peggioramenti di tempo')
slow=sorted(common,key=lambda k:after[k]['ms'],reverse=True)[:10]
body.append(table(['Caso','Prima [ms]','Dopo [ms]','Iterazioni prima/dopo','Percorso dopo'],[[k,before[k]['ms'],after[k]['ms'],f'{before[k]["iterations"]}/{after[k]["iterations"]}',after[k]['path']] for k in slow]))
slower=sorted([k for k in common if after[k]['ms']>1.2*before[k]['ms'] and after[k]['ms']-before[k]['ms']>.1],key=lambda k:after[k]['ms']-before[k]['ms'],reverse=True)
p(f'Casi continui comuni con peggioramento di tempo superiore al 20% e a 0,1 ms: {len(slower)}. Segue il dettaglio dei primi dieci; il guadagno medio non è uniforme su ogni richiesta.')
body.append(table(['Caso','Prima [ms]','Dopo [ms]','Percorso prima/dopo'],[[k,before[k]['ms'],after[k]['ms'],before[k]['path']+' / '+after[k]['path']] for k in slower[:10]]))
h('Verifiche eseguite e vincoli al rilascio')
body.append(table(['Suite','Totali','Superati','Falliti'],test_summary))
p('I dieci nuovi test coprono 240 inversioni di piani deformativi nelle cinque modalità, 120 ricerche dirette a momenti assegnati, 216 ricerche a piccola eccentricità con carichi riscalati, il recupero del caso R3 entro le tolleranze pubbliche, scala geometrica, poli assiali, determinismo concorrente, estremi dei campi, input non finiti e rifiuto della falsa convergenza sul caso S1. '
  'La correttezza costitutiva e le resistenze sono controllate dalle suite indipendenti già presenti; il benchmark misura prestazioni e residui del solver.')
p('I fallimenti residui riguardano contratti del vecchio algoritmo: snapshot esatti del dominio e della curva momento-curvatura, confronto legacy del momento limite, '
  'un caso che richiede il vecchio arresto oltre tolleranza senza fallback e un test ModelChecker che si aspetta il candidato grezzo fuori tolleranza. '
  'Nessuna fixture è stata rigenerata e nessuna tolleranza è stata allargata. Gli attesi devono essere riesaminati esplicitamente prima del rilascio; la suite non è integralmente verde.')
p('Esempio verificato nella regressione momento-curvatura: a N=−200 kN lo scarto assiale del punto limite passa da −40,8231 N a +0,00760024 N; '
  'il momento limite passa da 192,010838 kNm a 192,003725 kNm (circa −0,0037%). Il cambiamento fa fallire il confronto numerico storico pur riducendo il residuo assiale.')
h('File e riproduzione')
p('<a href="casi-prima-dopo.csv">CSV dei 960 casi</a> · <a href="riepilogo.json">Riepilogo strutturato</a> · <a href="test-da-aggiornare.txt">Dettaglio dei test storici non superati</a>. '
  'CSV UTF-8, separatore punto e virgola, separatore decimale punto; celle vuote per indicatori non applicabili.')
p('<a href="impronte-sorgenti.json">Impronte dei sorgenti e degli eseguibili misurati</a> · '
  '<a href="impronte-dipendenze.json">Dipendenze identiche nelle due versioni</a> · <a href="impronte-dati.json">Impronte delle misure grezze</a>.')
p('Baseline: commit <code>5f97766a</code>. Eseguibili isolati creati da <code>Prepare.ps1</code>; esecuzioni da <code>Run.ps1</code>; report da <code>Report.py</code>, nella cartella <code>tools/Concrete.DomainBenchmark</code>. '
  'Le misure grezze JSONL e i TRX sono conservati nella cartella di audit. Il report caratterizza questo catalogo e questo ambiente; non costituisce una garanzia universale di accelerazione o convergenza.')
p('La baseline originale è invariata e le sue misure sono conservate dalla campagna precedente; la candidata corretta è misurata nuovamente a parità di dipendenze, catalogo e protocollo. '
  'Nel controllo in ordine inverso entrambe le versioni sono rieseguite, candidata poi baseline. Per il sottogruppo critico sono inoltre conservate nuove misure ravvicinate prima/dopo, successive ai test.')

style='''body{font:15px/1.55 Segoe UI,Arial,sans-serif;color:#203047;max-width:1350px;margin:40px auto;padding:0 30px}h1{font-size:34px;line-height:1.18;color:#13283e}h2{font-size:22px;margin-top:34px;border-top:2px solid #dce6ed;padding-top:20px}table{border-collapse:collapse;width:100%;font-size:12px;margin:18px 0}th{background:#193f5a;color:white;text-align:left}td,th{padding:9px;border:1px solid #dae2e8;vertical-align:top;overflow-wrap:anywhere}tbody tr:nth-child(even){background:#f2f6f9}td:not(:first-child){font-variant-numeric:tabular-nums}code{background:#eef2f5;padding:2px 4px}a{color:#135b94}@media print{body{margin:0;padding:0;font-size:10px;max-width:none}h1{font-size:25px}h2{font-size:16px}table{font-size:8px}td,th{padding:4px}tr{break-inside:avoid}h2{break-after:avoid}}'''
(dest/'report-prima-dopo.html').write_text('<!doctype html><html lang="it"><meta charset="utf-8"><title>Ricerca del punto resistente — report prima/dopo</title><style>'+style+'</style><body>\n'+'\n'.join(body)+'\n</body></html>',encoding='utf-8')
manifest={str(p.relative_to(measure)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(measure.glob('*.json*'))}
(dest/'impronte-dati.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in summary.items() if k in groups},indent=2,ensure_ascii=False))
