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
    public class SectionCheckerModelCode2010 : SectionChecker, ISerializable
    {
        #region Properties

        public StandardModelCode2010 StandardModelCode2010 => (StandardModelCode2010)_standard;

        public SectionOptionsModelCode2010 SectionCheckerOptionsModelCode2010 => (SectionOptionsModelCode2010)_options;

        public StandardEN1993p11 StandardStructuralSteel => (StandardEN1993p11)_standardStructuralSteel;

        #endregion

        #region Constructor

        /// <inheritdoc cref="SectionChecker(SectionCheckerAttribute, SectionOptions, Standard, SectionSolver int)"/>
        public SectionCheckerModelCode2010(SectionCheckerAttribute checkerAttribute, SectionOptionsModelCode2010 options,
            StandardModelCode2010 standard, bool considerTensileConcrete = false, int id = ModelObjectId.IDUNASSIGNED,
            StandardEN1993p11 standardStructuralSteel = null)
            : base(checkerAttribute, options, standard,
                  new SectionSolverModelCode2010(checkerAttribute.Section, standard, considerTensileConcrete, id, standardStructuralSteel),
                  id, standardStructuralSteel)
        {
        }

        public SectionCheckerModelCode2010(SectionCheckerAttribute checkerAttribute, SectionOptionsModelCode2010 options,
            StandardModelCode2010 standard, SectionSolverModelCode2010 solver, int id = ModelObjectId.IDUNASSIGNED,
            StandardEN1993p11 standardStructuralSteel = null)
            : base(checkerAttribute, options, standard, solver, id, standardStructuralSteel)
        {
        }

        #endregion

        #region Nested class

        [Serializable]
        public class SectionOptionsModelCode2010 : SectionOptions, ISerializable
        {
            public SectionOptionsModelCode2010(CoordinateSystem coordinateSystem, SectionSolver.FailureAnalysisTypes failureAnalysisType = SectionSolver.FailureAnalysisTypes.ConstantEccentricity)
                : base(coordinateSystem, failureAnalysisType)
            {

            }

            public SectionOptionsModelCode2010()
                : base(CoordinateSystem.Global, SectionSolver.FailureAnalysisTypes.ConstantEccentricity)
            {

            }

            protected SectionOptionsModelCode2010(SerializationInfo info, StreamingContext context)
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
