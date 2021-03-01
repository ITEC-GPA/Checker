using GPC.Model.Glasses;
using GPC.Model.Restrains;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GPC.Checker.Glasses.Models
{
    public class Prototype : GPC.Model.ModelObject
    {
        #region PUBLIC ENUMS

        public enum AnalysisType
        {
            LinearStaticAnalisys,
            NonLinearStaticAnalysis,
        }

        public enum CheckMethod
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

        public enum LaminatedEqThicknessMethod
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

        public enum Standard
        {
            /// <summary>EN 16612 - 2019</summary>
            EN16612 = 0,

            /// <summary>ASTM E1300 - 16 </summary>
            ASTME1300 = 2
        }

        #endregion 

        #region Variables

        // Parametri
        private AnalysisType _analysisType;

        private CheckMethod _checkMethod;
        private LaminatedEqThicknessMethod _laminatedEqThicknessMethod;
        private Standard _standard;

        // Proprietà vetro
        protected Glass _glass;

        // Proprietà mesh
        protected double _meshSize;

        // Restrain
        protected List<GeometryRestrain> _restrains;

        #endregion

        #region Property

        public Glass Glass => _glass;

        public double MeshSize => _meshSize;

        #endregion

        public Prototype(string name, Glass glass, List<GeometryRestrain> restrains, Standard standard, AnalysisType analysisType, CheckMethod checkMethod, LaminatedEqThicknessMethod laminatedEqThicknessMethod)
            : base(Guid.NewGuid(), name)
        {
            this._standard = standard;
            this._analysisType = analysisType;
            this._checkMethod = checkMethod;
            this._laminatedEqThicknessMethod = laminatedEqThicknessMethod;

            this._glass = glass ?? throw new ArgumentNullException("Glass cannot be null");
            this._meshSize = 50;
            this._restrains = restrains == null ? new List<GeometryRestrain>() : restrains;
        }

        public Prototype(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            throw new NotSupportedException();
        }

        public AnalysisType GetAnalysisType()
        {
            return _analysisType;
        }

        public CheckMethod GetCheckMethod()
        {
            return _checkMethod;
        }

        public Standard GetStandard()
        {
            return _standard;
        }

        public LaminatedEqThicknessMethod GetLaminatedEqThicknessMethod()
        {
            return _laminatedEqThicknessMethod;
        }
    }
}