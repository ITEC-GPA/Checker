using GPC.Checkers.Concrete.Attributes;
using GPC.Model;
using GPC.Model.Standards;
using System;
using System.Runtime.Serialization;

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
