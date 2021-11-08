using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.ConcreteCheckerSolver;
using GPC.Geometry;
using GPC.Model.Results;

namespace GPC.Checkers.Concrete.Results
{
	public class FailureDomain
	{
		protected readonly FailureDomainPoint[][] _domainPoints;
		//protected readonly FailureDomainPoint[][] _domainGeometry;


		public FailureDomainPoint[][] DomainPoints => _domainPoints;

		public FailureDomain(FailureDomainPoint[][] domainPoints)
		{
			_domainPoints = domainPoints ?? throw new ArgumentNullException(nameof(domainPoints));
			//_domainGeometry = CalculateLevelCurves();
		}


		protected FailureDomainPoint[][] CalculateLevelCurves()
		{
			//TODO: implementare interpolazione
			throw new Exception();
		}

		public class FailureDomainPoint 
		{
			private readonly Point3d _point;
			private readonly SectionSolverULS.FailureIndices _failureIndex;
			private readonly StrainPlane _strainPlane;

			/// <summary>
			/// 
			/// </summary>
			public double NRd => _point.Z;

			/// <summary>
			/// 
			/// </summary>
			public double MxRd => _point.X;

			/// <summary>
			/// 
			/// </summary>
			public double MyRd => _point.Y;

			/// <summary>
			/// 
			/// </summary>
			public Point3d Point => _point;

			/// <summary>
			/// The failure type
			/// 1) Epsilon sup: EpsilonConcrete = EpsilonSu -> 0 /// EpsilonC = Epsilon inf: EpsilonSu
			/// 2) Epsilon sup: EpsilonConcrete = 0 -> EpsilonCu /// Epsilon inf: EpsilonSteel = EpsilonSu
			/// 3) Epsilon sup: EpsilonConcrete = EpsilonCu /// Epsilon inf: EpsilonSteel = EpsilonSu -> EpsilonSy
			/// 4) Epsilon sup: EpsilonConcrete = EpsilonCu /// Epsilon inf: EpsilonSteel = EpsilonSy -> 0
			/// 5) Epsilon sup: EpsilonConcrete = EpsilonCu /// Epsilon inf: EpsilonSteel = 0 -> EpsilonConcrete = 0
			/// 6) Epsilon sup: EpsilonConcrete = EpsilonCy /// Epsilon inf: EpsilonConcrete = EpsilonCy
			/// </summary>
			public SectionSolverULS.FailureIndices FailureIndex => _failureIndex;


			internal FailureDomainPoint(double nRd, double mxRd, double myRd, SectionSolverULS.FailureIndices failureIndex, StrainPlane strainPlane)
			{
				_point = new Point3d(mxRd, myRd, nRd);
				_failureIndex = failureIndex;
				_strainPlane = strainPlane ?? throw new ArgumentNullException(nameof(strainPlane));
			}

			internal FailureDomainPoint(SerializationInfo info, StreamingContext context)
			{
				_point = (Point3d)info.GetValue("Point", typeof(Point3d));
				_strainPlane = (StrainPlane)info.GetValue("StrainPlane", typeof(StrainPlane));
				_failureIndex = (SectionSolverULS.FailureIndices)info.GetValue("FailureIndex", typeof(SectionSolverULS.FailureIndices));
			}

			public void GetObjectData(SerializationInfo info, StreamingContext context)
			{
				info.AddValue("Point", _point, typeof(Point3d));
				info.AddValue("StrainPlane", _strainPlane, typeof(StrainPlane));
				info.AddValue("FailureIndex", _failureIndex, typeof(SectionSolverULS.FailureIndices));
			}
		}
	}
}