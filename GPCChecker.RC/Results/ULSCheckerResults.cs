using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver;
using GPC.Model.Results;

namespace GPC.Checkers.ReinforcedConcrete.Results
{
	public class ULSCheckerResults : Model.ModelObjectId
	{
		protected readonly FailureDomain _failureDomain;
		protected List<ResultType> _forces;
		protected double _workingRatio;

		//TODO: aggiungere tassi di lavoro necessari


		public List<ResultType> Forces => _forces;

		public FailureDomain Domain => _failureDomain;


		public ULSCheckerResults(FailureDomain failureDomain, List<ResultType> forces)
		{
			_failureDomain = failureDomain ?? throw new ArgumentNullException(nameof(failureDomain));

			foreach (ResultType resultType in forces)
				if (resultType.GetType() != typeof(ResultBeamForces) && resultType.GetType() != typeof(ResultPlateForces))
					throw new ArgumentException("Result must be ResultBeamForces or ResultPlateForces");

			_forces = forces ?? throw new ArgumentNullException(nameof(forces));
		}

		public void AddForces(ResultBeamForces forces)
		{
			_forces.Add(forces);
		}



		public class FailureDomain
		{
			protected readonly FailureDomainPoint[][] _domainPoints;
			protected readonly FailureDomainPoint[][] _domainGeometry;


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
				private readonly double _nRd;
				private readonly double _mxRd;
				private readonly double _myRd;
				private readonly ConcreteSectionSolver.FailureIndices _failureIndex;
				private readonly double _teta;
				private readonly double _eta;

				/// <summary>
				/// 
				/// </summary>
				public double NRd => _nRd;

				/// <summary>
				/// 
				/// </summary>
				public double MxRd => _mxRd;

				/// <summary>
				/// 
				/// </summary>
				public double MyRd => _myRd;

				/// <summary>
				/// The failure type
				/// 1) Epsilon sup: EpsilonConcrete = EpsilonSu -> 0 /// EpsilonC = Epsilon inf: EpsilonSu
				/// 2) Epsilon sup: EpsilonConcrete = 0 -> EpsilonCu /// Epsilon inf: EpsilonSteel = EpsilonSu
				/// 3) Epsilon sup: EpsilonConcrete = EpsilonCu /// Epsilon inf: EpsilonSteel = EpsilonSu -> EpsilonSy
				/// 4) Epsilon sup: EpsilonConcrete = EpsilonCu /// Epsilon inf: EpsilonSteel = EpsilonSy -> 0
				/// 5) Epsilon sup: EpsilonConcrete = EpsilonCu /// Epsilon inf: EpsilonSteel = 0 -> EpsilonConcrete = 0
				/// 6) Epsilon sup: EpsilonConcrete = EpsilonCy /// Epsilon inf: EpsilonConcrete = EpsilonCy
				/// </summary>
				public ConcreteSectionSolver.FailureIndices FailureIndex => _failureIndex;

				/// <summary>
				/// The angle between the strain plane and the plane of section
				/// </summary>
				public double Teta => _teta;

				/// <summary>
				/// The immersion in the <see cref="FailureIndices"/>
				/// eta = (chi - chi1 ) / (chi2 - chi1)
				/// where: 
				/// chi = curvature of the section 
				/// chi1 = curvature of the section in the previous <see cref="FailureIndices"/> boundary 
				/// chi2 = curvature of the section in the following <see cref="FailureIndices"/> boundary 
				/// </summary>
				public double Eta => _eta;


				internal FailureDomainPoint(double nRd, double mxRd, double myRd, ConcreteSectionSolver.FailureIndices failureIndex, double teta, double eta)
				{
					_nRd = nRd;
					_mxRd = mxRd;
					_myRd = myRd;
					_failureIndex = failureIndex;
					_teta = teta;
					_eta = eta;
				}

				internal FailureDomainPoint(SerializationInfo info, StreamingContext context)
				{
					_nRd = (double)info.GetValue("NRd", typeof(double));
					_mxRd = (double)info.GetValue("MxRd", typeof(double));
					_myRd = (double)info.GetValue("MyRd", typeof(double));
					_teta = (double)info.GetValue("Angle", typeof(double));
					_eta = (double)info.GetValue("Eta", typeof(double));
					_failureIndex = (ConcreteSectionSolver.FailureIndices)info.GetValue("FailureIndex", typeof(ConcreteSectionSolver.FailureIndices));
				}


				public void GetObjectData(SerializationInfo info, StreamingContext context)
				{
					info.AddValue("NRd", _nRd, typeof(double));
					info.AddValue("MxRd", _mxRd, typeof(double));
					info.AddValue("MyRd", _myRd, typeof(double));
					info.AddValue("Angle", _teta, typeof(double));
					info.AddValue("Eta", _eta, typeof(double));
					info.AddValue("FailureIndex", _failureIndex, typeof(ConcreteSectionSolver.FailureIndices));
				}
			}
		}
	}
}