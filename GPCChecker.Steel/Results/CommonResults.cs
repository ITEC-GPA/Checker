using GPC.Checkers.Steel.Checkers;
using GPC.Model.LoadCases;
using GPC.Model.Standards;
using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.Results
{
	public abstract class CommonResults : Model.ModelObject, ISerializable
	{
		#region Variables

		protected readonly ILoadCase _case;
		protected readonly Standard _standard;
		protected readonly Checker.Options _options;

		#endregion

		#region Properties

		/// <summary>
		/// The <see cref="ILoadCase"/> to check
		/// </summary>
		public ILoadCase LoadCase => _case;

		/// <summary>
		/// The Standard for the Check
		/// </summary>
		public Standard Standard => _standard;

		/// <summary>
		/// The options to perform the check.
		/// </summary>
		public Checker.Options Options => _options;

		/// <summary>
		/// The max working ratio 
		/// </summary>
		public double WorkingRatio => GetMaxWorkingRatio();

		#endregion

		internal CommonResults(ILoadCase Case, Standard standard, Checker.Options checkerOptions, string name = "")
			: base(name)
		{
			_case = Case ?? throw new ArgumentNullException(nameof(Case));
			_standard = standard ?? throw new ArgumentNullException(nameof(standard));
			_options = checkerOptions ?? throw new ArgumentNullException(nameof(checkerOptions));
		}

		internal CommonResults(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			_case = (ILoadCase)info.GetValue("ILoadCase", typeof(ILoadCase));
			_standard = (Standard)info.GetValue("Standard", typeof(Standard));
			_options = (BeamChecker.BeamOptions)info.GetValue("CheckerOptions", typeof(BeamChecker.BeamOptions));
		}

		internal abstract double GetMaxWorkingRatio();

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("ILoadCase", _case, typeof(ILoadCase));
			info.AddValue("Standard", _standard, typeof(Standard));
			info.AddValue("CheckerOptions", _options, typeof(BeamChecker.BeamOptions));
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
				return true;

			return (obj is CommonResults objCasted)
				&& _case.Equals(objCasted._case)
				&& _standard.Equals(objCasted._standard)
				&& _options.Equals(objCasted._options)
				&& base.Equals(objCasted);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = 23;
				hashCode = hashCode * -17 + base.GetHashCode();
				hashCode = hashCode * -17 + _case.GetHashCode();
				hashCode = hashCode * -17 + _standard.GetHashCode();
				hashCode = hashCode * -17 + _options.GetHashCode();

				return hashCode;
			}
		}
	}
}
