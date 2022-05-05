using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Results
{
    [Serializable]
    public class StressAnalysisResult : CheckerResultType, ISerializable
    {
        #region Variables

        protected readonly ResultBeamForces _force;
        protected readonly StrainPlane _strainPlane;
        protected readonly SectionSolver _sectionSolver;

        #endregion

        #region Properties

        public ResultBeamForces Force => _force;

        public StrainPlane StrainPlane => _strainPlane;

        #endregion

        #region Constructor

        public StressAnalysisResult(IConcreteSection section, ResultBeamForces force, StrainPlane strainPlane, SectionSolver solver, Standard standard, int id = IDUNASSIGNED)
            : base(section, standard, id)
        {
            _force = force ?? throw new ArgumentNullException(nameof(force));
            _strainPlane = strainPlane;
            _sectionSolver = solver ?? throw new ArgumentNullException(nameof(solver));
        }

		protected StressAnalysisResult(SerializationInfo info, StreamingContext context) 
            : base(info, context)
		{
            _force = (ResultBeamForces)info.GetValue("Force", typeof(ResultBeamForces));
            _strainPlane = (StrainPlane)info.GetValue("StrainPlane", typeof(StrainPlane));
            _sectionSolver = (SectionSolver)info.GetValue("SectionSolver", typeof(SectionSolver));
        }

        #endregion

        #region Rebar

        public virtual double GetRebarTension(ReinforcedConcreteRebar rebar)
        {            
            return _sectionSolver.CalculateStressRebar(rebar, StrainPlane.GetStrain(rebar.Position));
        }

        public virtual (ReinforcedConcreteRebar rebar, double tension)[] GetRebarsTension()
        {
            return _section.Rebars.Select(i => (i, _sectionSolver.CalculateStressRebar(i, StrainPlane.GetStrain(i.Position)) )  ).ToArray();
        }

        public virtual double GetRebarTension(double phi, ReinforcedConcreteRebar rebar)
        {
            return _sectionSolver.CalculateElasticSigmaS(phi, rebar, StrainPlane.GetStrain(rebar.Position));
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

        #endregion

        #region Concrete

        public virtual double GetConcreteTension(Point2d point)
        {
            return _sectionSolver.CalculateSigmaC(StrainPlane.GetStrain(point));   
        }

        public virtual (Point2d point, double tension)[] GetConcreteVerticesTension()
        {
            return _section.Shape.GetPoints2d().Select(i => (i, _sectionSolver.CalculateSigmaC(StrainPlane.GetStrain(i)))).ToArray();
        }

        public virtual double GetConcreteTension(double psi, Point2d point)
        {
            return _sectionSolver.CalculateElasticSigmaC(StrainPlane.GetStrain(point));
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

        public virtual bool GetConcreteTension(double n, Point2d point, out double tension)
		{
			try
			{
                tension = GetConcreteTension(n, point);
                return true;
			}
			catch (Exception)
			{
                tension = double.NaN;
                return false;
			}
		}

        public virtual bool GetConcreteVerticesTension(double n, out (Point2d point, double tension)[] verticesTensionAssociation)
		{
			try
			{
                verticesTensionAssociation = GetConcreteVerticesTension(n);
                return true;
            }
			catch
			{
                verticesTensionAssociation= null;
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
                   EqualityComparer<IConcreteSection>.Default.Equals(_section, result._section) &&
                   EqualityComparer<Standard>.Default.Equals(_standard, result._standard) &&
                   EqualityComparer<ResultBeamForces>.Default.Equals(_force, result._force) &&
                   EqualityComparer<StrainPlane>.Default.Equals(_strainPlane, result._strainPlane) &&
                   EqualityComparer<SectionSolver>.Default.Equals(_sectionSolver, result._sectionSolver);
        }

        public override int GetHashCode()
        {
            int hashCode = -2006748420;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(_name);
            hashCode = hashCode * -1521134295 + _id.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<IConcreteSection>.Default.GetHashCode(_section);
            hashCode = hashCode * -1521134295 + EqualityComparer<Standard>.Default.GetHashCode(_standard);
            hashCode = hashCode * -1521134295 + EqualityComparer<ResultBeamForces>.Default.GetHashCode(_force);
            hashCode = hashCode * -1521134295 + EqualityComparer<StrainPlane>.Default.GetHashCode(_strainPlane);
            hashCode = hashCode * -1521134295 + EqualityComparer<SectionSolver>.Default.GetHashCode(_sectionSolver);
            return hashCode;
        }

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
            info.AddValue("Force", _force);
            info.AddValue("StrainPlane", _strainPlane);
            info.AddValue("SectionSolver", _sectionSolver);
        }

        #endregion
    }
}
