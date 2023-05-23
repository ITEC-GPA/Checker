using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Model;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Checkers
{
    [Serializable]
    public abstract class BeamChecker : Checker, ISerializable
    {

        protected readonly BeamCheckerAttribute _checkerAttributes;


        /// <param name="checkerAttribute">This rapresent each section of the beam, one for each station</param>
        /// <param name="options"></param>
        /// <param name="standard"></param>
        /// <param name="id"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public BeamChecker(BeamCheckerAttribute checkerAttribute, Options options, Standard standard, int id = ModelObjectId.IDUNASSIGNED)
            : base(standard, options, id)
        {
            _checkerAttributes = checkerAttribute ?? throw new ArgumentNullException(nameof(checkerAttribute));
        }


    }
}
