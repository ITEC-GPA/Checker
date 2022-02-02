using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Attributes;
using GPC.Model;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Checkers
{

    [Serializable]
    public abstract class PlateChecker : Checker, ISerializable
    {

        protected readonly PlateCheckerAttribute _checkerAttributes;


        /// <param name="checkerAttribute">This rapresent each section of the plate</param>
        /// <param name="options"></param>
        /// <param name="standard"></param>
        /// <param name="id"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public PlateChecker(PlateCheckerAttribute checkerAttribute, Options options, Standard standard, int id = ModelObjectId.IDUNASSIGNED)
            : base(standard, options, id)
        {
            _checkerAttributes = checkerAttribute ?? throw new ArgumentNullException(nameof(checkerAttribute));
        }


    }
}
