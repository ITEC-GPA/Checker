"""Audit all 11 legacy closures; standard-library Python only.

Run in any directory: python tools/Steel.LegacyDiagnostics/verify_legacy_closures.py
Optional --results <build/Verify.ps1 output> verifies all ten TRX files too.
Does not rewrite fixtures or modify the checkout.
"""
from pathlib import Path
import argparse
import hashlib
import importlib
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

sys.dont_write_bytecode=True


def audit(results=None):
    diag=Path(__file__).resolve().parent
    repo=diag.parent.parent
    git=['git','-c','safe.directory='+repo.as_posix(),'-C',str(repo)]
    path='GPCChecker.Test.Steel/EN1993p11Test.cs'
    base=subprocess.check_output(git+['show','dafeea29:'+path]).decode('utf-8-sig').replace('\r\n','\n')
    current=(repo/path).read_text(encoding='utf-8-sig')
    cases=[('SectionHBuckling1','hea300_flexural_reference',()),
           ('SectionHInteraction2','ipe300_annex_a_reference',()),
           ('SectionHInteraction3','hea260_annex_a_reference',()),
           ('SectionHLateralTorsionalBuckling1','ltb_ipe_reference',(1,)),
           ('SectionHLateralTorsionalBuckling2','ltb_ipe_reference',(2,)),
           ('SectionCLateralTorsionalBuckling2','channel_ltb_reference',()),
           ('Sap_SectionHAsymmetricTest1','sap_legacy_section_reference',(7,)),
           ('Sap_SectionHAsymmetricTest2','sap_legacy_section_reference',(8,)),
           ('Sap_SectionTTest','sap_legacy_section_reference',(9,)),
           ('Sap_SectionTTest2','sap_legacy_section_reference',(10,)),
           ('Sap_SectionLTest','sap_legacy_section_reference',(11,))]

    def method(text,name):
        start=text.index('        [TestMethod]\n        public void '+name+'()')
        end=start+re.search(r'^        }$',text[start:],re.M).end()
        return text[start:end]

    def setup(text):
        text=text.split('.PerformCheck();')[0]
        return re.sub(r'/\*.*?\*/|//[^\n]*|\s+','',text,flags=re.S)

    without_old,without_new=base,current
    verified=[]
    for index,(name,module,args) in enumerate(cases,1):
        old,new=method(base,name),method(current,name)
        archived=(diag/f'closed-cases/{name}.original.cs.txt').read_text(encoding='utf-8').rstrip()
        assert old.rstrip()==archived,(name,'original changed')
        assert setup(old)==setup(new),(name,'setup changed')
        without_old=without_old.replace(old,'')
        without_new=without_new.replace(new,'')
        reference=importlib.import_module(module).calculate(*args)
        saved=json.loads((diag/f'closed-cases/{name}.reference.json').read_text(encoding='utf-8'))
        assert json.loads(json.dumps(reference,default=str))==saved,(name,'reference not reproducible')
        if index>=4:
            for key,value in reference['values'].items():
                v=format(float(value),'.17g')+'d'
                assert (f'nameof(result.{key}), Value = {v}' in new or
                        f'Assert.AreEqual({v}, result.{key},' in new),(name,key,'constant mismatch')
        if index>=7:
            for key in ('WorkingRatio','BucklingInteraction1Axis','BucklingInteraction2Axis'):
                assert f'double.IsNaN(result.{key})' in new,(name,key,'unsupported result approved')
        verified.append(dict(step=index,test=name,original_preserved=True,inputs_unchanged=True,
                             reference_reproduced=True,
                             closure_kind=('independent_arithmetic_for_declared_inputs' if index<=5 else
                                           'conditional_channel_arithmetic' if index==6 else
                                           'explicit_unsupported_contract')))
    assert without_old==without_new,'Unrelated legacy methods changed'
    allowed=json.loads((repo/'build/known-failures.json').read_text())
    assert not any(s['tests'] for s in allowed['suites'].values()),'Allowlist is not empty'
    changes=subprocess.check_output(git+['diff','--name-only','dafeea29']).decode().splitlines()
    historical_status_docs={'src/Steel/REFACTORING_PLAN.txt','src/Steel/COUPLED_MEMBERS_VERIFICATION.txt'}
    assert not any(p.startswith(('src/','GPCChecker.Steel/','ModelChecker/')) and
                   p not in historical_status_docs for p in changes),'Production changed'
    output=dict(base_checker='dafeea29',cases=verified,production_changed=False,
                other_legacy_methods_unchanged=True,allowlist_empty=True)
    if results:
        results=Path(results)
        summary=json.loads((results/'summary.json').read_text(encoding='utf-8-sig'))
        assert summary['suites']=='all' and len(summary['results'])==10
        assert summary['apiCompatible'] and not summary['unexpected']
        ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
        total=0; suites=[]
        for row in summary['results']:
            assert row['passed']>0 and row['failed']==row['other']==0,row
            trx=results/(row['name']+'.trx')
            tests=ET.parse(trx).findall('.//t:UnitTestResult',ns)
            assert len(tests)==row['passed'] and all(t.attrib['outcome']=='Passed' for t in tests)
            total+=len(tests)
            suites.append(dict(name=row['name'],passed=len(tests),trx_sha256=hashlib.sha256(trx.read_bytes()).hexdigest()))
        output.update(total_passed=total,failed=0,skipped=0,api_compatible=True,suites=suites,
                      summary_sha256=hashlib.sha256((results/'summary.json').read_bytes()).hexdigest())
    return output


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--results')
    args=parser.parse_args()
    print(json.dumps(audit(args.results),indent=2))
