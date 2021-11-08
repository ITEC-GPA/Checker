using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Model;
using GPC.Model.Standards;
using GPC.Checkers.Concrete.Attributes;

namespace GPC.Checkers.Concrete.Checkers
{
    public class SectionChecker : Checker, ISerializable
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
            throw new NotImplementedException();
        }


        public abstract class SectionCheckerOptions : Options
        {


            public SectionCheckerOptions()
            {

            }


        }
    }
}
