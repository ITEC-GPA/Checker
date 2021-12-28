using GPC.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.Glasses.Models
{
    // https://csharpindepth.com/articles/singleton

    /// <summary>
    /// This is a singleton class that collects options related to the fem model 
    /// </summary>
    public sealed class ModelAnalysisOptions
    {

        #region Singleton setup

        private static readonly ModelAnalysisOptions instance = new ModelAnalysisOptions();

        public static ModelAnalysisOptions Instance
        {
            get
            {
                return instance;
            }
        }

        // Explicit static constructor to tell C# compiler not to mark type as beforefieldinit.
        // NON TOCCARE PER NESSUN MOTIVO
        static ModelAnalysisOptions()
        {

        }

        #endregion


        public enum GravityAxes
        {
            X,
            Y,
            Z,
        }

        /// <summary>
        /// Rapresent the global axis where the gravity is acting
        /// </summary>
        public GravityAxes GravityAxis { get; set; }

        /// <summary>
        /// Rapresent the gravity direction. <see langword="False"/> means that gravity is directed in the opposite direction of axis: <see cref="GravityAxis"/>
        /// </summary>
        public bool GravityPositiveAxis { get; set; }


        /// <summary>
        /// If true the bricks exported to straus7 will have the bubble function option active.
        /// </summary>
        public bool Straus7BrickBubbleFunction { get; set; }

        /// <summary>
        /// Rapresent the value of the Elastic Modulus to be used to define the 
        /// <see cref="GPC.Model.FEM.Materials.OrthotropicFemMaterial"/> of the interlayer bricks. 
        /// In order to avoid numerical singularity.         
        /// </summary>
        public double InterlayerBrickElasticModulus { get; set; }


        // NON TOCCARE PER NESSUN MOTIVO
        private ModelAnalysisOptions()
        {
            GravityAxis = GravityAxes.Z;
            GravityPositiveAxis = false;
            Straus7BrickBubbleFunction = false;
            InterlayerBrickElasticModulus = 1e3;
        }

        /// <summary>
        /// Get the sign of the gravity
        /// </summary>
        /// <returns>-1 if <see cref="GravityPositiveAxis"/> is <see langword="False"/>. 
        /// I.e. gravity is directed in the opposite direction of axis: <see cref="GravityAxis"/>.
        /// Otherwise 1 </returns>
        public int GetGravitySign()
        {
            return GravityPositiveAxis ? 1 : -1;
        }


        /// <returns> The gravity vector</returns>
        public Vector3d GetGravityVector()
        {
            return new Vector3d(GravityAxis == GravityAxes.X ? 1 : 0,
                                GravityAxis == GravityAxes.Y ? 1 : 0,
                                GravityAxis == GravityAxes.Z ? 1 : 0);
        }


    }

}
