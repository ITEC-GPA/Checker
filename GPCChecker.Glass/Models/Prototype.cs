using GPC.Model.Glasses;
using GPC.Checker.Glasses.Restrain;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using GPC.Model.Combinations;
using GPC.Utilities.Extensions;
using System.ComponentModel;

namespace GPC.Checker.Glasses.Models
{
    public sealed class Prototype : GPC.Model.ModelObject, IEquatable<Prototype>
    {
        #region PUBLIC ENUMS

        public enum AnalysisTypes
        {
            [Description("Linear static analysis")]
            LinearStaticAnalisys,
            [Description("Nonlinear static analysis")]
            NonLinearStaticAnalysis,
        }

        public enum CheckMethods
        {
            /// <summary> Ref prEn16612 annex A </summary>
            [Description("Dominant load")]
            DominantLoad = 0,

            /// <summary> Ref prEn16612 annex A </summary>
            [Description("Shorter load")]
            ShorterLoad = 1,

            /// <summary> Ref CNR-DT 210/2013 pag 224  </summary>
            [Description("Palmgren miner")]
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

        public enum SolverTypes
        {
            [Description("GPC solver")]
            GPCSolver,
            Straus7
        }

        #endregion 

        #region Variables

        // Parametri
        private AnalysisTypes _analysisType;

        private CheckMethods _checkMethod;
        private LaminatedEqThicknessMethods _laminatedEqThicknessMethod;
        private Standards _standard;
        private SolverTypes _solverType;

        // Proprietà vetro
        private Glass _glass;

        // Restrain
        private List<IParametricRestrain> _restrains;

        private List<Combination> _combinations;

        #endregion

        #region Property

        public Glass Glass => _glass;

        public AnalysisTypes AnalysisType => _analysisType;

        public CheckMethods CheckMethod => _checkMethod;

        public LaminatedEqThicknessMethods LaminatedEqThicknessMethod => _laminatedEqThicknessMethod;

        public Standards Standard => _standard;

        public SolverTypes SolverType => _solverType;

        public List<IParametricRestrain> Restrains => _restrains;

        public List<Combination> Combinations => _combinations;

        #endregion

        public Prototype(string name, Glass glass, List<IParametricRestrain> restrains, List<Combination> combinations,
                         Standards standard, AnalysisTypes analysisType, CheckMethods checkMethod, LaminatedEqThicknessMethods laminatedEqThicknessMethod, SolverTypes solverType)
                        : base(name)
        {
            this._standard = standard;
            this._analysisType = analysisType;
            this._checkMethod = checkMethod;
            this._laminatedEqThicknessMethod = laminatedEqThicknessMethod;
            this._solverType = solverType;

            this._glass = glass ?? throw new ArgumentNullException("Glass cannot be null");

            this._restrains = restrains == null ? new List<IParametricRestrain>() : restrains;
            this._combinations = combinations == null ? new List<Combination>() : combinations;
        }


        public Prototype(string name, Glass glass,
                         Standards standard, AnalysisTypes analysisType, CheckMethods checkMethod, LaminatedEqThicknessMethods laminatedEqThicknessMethod, SolverTypes solverType)
                        : this(name, glass, null, null, standard, analysisType, checkMethod, laminatedEqThicknessMethod, solverType)
        {

        }


        public Prototype(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            throw new NotSupportedException();
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            throw new NotSupportedException();
        }


        #region Public methods

        /// <summary>
        /// Add a combination to this prototype
        /// </summary>
        public void AddCombination(Combination combination)
        {
            _combinations.Add(combination);
        }

        /// <summary>
        /// Add a parametric restrain to this prototype
        /// </summary>
        public void AddParametricRestrain(IParametricRestrain restrain)
        {
            _restrains.Add(restrain);
        }


        #endregion



        #region Equals, hashcode, operators

        public override bool Equals(object obj)
        {
            return Equals(obj as Prototype);
        }

        public bool Equals(Prototype other)
        {
            if (ReferenceEquals(this, other))
                return true;

            return !(other is null) && other._analysisType.Equals(_analysisType)
                                    && other._checkMethod.Equals(_checkMethod)
                                    && other._laminatedEqThicknessMethod.Equals(_laminatedEqThicknessMethod)
                                    && other._standard.Equals(_standard)
                                    && other._solverType.Equals(_solverType)
                                    && other._glass.Equals(_glass)
                                    && other._restrains.ScrambledEquals(_restrains)
                                    && other._combinations.ScrambledEquals(_combinations)
                                    && base.Equals(other);
        }

        public override int GetHashCode()
        {
            int hashCode = 23;
            hashCode = hashCode * -17 + base.GetHashCode();
            hashCode = hashCode * -17 + EqualityComparer<AnalysisTypes>.Default.GetHashCode(_analysisType);
            hashCode = hashCode * -17 + EqualityComparer<CheckMethods>.Default.GetHashCode(_checkMethod);
            hashCode = hashCode * -17 + EqualityComparer<LaminatedEqThicknessMethods>.Default.GetHashCode(_laminatedEqThicknessMethod);
            hashCode = hashCode * -17 + EqualityComparer<Standards>.Default.GetHashCode(_standard);
            hashCode = hashCode * -17 + EqualityComparer<SolverTypes>.Default.GetHashCode(_solverType);

            hashCode = hashCode * -17 + EqualityComparer<Glass>.Default.GetHashCode(_glass);

            foreach (var el in _restrains)
            {
                hashCode = hashCode + 17 * EqualityComparer<IParametricRestrain>.Default.GetHashCode(el);
            }

            foreach (var el in _combinations)
            {
                hashCode = hashCode + 17 * EqualityComparer<Combination>.Default.GetHashCode(el);
            }

            return hashCode;
        }



        #endregion
    }
}