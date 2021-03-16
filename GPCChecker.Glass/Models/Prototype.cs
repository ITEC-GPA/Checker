using GPC.Model.Glasses;
using GPC.Checker.Glasses.Restrain;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using GPC.Model.Combinations;

namespace GPC.Checker.Glasses.Models
{
    public class Prototype : GPC.Model.ModelObject
    {
        #region PUBLIC ENUMS

        public enum AnalysisTypes
        {
            LinearStaticAnalisys,
            NonLinearStaticAnalysis,
        }

        public enum CheckMethods
        {
            /// <summary> Ref prEn16612 annex A </summary>
            DominantLoad = 0,

            /// <summary> Ref prEn16612 annex A </summary>
            ShorterLoad = 1,

            /// <summary> Ref CNR-DT 210/2013 pag 224  </summary>
            PalmgrenMiner = 2,

            /// <summary> Ref. ASTM E1300-16 §X5 </summary>
            ASTME1300 = 3
        }

        public enum LaminatedEqThicknessMethods
        {
            /// <summary> Ref CNR DT 210-13 </summary>
            EET = 0,

            /// <summary> Ref prEn16612 </summary>
            Omega = 1,

            /// <summary> Ref ASTM E1300-16 §X9 </summary>
            ASTME1300 = 2,

            /// <summary> Ref NEN 2608:2014 §F </summary>
            NEN = 3,
        }

        public enum Standards
        {
            /// <summary>EN 16612 - 2019</summary>
            EN16612 = 0,

            /// <summary>ASTM E1300 - 16 </summary>
            ASTME1300 = 2
        }

        #endregion 

        #region Variables

        // Parametri
        private AnalysisTypes _analysisType;

        private CheckMethods _checkMethod;
        private LaminatedEqThicknessMethods _laminatedEqThicknessMethod;
        private Standards _standard;

        // Proprietà vetro
        protected Glass _glass;

        // Proprietà mesh
        protected double _meshSize;

        // Restrain
        protected List<IParametricRestrain> _restrains;

        protected List<Combination> _combinations;

        #endregion

        #region Property

        public Glass Glass => _glass;

        public double MeshSize => _meshSize;

        public AnalysisTypes AnalysisType => _analysisType;
        
        public CheckMethods CheckMethod => _checkMethod;

        public LaminatedEqThicknessMethods LaminatedEqThicknessMethod => _laminatedEqThicknessMethod;

        public Standards Standard => _standard;

        public List<IParametricRestrain> Restrains => _restrains;

        public List<Combination> Combinations => _combinations;

        #endregion

        public Prototype(string name, Glass glass, List<IParametricRestrain> restrains, Standards standard, AnalysisTypes analysisType, CheckMethods checkMethod, LaminatedEqThicknessMethods laminatedEqThicknessMethod)
            : base(Guid.NewGuid(), name)
        {
            this._standard = standard;
            this._analysisType = analysisType;
            this._checkMethod = checkMethod;
            this._laminatedEqThicknessMethod = laminatedEqThicknessMethod;

            this._glass = glass ?? throw new ArgumentNullException("Glass cannot be null");
            this._meshSize = 50;
            this._restrains = restrains == null ? new List<IParametricRestrain>() : restrains;
            this._combinations = new List<Combination>();
        }

        public Prototype(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            throw new NotSupportedException();
        }


    }
}