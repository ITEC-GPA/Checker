using GPC.Checker.Glasses.Restrain;
using GPC.Geometry;
using GPC.Model.Combinations;
using GPC.Model.Glasses;
using GPC.Utilities.Converters;
using GPC.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace GPC.Checker.Glasses.Models
{
    [Serializable]
    public sealed class Prototype : GPC.Model.ModelObject, IEquatable<Prototype>
    {
        #region PUBLIC ENUMS

        [TypeConverter(typeof(EnumDescriptionTypeConverter))]
        public enum AnalysisTypes
        {
            [Description("Linear static analysis")]
            LinearStaticAnalysis,
            [Description("Nonlinear static analysis")]
            NonLinearStaticAnalysis,
        }

        [TypeConverter(typeof(EnumDescriptionTypeConverter))]
        public enum CheckMethods
        {
            /// <summary> Ref EN 16612-2019 annex A </summary>
            [Description("Dominant load")]
            DominantLoad = 0,

            /// <summary> Ref EN 16612-2019 annex A </summary>
            [Description("Shorter load")]
            ShorterLoad = 1,

            /// <summary> Ref CNR-DT 210/2013 pag 224 </summary>
            [Description("Palmgren miner")]
            PalmgrenMiner = 2,

            /// <summary> Ref. ASTM E1300-16 §X5 </summary>
            [Description("ASTM E1300-16")]
            ASTME1300 = 3
        }

        [TypeConverter(typeof(EnumDescriptionTypeConverter))]
        public enum LaminatedEqThicknessMethods
        {
            /// <summary> Ref CNR DT 210-13 </summary>
            EET = 0,

            /// <summary> Ref EN 16612-2019 </summary>
            Omega = 1,

            /// <summary> Ref ASTM E1300-16 §X9 </summary>
            [Description("ASTM E1300-16")]
            ASTME1300 = 2,

            /// <summary> Ref NEN 2608:2014 §F </summary>
            NEN = 3,
        }

        [TypeConverter(typeof(EnumDescriptionTypeConverter))]
        public enum Standards
        {
            /// <summary>EN 16612-2019</summary>
            [Description("EN 16612")]
            EN16612 = 0,

            /// <summary>ASTM E1300 - 16 </summary>
            [Description("ASTM E1300-16")]
            ASTME1300 = 2
        }

        [TypeConverter(typeof(EnumDescriptionTypeConverter))]
        public enum SolverTypes
        {
            [Description("GPC")]
            GPCSolver,
            Straus7
        }

        [TypeConverter(typeof(EnumDescriptionTypeConverter))]
        public enum LaminatedAnalysisTypes
        {
            [Description("Multi element")]
            MultiElement,
            [Description("Equivalent thickness")]
            EquivalentThickness,
            [Description("Multi-layered")]
            MultiLayered
        }


        #endregion 

        #region Variables

        // Parametri
        private readonly AnalysisTypes _analysisType;
        private readonly CheckMethods _checkMethod;
        private readonly LaminatedEqThicknessMethods _laminatedEqThicknessMethod;
        private readonly Standards _standard;
        private readonly SolverTypes _solverType;
        private readonly LaminatedAnalysisTypes _laminatedAnalysisType;

        // Proprietà vetro
        private readonly Glass _glass;

        // Restrain
        private Polygon3d _polygon;
        private readonly List<IParametricRestrain> _restrains;

        private readonly List<Combination> _combinations;

        #endregion

        #region Property

        public Glass Glass => _glass;

        public AnalysisTypes AnalysisType => _analysisType;

        public CheckMethods CheckMethod => _checkMethod;

        public LaminatedEqThicknessMethods LaminatedEqThicknessMethod => _laminatedEqThicknessMethod;

        public LaminatedAnalysisTypes LaminatedAnalysisType => _laminatedAnalysisType;

        public Standards Standard => _standard;

        public SolverTypes SolverType => _solverType;

        public Polygon3d Polygon
        {
            get => _polygon;
            set => _polygon = value;
        }

        public IEnumerable<IParametricRestrain> Restrains => _restrains;

        public IEnumerable<Combination> Combinations => _combinations;

        #endregion

        public Prototype(string name, Glass glass, Polygon3d polygon, List<IParametricRestrain> restrains, List<Combination> combinations, Standards standard, 
            AnalysisTypes analysisType, CheckMethods checkMethod, LaminatedEqThicknessMethods laminatedEqThicknessMethod, SolverTypes solverType, LaminatedAnalysisTypes laminatedAnalysisType)
            : base(name)
        {
            _standard = standard;
            _analysisType = analysisType;
            _checkMethod = checkMethod;
            _laminatedEqThicknessMethod = laminatedEqThicknessMethod;
            _solverType = solverType;
            _laminatedAnalysisType = laminatedAnalysisType;

            _glass = glass ?? throw new ArgumentNullException("Glass cannot be null");

            if (restrains != null && polygon == null)
                throw new ArgumentException("If there are restraints provided the polygon cannot be null");
            _polygon = polygon;
            _restrains = restrains == null ? new List<IParametricRestrain>() : restrains;
            _combinations = combinations == null ? new List<Combination>() : combinations;
        }


        public Prototype(string name, Glass glass, Standards standard, AnalysisTypes analysisType, CheckMethods checkMethod, 
            LaminatedEqThicknessMethods laminatedEqThicknessMethod, SolverTypes solverType, LaminatedAnalysisTypes laminatedAnalysisType)
            : this(name, glass, null, null, null, standard, analysisType, checkMethod, laminatedEqThicknessMethod, solverType, laminatedAnalysisType)
        {
        }


        public Prototype(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _analysisType = (AnalysisTypes)info.GetValue("AnalysisType", typeof(AnalysisTypes));
            _checkMethod = (CheckMethods)info.GetValue("CheckMethod", typeof(CheckMethods));
            _laminatedEqThicknessMethod = (LaminatedEqThicknessMethods)info.GetValue("LaminatedEqThicknessMethod", typeof(LaminatedEqThicknessMethods));
            _standard = (Standards)info.GetValue("Standard", typeof(Standards));      
            _solverType = (SolverTypes)info.GetValue("SolverType", typeof(SolverTypes));
            _laminatedAnalysisType = (LaminatedAnalysisTypes)info.GetValue("LaminatedAnalysisType", typeof(LaminatedAnalysisTypes));
            _glass = (Glass)info.GetValue("Glass", typeof(Glass));
            _polygon = (Polygon3d)info.GetValue("Polygon", typeof(Polygon3d));
            _restrains = (List<IParametricRestrain>)info.GetValue("Restraints", typeof(List<IParametricRestrain>));
            _combinations = (List<Combination>)info.GetValue("Combinations", typeof(List<Combination>));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("AnalysisType", _analysisType, typeof(AnalysisTypes));
            info.AddValue("CheckMethod", _checkMethod, typeof(CheckMethods));
            info.AddValue("LaminatedEqThicknessMethod", _laminatedEqThicknessMethod, typeof(LaminatedEqThicknessMethods));
            info.AddValue("Standard", _standard, typeof(Standards));
            info.AddValue("SolverType", _solverType, typeof(SolverTypes));
            info.AddValue("Glass", _glass, typeof(Glass));
            info.AddValue("Polygon", _polygon, typeof(Polygon3d));
            info.AddValue("Restraints", _restrains, typeof(List<IParametricRestrain>));
            info.AddValue("Combinations", _combinations, typeof(List<Combination>));
            info.AddValue("LaminatedAnalysisType", _laminatedAnalysisType, typeof(LaminatedAnalysisTypes));
        }


        #region Public methods

        /// <summary>
        /// Add a combination to this prototype
        /// </summary>
        public void AddCombination(Combination combination)
        {
            _combinations.Add(combination);
        }

        public void DeleteCombination(Combination combination)
        {
            _combinations.Remove(combination);
        }

        public void DeleteCombinationAt(int index)
        {
            _combinations.RemoveAt(index);
        }

        /// <summary>
        /// Add a parametric restrain to this prototype
        /// </summary>
        public void AddParametricRestrain(IParametricRestrain restrain)
        {
            // TODO: validare qui il restraint sulla base di polygon
            if (_polygon == null)
                throw new InvalidOperationException("Can't add restraint if the Polygon property is null");
            _restrains.Add(restrain);
        }

        public void DeleteParametricRestrain(IParametricRestrain restrain)
        {
            _restrains.Remove(restrain);
        }

        public void DeleteParametricRestrainAt(int index)
        {
            _restrains.RemoveAt(index);
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
                                    && other._polygon == _polygon
                                    && other._restrains.ScrambledEquals(_restrains)
                                    && other._combinations.ScrambledEquals(_combinations)
                                    && other._laminatedAnalysisType.Equals(_laminatedAnalysisType)
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
            hashCode = hashCode * -17 + EqualityComparer<LaminatedAnalysisTypes>.Default.GetHashCode(_laminatedAnalysisType);
            hashCode = hashCode * -17 + EqualityComparer<Glass>.Default.GetHashCode(_glass);
            hashCode = hashCode * -17 + EqualityComparer<Polygon3d>.Default.GetHashCode(_polygon);

            foreach (var el in _restrains)
                hashCode = hashCode + 17 * EqualityComparer<IParametricRestrain>.Default.GetHashCode(el);

            foreach (var el in _combinations)
                hashCode = hashCode + 17 * EqualityComparer<Combination>.Default.GetHashCode(el);

            return hashCode;
        }



        #endregion
    }
}
