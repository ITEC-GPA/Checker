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
    public class FailureDomainResult : CheckerResultType
    {

        protected readonly FailureDomain _failureDomain;
        protected List<ResultBeamForces> _forces;


        public List<ResultBeamForces> Forces => _forces;

        public FailureDomain Domain => _failureDomain;


        public FailureDomainResult(IConcreteSection section, FailureDomain failureDomain, IEnumerable<ResultBeamForces> forces, Standard standard, int id = IDUNASSIGNED)
            : base(section, standard, id)
        {
            _failureDomain = failureDomain ?? throw new ArgumentNullException(nameof(failureDomain));

            if (forces == null)
                _forces = new List<ResultBeamForces>();
            else
				_forces = forces.ToList();
        }

        public void AddForces(ResultBeamForces forces)
        {
            _forces.Add(forces);
        }



    }
}