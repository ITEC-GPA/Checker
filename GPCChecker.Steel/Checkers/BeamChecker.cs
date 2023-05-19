using GPC.Checkers.Steel.Results;
using GPC.Model.LoadCases;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace GPC.Checkers.Steel.Checkers
{
	public abstract class BeamChecker : Checker
	{
		#region Variables

		protected readonly BeamCheckerAttributes _beamCheckerAttributes;
		protected BeamStationResults[] _beamStationResults;

		#endregion

		#region Properties

		public BeamCheckerAttributes BeamCheckersAttribute => _beamCheckerAttributes;

		public BeamStationResults[] BeamStationCheckerResults => _beamStationResults;

		public BeamOptions BeamCheckerOptions => (BeamOptions)_options;

		public double BeamLength => _beamCheckerAttributes.Length;

		/// <summary>
		/// The unique ILoadCases array
		/// </summary>
		public ILoadCase[] LoadCases => GetLoadCases();

		/// <summary>
		/// Name of the beam
		/// </summary>
		public string BeamName => _beamCheckerAttributes.Name;


		#endregion

		#region Constructor

		public BeamChecker(BeamCheckerAttributes beamCheckerAttributes, Options options, Standard standard, string name = "")
			: this(beamCheckerAttributes, options, standard, Model.ModelObjectId.IDUNASSIGNED, name)
		{
			_errorLog = new List<string>();
		}

		public BeamChecker(BeamCheckerAttributes beamCheckerAttributes, Options options, Standard standard, int id, string name = "")
			: base(options, standard, id, name)
		{
			if (beamCheckerAttributes is null)			
				throw new ArgumentNullException(nameof(beamCheckerAttributes));			

			if (options is null)			
				throw new ArgumentNullException(nameof(options));			

			_beamCheckerAttributes = beamCheckerAttributes;
		}

		protected BeamChecker(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			_beamCheckerAttributes = (BeamCheckerAttributes)info.GetValue("BeamCheckerAttributes", typeof(BeamCheckerAttributes));
		}

		#endregion		

		#region Length

		public double GetLengthAxialBuckling1()
		{
			return BeamLength * BeamCheckerOptions.UnbracedLengthFactorAxialBuck1 * BeamCheckerOptions.EffectiveLengthFactorAxialBuck1;
		}

		public double GetLengthAxialBuckling2()
		{
			return BeamLength * BeamCheckerOptions.UnbracedLengthFactorAxialBuck2 * BeamCheckerOptions.EffectiveLengthFactorAxialBuck2;
		}

		public double GetEffectiveLengthAxialBuckling1()
		{
			return BeamLength * BeamCheckerOptions.EffectiveLengthFactorAxialBuck1;
		}

		public double GetEffectiveLengthAxialBuckling2()
		{
			return BeamLength * BeamCheckerOptions.EffectiveLengthFactorAxialBuck2;
		}

		public double GetLengthLatTorsBuckling()
		{
			return BeamLength * BeamCheckerOptions.UnbracedLengthFactorLatTorsBuck * BeamCheckerOptions.EffectiveLengthFactorLatTorsBuck;
		}

		public double GetLengthCriticalMoment1()
		{
			return BeamLength * BeamCheckerOptions.UnbracedLengthFactorCriticalMoment1 * BeamCheckerOptions.EffectiveLengthFactorCriticalMoment1;
		}

		public double GetLengthCriticalMoment2()
		{
			return BeamLength * BeamCheckerOptions.UnbracedLengthFactorCriticalMoment2 * BeamCheckerOptions.EffectiveLengthFactorCriticalMoment2;
		}

		#endregion

		#region Private Methods

		/// <returns>The unique ILoadCases array</returns>
		private ILoadCase[] GetLoadCases()
		{
			return _beamCheckerAttributes.Results.Select(i => i.Case).Distinct().ToArray();
		}

		#endregion

		#region Equals - hashcode - operators - serialization

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Standard", _standard);
			info.AddValue("Options", _options);
			info.AddValue("BeamCheckerAttributes", _beamCheckerAttributes);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
				return true;

			return obj is BeamChecker checker &&
				base.Equals(obj) &&
				_beamCheckerAttributes.Equals(checker._beamCheckerAttributes);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = 23;
				hashCode = hashCode * -17 + base.GetHashCode();
				hashCode = hashCode * -17 + _standard.GetHashCode();
				hashCode = hashCode * -17 + _options.GetHashCode();
				hashCode = hashCode * -17 + _beamCheckerAttributes.GetHashCode();
				return hashCode;
			}
		}

		#endregion

		#region Nested Class Options

		public abstract class BeamOptions : Options
		{
			#region Variables

			protected double _kAxialBuckling1;
			protected double _mAxialBuckling1;

			protected double _kAxialBuckling2;
			protected double _mAxialBuckling2;

			protected double _kLatTorsBuckling;
			protected double _mLatTorsBuckling;

			protected double _kCriticalMoment1;
			protected double _mCriticalMoment1;

			protected double _kCriticalMoment2;
			protected double _mCriticalMoment2;

			protected double _m1;
			protected double _m2;
			protected double _mLT;

			#endregion

			#region Properties

			/// <summary>
			/// Unbraced length factor for axial buckling about the frame object 1-axis
			/// </summary>
			public double UnbracedLengthFactorAxialBuck1 { get => _kAxialBuckling1; set => _kAxialBuckling1 = value; }

			/// <summary>
			/// Unbraced length factor for buckling about the frame object 2-axis
			/// </summary>
			public double UnbracedLengthFactorAxialBuck2 { get => _kAxialBuckling2; set => _kAxialBuckling2 = value; }

			/// <summary>
			/// Unbraced length factor for lateral torsional buckling
			/// </summary>
			public double UnbracedLengthFactorLatTorsBuck { get => _kLatTorsBuckling; set => _kLatTorsBuckling = value; }

			/// <summary>
			/// Unbraced length factor for critical moment about the frame object 1-axis
			/// </summary>
			public double UnbracedLengthFactorCriticalMoment1 { get => _kCriticalMoment1; set => _kCriticalMoment1 = value; }

			/// <summary>
			/// Unbraced length factor for critical moment about the frame object 2-axis
			/// </summary>
			public double UnbracedLengthFactorCriticalMoment2 { get => _kCriticalMoment2; set => _kCriticalMoment2 = value; }

			/// <summary>
			/// Effective length factor for axial buckling about the frame object 1-axis
			/// </summary>
			public double EffectiveLengthFactorAxialBuck1 { get => _mAxialBuckling1; set => _mAxialBuckling1 = value; }

			/// <summary>
			/// Effective length factor for axial buckling about the frame object 2-axis
			/// </summary>
			public double EffectiveLengthFactorAxialBuck2 { get => _mAxialBuckling2; set => _mAxialBuckling2 = value; }

			/// <summary>
			/// Effective length factor for lateral torsional buckling
			/// </summary>
			public double EffectiveLengthFactorLatTorsBuck { get => _mLatTorsBuckling; set => _mLatTorsBuckling = value; }

			/// <summary>
			/// Effective length factor for critical moment about the frame object 1-axis
			/// </summary>
			public double EffectiveLengthFactorCriticalMoment1 { get => _mCriticalMoment1; set => _mCriticalMoment1 = value; }

			/// <summary>
			/// Effective length factor for critical moment about the frame object 2-axis
			/// </summary>
			public double EffectiveLengthFactorCriticalMoment2 { get => _mCriticalMoment2; set => _mCriticalMoment2 = value; }

			/// <summary>
			/// Equivalent uniform moment factor for lateral torsional buckling 
			/// </summary>
			public double UniformMomentFactormLT { get => _mLT; set => _mLT = value; }

			/// <summary>
			/// Equivalent uniform moment factor for lateral torsional buckling 
			/// </summary>
			public double UniformMomentFactorm1 { get => _m1; set => _m1 = value; }

			/// <summary>
			/// Equivalent uniform moment factor for lateral torsional buckling 
			/// </summary>
			public double UniformMomentFactorm2 { get => _m2; set => _m2 = value; }

			#endregion

			#region Constructor

			public BeamOptions(double unbracedLengthFactorAxialBuck1 = 1, double effectiveLengthFactorAxialBuck1 = 1,
							double unbracedLengthFactorAxialBuck2 = 1, double effectiveLengthFactorAxialBuck2 = 1,
							double unbracedLengthFactorLatTorsBuck = 1, double effectiveLengthFactorLatTorsBuck = 1,
							double unbracedLengthFactorCriticalMoment1 = 1, double effectiveLengthFactorCriticalMoment1 = 1,
							double unbracedLengthFactorCriticalMoment2 = 1, double effectiveLengthFactorCriticalMoment2 = 1,
							double eqvUniformMomentFactorm1 = 1, double eqvUniformMomentFactorm2 = 1,
							double eqvUniformMomentFactormLT = 1)
			{
				if (unbracedLengthFactorAxialBuck1 < 0)
					throw new ArgumentException("UnbracedLengthFactorAxialBuck1 must be positive");
				_kAxialBuckling1 = unbracedLengthFactorAxialBuck1;

				if (unbracedLengthFactorAxialBuck2 < 0)
					throw new ArgumentException("UnbracedLengthFactorAxialBuck2 must be positive");
				_kAxialBuckling2 = unbracedLengthFactorAxialBuck2;

				if (unbracedLengthFactorLatTorsBuck < 0)
					throw new ArgumentException("UnbracedLengthFactorLatTorsBuck must be positive");
				_kLatTorsBuckling = unbracedLengthFactorLatTorsBuck;

				if (unbracedLengthFactorCriticalMoment1 < 0)
					throw new ArgumentException("UnbracedLengthFactorCriticalMoment1 must be positive");
				_kCriticalMoment1 = unbracedLengthFactorCriticalMoment1;

				if (unbracedLengthFactorCriticalMoment2 < 0)
					throw new ArgumentException("UnbracedLengthFactorCriticalMoment2 must be positive");
				_kCriticalMoment2 = unbracedLengthFactorCriticalMoment2;

				if (effectiveLengthFactorAxialBuck1 < 0)
					throw new ArgumentException("EffectiveLengthFactorAxialBuck1 must be positive");
				_mAxialBuckling1 = effectiveLengthFactorAxialBuck1;

				if (effectiveLengthFactorAxialBuck2 < 0)
					throw new ArgumentException("EffectiveLengthFactorAxialBuck2 must be positive");
				_mAxialBuckling2 = effectiveLengthFactorAxialBuck2;

				if (effectiveLengthFactorLatTorsBuck < 0)
					throw new ArgumentException("EffectiveLengthFactorLatTorsBuck must be positive");
				_mLatTorsBuckling = effectiveLengthFactorLatTorsBuck;

				if (effectiveLengthFactorCriticalMoment1 < 0)
					throw new ArgumentException("EffectiveLengthFactorCriticalMoment1 must be positive");
				_mCriticalMoment1 = effectiveLengthFactorCriticalMoment1;

				if (effectiveLengthFactorCriticalMoment2 < 0)
					throw new ArgumentException("EffectiveLengthFactorCriticalMoment2 must be positive");
				_mCriticalMoment2 = effectiveLengthFactorCriticalMoment2;

				if (eqvUniformMomentFactorm1 < 0)
					throw new ArgumentException("EffectiveLengthFactorCriticalMoment2 must be positive");
				_m1 = eqvUniformMomentFactorm1;

				if (eqvUniformMomentFactorm2 < 0)
					throw new ArgumentException("EffectiveLengthFactorCriticalMoment2 must be positive");
				_m2 = eqvUniformMomentFactorm2;

				if (eqvUniformMomentFactormLT < 0)
					throw new ArgumentException("EffectiveLengthFactorCriticalMoment2 must be positive");
				_mLT = eqvUniformMomentFactormLT;

			}


			#endregion

			#region Setter

			public void SetUnbracedLengthFactorAxialBuckling1(double unbracedLengthFactorAxialBuck1)
			{
				if (unbracedLengthFactorAxialBuck1 < 0)
					throw new ArgumentException("UnbracedLengthFactorAxialBuck1 must be positive");
				_kAxialBuckling1 = unbracedLengthFactorAxialBuck1;
			}

			public void SetUnbracedLengthFactorAxialBuckling2(double unbracedLengthFactorAxialBuck2)
			{
				if (unbracedLengthFactorAxialBuck2 < 0)
					throw new ArgumentException("UnbracedLengthFactorAxialBuck2 must be positive");
				_kAxialBuckling2 = unbracedLengthFactorAxialBuck2;
			}

			public void SetUnbracedLengthFactorLatTorsBuck(double unbracedLengthFactorLatTorsBuck)
			{
				if (unbracedLengthFactorLatTorsBuck < 0)
					throw new ArgumentException("UnbracedLengthFactorLatTorsBuck must be positive");
				_kLatTorsBuckling = unbracedLengthFactorLatTorsBuck;
			}

			public void SetUnbracedLengthFactorCriticalMoment1(double unbracedLengthFactorCriticalMoment1)
			{
				if (unbracedLengthFactorCriticalMoment1 < 0)
					throw new ArgumentException("UnbracedLengthFactorCriticalMoment1 must be positive");
				_kCriticalMoment1 = unbracedLengthFactorCriticalMoment1;
			}

			public void SetUnbracedLengthFactorCriticalMoment2(double unbracedLengthFactorCriticalMoment2)
			{
				if (unbracedLengthFactorCriticalMoment2 < 0)
					throw new ArgumentException("UnbracedLengthFactorCriticalMoment2 must be positive");
				_kCriticalMoment2 = unbracedLengthFactorCriticalMoment2;
			}

			public void SetEffectiveLengthFactorAxialBuck1(double effectiveLengthFactorAxialBuck1)
			{
				if (effectiveLengthFactorAxialBuck1 < 0)
					throw new ArgumentException("EffectiveLengthFactorAxialBuck1 must be positive");
				_mAxialBuckling1 = effectiveLengthFactorAxialBuck1;
			}

			public void SetEffectiveLengthFactorAxialBuck2(double effectiveLengthFactorAxialBuck2)
			{
				if (effectiveLengthFactorAxialBuck2 < 0)
					throw new ArgumentException("EffectiveLengthFactorAxialBuck2 must be positive");
				_mAxialBuckling2 = effectiveLengthFactorAxialBuck2;
			}

			public void SetEffectiveLengthFactorLatTorsBuck(double effectiveLengthFactorLatTorsBuck)
			{
				if (effectiveLengthFactorLatTorsBuck < 0)
					throw new ArgumentException("EffectiveLengthFactorLatTorsBuck must be positive");
				_mLatTorsBuckling = effectiveLengthFactorLatTorsBuck;
			}

			public void SetEffectiveLengthFactorCriticalMoment1(double effectiveLengthFactorCriticalMoment1)
			{
				if (effectiveLengthFactorCriticalMoment1 < 0)
					throw new ArgumentException("EffectiveLengthFactorCriticalMoment1 must be positive");
				_mCriticalMoment1 = effectiveLengthFactorCriticalMoment1;
			}

			public void SetEffectiveLengthFactorCriticalMoment2(double effectiveLengthFactorCriticalMoment2)
			{
				if (effectiveLengthFactorCriticalMoment2 < 0)
					throw new ArgumentException("EffectiveLengthFactorCriticalMoment2 must be positive");
				_mCriticalMoment2 = effectiveLengthFactorCriticalMoment2;
			}

			public void SetUniformMomentFactorm1(double uniformMomentFactorm1)
			{
				if (uniformMomentFactorm1 < 0)
					throw new ArgumentException("UniformMomentFactorm1 must be positive");
				_m1 = uniformMomentFactorm1;
			}

			public void SetUniformMomentFactorm2(double uniformMomentFactorm2)
			{
				if (uniformMomentFactorm2 < 0)
					throw new ArgumentException("UniformMomentFactorm2 must be positive");
				_m2 = uniformMomentFactorm2;
			}

			public void SetUniformMomentFactormLT(double uniformMomentFactormLT)
			{
				if (uniformMomentFactormLT < 0)
					throw new ArgumentException("UniformMomentFactormLT must be positive");
				_mLT = uniformMomentFactormLT;
			}

			#endregion
		}

		#endregion
	}
}
