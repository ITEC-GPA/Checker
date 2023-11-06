using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Standards;
using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.Concrete.Checkers
{
    [Serializable]
    public class SectionCheckerACI318 : SectionChecker, ISerializable
    {
        #region Properties

        public StandardACI318 StandardACI318 => (StandardACI318)_standard;

        public SectionOptionsStandardACI318 SectionCheckerOptionsACI318 => (SectionOptionsStandardACI318)_options;

        #endregion

        #region Constructors

        /// <inheritdoc cref="SectionChecker(SectionCheckerAttribute, SectionOptions, Standard, SectionSolver int)"/>
        public SectionCheckerACI318(SectionCheckerAttribute checkerAttribute, SectionOptionsStandardACI318 options,
            StandardACI318 standard, bool haveSpiral, bool considerTensileConcrete = false, int id = ModelObjectId.IDUNASSIGNED,
            StandardAISC standardStructuralSteel = null)
            : base(checkerAttribute, options, standard,
                  new SectionSolverACI318(checkerAttribute.Section, standard, haveSpiral, options.ForceReferenceCoordinateSystem.Origin, considerTensileConcrete, id, standardStructuralSteel),
                  id, standardStructuralSteel)
        {
        }

        /// <inheritdoc cref="SectionChecker(SectionCheckerAttribute, SectionOptions, Standard, SectionSolver int)"/>
        public SectionCheckerACI318(SectionCheckerAttribute checkerAttribute, SectionOptionsStandardACI318 options,
            StandardACI318 standard, SectionSolverACI318 solver, int id = ModelObjectId.IDUNASSIGNED,
            StandardEN1993p11 standardStructuralSteel = null)
            : base(checkerAttribute, options, standard, solver, id, standardStructuralSteel)
        {
        }

        #endregion

        #region Nested class

        [Serializable]
        public class SectionOptionsStandardACI318 : SectionOptions, ISerializable
        {
            public SectionOptionsStandardACI318(CoordinateSystem coordinateSystem,
                SectionSolver.FailureAnalysisTypes failureAnalysisType, SectionSolver.FailureDomainTypes failureDomainType)
                : base(coordinateSystem, failureAnalysisType, failureDomainType)
            {

            }

            protected SectionOptionsStandardACI318(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }

            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                base.GetObjectData(info, context);
            }
        }

        #endregion
    }
}
