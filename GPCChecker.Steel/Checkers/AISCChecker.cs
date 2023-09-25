using GPC.Checkers.Steel.Results;
using GPC.Model.Standards;
using System.ComponentModel;
using static GPC.Checkers.Steel.Checkers.Cop2011Checker;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using GPC.Model.Results;
using GPC.Model.Sections.Steel;
using System;
using GPC.Model.Sections;

namespace GPC.Checkers.Steel.Checkers
{
	public abstract class AISCChecker : BeamChecker
	{
		#region Public enum

		public enum SectionClass
		{
			[Description("Compact")]
			Class1 = 1,
			[Description("Non-Compact")]
			Class2 = 2,
			[Description("Slender")]
			Class3 = 3,
		}

		#endregion

		#region Properties

		/// <summary>
		/// Array of AISCBeamStationResults
		/// </summary>
		public AISCBeamStationResults[] AISCBeamStationResults => _beamStationResults.Cast<AISCBeamStationResults>().ToArray();

		public AISCOptions AISCOption => (AISCOptions)_options;

		#endregion

		#region Constructor

		public AISCChecker(BeamCheckerAttributes attributes, Options options, Standard standard, int id = IDUNASSIGNED, string name = "")
			: base(attributes, options, standard, id, name)
		{

		}

		public AISCChecker(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}

		#endregion

		#region Public Methods

		public override void PerformCheck()
		{
			_beamStationResults = PerformCheck(_beamCheckerAttributes.Sections, _beamCheckerAttributes.Results);
		}

		public async void PerformCheckAsync()
		{
			await Task.Run(() =>
			{
				_beamStationResults = PerformCheck(_beamCheckerAttributes.Sections, _beamCheckerAttributes.Results);
			});
		}

		#endregion

		#region Private PerformCheck Method

		/// <param name="steelSection">section of each station</param>
		/// <param name="beamResult">result for each station and loadcase</param>
		/// <returns></returns>
		protected AISCBeamStationResults[] PerformCheck(ISteelSection[] steelSection, BeamResult[] beamResult)
		{
			AISCBeamStationResults[] stationResults = new AISCBeamStationResults[steelSection.Length * beamResult.Select(i => i.ResultLocations.Length).Sum()];

			for (int k = 0; k < beamResult.Length; k++)
			{
				for (int j = 0; j < beamResult[k].ResultLocations.Length; j++)
				{
					for (int i = 0; i < beamResult[k].ResultLocations[j].ResultTypes.Length; i++)
					{
						
					}
				}
			}

			return stationResults;
		}

		#endregion

		#region Section Private Method

		#region Axial Tension



		#endregion

		#region Axial Compression



		#endregion

		#region Axial Buckling



		#endregion

		#region Shear Capacity



		#endregion

		#region Bending Moment Capacity



		#endregion

		#region Lateral Torsional Bucking Capacity



		#endregion

		#region Section Class



		#endregion

		#endregion

		#region Equals, hashcode, operators

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
				return true;

			return obj is Cop2011Checker checker &&
				   base.Equals(obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = 23;
				hashCode = hashCode * -17 + base.GetHashCode();
				return hashCode;
			}
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
		}

		#endregion

		#region Nested class Options

		public class AISCOptions : BeamOptions
		{
			#region Enumerable

			public enum BuckingCurves
			{
				a,
				b,
				c,
				d
			}

			public enum LateralTorsionalBucklingConditions
			{
				[Description("Compressed flange restrained at ends")]
				Default,
				[Description("Compressed flange fully Restrained")]
				FullyRestrained,
				[Description("Compressed flange unrestrained")]
				Unrestrained,
				[Description("Compressed flange unrestrained and under destabilizing loads")]
				DestabilizingLoad,
			}

			#endregion

			#region Variables

			protected LateralTorsionalBucklingConditions _lateralTorsionalBucklingConditions;

			#endregion

			#region Properties

			public LateralTorsionalBucklingConditions LateralTorsionalBucklingCondition { get => _lateralTorsionalBucklingConditions; set => _lateralTorsionalBucklingConditions = value; }

			#endregion

			#region Constructor

			public AISCOptions(LateralTorsionalBucklingConditions latTorsBucklingCondition = LateralTorsionalBucklingConditions.Default,
				double unbracedLengthFactorAxialBuck1 = 1, double effectiveLengthFactorAxialBuck1 = 1, double unbracedLengthFactorAxialBuck2 = 1,
				double effectiveLengthFactorAxialBuck2 = 1, double UnbracedLengthFactorLatTorsBuck = 1, double effectiveLengthFactorLatTorsBuck = 1,
				double eqvUniformMomentFactorm1 = 1, double eqvUniformMomentFactorm2 = 1, double eqvUniformMomentFactormLT = 1)
				: base(unbracedLengthFactorAxialBuck1, effectiveLengthFactorAxialBuck1, unbracedLengthFactorAxialBuck2,
					  effectiveLengthFactorAxialBuck2, UnbracedLengthFactorLatTorsBuck, effectiveLengthFactorLatTorsBuck, 1, 1, 1, 1,
					  eqvUniformMomentFactorm1, eqvUniformMomentFactorm2, eqvUniformMomentFactormLT)
			{
				_lateralTorsionalBucklingConditions = latTorsBucklingCondition;
			}

			#endregion

		}

		#endregion
	}
}
