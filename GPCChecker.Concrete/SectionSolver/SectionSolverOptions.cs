using GPC.Geometry;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    // https://csharpindepth.com/articles/singleton

    /// <summary>
    /// This is a singleton class that collects options related to the concrete section solver
    /// </summary>
    public sealed class SectionSolverOptions
    {
        #region Singleton setup

        private static readonly SectionSolverOptions instance = new SectionSolverOptions();

        public static SectionSolverOptions Instance
        {
            get
            {
                return instance;
            }
        }

        static SectionSolverOptions()
        {

        }

        #endregion

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver
        /// </summary>
        public (SectionSolverULS.FailureZones, int)[] AxialForceDiscretizations { get; set; }

        /// <summary>
        /// Rapresent the discretization of the moments around the axial force axis
        /// </summary>
        public int MomentsDiscretizations { get; set; }

        public double SLSconvergenceTolerance { get; set; }

        public double ULSconvergenceTolerance { get; set; }

        public Point3d DistanceFromCentroid { get; set; }

        public int GaussIntegrationQuadLowPoints { get; set; }
        public int GaussIntegrationQuadMidPoints { get; set; }
        public int GaussIntegrationQuadHighPoints { get; set; }
        public int GaussIntegrationTriLowPoints { get; set; }
        public int GaussIntegrationTriMidPoints { get; set; }
        public int GaussIntegrationTriHighPoints { get; set; }

        private SectionSolverOptions()
        {
            AxialForceDiscretizations = new (SectionSolverULS.FailureZones, int)[] { (SectionSolverULS.FailureZones.Iz1, 1),
                                                                                               (SectionSolverULS.FailureZones.Iz2, 1),
                                                                                               (SectionSolverULS.FailureZones.Iz3, 1),
                                                                                               (SectionSolverULS.FailureZones.Iz4, 30),
                                                                                               (SectionSolverULS.FailureZones.Iz5, 2),
                                                                                               (SectionSolverULS.FailureZones.Iz6, 1),
                                                                                               (SectionSolverULS.FailureZones.Iz7, 4) };

            MomentsDiscretizations = 32;

            SLSconvergenceTolerance = 1e-5;
            ULSconvergenceTolerance = 5e-4;

            DistanceFromCentroid = new Point3d(0, 0, 0);

            GaussIntegrationQuadLowPoints = 12;
            GaussIntegrationQuadMidPoints = 49;
            GaussIntegrationQuadHighPoints = 121;
            GaussIntegrationTriLowPoints = 6;
            GaussIntegrationTriMidPoints = 33;
            GaussIntegrationTriHighPoints = 79;

        }
    }    
}
