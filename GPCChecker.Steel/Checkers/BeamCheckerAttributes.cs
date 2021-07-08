using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.LoadCases;
using GPC.Model.Results;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.Checkers
{
    /// <summary>
    /// This class rapresent the results of one beam (multiple loadcase/combination).
    /// </summary>
    
    [Serializable]
    public class BeamCheckerAttributes : Model.ModelObject, ISerializable
    {

        #region Variables

        protected readonly ISteelSection[] _sections;
        protected readonly BeamResult[] _results;

        #endregion


        #region Properties

        public ISteelSection[] Sections => _sections;

        public BeamResult[] Results => _results;

        public double Length => _results.First().Length;

        #endregion


        public BeamCheckerAttributes(ISteelSection section, BeamResult[] beamResults, string name = "")
            : base(name)
        {
            if (section is null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            _results = beamResults ?? throw new ArgumentException("Input resultBeamForces can not be null");

            for(int i = 0; i < beamResults.Length; i++)            
                for(int c = 0; c < beamResults[i].Results.Length; c++)                
                    if(!(beamResults[i].Results[c] is ResultBeamForces))
                        throw new ArgumentException("Input BeamResult.Results must be ResultBeamForces");


            if (beamResults.Select(i => i.Points.Length).Distinct().Count() > 1)
            {
                throw new ArgumentException("Different beam station number");
            }
                        
            if (beamResults.Select(i => i.Length).Distinct().Count() > 1)
            {
                throw new ArgumentException("Different beam result lenght");
            }

            _sections = Enumerable.Repeat(section, beamResults.First().Points.Length).ToArray();
        }


        public BeamCheckerAttributes(ISteelSection[] sections, BeamResult[] beamResults, string name = "") 
            : base(name)
        {
            _results = beamResults ?? throw new ArgumentException("Input resultBeamForces can not be null");
            _sections = sections ?? throw new ArgumentException("Input sections can not be null");


            for (int i = 0; i < beamResults.Length; i++)
                for (int c = 0; c < beamResults[i].Results.Length; c++)
                    if (!(beamResults[i].Results[c] is ResultBeamForces))
                        throw new ArgumentException("Input BeamResult.Results must be ResultBeamForces");

            if (beamResults.Select(i => i.Points.Length).Distinct().Count() > 1)
            {
                throw new ArgumentException("Different beam station number");
            }
                        
            if (beamResults.Select(i => i.Length).Distinct().Count() > 1)
            {
                throw new ArgumentException("Different beam result lenght");
            }

            if (sections.Length != beamResults.First().Points.Length)
                throw new ArgumentException("Sections number different than station number");
        }

        public BeamCheckerAttributes(SerializationInfo info, StreamingContext context) 
            : base(info, context)
        {
            _sections = (ISteelSection[])info.GetValue("Sections", typeof(ISteelSection[]));
            _results = (BeamResult[])info.GetValue("BeamResult", typeof(BeamResult[]));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ISteelSection", _sections, typeof(ISteelSection[]));
            info.AddValue("BeamResult", _results, typeof(BeamResult[]));
        }


        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            return (obj is BeamCheckerAttributes objCasted) && _sections.SequenceEqual(objCasted.Sections)
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
