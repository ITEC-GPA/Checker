using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;

namespace GPC.Checkers.ReinforcedConcrete.Checkers
{
    /// <summary>
    /// This class rapresent the results of one plate (multiple loadcase/combination).
    /// </summary>
    /// 
    [Serializable]
    public class PlateCheckerAttributes : Model.ModelObject, ISerializable //, ICheckerAttribute
    {
        #region Variables

        protected readonly IConcreteSection[] _sections;
        protected readonly PlateResult[] _results;

        #endregion


        #region Properties

        public IConcreteSection[] Sections => _sections;

        public PlateResult[] Results => _results;

        #endregion


        public PlateCheckerAttributes(IConcreteSection section, PlateResult[] beamResults, string name = "")
            : base(name)
        {
            _results = beamResults ?? throw new ArgumentException("Input resultBeamForces can not be null");

            for (int i = 0; i < beamResults.Length; i++)
                for (int c = 0; c < beamResults[i].Results.Length; c++)
                    if (!(beamResults[i].Results[c] is ResultPlateForces))
                        throw new ArgumentException("Input plateResults.Results must be ResultBeamForces");

            if (section is null)            
                throw new ArgumentNullException(nameof(section));
            
            _sections = Enumerable.Repeat(section, beamResults.First().Points.Length).ToArray();
        }

        public PlateCheckerAttributes(IConcreteSection[] sections, PlateResult[] plateResults, string name = "")
            : base(name)
        {
            _results = plateResults ?? throw new ArgumentException("Input resultBeamForces can not be null");
            _sections = sections ?? throw new ArgumentException("Input sections can not be null");

            for (int i = 0; i < plateResults.Length; i++)
                for (int c = 0; c < plateResults[i].Results.Length; c++)
                    if (!(plateResults[i].Results[c] is ResultPlateForces))
                        throw new ArgumentException("Input plateResults.Results must be ResultBeamForces");

            if (sections.Length != plateResults.First().Points.Length)
                throw new ArgumentException("Sections number different than station number");
        }

        public PlateCheckerAttributes(SerializationInfo info, StreamingContext context)
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

            return (obj is PlateCheckerAttributes objCasted) && _sections.SequenceEqual(objCasted.Sections)
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
