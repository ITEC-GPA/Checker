using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.SectionSolver;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Checkers
{
    public class SectionCheckerModelCode2010 : SectionChecker, ISerializable
    {


        public StandardModelCode2010 StandardModelCode2010 => (StandardModelCode2010)_standard;



        public SectionCheckerModelCode2010(SectionCheckerAttribute checkerAttribute, SectionCheckerOptions options,
                                            StandardModelCode2010 standard, int id = -1, string name = "")
            : base(checkerAttribute, options, standard, id, name)
        {

        }

        public SectionCheckerModelCode2010(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }

        public override void PerformCheck()
        {
            throw new NotImplementedException();
        }

        public override void ULSPerformCheck()
        {
            new SectionSolverULSModelCode2010(CheckerAttribute.Sections[0], StandardModelCode2010);
        }

        public override void SLSPerformCheck()
        {
            throw new NotImplementedException();
        }
    }
}
