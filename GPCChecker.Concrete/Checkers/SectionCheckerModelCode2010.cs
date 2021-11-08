using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Checkers
{
    public class SectionCheckerModelCode2010 : SectionChecker, ISerializable
    {


        public SectionCheckerModelCode2010(SectionCheckerAttribute checkerAttribute, SectionCheckerOptions options, StandardModelCode2010 standard, int id = -1, string name = "")
            : base(checkerAttribute, options, standard, id, name)
        {

        }

        public SectionCheckerModelCode2010(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }

    }
}
