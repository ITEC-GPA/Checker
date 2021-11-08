using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Results;
using GPC.Model;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Checkers
{
    public abstract class SectionChecker : Checker, ISerializable
    {

        public SectionChecker(SectionCheckerAttribute checkerAttribute, SectionCheckerOptions options, Standard standard,
                                int id = ModelObjectId.IDUNASSIGNED, string name = "")
            : base(checkerAttribute, options, standard, id, name)
        {

        }

        public SectionChecker(SerializationInfo info, StreamingContext context) : base(info, context)
        {

        }

        public override void PerformCheck()
        {
            throw new NotImplementedException();
        }

        public override void SLSPerformCheck()
        {
            throw new NotImplementedException();
        }

        public override void ULSPerformCheck()
        {

        }


        public abstract class SectionCheckerOptions : Options
        {


            public SectionCheckerOptions()
            {

            }


        }
    }
}
