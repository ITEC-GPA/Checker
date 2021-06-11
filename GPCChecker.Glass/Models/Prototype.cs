using GPC.Checkers.Glasses.Restrain;
using GPC.Geometry;
using GPC.Model;
using GPC.Model.Combinations;
using GPC.Model.Glasses;
using GPC.Utilities.Converters;
using GPC.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace GPC.Checkers.Glasses.Models
{
    [Serializable]
    public sealed class Prototype : ModelObject, IEquatable<Prototype>
    {
        #region PUBLIC ENUMS

        [TypeConverter(typeof(EnumDescriptionTypeConverter))]
        public enum AnalysisTypes
        {
            [Description("Linear static analysis")]
            LinearStaticAnalysis = GPC.Model.FEM.FemModel.AnalysisTypes.Linear,
            [Description("Nonlinear static analysis")]
            NonLinearStaticAnalysis = GPC.Model.FEM.FemModel.AnalysisTypes.NonLinear
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
            [Description("EN 16612-2019 §D")]
            Omega = 1,

            /// <summary> Ref ASTM E1300-16 §X9 </summary>
            [Description("ASTM E1300-16")]
            ASTME1300 = 2,

            /// <summary> Ref NEN 2608:2014 §F </summary>
            [Description("NEN 2608:2014 §F")]
            NEN = 3,
        }


        [TypeConverter(typeof(EnumDescriptionTypeConverter))]
        public enum LaminatedEqThicknessBoundaryConditions
        {
            Other = 0,
            RectangularOneSideClamped = 1,
            RectangularTwoSidesSimplySupported = 2,
            RectangularThreeSidesSimplySupported = 3,
            RectangularFourSidesSimplySupported = 4,
        }



        [TypeConverter(typeof(EnumDescriptionTypeConverter))]
        public enum Standards
        {
            /// <summary>EN 16612-2019</summary>
            [Description("EN 16612-2019")]
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
        private readonly Standards _standard;
        private readonly SolverTypes _solverType;
        private readonly LaminatedAnalysisTypes _laminatedAnalysisType;
        private readonly LaminatedEqThicknessParameters _laminatedEqThicknessParameters;
        private readonly Geometry.Meshes.Mesh.GenerateOptions _meshOptions;

        // Proprietà vetro
        private readonly Glass _glass;

        // Restrain
        private Polygon3d _polygon;
        private readonly List<IParametricRestrain> _restrains;

        private readonly UniqueNameCollection<Combination> _combinations;

        #endregion

        #region Property

        /// <remarks>Order of the glass panels is from external to internal</remarks>
        public Glass Glass => _glass;

        public AnalysisTypes AnalysisType => _analysisType;

        public CheckMethods CheckMethod => _checkMethod;

        public LaminatedAnalysisTypes LaminatedAnalysisType => _laminatedAnalysisType;

        public LaminatedEqThicknessParameters LaminatedEqThicknessParameter => _laminatedEqThicknessParameters;

        public Standards Standard => _standard;

        public SolverTypes SolverType => _solverType;

        public Polygon3d Polygon
        {
            get => _polygon;
            set => _polygon = value;
        }

        public IEnumerable<IParametricRestrain> Restrains => _restrains;

        public UniqueNameCollection<Combination> Combinations => _combinations;

        public Geometry.Meshes.Mesh.GenerateOptions MeshOptions => _meshOptions;

        #endregion

        public Prototype(string name, Glass glass, Polygon3d polygon, List<IParametricRestrain> restrains, IEnumerable<Combination> combinations, Standards standard,
            AnalysisTypes analysisType, CheckMethods checkMethod, SolverTypes solverType, LaminatedAnalysisTypes laminatedAnalysisType,
            LaminatedEqThicknessParameters laminatedEqThicknessParameters)
            : base(name)
        {
            _standard = standard;
            _analysisType = analysisType;
            _checkMethod = checkMethod;
            _solverType = solverType;
            _laminatedAnalysisType = laminatedAnalysisType;

            _glass = glass ?? throw new ArgumentNullException("Glass cannot be null");

            if (restrains != null && polygon == null)
                throw new ArgumentException("If there are restraints provided the polygon cannot be null");
            _polygon = polygon;
            _restrains = restrains ?? new List<IParametricRestrain>();

            _combinations = new UniqueNameCollection<Combination>();
            _combinations.AddRange(combinations);
            _meshOptions = new Geometry.Meshes.Mesh.GenerateOptions();

            _laminatedEqThicknessParameters = laminatedEqThicknessParameters ?? throw new ArgumentNullException(nameof(laminatedEqThicknessParameters));
        }


        public Prototype(string name, Glass glass, Standards standard, AnalysisTypes analysisType, CheckMethods checkMethod, SolverTypes solverType, 
            LaminatedAnalysisTypes laminatedAnalysisType, LaminatedEqThicknessParameters laminatedEqThicknessParameters)
            : this(name, glass, null, null, null, standard, analysisType, checkMethod, solverType, laminatedAnalysisType, laminatedEqThicknessParameters)
        {

        }


        public Prototype(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _analysisType = (AnalysisTypes)info.GetValue("AnalysisType", typeof(AnalysisTypes));
            _checkMethod = (CheckMethods)info.GetValue("CheckMethod", typeof(CheckMethods));
            _laminatedEqThicknessParameters = (LaminatedEqThicknessParameters)info.GetValue("LaminatedEqThicknessParameters", typeof(LaminatedEqThicknessParameters));
            _standard = (Standards)info.GetValue("Standard", typeof(Standards));      
            _solverType = (SolverTypes)info.GetValue("SolverType", typeof(SolverTypes));
            _laminatedAnalysisType = (LaminatedAnalysisTypes)info.GetValue("LaminatedAnalysisType", typeof(LaminatedAnalysisTypes));
            _glass = (Glass)info.GetValue("Glass", typeof(Glass));
            _polygon = (Polygon3d)info.GetValue("Polygon", typeof(Polygon3d));
            _restrains = (List<IParametricRestrain>)info.GetValue("Restraints", typeof(List<IParametricRestrain>));
            _combinations = (UniqueNameCollection<Combination>)info.GetValue("Combinations", typeof(UniqueNameCollection<Combination>));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("AnalysisType", _analysisType, typeof(AnalysisTypes));
            info.AddValue("CheckMethod", _checkMethod, typeof(CheckMethods));
            info.AddValue("LaminatedEqThicknessParameters", _laminatedEqThicknessParameters, typeof(LaminatedEqThicknessParameters));
            info.AddValue("Standard", _standard, typeof(Standards));
            info.AddValue("SolverType", _solverType, typeof(SolverTypes));
            info.AddValue("Glass", _glass, typeof(Glass));
            info.AddValue("Polygon", _polygon, typeof(Polygon3d));
            info.AddValue("Restraints", _restrains, typeof(List<IParametricRestrain>));
            info.AddValue("Combinations", _combinations, typeof(UniqueNameCollection<Combination>));
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

        public void DeleteCombinationByName(string name)
        {
            _combinations.Remove(name);
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
                                    && other._laminatedEqThicknessParameters.Equals(_laminatedEqThicknessParameters)
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
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();
                hashCode = hashCode * -17 + EqualityComparer<AnalysisTypes>.Default.GetHashCode(_analysisType);
                hashCode = hashCode * -17 + EqualityComparer<CheckMethods>.Default.GetHashCode(_checkMethod);
                hashCode = hashCode * -17 + EqualityComparer<LaminatedEqThicknessParameters>.Default.GetHashCode(_laminatedEqThicknessParameters);
                hashCode = hashCode * -17 + EqualityComparer<Standards>.Default.GetHashCode(_standard);
                hashCode = hashCode * -17 + EqualityComparer<SolverTypes>.Default.GetHashCode(_solverType);
                hashCode = hashCode * -17 + EqualityComparer<LaminatedAnalysisTypes>.Default.GetHashCode(_laminatedAnalysisType);
                hashCode = hashCode * -17 + EqualityComparer<Glass>.Default.GetHashCode(_glass);
                hashCode = hashCode * -17 + EqualityComparer<Polygon3d>.Default.GetHashCode(_polygon);

                foreach (var el in _restrains)
                    hashCode += 17 * EqualityComparer<IParametricRestrain>.Default.GetHashCode(el);

                foreach (var el in _combinations)
                    hashCode += 17 * EqualityComparer<Combination>.Default.GetHashCode(el);

                return hashCode; 
            }
        }



        #endregion


        [Serializable]
        public class LaminatedEqThicknessParameters : ISerializable
        {

            private readonly LaminatedEqThicknessBoundaryConditions _laminatedEqThicknessBoundaryCondition;
            private readonly LaminatedEqThicknessMethods _laminatedEqThicknessMethod;

            private readonly double _a;
            private readonly double _b;


            /// <summary>
            /// Minor lenght of the rectangular plate, according to EET notation
            /// </summary>
            public double B => _b;

            /// <summary>
            /// Major lenght of the rectangular plate, according to EET notation
            /// </summary>
            public double A => _a;

            /// <summary>
            /// Boundary condition of glass
            /// </summary>
            public LaminatedEqThicknessBoundaryConditions LaminatedEqThicknessBoundaryCondition => _laminatedEqThicknessBoundaryCondition;

            /// <summary>
            /// Method to use to calculate the eq thickness
            /// </summary>
            public LaminatedEqThicknessMethods LaminatedEqThicknessMethod => _laminatedEqThicknessMethod;


            /// <param name="laminatedEqThicknessMethod"></param>
            /// <param name="laminatedEqThicknessBoundaryCondition"></param>
            /// <param name="a">Major lenght of the rectangular plate, according to EET notation</param>
            /// <param name="b">Minor lenght of the rectangular plate, according to EET notation</param>
            public LaminatedEqThicknessParameters(LaminatedEqThicknessMethods laminatedEqThicknessMethod,
                                                 LaminatedEqThicknessBoundaryConditions laminatedEqThicknessBoundaryCondition,
                                                 double a, double b)
            {
                _laminatedEqThicknessBoundaryCondition = laminatedEqThicknessBoundaryCondition;
                _laminatedEqThicknessMethod = laminatedEqThicknessMethod;

                if (a < b)
                    throw new ArgumentException();

                if (laminatedEqThicknessBoundaryCondition != LaminatedEqThicknessBoundaryConditions.Other)
                {
                    _a = a <= 0 ? throw new ArgumentException() : a;
                    _b = b <= 0 ? throw new ArgumentException() : b;
                }

            }

            /// <summary>
            /// This construct the object with <see cref="LaminatedEqThicknessBoundaryConditions.Other"/> and <see cref="LaminatedEqThicknessMethods.EET"/>, 
            /// set <see cref="A"/> and <see cref="B"/> to zero
            /// </summary>
            public LaminatedEqThicknessParameters()
            {
                _laminatedEqThicknessBoundaryCondition = LaminatedEqThicknessBoundaryConditions.Other;
                _laminatedEqThicknessMethod = LaminatedEqThicknessMethods.EET;

                _a = 0;
                _b = 0;
            }


            public override bool Equals(object obj)
            {
                return obj is LaminatedEqThicknessParameters parameters &&
                       _laminatedEqThicknessBoundaryCondition == parameters._laminatedEqThicknessBoundaryCondition &&
                       _laminatedEqThicknessMethod == parameters._laminatedEqThicknessMethod &&
                       _a == parameters._a && _b == parameters._b;
            }


            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = 17;
                    hashCode = hashCode * -23 + _laminatedEqThicknessBoundaryCondition.GetHashCode();
                    hashCode = hashCode * -23 + _laminatedEqThicknessMethod.GetHashCode();
                    hashCode = hashCode * -23 + _a.GetHashCode();
                    hashCode = hashCode * -23 + _b.GetHashCode();
                    return hashCode; 
                }
            }

            protected LaminatedEqThicknessParameters(SerializationInfo info, StreamingContext context)
            {
                _a = (double)info.GetValue("A", typeof(double));
                _b = (double)info.GetValue("B", typeof(double));
                _laminatedEqThicknessBoundaryCondition = (LaminatedEqThicknessBoundaryConditions)info.GetValue("LaminatedEqThicknessBoundaryCondition", typeof(LaminatedEqThicknessBoundaryConditions));
                _laminatedEqThicknessMethod = (LaminatedEqThicknessMethods)info.GetValue("LaminatedEqThicknessMethods", typeof(LaminatedEqThicknessMethods));
            }


            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("A", _a);
                info.AddValue("B", _b);
                info.AddValue("LaminatedEqThicknessBoundaryCondition", _laminatedEqThicknessBoundaryCondition);
                info.AddValue("LaminatedEqThicknessMethods", _laminatedEqThicknessMethod);
            }
        }


    }
 
}
