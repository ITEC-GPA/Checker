using GPC.Checkers.GlassV2.Solvers;
using GPC.Model.Results;
using GPC.Model.Sections.Glass;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GPC.Checkers.GlassV2.Results
{
    public class AnalysisResult : CheckerResultType, ISerializable
    {
        #region Variables

        protected readonly ResultBeamForces _force;
        protected readonly Solver _sectionSolver;

        #endregion

        #region Properties

        public ResultBeamForces Force => _force;

        internal Solver SectionSolver => _sectionSolver;


        #endregion

        #region Constructor

        public AnalysisResult(GlassPlateProperty section, ResultBeamForces force, Solver solver, Standard standard, int id = IDUNASSIGNED)
            : base(section, standard, id)
        {
            _force = force ?? throw new ArgumentNullException(nameof(force));
            _sectionSolver = solver ?? throw new ArgumentNullException(nameof(solver));
        }

        protected AnalysisResult(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _force = (ResultBeamForces)info.GetValue("Force", typeof(ResultBeamForces));
            _sectionSolver = (Solver)info.GetValue("Solver", typeof(Solver));
        }

        #endregion

        #region Equals, hashcode, operators

        public List<string> GetLog()
        {
            return _sectionSolver.GetLog();
        }

        public override bool Equals(object obj)
        {
            return obj is AnalysisResult result &&
                   base.Equals(obj) &&
                   SectionSolver.Equals(result.SectionSolver) &&
                   EqualityComparer<ResultBeamForces>.Default.Equals(_force, result._force);
        }

        public override int GetHashCode()
        {
            int hashCode = -23;
            hashCode = hashCode * -17 + base.GetHashCode();
            hashCode = hashCode * -17 + _sectionSolver.GetHashCode();
            hashCode = hashCode * -17 + EqualityComparer<ResultBeamForces>.Default.GetHashCode(_force);
            return hashCode;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Force", _force);
            info.AddValue("Solver", _sectionSolver);
        }

        #endregion
    }
}
