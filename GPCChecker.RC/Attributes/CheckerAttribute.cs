using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using System;
using System.Linq;
using System.Runtime.Serialization;

namespace GPC.Checkers.ReinforcedConcrete
{
	public abstract class CheckerAttribute : Model.ModelObject
	{
		#region Variables

		protected readonly IConcreteSection[] _sections;
		protected readonly IElementResult[] _results;

		#endregion

		#region Properties

		public IConcreteSection[] Sections => _sections;

		public IElementResult[] Results => _results;

        #endregion

        public CheckerAttribute(IConcreteSection section, IElementResult[] elementResults, string name = "")
            : base(name)
        {
            if (section is null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            _results = elementResults ?? throw new ArgumentException("Input resultBeamForces can not be null");

            for (int i = 0; i < elementResults.Length; i++)
                for (int c = 0; c < elementResults[i].Results.Length; c++)
                    if (!(elementResults[i].Results[c] is ResultBeamForces))
                        throw new ArgumentException("Input BeamResult.Results must be ResultBeamForces");


            if (elementResults.Select(i => i.Points.Length).Distinct().Count() > 1)
            {
                throw new ArgumentException("Different station number");
            }

            _sections = Enumerable.Repeat(section, elementResults.First().Points.Length).ToArray();
        }

        public CheckerAttribute(IConcreteSection[] sections, IElementResult[] elementResults, string name = "")
            : base(name)
        {
            _results = elementResults ?? throw new ArgumentException("Input resultBeamForces can not be null");
            _sections = sections ?? throw new ArgumentException("Input sections can not be null");

            for (int i = 0; i < elementResults.Length; i++)
                for (int c = 0; c < elementResults[i].Results.Length; c++)
                    if (!(elementResults[i].Results[c] is ResultPlateForces))
                        throw new ArgumentException("Input plateResults.Results must be ResultBeamForces");

            if (sections.Length != elementResults.First().Points.Length)
                throw new ArgumentException("Sections number different than station number");
        }

        public CheckerAttribute(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _sections = (IConcreteSection[])info.GetValue("Sections", typeof(IConcreteSection[]));
            _results = (PlateResult[])info.GetValue("PlateResult", typeof(PlateResult[]));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Sections", _sections, typeof(IConcreteSection[]));
            info.AddValue("PlateResult", _results, typeof(PlateResult[]));
        }


        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is CheckerAttribute objCasted) && _sections.SequenceEqual(objCasted.Sections)
                                                            && _results.SequenceEqual(objCasted.Results)
                                                            && base.Equals(objCasted);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();

                for (int i = 0; i < _sections.Length; i++)
                {
                    hashCode = hashCode * -17 + _sections[i].GetHashCode();
                }

                for (int i = 0; i < _results.Length; i++)
                {
                    hashCode = hashCode * -17 + _results[i].GetHashCode();
                }

                return hashCode;
            }
        }
    }
}
