using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Results
{
    public class ULSCheckerResultsType : CheckerResultType
    {
        protected readonly FailureDomain _failureDomain;
        protected List<ResultBeamForces> _forces;


        public List<ResultBeamForces> Forces => _forces;

        public FailureDomain Domain => _failureDomain;


        public ULSCheckerResultsType(IConcreteSection section, FailureDomain failureDomain, IEnumerable<ResultBeamForces> forces, Standard standard, int id = IDUNASSIGNED)
            : base(section, standard, id)
        {
            _failureDomain = failureDomain ?? throw new ArgumentNullException(nameof(failureDomain));

            _forces = forces.ToList() ?? throw new ArgumentNullException(nameof(forces));
        }

        public void AddForces(ResultBeamForces forces)
        {
            _forces.Add(forces);
        }



    }
}