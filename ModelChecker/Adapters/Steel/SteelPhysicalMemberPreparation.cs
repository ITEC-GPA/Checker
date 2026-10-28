using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Model.Attributes;
using GPC.Model.Checking.Contracts;
using GPC.Model.Checking.Preparation;
using GPC.Model.Checking.Reports;
using GPC.Model.Core;
using GPC.Model.Core.Coordinates;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Structure.Members;

namespace GPC.Model.Checker
{
    /// <summary>Qualifies a straight, continuous, prismatic physical span and puts
    /// every concomitant cut into one section frame. FE nodes do not create design
    /// restraints or effective lengths. No interpolation or cross-state envelopes.
    /// The beam-column route extracts member maxima within one state only.</summary>
    internal sealed class SteelPhysicalMemberPreparation
    {
        internal sealed class Cut
        {
            internal BeamActionInput Source;
            internal double Distance;
            internal SteelActions Actions;
        }
        internal IReadOnlyList<Cut> Cuts { get; private set; }
        internal IReadOnlyList<CheckCalculationValue> Trace { get; private set; }

        /// <param name="shape">Shape of the owned cut property the section snapshot was copied from:
        /// reading it avoids a further XML copy of the snapshot.</param>
        internal static SteelPhysicalMemberPreparation Create(PhysicalMemberCheckInput input, SteelSectionInput section, ISectionShape shape, SteelDesignContext code)
        {
            var s=input.Snapshot;var first=input.ActionSections[0];
            var geometry=new PhysicalMemberGeometry(first.Model,s.Definition);
            if (!(s.SpanEnd>s.SpanStart) || s.SpanStart<0 || s.SpanEnd>s.Length || Math.Abs(s.Length-s.DeformableLength)>1e-7)
                throw new NotSupportedException("Prismatic member requires a nonzero deformable span without rigid zones.");
            var old=first.Forces.CoordinateSystem;var frame=Axes.Beam(old.Origin,old.Origin+s.Axes.V3,old.V1);
            bool symmetric=Bisymmetric(shape);
            string property=ModelValues.Fingerprint(new object[]{first.Property});
            var cuts=new List<Cut>();var trace=new List<CheckCalculationValue>{
                new CheckCalculationValue("Member.Geometry.Length",s.Length,"mm","Physical path; independent of FE discretization"),
                new CheckCalculationValue("Member.Geometry.SpanStart",s.SpanStart,"mm","Explicit verification-span boundary"),
                new CheckCalculationValue("Member.Geometry.SpanEnd",s.SpanEnd,"mm","Explicit verification-span boundary"),
                new CheckCalculationValue("Member.Geometry.LengthSource",null,null,s.Context.LengthSource),
                new CheckCalculationValue("Member.Geometry.Restraints",s.Context.Restraints.Count,"1","Declared restraints; never inferred from mesh nodes")};
            foreach(var cut in input.ActionSections)
            {
                if(!ReferenceEquals(cut.Property,first.Property)&&ModelValues.Fingerprint(new object[]{cut.Property})!=property)throw new NotSupportedException("Nonuniform prismatic section.");
                var axes=cut.Forces.CoordinateSystem;Axes.Validate(axes);
                double x=Axes.Dot(axes.V1,frame.V1),y=Axes.Dot(axes.V2,frame.V2),z=Axes.Dot(axes.V3,frame.V3);
                if(Math.Abs(x)<1-1e-10 || Math.Abs(y)<1-1e-10 || Math.Abs(z)<1-1e-10)
                    throw new NotSupportedException("Section frames rotate along the member; a common principal frame is not qualified.");
                if(!symmetric && (x<0 || y<0 || z<0))
                    throw new NotSupportedException("A reflected asymmetric section requires an explicit common physical geometry frame.");
                // Axis directions have been qualified as parallel/antiparallel.
                // Exact signs avoid leaking roundoff into inactive force components.
                double face=z<0?-1:1,sx=face*(x<0?-1:1),sy=face*(y<0?-1:1);
                var f=cut.Forces;
                var normalized=new ResultBeamForces(f.N,sx*f.V1,sy*f.V2,f.T,sx*f.M1,sy*f.M2,frame);
                var location=geometry.FromElement(cut.Element.Id,cut.Sample.ParametricDistance,cut.Sample.StationDomain,cut.Sample.Side);
                cuts.Add(new Cut{Source=cut,Distance=location.Distance,Actions=ModelSteelInput.FromSectionAxes(section,normalized,code).Actions});
            }
            int count=0;
            foreach(var part in s.Definition.Parts)
            {
                var beam=first.Model.BeamElements[part.BeamId];
                double i=geometry.FromElement(beam.Id,0,s.Definition.StationDomain,SectionSide.Unspecified).Distance;
                double j=geometry.FromElement(beam.Id,1,s.Definition.StationDomain,SectionSide.Unspecified).Distance;
                double start=Math.Max(Math.Min(i,j),s.SpanStart),end=Math.Min(Math.Max(i,j),s.SpanEnd);
                if(start>=end)continue;
                count++;
                var a=beam.Assignments;
                if(a.RigidLengthI!=0 || a.RigidLengthJ!=0 || Nonzero(a.OffsetI) || Nonzero(a.OffsetJ)
                    || a.SectionCentroidOffset!=null && (a.SectionCentroidOffset.X!=0 || a.SectionCentroidOffset.Y!=0))
                    throw new NotSupportedException("Offsets or rigid zones need a separately qualified member model.");
                // Inspect the full assignment, including intervals without exported cuts.
                if(a.Sections.Any(v=>v.Law!="Constant" || ModelValues.Fingerprint(new object[]{v.Property})!=property
                    || !(v.EndProperty is null) && ModelValues.Fingerprint(new object[]{v.EndProperty})!=property))
                    throw new NotSupportedException("Nonuniform or interpolated member section assignment.");
                foreach(var release in beam.Attributes.Values.OfType<BeamReleasesAttribute>())
                {
                    bool innerI=i>s.SpanStart+1e-7&&i<s.SpanEnd-1e-7,innerJ=j>s.SpanStart+1e-7&&j<s.SpanEnd-1e-7;
                    if(innerI&&release.I.Any(v=>v==null||v.Kind!=BeamConnectionKind.Continuous)
                        || innerJ&&release.J.Any(v=>v==null||v.Kind!=BeamConnectionKind.Continuous))
                        throw new NotSupportedException("An internal release or connection spring interrupts the continuous prismatic span.");
                }
                foreach(double boundary in new[]{start,end})
                    if(!cuts.Any(c=>c.Source.Element.Id==beam.Id&&Math.Abs(c.Distance-boundary)<=1e-7))
                        throw new ArgumentException("Both boundaries of every participating FE interval require exported concomitant cuts.");
                trace.Add(new CheckCalculationValue("Member.Geometry.Beam."+beam.Id,part.AlongIJ?1:-1,"1","I/J orientation in physical-member path; both interface faces retained"));
            }
            trace.Add(new CheckCalculationValue("Member.Geometry.ElementCount",count,"1","Number of participating FE elements; not a restraint count"));
            trace.Add(new CheckCalculationValue("Member.Geometry.CutCount",cuts.Count,"1","Exported concomitant cuts; no assertion of continuous coverage"));
            return new SteelPhysicalMemberPreparation{Cuts=cuts.OrderBy(c=>c.Distance).ToArray(),Trace=trace};
        }
        internal SteelActions BeamColumnEnvelope()
        {
            double n=Cuts[0].Actions.N;
            // The member route neglects tension (favourable to flexural/LTB buckling): only compression must be constant.
            bool tension=Cuts.All(c=>c.Actions.N>=0);
            foreach(var cut in Cuts)
            {
                if(cut.Actions.T!=0)throw new NotSupportedException("Applied torque is outside the beam-column route.");
                if(!tension&&Math.Abs(cut.Actions.N-n)>1e-10*Math.Max(1,Math.Abs(n)))throw new NotSupportedException("Beam-column compression must be constant over the selected span.");
            }
            var evidence=new List<CheckCalculationValue>(Trace);
            if(tension&&Cuts.Any(c=>c.Actions.N!=n)) {
                var least=Cuts.OrderBy(c=>c.Actions.N).First();n=least.Actions.N;
                evidence.Add(new CheckCalculationValue("Member.Envelope.N",n,"N","Varying tension: smallest value of this ONE design state; neglected as favourable by the member stability route"));
                evidence.Add(new CheckCalculationValue("Member.Envelope.N.Distance",least.Distance,"mm","Physical-member coordinate of controlling exported cut"));
            }
            double Maximum(string name,Func<SteelActions,double> component)
            {
                var cut=Cuts.OrderByDescending(c=>Math.Abs(component(c.Actions))).First();double value=component(cut.Actions);
                evidence.Add(new CheckCalculationValue("Member.Envelope."+name,value,name.StartsWith("V")?"N":"Nmm","Signed absolute maximum within this ONE design state; not a fictitious section cut"));
                evidence.Add(new CheckCalculationValue("Member.Envelope."+name+".Distance",cut.Distance,"mm","Physical-member coordinate of controlling exported cut"));
                evidence.Add(new CheckCalculationValue("Member.Envelope."+name+".BeamId",cut.Source.Element.Id,"1","Original FE element; concomitant state retained in member snapshot"));
                return value;
            }
            double v1=Maximum("V1",a=>a.V1),v2=Maximum("V2",a=>a.V2),m1=Maximum("M1",a=>a.M1),m2=Maximum("M2",a=>a.M2);
            Trace=evidence;return new SteelActions(n,v1,v2,0,m1,m2);
        }
        internal void AddTrace(CheckResult result)
        {
            if(!(result.Details is NativeMethodDetails d))return;
            result.Details=new NativeMethodDetails(d.MethodId,d.ScopeDescription,d.NativeInputFingerprint,d.Metrics,
                d.Trace.Concat(Trace),d.Warnings,d.Points,d.Convergence,d.UtilizationDefinition);
        }
        private static bool Nonzero(Vector3d v)=>v!=null&&(v.X!=0||v.Y!=0||v.Z!=0);
        private static bool Bisymmetric(ISectionShape shape)
        {
            if(shape.GetType()==typeof(SectionH)) { var h=(SectionH)shape;return h.LenghtTopFlange==h.LenghtBottomFlange&&h.ThicknessTopFlange==h.ThicknessBottomFlange; }
            if(shape.GetType()==typeof(SectionRHS)) { var r=(SectionRHS)shape;return r.ThicknessTop==r.ThicknessBottom&&r.ThicknessWebLeft==r.ThicknessWebRight; }
            return shape.GetType()==typeof(SectionRHSRoundedCorners)||shape.GetType()==typeof(SectionCHS)
                ||shape.GetType()==typeof(SectionRectangular)||shape.GetType()==typeof(SectionCircular);
        }
    }
}
