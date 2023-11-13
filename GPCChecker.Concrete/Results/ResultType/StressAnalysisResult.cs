using GPC.Checker.Results.ResultType;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace GPC.Checkers.Concrete.Results
{
    [Serializable]
    public class StressAnalysisResult : CheckerResultType, ISerializable
    {
        #region Variables

        protected readonly ResultBeamForces _force;
        protected readonly StrainPlane _strainPlane;
        protected readonly SectionSolver _sectionSolver;
        protected bool _linearElasticAnalysis;
        protected double? _psiRebar;
        protected double? _psiTendon;

        #endregion

        #region Properties

        public ResultBeamForces Force => _force;

        public StrainPlane StrainPlane => _strainPlane;

        internal SectionSolver SectionSolver => _sectionSolver;

        public bool LinearElasticAnalysis => _linearElasticAnalysis;

        public double? PsiRebar => _psiRebar;

        public double? PsiTendon => _psiTendon;

        #endregion

        #region Constructor

        public StressAnalysisResult(IConcreteSection section, ResultBeamForces force, StrainPlane strainPlane, SectionSolver solver, Standard standard,
            bool linearAnalysis = false, double? psiRebar = null, double? psiTendon = null, int id = IDUNASSIGNED,
            Standard standardStructuralSteel = null)
            : base(section, standard, id, standardStructuralSteel)
        {
            _force = force ?? throw new ArgumentNullException(nameof(force));
            _strainPlane = strainPlane;
            _sectionSolver = solver ?? throw new ArgumentNullException(nameof(solver));
            _linearElasticAnalysis = linearAnalysis;
            _psiRebar = psiRebar;
            _psiTendon = psiTendon;
        }

        protected StressAnalysisResult(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _force = (ResultBeamForces)info.GetValue("Force", typeof(ResultBeamForces));
            _strainPlane = (StrainPlane)info.GetValue("StrainPlane", typeof(StrainPlane));
            _sectionSolver = (SectionSolver)info.GetValue("SectionSolver", typeof(SectionSolver));
            _linearElasticAnalysis = info.GetBoolean("LinearElasticAnalysis");
            _psiRebar = info.GetDouble("psiRebar");
            _psiTendon = info.GetDouble("psiTendon");
        }

        #endregion

        #region StrainPlaneResult

        public StrainPlaneResult CalculateStrainPlaneResult(bool linearAnalysis = false, double psiR = 0, double psiT = 0)
        {
            StrainPlaneResult strainPlaneResult = new StrainPlaneResult(_section, _force, _strainPlane, _sectionSolver, _standard, _id, _standardStructuralSteel);
            return strainPlaneResult.CalculateStrainPlaneResult(linearAnalysis, psiR, psiT);
        }

        #endregion

        #region Rebar

        public virtual double GetRebarTension(ReinforcedConcreteRebar rebar)
        {
            return _sectionSolver.CalculateStressRebar(rebar, StrainPlane.GetStrain(rebar.Position));
        }

        public virtual (ReinforcedConcreteRebar rebar, double tension)[] GetRebarsTension()
        {
            return _section.Rebars.Select(i => (i, _sectionSolver.CalculateStressRebar(i, StrainPlane.GetStrain(i.Position)))).ToArray();
        }

        public virtual double GetRebarTension(double phi, ReinforcedConcreteRebar rebar)
        {
            return _sectionSolver.CalculateElasticSigmaS(phi, rebar, StrainPlane.GetStrain(rebar.Position));
        }

        public virtual double GetRebarTension(SteelMaterial steelMaterial, Point2d position, double epsilonP)
        {
            return _sectionSolver.CalculateStressRebar(steelMaterial, StrainPlane.GetStrain(position), epsilonP);
        }

        public virtual double GetRebarTension(double phi, SteelMaterial steelMaterial, Point2d position, double epsilonP)
        {
            return _sectionSolver.CalculateElasticSigmaS(phi, steelMaterial, StrainPlane.GetStrain(position), epsilonP);
        }

        public virtual (ReinforcedConcreteRebar rebar, double tension)[] GetRebarsTension(double phi)
        {
            return _section.Rebars.Select(i => (i, GetRebarTension(phi, i))).ToArray();
        }

        public virtual (ReinforcedConcreteRebar rebar, double tension)[] GetRebarsTension(double phi, double phiTendon)
        {
            (ReinforcedConcreteRebar rebar, double tension)[] results = new (ReinforcedConcreteRebar rebar, double tension)[_section.RebarsCount];
            var array = _section.Rebars.ToArray();

            for (int i = 0; i < _section.RebarsCount; i++)
            {
                if (array[i].EpsilonP != 0)
                    results[i] = (array[i], GetRebarTension(phiTendon, array[i]));
                else
                    results[i] = (array[i], GetRebarTension(phi, array[i]));
            }

            return results;
        }

        public virtual bool GetRebarTension(ReinforcedConcreteRebar rebar, out double tension)
        {
            try
            {
                tension = GetRebarTension(rebar);
                return true;
            }
            catch (Exception)
            {
                tension = double.NaN;
                return false;
            }
        }

        public virtual bool GetRebarTension(SteelMaterial steelMaterial, Point2d position, double epsilonP, out double tension)
        {
            try
            {
                tension = GetRebarTension(steelMaterial, position, epsilonP);
                return true;
            }
            catch (Exception)
            {
                tension = double.NaN;
                return false;
            }
        }

        public virtual bool GetRebarsTension(out (ReinforcedConcreteRebar rebar, double tension)[] rebarTensionAssociation)
        {
            try
            {
                rebarTensionAssociation = GetRebarsTension();
                return true;
            }
            catch (Exception)
            {
                rebarTensionAssociation = null;
                return false;
            }
        }

        public virtual bool GetRebarTension(double phi, ReinforcedConcreteRebar rebar, out double tension)
        {
            try
            {
                tension = GetRebarTension(phi, rebar);
                return true;
            }
            catch (Exception)
            {
                tension = double.NaN;
                return false;
            }
        }

        public virtual bool GetRebarTension(double phi, SteelMaterial steelMaterial, Point2d position, double epsilonP, out double tension)
        {
            try
            {
                tension = GetRebarTension(phi, steelMaterial, position, epsilonP);
                return true;
            }
            catch (Exception)
            {
                tension = double.NaN;
                return false;
            }
        }

        public virtual bool GetRebarsTension(double phi, out (ReinforcedConcreteRebar rebar, double tension)[] rebarTensionAssociation)
        {
            try
            {
                rebarTensionAssociation = GetRebarsTension(phi);
                return true;
            }
            catch (Exception)
            {
                rebarTensionAssociation = null;
                return false;
            }
        }

        public virtual bool GetRebarsTension(double phi, double phiTendon, out (ReinforcedConcreteRebar rebar, double tension)[] rebarTensionAssociation)
        {
            try
            {
                rebarTensionAssociation = GetRebarsTension(phi, phiTendon);
                return true;
            }
            catch (Exception)
            {
                rebarTensionAssociation = null;
                return false;
            }
        }

        /// <summary>
        /// Use constitutive law defined in material.
        /// </summary>
        /// <returns></returns>
        public (ReinforcedConcreteRebar rebar, double strain)[] GetRebarsStrain()
        {
            (ReinforcedConcreteRebar rebar, double strain)[] results = new (ReinforcedConcreteRebar rebar, double strain)[_section.RebarsCount];

            for (int r = 0; r < ConcreteSection.RebarsCount; r++)
            {
                ReinforcedConcreteRebar rebar = ConcreteSection.Rebars.ElementAt(r);
                double strain = StrainPlane.GetStrain(rebar.Position);

                results[r] = (rebar, strain);
            }

            return results;
        }

        /// <summary>
        /// Linear elastic material ccnstituve law.
        /// </summary>
        /// <param name="phi"></param>
        /// <returns></returns>
        public (ReinforcedConcreteRebar rebar, double strain)[] GetRebarsStrain(double phi)
        {
            (ReinforcedConcreteRebar rebar, double strain)[] results = new (ReinforcedConcreteRebar rebar, double strain)[_section.RebarsCount];

            for (int r = 0; r < ConcreteSection.RebarsCount; r++)
            {
                ReinforcedConcreteRebar rebar = ConcreteSection.Rebars.ElementAt(r);
                double strain = StrainPlane.GetStrain(rebar.Position) * (1 + phi);

                results[r] = (rebar, strain);
            }

            return results;
        }

        public double GetRebarStrain(ReinforcedConcreteRebar rebar, double phi)
        {
            return StrainPlane.GetStrain(rebar.Position) * (1 + phi);
        }

        public double GetRebarStrain(ReinforcedConcreteRebar rebar)
        {
            return StrainPlane.GetStrain(rebar.Position);
        }

        #endregion

        #region Concrete

        public virtual double GetConcreteTension(Point2d point)
        {
            return _sectionSolver.CalculateSigmaC(StrainPlane.GetStrain(point));
        }

        public virtual double GetConcreteTension(double psi, Point2d point)
        {
            return _sectionSolver.CalculateElasticSigmaC(StrainPlane.GetStrain(point));
        }

        public virtual (Point2d point, double tension)[] GetConcreteVerticesTension()
        {
            return _section.Shape.GetPoints2d().Select(i => (i, _sectionSolver.CalculateSigmaC(StrainPlane.GetStrain(i)))).ToArray();
        }

        public virtual (Point2d point, double tension)[] GetConcreteVerticesTension(double psi)
        {
            return _section.Shape.GetPoints2d().Select(i => (i, _sectionSolver.CalculateElasticSigmaC(StrainPlane.GetStrain(i)))).ToArray();
        }

        public virtual bool GetConcreteTension(Point2d point, out double tension)
        {
            try
            {
                tension = GetConcreteTension(point);
                return true;
            }
            catch
            {
                tension = double.NaN;
                return false;
            }
        }

        public virtual bool GetConcreteVerticesTension(out (Point2d point, double tension)[] verticesTensionAssociation)
        {
            try
            {
                verticesTensionAssociation = GetConcreteVerticesTension();
                return true;
            }
            catch
            {
                verticesTensionAssociation = null;
                return false;
            }
        }

        public virtual bool GetConcreteTension(double psi, Point2d point, out double tension)
        {
            try
            {
                tension = GetConcreteTension(psi, point);
                return true;
            }
            catch (Exception)
            {
                tension = double.NaN;
                return false;
            }
        }

        public virtual bool GetConcreteVerticesTension(double psi, out (Point2d point, double tension)[] verticesTensionAssociation)
        {
            try
            {
                verticesTensionAssociation = GetConcreteVerticesTension(psi);
                return true;
            }
            catch
            {
                verticesTensionAssociation = null;
                return false;
            }
        }

        public double[] GetVerticesStrain()
        {
            List<Point3d> vertices = new List<Point3d>();

            vertices.AddRange(ConcreteSection.Shape.Fill);

            if (ConcreteSection.Shape.HasHoles)
                for (int i = 0; i < ConcreteSection.Shape.Holes.Count(); i++)
                    vertices.AddRange(ConcreteSection.Shape.Holes[i]);

            double[] strains = new double[vertices.Count];

            for (int i = 0; i < strains.Length; i++)
                strains[i] = _strainPlane.GetStrain(vertices[i]);

            return strains;
        }

        public double GetVerticeStrain(Point2d point)
        {
            return _strainPlane.GetStrain(point);
        }

        public double GetVerticeStrain(Point2d point, double phi)
        {
            return _strainPlane.GetStrain(point) * (1 + phi);
        }

        #endregion

        #region Steel sections

        public virtual (Point2d point, double tension)[] GetStructuralSteelVerticesTension(double? phi = null)
        {
            var structSteelTension = new List<(Point2d point, double tension)>();
            foreach (var steelSection in _section.SteelSections)
            {
                foreach (var localVertex in steelSection.Section.Shape.Fill)
                {
                    var globalVertex = steelSection.PositionToGlobal(localVertex);
                    double strain = StrainPlane.GetStrain(globalVertex);
                    if (phi.HasValue)
                        structSteelTension.Add((globalVertex, _sectionSolver.CalculateElasticSigmaS(phi.Value, steelSection.Section, strain)));
                    else
                        structSteelTension.Add((globalVertex, _sectionSolver.CalculateStressStructuralSteel(steelSection.Section, strain)));
                }
            }
            return structSteelTension.ToArray();
        }

        /// <summary>
        /// Return the tension at a point that is assumed to belong to the section being passed.
        /// </summary>
        /// <param name="steelSectionPosition">Section to which the point belongs.</param>
        /// <param name="point">Point at which tension is required.</param>
        /// <param name="phi"></param>
        /// <returns></returns>
        public bool GetStructuralSteelTension(in SteelSectionPosition steelSectionPosition, in Point2d point, out double tension, in double? phi = null)
        {
            try
            {
                double strain = StrainPlane.GetStrain(point);
                if (phi.HasValue)
                    tension = _sectionSolver.CalculateElasticSigmaS(phi.Value, steelSectionPosition.Section, strain);
                else
                    tension = _sectionSolver.CalculateStressStructuralSteel(steelSectionPosition.Section, strain);
                return true;
            }
            catch
            {
                tension = 0.0;
                return false;
            }
        }

        /// <summary>
        /// Linear elastic material ccnstituve law.
        /// </summary>
        /// <param name="phi"></param>
        /// <returns></returns>
        public (Point2d point, double strain)[] GetStructuralSteelVerticesStrain(double? phi = null)
        {
            var structSteelStrain = new List<(Point2d point, double strain)>();
            foreach (var steelSection in _section.SteelSections)
            {
                foreach (var localVertex in steelSection.Section.Shape.Fill)
                {
                    var globalVertex = steelSection.PositionToGlobal(localVertex);
                    double strain = StrainPlane.GetStrain(globalVertex);
                    if (phi.HasValue)
                        strain *= 1 + phi.Value;

                    structSteelStrain.Add((globalVertex, strain));
                }
            }
            return structSteelStrain.ToArray();
        }

        #endregion

        #region Equals, hashcode, operators

        public List<string> GetLog()
        {
            return _sectionSolver.GetLog();
        }

        public override bool Equals(object obj)
        {
            return obj is StressAnalysisResult result &&
                   //base.Equals(obj) &&
                   _name == result._name &&
                   _id == result._id &&
                   _linearElasticAnalysis == result.LinearElasticAnalysis &&
                   _psiRebar == result.PsiRebar &&
                   _psiTendon == result.PsiTendon &&
                   EqualityComparer<IConcreteSection>.Default.Equals(_section, result._section) &&
                   EqualityComparer<Standard>.Default.Equals(_standard, result._standard) &&
                   EqualityComparer<ResultBeamForces>.Default.Equals(_force, result._force) &&
                   EqualityComparer<StrainPlane>.Default.Equals(_strainPlane, result._strainPlane) &&
                   EqualityComparer<SectionSolver>.Default.Equals(_sectionSolver, result._sectionSolver);
        }

        public override int GetHashCode()
        {
            int hashCode = -23;
            hashCode = hashCode * -17 + base.GetHashCode();
            hashCode = hashCode * -17 + EqualityComparer<string>.Default.GetHashCode(_name);
            hashCode = hashCode * -17 + _id.GetHashCode();
            if (_psiRebar != null)
                hashCode = hashCode * -17 + _psiRebar.GetHashCode();
            if (_psiTendon != null)
                hashCode = hashCode * -17 + _psiTendon.GetHashCode();
            hashCode = hashCode * -17 + _linearElasticAnalysis.GetHashCode();
            hashCode = hashCode * -17 + EqualityComparer<IConcreteSection>.Default.GetHashCode(_section);
            hashCode = hashCode * -17 + EqualityComparer<Standard>.Default.GetHashCode(_standard);
            hashCode = hashCode * -17 + EqualityComparer<ResultBeamForces>.Default.GetHashCode(_force);
            hashCode = hashCode * -17 + EqualityComparer<StrainPlane>.Default.GetHashCode(_strainPlane);
            hashCode = hashCode * -17 + EqualityComparer<SectionSolver>.Default.GetHashCode(_sectionSolver);
            return hashCode;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Force", _force);
            info.AddValue("StrainPlane", _strainPlane);
            info.AddValue("SectionSolver", _sectionSolver);
            info.AddValue("LinearElasticAnalysis", _linearElasticAnalysis);
            info.AddValue("psiRebar", _psiRebar);
            info.AddValue("psiTendon", _psiTendon);
        }

        #endregion
    }
}
