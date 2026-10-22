using System;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.ModelAdapters
{
    /// <summary>Local chord result for a declared axial fork/free-warping panel model.
    /// It is not the global pair buckling result or a connection verification.</summary>
    public sealed class SteelPackedChordResistance
    {
        public SteelMemberAnalysis Analysis { get; }
        public SteelCheckExecution Compression { get; }
        public double TensionResistance { get; }
        public string ModelSource { get; }
        private SteelPackedChordResistance(SteelMemberAnalysis analysis,SteelCheckExecution compression,double tension,string source)
        { Analysis=analysis;Compression=compression;TensionResistance=tension;ModelSource=source; }

        /// <summary>Identical unperforated C/L, classes1-3. Caller establishes that each
        /// chord panel has constant centroidal axial force, no local shear/moment,
        /// fork ends/free warping over maximumPitch in both principal planes and torsion.
        /// The explicit compression envelope includes imperfections and force transfer;
        /// it is never inferred as N/2. No connection capacity is computed here.</summary>
        public static SteelPackedChordResistance UniformFork(SteelVerificationInput pair,double maximumPitch,
            double compressionEnvelope,string modelSource)
        {
            if(pair==null)throw new ArgumentNullException(nameof(pair));
            SteelInputValidation.Positive(maximumPitch,nameof(maximumPitch));
            SteelInputValidation.Positive(compressionEnvelope,nameof(compressionEnvelope));
            if(string.IsNullOrWhiteSpace(modelSource))throw new ArgumentException("Explicit local axial fork model source required.",nameof(modelSource));
            if(pair.Section.Unperforated!=true||pair.Actions.N>=0||compressionEnvelope < -pair.Actions.N/2)
                throw new ArgumentException("Unperforated compressive pair and a complete envelope at least N/2 required.");
            var property=pair.Section.CreateSectionCopy();
            if(!(property.SectionShape is SectionBuiltUp shape))throw new NotSupportedException("Identical packed C/L pair required.");
            ModelBuiltUpGeometry.Create(shape);
            var chord=new SteelSection(shape.Parts[0].Section,property.SteelMaterial,property.SectionType,property.FormedType);
            var input=new SteelVerificationInput(new SteelSectionInput(chord,true),new SteelActions(-compressionEnvelope,0,0,0,0,0),
                pair.Code,new SteelMemberInput(maximumPitch,maximumPitch,maximumPitch,maximumPitch));
            var curves=SteelAutomaticStability.CompressionCurves(input.Section,input.Code.SectionCode.Standard);
            var values=SteelAutomaticStability.UniformFork(input,false,modelSource);
            var analysis=SteelMemberAnalysis.Compression(input,modelSource,curves.Source,curves.First,curves.Second,curves.Torsional,values);
            var check=SteelVerificationPipeline.Execute(SteelMethodSelector.SelectMember(analysis)).Checks.Single();
            if(!check.IsEvaluated)throw new NotSupportedException("Local chord analysis unavailable: "+check.DiagnosticCode+"; "+check.Message);
            double tension=chord.Area*input.Section.YieldStrength/input.Code.SectionCode.GammaM0;
            SteelInputValidation.Positive(tension,"chord tensile resistance");
            return new SteelPackedChordResistance(analysis,check,tension,modelSource);
        }
    }
}
