using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Model;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.Concrete.Attributes
{
    public class SectionCheckerAttribute : CheckerAttribute
    {


        public SectionCheckerAttribute(IConcreteSection section, IElementResult[] slsResults, IElementResult[] ulsResults,
                                        int id = ModelObjectId.IDUNASSIGNED, string name = "")
            : base(section, slsResults, ulsResults, id, name)
        {

        }


        public SectionCheckerAttribute(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }



    }
}
