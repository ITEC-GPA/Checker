"""Paired analysis of the fixed parabola-rectangle catalogue; called by Report.py."""
import csv
import html
import json
import math
from pathlib import Path
import statistics as st


def quantile(values, p):
    values = sorted(v for v in values if v is not None and math.isfinite(v))
    if not values:
        return None
    x = (len(values) - 1) * p
    lo = int(x)
    return values[lo] + (values[min(lo + 1, len(values) - 1)] - values[lo]) * (x - lo)


def load(path):
    groups = {}
    for line in path.read_text(encoding='utf-8-sig').splitlines():
        row = json.loads(line)
        groups.setdefault(row['Id'], []).append(row)
    result = {}
    meta = json.loads(Path(str(path) + '.meta.json').read_text())
    assert len(groups) == meta['cases']
    for key, rows in groups.items():
        assert sorted(r['run'] for r in rows) == list(range(meta['repeats'] + 1)), key
        warm = [r for r in rows if r['run'] > 0]
        r = dict(warm[0])
        r['ms'] = st.median(x['ms'] for x in warm)
        r['coldMs'] = next(x['ms'] for x in rows if x['cold'])
        r['accepted'] = all(x['accepted'] and not x['error'] for x in rows)
        r['planeAccepted'] = all(x['planeAccepted'] and not x['error'] for x in rows)
        r['stable'] = all(all(x[f] == rows[0][f] for f in
                             ('n', 'mx', 'my', 'accepted', 'planeAccepted', 'path', 'iterations', 'evaluations')) for x in rows)
        r['warmMinMs'], r['warmMaxMs'] = min(x['ms'] for x in warm), max(x['ms'] for x in warm)
        result[key] = r
    return result


def stats(rows):
    rows = list(rows)
    return dict(cases=len(rows), accepted=sum(r['accepted'] for r in rows),
                planeAccepted=sum(r['accepted'] and r['planeAccepted'] for r in rows),
                medianMs=quantile([r['ms'] for r in rows], .5),
                p90Ms=quantile([r['ms'] for r in rows], .9),
                p95Ms=quantile([r['ms'] for r in rows], .95),
                p99Ms=quantile([r['ms'] for r in rows], .99),
                maxMs=max((r['ms'] for r in rows), default=None),
                sumSeconds=sum(r['ms'] for r in rows)/1000,
                coldP95Ms=quantile([r['coldMs'] for r in rows], .95),
                meanIterations=st.mean(r['iterations'] for r in rows) if rows else None,
                p95Iterations=quantile([r['iterations'] for r in rows], .95),
                meanScalarEvaluations=st.mean(r['evaluations'] for r in rows) if rows else None,
                fallback=sum(r['path'] != 'iterative' for r in rows), unstable=sum(not r['stable'] for r in rows))


def residual(row, quantity, plane=False):
    return next((abs(r['Residual']) for r in row['planeResiduals' if plane else 'residuals']
                 if r['Quantity'] == quantity), None)


def capacity(row):
    if row['n'] is None:
        return None
    if row['criterion'] == 'ConstantN':
        return math.hypot(row['mx'], row['my'])
    if row['demandN']:
        return row['n']/row['demandN']
    return (row['mx']*row['demandMx'] + row['my']*row['demandMy'])/(row['demandMx']**2 + row['demandMy']**2)


def capacity_change(b, a):
    cb, ca = capacity(b), capacity(a)
    return abs(ca-cb)/abs(cb)*100 if cb is not None and ca is not None and abs(cb) > 1e-12 else None


def build(measure, dest):
    b, a, rb, ra = [load(measure/(name+'.jsonl')) for name in
                    ('focus-before', 'focus-after', 'focus-before-reverse', 'focus-after-reverse')]
    assert b.keys() == a.keys() == rb.keys() == ra.keys()
    for key in b:
        for field in ('demandN', 'demandMx', 'demandMy', 'section', 'diagram', 'state', 'criterion'):
            assert b[key][field] == a[key][field] == rb[key][field] == ra[key][field], (key, field)
    result = {}
    intermediate_path=dest/'focus-candidata-intermedia.json'
    intermediate=json.loads(intermediate_path.read_text(encoding='utf-8')) if intermediate_path.exists() else None
    for criterion in ('ConstantN', 'ConstantEccentricity'):
        keys = [k for k in b if b[k]['criterion'] == criterion]
        common = [k for k in keys if b[k]['accepted'] and a[k]['accepted']]
        checked = [k for k in common if b[k]['planeAccepted'] and a[k]['planeAccepted']]
        item = dict(total=len(keys), commonCount=len(common), checkedCount=len(checked),
                    new=[k for k in keys if not b[k]['accepted'] and a[k]['accepted']],
                    lost=[k for k in keys if b[k]['accepted'] and not a[k]['accepted']],
                    rejectedBoth=[k for k in keys if not b[k]['accepted'] and not a[k]['accepted']],
                    planeFailuresBefore=[k for k in keys if b[k]['accepted'] and not b[k]['planeAccepted']],
                    planeFailuresAfter=[k for k in keys if a[k]['accepted'] and not a[k]['planeAccepted']])
        for cohort, ids in [('all', keys), ('common', common), ('checked', checked)]:
            old, new = stats(b[k] for k in ids), stats(a[k] for k in ids)
            item[cohort] = dict(before=old, after=new, speedup=old['sumSeconds']/new['sumSeconds'] if ids else None)
        item['separateAccepted'] = dict(before=stats(b[k] for k in keys if b[k]['accepted']),
                                        after=stats(a[k] for k in keys if a[k]['accepted']))
        item['reverseCommon'] = dict(before=stats(rb[k] for k in common), after=stats(ra[k] for k in common))
        item['byState'] = {state:dict(before=stats(b[k] for k in common if b[k]['state']==state),
                                    after=stats(a[k] for k in common if a[k]['state']==state)) for state in ('Plastic', 'Elastic')}
        item['slower'] = [k for k in common if a[k]['ms'] > b[k]['ms']*1.2 and a[k]['ms']-b[k]['ms'] > .1]
        item['capacityChangeP95Percent'] = quantile([capacity_change(b[k], a[k]) for k in common], .95)
        item['capacityChangeMaxPercent'] = quantile([capacity_change(b[k], a[k]) for k in common], 1)
        item['checkedCapacityChangeP95Percent'] = quantile([capacity_change(b[k], a[k]) for k in checked], .95)
        item['checkedCapacityChangeMaxPercent'] = quantile([capacity_change(b[k], a[k]) for k in checked], 1)
        if criterion == 'ConstantEccentricity':
            original = [k for k in common if not k.startswith('Focus/')]
            item['originalCommon'] = dict(before=stats(b[k] for k in original),after=stats(a[k] for k in original))
            item['byStateAndAxialSign'] = []
            for state in ('Plastic','Elastic'):
                for sign,label in ((-1,'Compressione'),(0,'Flessione pura'),(1,'Trazione')):
                    subset=[k for k in common if b[k]['state']==state and (1 if b[k]['demandN']>0 else -1 if b[k]['demandN']<0 else 0)==sign]
                    item['byStateAndAxialSign'].append(dict(state=state,load=label,before=stats(b[k] for k in subset),after=stats(a[k] for k in subset)))
            item['compressionPlasticByEccentricity'] = []
            for ratio in ('0.05','0.2','0.75','2'):
                subset=[k for k in common if k.startswith('Focus/') and b[k]['state']=='Plastic' and b[k]['demandN']<0 and k.split('/')[-2]==ratio]
                quality=[k for k in subset if b[k]['planeAccepted'] and a[k]['planeAccepted']]
                item['compressionPlasticByEccentricity'].append(dict(ratio=float(ratio),before=stats(b[k] for k in subset),after=stats(a[k] for k in subset),
                    checkedBefore=stats(b[k] for k in quality),checkedAfter=stats(a[k] for k in quality),
                    reverseBefore=stats(rb[k] for k in subset),reverseAfter=stats(ra[k] for k in subset)))
        result[criterion] = item

    mismatches = [k for k in b if any(b[k][f] != rb[k][f] or a[k][f] != ra[k][f] for f in
                                    ('n', 'mx', 'my', 'iterations', 'evaluations', 'accepted', 'planeAccepted', 'path'))]
    result['reverseNumericalDifferences'] = mismatches
    fresh_before, fresh_after = [load(measure/(name+'.jsonl')) for name in ('low-before-check','low-after-check')]
    assert fresh_before.keys()==fresh_after.keys()
    fresh_mismatches=[k for k in fresh_before if any(fresh_before[k][f]!=b[k][f] or fresh_after[k][f]!=a[k][f] for f in
                                                    ('n','mx','my','iterations','evaluations','accepted','planeAccepted','path'))]
    fresh=dict(before=stats(fresh_before.values()),after=stats(fresh_after.values()),numericalDifferences=fresh_mismatches)
    result['freshSmallEccentricityControl']=fresh
    (dest/'focus-parabola-rettangolo.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
    fields = ['Id', 'section', 'state', 'criterion', 'demandN', 'demandMx', 'demandMy', 'common', 'common_plane_checked', 'capacity_change_percent']
    measures = ['accepted', 'planeAccepted', 'ms', 'coldMs', 'iterations', 'evaluations', 'bracketIterations', 'path',
                'warmMinMs', 'warmMaxMs', 'n', 'mx', 'my', 'planeN', 'planeMx', 'planeMy', 'stable']
    fields += [prefix+'_'+field for prefix in ('before', 'after') for field in measures]
    fields += [prefix+'_'+field for prefix in ('before', 'after') for field in ('N_residual_N', 'angle_rad', 'plane_N_residual_N', 'plane_angle_rad')]
    with (dest/'focus-parabola-rettangolo.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer=csv.writer(stream, delimiter=';'); writer.writerow(fields)
        for k in b:
            old,new=b[k],a[k]
            common=old['accepted'] and new['accepted']
            row=[k]+[old[f] for f in ('section','state','criterion','demandN','demandMx','demandMy')]
            row += [common, common and old['planeAccepted'] and new['planeAccepted'],capacity_change(old,new)]
            row += [r[f] for r in (old,new) for f in measures]
            row += [value for r in (old,new) for value in (residual(r,'N'),residual(r,'SearchAngle'),residual(r,'N',True),residual(r,'SearchAngle',True))]
            writer.writerow(row)

    def fmt(v):
        if v is None: return '—'
        if isinstance(v,str): return html.escape(v)
        if isinstance(v,int): return str(v)
        return (f'{v:.4g}' if 0 < abs(v) < .001 else f'{v:,.3f}').replace(',',' ').replace('.',',')
    def table(headers, rows):
        return '<table><thead><tr>'+''.join('<th>'+html.escape(h)+'</th>' for h in headers)+'</tr></thead><tbody>'+''.join(
            '<tr>'+''.join('<td>'+fmt(v)+'</td>' for v in row)+'</tr>' for row in rows)+'</tbody></table>'
    parts=[]
    def p(text): parts.append('<p>'+text+'</p>')
    def h(text): parts.append('<h2>'+html.escape(text)+'</h2>')
    h('Focus prioritario: parabola-rettangolo, stessi casi prima e dopo')
    p('<strong>Il confronto principale usa esclusivamente gli stessi ID accettati da entrambe le versioni.</strong> '
      'I casi accettati solo dopo sono conteggiati a parte e non entrano nelle statistiche principali di tempo, iterazioni o precisione. '
      'La frequenza d’uso indicata del 90% motiva questo focus; non viene trasformata in una media pesata, perché non conosciamo la distribuzione reale di criteri, geometrie e azioni.')
    lost_count=sum(len(result[c]['lost']) for c in ('ConstantN','ConstantEccentricity'))
    bad_after=sum(len(result[c]['planeFailuresAfter']) for c in ('ConstantN','ConstantEccentricity'))
    new_plane_failures=sum(len(set(result[c]['planeFailuresAfter'])-set(result[c]['planeFailuresBefore'])) for c in ('ConstantN','ConstantEccentricity'))
    p(f'<strong>Robustezza:</strong> {lost_count} casi prima accettati e ora respinti; {bad_after} punti accettati dopo con piano reintegrato fuori tolleranza, '
      f'di cui {new_plane_failures} nuovi rispetto alla baseline. Questi esiti restano visibili nei conteggi e nel confronto più restrittivo dei piani validi.')
    low=result['ConstantEccentricity']['compressionPlasticByEccentricity'][0]
    p('<strong>ConstantEccentricity: verifica della correzione a piccola eccentricità.</strong> '
      f'Nei {low["before"]["cases"]} casi comuni con e/D=0,05, la mediana passa da {fmt(low["before"]["medianMs"])} a {fmt(low["after"]["medianMs"])} ms '
      f'e il P95 da {fmt(low["before"]["p95Ms"])} a {fmt(low["after"]["p95Ms"])} ms. '
      f'I fallback passano da {low["before"]["fallback"]} a {low["after"]["fallback"]}.')
    if intermediate:
        prior=intermediate['ConstantEccentricity']['compressionPlasticByEccentricity'][0]['after']
        parts.append(table(['Piccola eccentricità: stessi 72 casi','P50 [ms]','P95 [ms]','Iterazioni medie','Fallback'],[
            [label,s['medianMs'],s['p95Ms'],s['meanIterations'],s['fallback']] for label,s in
            [('Baseline originale',low['before']),('Candidata intermedia, con rallentamento',prior),('Candidata corretta',low['after'])]]))
        p('La candidata intermedia è conservata separatamente per rendere verificabile la correzione. '
          'Le tabelle principali confrontano sempre la baseline originale con la candidata corretta. '
          'I tempi della versione intermedia sono una campagna precedente sullo stesso catalogo e ambiente, non una terza esecuzione intercalata alle due definitive.')
    parts.append(table(['Controllo ravvicinato finale, e/D=0,05','Casi','P50 [ms]','P95 [ms]','Iterazioni medie','Fallback'],[
        [label,s['cases'],s['medianMs'],s['p95Ms'],s['meanIterations'],s['fallback']]
        for label,s in [('Baseline originale',fresh['before']),('Candidata corretta',fresh['after'])]]))
    p(f'Questo controllo riesegue entrambe le versioni in sequenza dopo i test, con cinque ripetizioni a regime. '
      f'Differenze numeriche o nei contatori rispetto alla campagna completa: {len(fresh_mismatches)}.')
    p('Catalogo aggiuntivo fissato prima delle misure: 1.896 casi, tutti parabola-rettangolo, sulle stesse sei sezioni e sui due domini. '
      'ConstantN: 576 casi, quattro N prefissati per sezione e 12 direzioni ogni 30°. ConstantEccentricity: 1.320 casi, '
      'N=±100 kN ed e=√(Mx²+My²)/|N|, con e/D=0,05; 0,20; 0,75; 2,00. D è max(b,h) per le sezioni rettangolari e il diametro per quelle circolari, espresso in metri; '
      '12 direzioni, più flessione pura e i 24 casi originari, inclusi i due C2 critici. Compressione N&lt;0. '
      'N, momenti, geometria e materiale sono identici nei due eseguibili; nessun carico è ricavato dalla soluzione di una versione.')
    p('Cinque ripetizioni a regime e controllo con ordine invertito, tre ripetizioni. P95 = percentile delle mediane per caso. '
      'I casi sono un catalogo tecnico, correlati per geometria e simmetria: il percentile descrive questo catalogo, non una probabilità stimata sui vostri modelli di produzione.')
    parts.append(table(['Criterio','Totali','Accettati prima','Accettati dopo','Comuni','Solo dopo','Solo prima','Respinti entrambi','Comuni + piano valido'],[
        [c,s['total'],s['all']['before']['accepted'],s['all']['after']['accepted'],s['commonCount'],len(s['new']),len(s['lost']),len(s['rejectedBoth']),s['checkedCount']]
        for c,s in result.items() if c.startswith('Constant')]))
    rows=[]
    for c in ('ConstantN','ConstantEccentricity'):
        s=result[c]['common']; old,new=s['before'],s['after']
        rows.append([c,old['cases'],old['medianMs'],new['medianMs'],old['p95Ms'],new['p95Ms'],old['sumSeconds'],new['sumSeconds'],s['speedup']])
    parts.append(table(['Solo casi comuni','Casi','P50 prima [ms]','P50 dopo [ms]','P95 prima [ms]','P95 dopo [ms]','Somma prima [s]','Somma dopo [s]','Fattore'],rows))
    parts.append(table(['Solo casi comuni','Iterazioni medie prima/dopo','Iterazioni P95 prima/dopo','Valutazioni scalari medie prima/dopo','Fallback prima/dopo','Peggiorati >20% e >0,1 ms'],[
        [c,fmt(s['common']['before']['meanIterations'])+' / '+fmt(s['common']['after']['meanIterations']),
         fmt(s['common']['before']['p95Iterations'])+' / '+fmt(s['common']['after']['p95Iterations']),
         fmt(s['common']['before']['meanScalarEvaluations'])+' / '+fmt(s['common']['after']['meanScalarEvaluations']),
         str(s['common']['before']['fallback'])+' / '+str(s['common']['after']['fallback']),len(s['slower'])]
        for c,s in result.items() if c.startswith('Constant')]))
    h('Controllo della selezione: quanto cambia il P95')
    rows=[]
    for c in ('ConstantN','ConstantEccentricity'):
        s=result[c]
        for label,group in [('Tutti, inclusi respinti',s['all']),('Accettati separatamente — campioni diversi',s['separateAccepted']),
                            ('Stessi casi accettati da entrambi',s['common']),('Stessi casi, anche piani validi',s['checked'])]:
            old,new=group['before'],group['after']
            rows.append([c,label,str(old['cases'])+' / '+str(new['cases']),old['p95Ms'],new['p95Ms']])
    parts.append(table(['Criterio','Selezione','Casi prima/dopo','P95 prima [ms]','P95 dopo [ms]'],rows))
    p('“Piano valido” significa che le risultanti ottenute reintegrando il piano deformativo restituito rispettano gli stessi vincoli e le stesse tolleranze pubbliche del punto resistente. '
      'È un controllo ulteriore, eseguito fuori dal timer: impedisce di confondere un punto interpolato accettabile con un piano che non soddisfa la richiesta. '
      'Non costituisce un riferimento indipendente per l’accuratezza del modello costitutivo o dell’integrazione.')
    parts.append(table(['Criterio','Punti accettati con piano fuori tolleranza prima','Dopo'],[
        [c,len(s['planeFailuresBefore']),len(s['planeFailuresAfter'])] for c,s in result.items() if c.startswith('Constant')]))
    h('ConstantEccentricity: coda dei tempi e prima ricerca')
    ce=result['ConstantEccentricity']
    rows=[]
    for label,group in [('24 casi originari comuni',ce['originalCommon']),('Comuni, entrambi i domini',ce['common']),('Comuni, plastico',ce['byState']['Plastic']),
                        ('Comuni, elastico',ce['byState']['Elastic']),('Comuni, ordine inverso',ce['reverseCommon'])]:
        old,new=group['before'],group['after']
        rows.append([label,old['cases'],old['p90Ms'],new['p90Ms'],old['p95Ms'],new['p95Ms'],old['p99Ms'],new['p99Ms'],old['maxMs'],new['maxMs'],old['coldP95Ms'],new['coldP95Ms']])
    parts.append(table(['Campione','Casi','P90 prima','P90 dopo','P95 prima','P95 dopo','P99 prima','P99 dopo','Max prima','Max dopo','P95 prima ricerca, prima','Dopo'],rows))
    p('Tutti i tempi della tabella sono in ms. P95 prima e dopo si riferiscono alla stessa popolazione di ID, '
      'ma possono essere determinati da casi diversi: sotto sono riportati i casi più lenti dopo con il rispettivo tempo prima.')
    parts.append(table(['Dominio','Tipo di azione','Casi comuni','P50 prima [ms]','P50 dopo [ms]','P95 prima [ms]','P95 dopo [ms]','Fallback prima/dopo'],[
        [s['state'],s['load'],s['before']['cases'],s['before']['medianMs'],s['after']['medianMs'],s['before']['p95Ms'],s['after']['p95Ms'],
         str(s['before']['fallback'])+' / '+str(s['after']['fallback'])] for s in ce['byStateAndAxialSign']]))
    parts.append(table(['Compressione plastica e/D','Casi comuni','P50 prima [ms]','P50 dopo [ms]','P95 prima [ms]','P95 dopo [ms]','Iterazioni medie prima/dopo','Fallback prima/dopo','P95 inverso prima [ms]','P95 inverso dopo [ms]'],[
        [s['ratio'],s['before']['cases'],s['before']['medianMs'],s['after']['medianMs'],s['before']['p95Ms'],s['after']['p95Ms'],
         fmt(s['before']['meanIterations'])+' / '+fmt(s['after']['meanIterations']),str(s['before']['fallback'])+' / '+str(s['after']['fallback']),
         s['reverseBefore']['p95Ms'],s['reverseAfter']['p95Ms']] for s in ce['compressionPlasticByEccentricity']]))
    p(f'Applicando anche il filtro sul piano valido, il sottogruppo e/D=0,05 contiene {low["checkedBefore"]["cases"]} casi; '
      f'P95 {fmt(low["checkedBefore"]["p95Ms"])} → {fmt(low["checkedAfter"]["p95Ms"])} ms. '
      'Questo confronto esclude sia i casi accettati soltanto dopo sia quelli con piano fuori tolleranza in una delle due versioni. '
      'La correzione sceglie il campo iniziale mediante il rapporto adimensionale tra momenti e N, indipendente dal modulo del vettore di carico. '
      'Nella compressione a piccola eccentricità inizia dal campo di sezione completamente compressa. Un test dedicato riscalando insieme N e M verifica l’invarianza della resistenza trovata.')
    ids=[k for k in b if b[k]['criterion']=='ConstantEccentricity' and b[k]['accepted'] and a[k]['accepted']]
    slow=sorted(ids,key=lambda k:a[k]['ms'],reverse=True)[:12]
    parts.append(table(['Caso comune','Prima [ms]','Dopo [ms]','Iterazioni prima/dopo','Percorso prima/dopo','Residuo angolare dopo [°]','Piano valido prima/dopo'],[
        [k,b[k]['ms'],a[k]['ms'],str(b[k]['iterations'])+' / '+str(a[k]['iterations']),b[k]['path']+' / '+a[k]['path'],
         residual(a[k],'SearchAngle')*180/math.pi,str(b[k]['planeAccepted'])+' / '+str(a[k]['planeAccepted'])] for k in slow]))
    anchors=[k for k in ids if not k.startswith('Focus/') and b[k]['section']=='C2' and b[k]['state']=='Plastic']
    parts.append(table(['C2 plastico del campione originario','Prima [ms]','Dopo [ms]','Iterazioni prima/dopo','Fallback dopo','Reintegrazione ΔN [N]','ΔMx [kNm]','ΔMy [kNm]'],[
        [k,b[k]['ms'],a[k]['ms'],str(b[k]['iterations'])+' / '+str(a[k]['iterations']),a[k]['path'],a[k]['planeN']*1000,a[k]['planeMx'],a[k]['planeMy']] for k in anchors]))
    if any(a[k]['ms']>b[k]['ms'] for k in anchors):
        p('<strong>L’ampliamento del catalogo non elimina il rallentamento dei casi C2 originari.</strong> '
          'Il loro peso sul percentile diminuisce quando si aggiungono altri casi: per questo sono riportati sia il P95 dei 24 casi originari sia i tempi individuali di C2. '
          'I tempi maggiori comprendono i tentativi del nuovo Newton prima del fallback; un guadagno del P95 complessivo non va interpretato come risoluzione di questi casi.')
    p(f'Differenze tra ordine diretto e inverso nei risultati, accettazione, percorsi o contatori: {len(mismatches)} casi. '
      'La variabilità temporale va distinta da cambiamenti numerici. I fallback e i riavvii sono inclusi nel tempo e nei conteggi.')
    h('Precisione sui soli casi comuni e controllo delle resistenze')
    rows=[]
    for c in ('ConstantN','ConstantEccentricity'):
        common=[k for k in b if b[k]['criterion']==c and b[k]['accepted'] and a[k]['accepted']]
        for label,quantity,factor in [('N imposto [N]','N',1),('Direzione [°]','SearchAngle',180/math.pi)]:
            if not common or residual(b[common[0]],quantity) is None: continue
            for plane in (False,True):
                values=[[residual(data[k],quantity,plane)*factor for k in common] for data in (b,a)]
                rows.append([c,label+(' — piano reintegrato' if plane else ' — punto restituito'),len(common),
                             quantile(values[0],.95),quantile(values[1],.95),max(values[0]),max(values[1])])
    parts.append(table(['Criterio','Residuo assoluto','Casi','P95 prima','P95 dopo','Max prima','Max dopo'],rows))
    p('Per ConstantEccentricity N non è imposto: si controlla la direzione nel sistema convenzionale (N in kN, M in kNm), '
      'non lo scarto N rispetto al carico iniziale. Il confronto delle resistenze usa |M| a N costante e il moltiplicatore λ a eccentricità costante '
      '(λ=N_R/N se N≠0; proiezione dei momenti nella flessione pura). Le differenze prima/dopo non sono errori rispetto a una soluzione esatta.')
    parts.append(table(['Criterio','Campione','P95 variazione assoluta resistenza [%]','Massimo [%]'],[
        row for c in ('ConstantN','ConstantEccentricity') for row in (
            [c,'Accettati da entrambi',result[c]['capacityChangeP95Percent'],result[c]['capacityChangeMaxPercent']],
            [c,'Anche piani validi in entrambe',result[c]['checkedCapacityChangeP95Percent'],result[c]['checkedCapacityChangeMaxPercent']])]))
    changed=sorted([k for k in b if b[k]['accepted'] and a[k]['accepted']],key=lambda k:capacity_change(b[k],a[k]) or 0,reverse=True)[:10]
    parts.append(table(['Maggiori differenze fra casi comuni','Variazione [%]','Piano valido prima/dopo','Percorso prima/dopo'],[
        [k,capacity_change(b[k],a[k]),str(b[k]['planeAccepted'])+' / '+str(a[k]['planeAccepted']),b[k]['path']+' / '+a[k]['path']] for k in changed]))
    h('Casi esclusi dal confronto comune')
    for c in ('ConstantN','ConstantEccentricity'):
        s=result[c]
        p(f'<strong>{c}:</strong> {len(s["new"])} accettati soltanto dopo, {len(s["lost"])} soltanto prima, {len(s["rejectedBoth"])} respinti da entrambe. '
          'Un caso respinto non prova da solo una mancata convergenza: può anche non avere un’intersezione ammissibile.')
        newly=s['new']
        if newly:
            parts.append(table(['Accettati soltanto dopo','Dopo [ms]','Iterazioni','Percorso','Residuo angolare [°]','Piano valido','NRd [kN]','MxRd [kNm]','MyRd [kNm]'],[
                [k,a[k]['ms'],a[k]['iterations'],a[k]['path'],residual(a[k],'SearchAngle')*180/math.pi,a[k]['planeAccepted'],a[k]['n'],a[k]['mx'],a[k]['my']] for k in newly[:20]]))
            if len(newly)>20:p('Primi 20 casi; elenco completo nel JSON e nel CSV.')
        for label,ids in [('Accettati prima con piano fuori tolleranza',s['planeFailuresBefore']),('Accettati dopo con piano fuori tolleranza',s['planeFailuresAfter'])]:
            if ids:p(html.escape(label+': '+', '.join(ids[:12]))+(' · Elenco limitato ai primi 12; tutti gli ID sono nel JSON.' if len(ids)>12 else ''))
        if s['lost']:p(html.escape('Accettati soltanto prima: '+', '.join(s['lost'])))
        if s['lost']:
            parts.append(table(['Regressione','N richiesto [kN]','NRd prima [kN]','NRd dopo [kN]','Residuo N prima [N]','Residuo N dopo [N]','Iterazioni prima/dopo','Percorso prima/dopo'],[
                [k,b[k]['demandN'],b[k]['n'],a[k]['n'],residual(b[k],'N'),residual(a[k],'N'),str(b[k]['iterations'])+' / '+str(a[k]['iterations']),b[k]['path']+' / '+a[k]['path']] for k in s['lost']]))
    h('Indicazioni operative emerse dal confronto')
    p('Sono state corrette l’inizializzazione a eccentricità costante e la perdita del candidato R3 quando il raffinamento più severo si arresta. '
      'Il metodo cerca inizialmente 1/10 delle tolleranze di arresto; se quel raffinamento si arresta, può conservare un piano effettivamente valutato che soddisfa tutte le tolleranze pubbliche. '
      'Non si allargano le soglie di accettazione e non si usa un punto interpolato per questo recupero. '
      f'Restano {bad_after} punti accettati con piano fuori tolleranza nel campione: sono elencati e separati nelle statistiche. '
      'I tempi peggiori e i fallback residui impediscono di promettere un’accelerazione su ogni singola richiesta; lo stress block rimane invariato.')
    r3='Focus/R3/ParabolaRectangle/Plastic/ConstantN/180/330'
    if r3 in a:
        parts.append(table(['Caso R3 recuperato','Prima','Dopo'],[
            ['Accettato',b[r3]['accepted'],a[r3]['accepted']],['Piano valido',b[r3]['planeAccepted'],a[r3]['planeAccepted']],
            ['NRd [kN]',b[r3]['n'],a[r3]['n']],['Residuo N [N]',residual(b[r3],'N'),residual(a[r3],'N')],
            ['Residuo direzione [°]',residual(b[r3],'SearchAngle')*180/math.pi,residual(a[r3],'SearchAngle')*180/math.pi],
            ['Iterazioni',b[r3]['iterations'],a[r3]['iterations']],['Tempo [ms]',b[r3]['ms'],a[r3]['ms']]]))
    p('<a href="focus-parabola-rettangolo.csv">CSV completo dei 1.896 casi appaiati</a> · '
      '<a href="focus-parabola-rettangolo.json">Statistiche, elenchi e controllo in ordine inverso</a>. '
      'I booleani common e common_plane_checked permettono di riprodurre esattamente i due filtri; evaluations conta solo le valutazioni scalari, non la costruzione vettoriale del dominio.')
    if intermediate:p('<a href="focus-candidata-intermedia.json">Misure della candidata intermedia prima della correzione</a>.')
    return '\n'.join(parts), result
