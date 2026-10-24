using System.Collections.Generic;
using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.EffectiveSections;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Pipeline;
using GPC.Checkers.Steel.Sections;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Steel.Members.Tests;

[TestClass]
public class EndPlateBeamSectionAdapterTests
{
    private static SectionH SciShape() => new(533.1, 10.1, 209.3, 15.6, 209.3, 15.6, "SCI C1 geometry", 12.7);
    private static double Area(SectionH h) => h.LenghtTopFlange * h.ThicknessTopFlange
        + h.LenghtBottomFlange * h.ThicknessBottomFlange + h.HeightWeb * h.ThicknessWeb + (4 - Math.PI) * h.R * h.R;
    private static ConnectionRolledISection Beam(SectionH h, double fy = 275, double fu = 410)
        => new(h.Height, h.LenghtTopFlange, h.ThicknessWeb, h.ThicknessTopFlange, h.R, Area(h), fy, fu);
    private static EndPlateBeamSectionInput Input(bool ntc = false, double moment = 300e6, double shear = 200000,
        Section? shape = null, double fy = 275, double fu = 410, ConnectionRolledISection? beam = null,
        ConnectionDesignCode? code = null, double? flangeFy = null, double? flangeFu = null, SteelActions? actions = null,
        bool? unperforated = true, double? netArea = null, bool notched = false,
        Section.SectionTypes production = Section.SectionTypes.Rolled,
        Section.FormedTypes formed = Section.FormedTypes.HotFinished,
        string geometrySource = "Actual SCI C1 beam geometry", string materialSource = "Fy/Fu both web10.1 and flange15.6mm",
        string caseSource = "One concomitant cut action set", double? ecEta = 1.2, string etaSource = "Explicit EC3 eta1.2")
    {
        shape ??= SciShape();
        beam ??= Beam(shape is SectionH h && h.LenghtTopFlange == h.LenghtBottomFlange
            && h.ThicknessTopFlange == h.ThicknessBottomFlange ? h : SciShape(), fy, fu);
        code ??= ntc ? ConnectionDesignCode.Ntc2018() : ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "Explicit test factors");
        var section = new SteelSectionInput(new SteelSection(shape, new SteelMaterial("Actual material", 210000, fy, fu), production, formed),
            unperforated, netArea);
        return new(code, beam, section, actions ?? new SteelActions(0, 0, shear, 0, moment, 0), flangeFy ?? fy,
            flangeFu ?? fu, notched, materialSource, geometrySource, caseSource, ntc ? null : ecEta, ntc ? null! : etaSource);
    }
    private static EndPlateBeamSectionAssessment Evaluate(EndPlateBeamSectionInput? input = null)
        => EndPlateBeamSectionAdapter.Evaluate(input ?? Input());
    private static double T(EndPlateBeamSectionAssessment r, string key) => r.Trace.Single(v => v.Name == key).Value;
    private static void Close(double expected, double actual, double relative = 2e-10)
        => Assert.AreEqual(expected, actual, Math.Max(1e-9, Math.Abs(expected) * relative));
    private static void Unsupported(EndPlateBeamSectionAssessment r, string issue)
    {
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status, string.Join(";", r.Diagnostics));
        Assert.IsFalse(r.IsSectionAssessmentComplete); Assert.IsNull(r.DerivedResistance);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Contains(issue)), string.Join(";", r.Diagnostics));
        Assert.IsFalse(r.CanQualifyWholeJoint); Assert.IsFalse(r.CanQualifyWholeMember);
    }
    private static EndPlateMomentComponentInput Joint(EndPlateBeamSectionAssessment r, EndPlateBeamMomentResistance? token = null,
        double? moment = null, double? shear = null, double? axial = null, ConnectionDesignCode? code = null,
        ConnectionRolledISection? beam = null, string source = "Joint provenance can have a different label")
    {
        code ??= r.Input.Code; beam ??= r.Input.Beam;
        var policy = code.IsNtc ? ColumnWebPolicy.Ntc2018(code) : ColumnWebPolicy.Eurocode3(code, 1);
        token ??= r.DerivedResistance ?? new EndPlateBeamMomentResistance(code, beam, 649e6, Math.Abs(r.Input.Actions.V2), "manual fallback for rejection test");
        return new(policy, new(266.7, 258.8, 12.8, 20.5, 12.7, 13600, 265, 410), beam, 265,
            new(250, 25, 265, 410), new(24, 26, 353, ConnectionBoltGrade.Class8_8, true),
            new(250, 258.8, 100, 10.1, 8, 12, 8, 501.9, new[] { 44.4, 134.4 },
                new(7.5, "SCI C1 graphical alpha"), "C1 actual detail"),
            new(250, 100, 40, 12, 50, "C1 outside row"), 1000, 1000, 46.9, token,
            moment ?? r.Input.Actions.M1, shear ?? Math.Abs(r.Input.Actions.V2), axial ?? r.Input.Actions.N,
            0, 0, ColumnWebPanelConfiguration.DoubleSidedSimilarBeamDepths,
            "Equal/opposite beams beta0", "Actual zero column stress", "Qualified sway restraint", source,
            TStubMode1Method.DistributedContact, 39.55, "NTC clearance SLE externally qualified");
    }

    [DataTestMethod] [DataRow(false, 649024039.6809642, 914751.8573499944)]
    [DataRow(true, 618118133.0294898, 871192.2450952327)]
    public void ActualRoundedRootGeometryMatchesIndependentIntegrals(bool ntc, double mc, double vpl)
    {
        var r = Evaluate(Input(ntc)); Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.Diagnostics));
        Close(11737.802520902502, T(r, "Geometry.ModelArea"));
        // Wpl integrates the existing64-chord/quadrant outline; exact circular oracle is unchanged.
        Close(2360087.417021688, T(r, "Geometry.Wpl1"), 3e-5); Close(552273065.0878704, T(r, "Geometry.I1"));
        Close(2071930.4636573638, T(r, "Geometry.Wel1")); Close(5761.442520902502, T(r, "Geometry.Av2"));
        Close(vpl, T(r, "VplRd")); Close(mc, r.DerivedResistance.Value, 3e-5);
        Assert.AreEqual(1, r.Classification.SectionClass); Assert.AreEqual(2, r.Checks.Count);
        Assert.IsTrue(r.SectionVerification.IsComplete); Assert.IsTrue(r.IsSectionAssessmentComplete);
        Assert.AreSame(r.Input.Actions, r.CoreInput.Actions); Assert.AreSame(r.Input.Section, r.CoreInput.Section);
        Assert.AreSame(r.Input.Code.Plates, r.CoreInput.Code.SectionCode);
        Assert.AreSame(r.CoreInput, r.Classification.Input); Assert.AreSame(r.CoreInput, r.ShearBucklingAssessment.Input);
        Assert.AreSame(r.Input.Code, r.DerivedResistance.Code); Assert.AreSame(r.Input.Beam, r.DerivedResistance.Beam);
        Assert.IsFalse(r.CanQualifyWholeMember); Assert.IsFalse(r.CanQualifyWholeJoint);
    }

    [TestMethod]
    public void PublishedSci649kNmIsSeparateFromTheExactGeometryOracle()
    {
        var r = Evaluate(); Assert.AreEqual(649e6, r.DerivedResistance.Value, .5e6);
        Assert.IsTrue(Math.Abs(r.DerivedResistance.Value - 649e6) > 20000);
        var catalogueBeam = new ConnectionRolledISection(533.1, 209.3, 10.1, 15.6, 12.7, 11700, 275, 410);
        var catalogue = Evaluate(Input(beam: catalogueBeam));
        Close(r.DerivedResistance.Value, catalogue.DerivedResistance.Value);
        Close(11700, T(catalogue, "Geometry.ConnectionArea"));
        Assert.AreNotEqual(T(catalogue, "Geometry.ModelArea"), T(catalogue, "Geometry.ConnectionArea"));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void PureBendingClassificationDoesNotUseUniformCompressionAtZeroOrNonzeroDemand(bool ntc)
    {
        var p = Input(ntc); var r = Evaluate(p); var zero = Evaluate(Input(ntc, moment: 0, shear: 0));
        var uniform = ntc ? Ntc2018CompressionClassification.Evaluate(ModelCompressionGeometry.Create(p.Section.CreateSectionCopy()), 275)
            : Ec3UniformCompressionClassification.Evaluate(ModelCompressionGeometry.Create(p.Section.CreateSectionCopy()), 275);
        Assert.AreEqual(4, uniform.NominalClass); Assert.AreEqual(1, r.Classification.SectionClass);
        Assert.AreEqual(1, zero.Classification.SectionClass); Close(r.DerivedResistance.Value, zero.DerivedResistance.Value);
        Assert.IsTrue(zero.SectionVerification.Checks.All(c => c.Status == SteelExecutionStatus.NotRequired));
        Assert.AreEqual(0, zero.CoreInput.Actions.M1); Assert.AreEqual(0, zero.CoreInput.Actions.V2);
        Assert.IsFalse(zero.SectionVerification.IsComplete); Assert.IsTrue(zero.IsSectionAssessmentComplete);
        Assert.AreEqual(ConnectionStatus.Passed, zero.Status);
        var web = r.ClassificationParts.Single(v => v.Id == "Web").Classification;
        Close(47.17821782178218, web.WidthThicknessRatio!.Value); Close(66.55797199707663, web.Class1Limit!.Value);
        Assert.IsTrue(web.Reference.StartsWith(ntc ? "NTC" : "EN"));
        CollectionAssert.AreEquivalent(new[] { "BottomFlange", "Web" }, r.ClassificationParts.Select(v => v.Id).ToArray());
    }

    [TestMethod]
    public void ExecutableCoreStressConfirmsPositiveM1TensionsTheNamedUpperFlange()
    {
        var p = Input(shear: 0); var geometry = SteelEffectiveGeometry.Create(p.Section); var state = geometry.Gross();
        double upper = state.Stress(geometry.GrossX, geometry.MaxY, 0, 300e6, 0);
        double lower = state.Stress(geometry.GrossX, geometry.MinY, 0, 300e6, 0);
        Assert.IsTrue(upper > 0); Assert.IsTrue(lower < 0);
        Close(300e6 / 2071930.4636573638, upper); Close(-upper, lower);
        Unsupported(Evaluate(Input(moment: -300e6, shear: 0)), "UPPER_FLANGE_TENSION");
    }

    [DataTestMethod]
    [DataRow(false, 0.0, 1.0)] [DataRow(false, .5, 1.0)] [DataRow(false, .500001, 1.0)]
    [DataRow(false, .75, 1.0)] [DataRow(false, 1.0, 1.0)] [DataRow(false, .75, -1.0)]
    [DataRow(true, 0.0, 1.0)] [DataRow(true, .5, 1.0)] [DataRow(true, .500001, 1.0)]
    [DataRow(true, .75, 1.0)] [DataRow(true, 1.0, 1.0)] [DataRow(true, .75, -1.0)]
    public void MajorMomentShearCapacityMatchesIndependentReducedStressIntegration(bool ntc, double ratio, double sign)
    {
        // Independent half-section stress integrals, not a call to the production interaction kernel.
        double gamma = ntc ? 1.05 : 1, q = 501.9 / 2, r = 12.7;
        double ar = (1 - Math.PI / 4) * r * r, qr = (5.0 / 6 - Math.PI / 4) * r * r * r;
        double flangeFirst = 209.3 * 15.6 * (533.1 / 2 - 15.6 / 2);
        double rootsFirst = 2 * (q * ar - qr), webFirst = 10.1 * q * q / 2;
        double rho = ratio <= .5 ? 0 : Math.Pow(2 * ratio - 1, 2);
        double expected = 2 * 275 / gamma * (flangeFirst + rootsFirst + (1 - rho) * webFirst);
        // Take the capacity boundary from the adapter's independently checked shear property to avoid
        // a false over-limit caused solely by a different floating-point summation of circular roots.
        double vpl = T(Evaluate(Input(ntc)), "VplRd");
        double demand = sign * ratio * vpl;
        var result = Evaluate(Input(ntc, moment: 100e6, shear: demand));
        Assert.AreEqual(ConnectionStatus.Passed, result.Status, string.Join(";", result.Diagnostics));
        // Only the circular-root plastic integral has the documented Model discretisation tolerance.
        Close(expected, result.DerivedResistance.Value, 3e-5); Close(rho, T(result, "Rho"));
        Assert.AreEqual(demand, result.CoreInput.Actions.V2); Assert.AreEqual(demand, T(result, "Actions.V2"));
        Assert.AreEqual(Math.Abs(demand), result.DerivedResistance.ConcomitantShear);
        if (ratio > .5)
        {
            var normal = result.SectionVerification.Checks.Single(c => c.Decision.Target == SteelCheckTarget.NormalResistance);
            Assert.AreEqual(SteelMethod.HMajorBendingShear, normal.Decision.Method); Close(expected, normal.Resistance!.Value, 3e-5);
            Close(result.DerivedResistance.Value, normal.Resistance.Value); // Same algorithm remains strict.
        }
    }

    [DataTestMethod] [DataRow(false, 1.0)] [DataRow(false, -1.0)] [DataRow(true, 1.0)] [DataRow(true, -1.0)]
    public void ZeroMomentAtHighShearDoesNotNeedAFictitiousPipelineMoment(bool ntc, double sign)
    {
        double v = .75 * T(Evaluate(Input(ntc)), "VplRd") * sign;
        var atZero = Evaluate(Input(ntc, moment: 0, shear: v)); var loaded = Evaluate(Input(ntc, moment: 1e6, shear: v));
        Close(loaded.DerivedResistance.Value, atZero.DerivedResistance.Value);
        Assert.AreEqual(0, atZero.SectionVerification.Plan.Input.Actions.M1);
        Assert.AreEqual(SteelExecutionStatus.NotRequired, atZero.SectionVerification.Checks.Single(c => c.Decision.Target == SteelCheckTarget.NormalResistance).Status);
        Assert.AreEqual(ConnectionStatus.Passed, atZero.Status);
    }

    [DataTestMethod] [DataRow(false, 0.0)] [DataRow(false, 300000000.0)]
    [DataRow(true, 0.0)] [DataRow(true, 300000000.0)]
    public void ShearBeyondItsCapacityIsFailedAndCannotExportMoment(bool ntc, double moment)
    {
        double v = T(Evaluate(Input(ntc)), "VplRd") * 1.000001;
        var r = Evaluate(Input(ntc, moment: moment, shear: v));
        Assert.AreEqual(ConnectionStatus.Failed, r.Status); Assert.IsNull(r.DerivedResistance);
        Assert.IsFalse(r.IsSectionAssessmentComplete);
        Assert.IsTrue(r.Diagnostics.Contains("SHEAR_EXCEEDS_VPL_NO_MOMENT_CAPACITY_EXPORT"));
        Assert.IsTrue(r.Checks.Any(c => c.Id == "BeamSection.WebShear" && !c.Passed));
        Assert.IsTrue(r.SectionVerification.Checks.Any(c => c.Status == SteelExecutionStatus.Failed));
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void FailedMomentRemainsVisibleAndFiniteResistanceIsRetained(bool ntc)
    {
        var r = Evaluate(Input(ntc, moment: 800e6)); Assert.AreEqual(ConnectionStatus.Failed, r.Status);
        Assert.IsNotNull(r.DerivedResistance); Assert.IsTrue(r.IsSectionAssessmentComplete);
        Assert.IsTrue(r.SectionVerification.Checks.Any(c => c.Status == SteelExecutionStatus.Failed));
        var joint = EndPlateMomentWithBeamSection.Verify(Joint(r), r);
        Assert.AreEqual(ConnectionStatus.Failed, joint.Status); Assert.AreSame(r, joint.BeamSection);
        Assert.IsNotNull(joint.MomentComponents); Assert.IsFalse(joint.CanQualifyWholeJoint);
    }

    [DataTestMethod]
    [DataRow(false, 9.0, 1)] [DataRow(false, 9.000001, 2)] [DataRow(false, 10.0, 2)]
    [DataRow(false, 10.000001, 3)] [DataRow(false, 14.0, 3)] [DataRow(false, 14.000001, 4)]
    [DataRow(true, 9.0, 1)] [DataRow(true, 9.000001, 2)] [DataRow(true, 10.0, 2)]
    [DataRow(true, 10.000001, 3)] [DataRow(true, 14.0, 3)] [DataRow(true, 14.000001, 4)]
    public void ActualFlangeWidthSpansAllNominalClassesWithoutCallerSuppliedClass(bool ntc, double ratio, int expectedClass)
    {
        double width = 12 + 24 * ratio;
        var h = new SectionH(500, 12, width, 12, width, 12, "class boundary", 0);
        var result = Evaluate(Input(ntc, shape: h, fy: 235, fu: 360, shear: 0, moment: 1e6));
        Assert.AreEqual(expectedClass, result.Classification.SectionClass);
        if (expectedClass == 4) Unsupported(result, "CLASS4_BEAM_ADAPTER_NOT_SUPPORTED");
        else
        {
            Assert.AreEqual(ConnectionStatus.Passed, result.Status, string.Join(";", result.Diagnostics));
            double gamma = ntc ? 1.05 : 1;
            double momentIntegral = 2 * width * 12 * 244 + 12 * 476 * 476 / 4;
            double inertia = 2 * (width * Math.Pow(12, 3) / 12 + width * 12 * 244 * 244) + 12 * Math.Pow(476, 3) / 12;
            Close((expectedClass <= 2 ? momentIntegral : inertia / 250) * 235 / gamma, result.DerivedResistance.Value);
            Assert.IsTrue(result.ClassificationParts.All(c => c.Classification.Reference.StartsWith(ntc ? "NTC" : "EN")));
        }
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ClassTwoHighShearUsesPlasticStressBlocksAndClassThreeShearRemainsExplicitlyUnsupported(bool ntc)
    {
        var h2 = new SectionH(300, 12, 240, 12, 240, 12, "class2", 0);
        var p2 = Input(ntc, shape: h2, fy: 235, fu: 360, shear: 0, moment: 1e6);
        var base2 = Evaluate(p2); Assert.AreEqual(2, base2.Classification.SectionClass);
        double v = .75 * T(base2, "VplRd"), gamma = ntc ? 1.05 : 1;
        var high = Evaluate(Input(ntc, shape: h2, fy: 235, fu: 360, shear: v, moment: 1e6));
        Close((2 * 240 * 12 * 144 + .75 * 12 * 276 * 276 / 4) * 235 / gamma, high.DerivedResistance.Value);
        var h3 = new SectionH(300, 12, 280, 12, 280, 12, "class3", 0);
        Unsupported(Evaluate(Input(ntc, shape: h3, fy: 235, fu: 360, shear: 1e-180)), "CLASS3_WITH_SHEAR");
    }

    [DataTestMethod] [DataRow(false)] [DataRow(true)]
    public void ClassThreeWebAtZeroShearUsesElasticCapacityWithoutPretendingShearBucklingIsExcluded(bool ntc)
    {
        var h = new SectionH(940, 10, 200, 20, 200, 20, "web class3", 0);
        var r = Evaluate(Input(ntc, shape: h, fy: 235, fu: 360, moment: 1e6, shear: 0));
        Assert.AreEqual(3, r.Classification.SectionClass); Assert.AreEqual(ConnectionStatus.Passed, r.Status, string.Join(";", r.Diagnostics));
        Assert.IsFalse(r.ShearBucklingAssessment.IsBucklingExcluded); Assert.AreEqual(0, r.Input.Actions.V2);
        Assert.IsFalse(r.Checks.Any(c => c.Id == "BeamSection.WebShear"));
        Assert.AreEqual(SteelExecutionStatus.NotRequired, r.SectionVerification.Checks.Single(c => c.Decision.Target == SteelCheckTarget.Shear2).Status);
        double inertia = 2 * (200 * Math.Pow(20, 3) / 12 + 200 * 20 * 460 * 460) + 10 * Math.Pow(900, 3) / 12;
        Close(inertia / 470 * 235 / (ntc ? 1.05 : 1), r.DerivedResistance.Value);
        Assert.AreEqual(SteelMethod.SectionBending1, r.SectionVerification.Checks.Single(c => c.Decision.Target == SteelCheckTarget.NormalResistance).Decision.Method);
    }

    [DataTestMethod] [DataRow(false, 1.2, 55.46497666423052)] [DataRow(true, 1.0, 66.55797199707663)]
    public void ShearBucklingUsesHeightBetweenFlangesIncludingTheRootZones(bool ntc, double eta, double limit)
    {
        var r = Evaluate(Input(ntc)); Close(501.9, T(r, "ShearBuckling.ClearWebHeight"));
        Close(49.693069306930695, T(r, "ShearBuckling.WebSlenderness"));
        Close(limit, T(r, "ShearBuckling.WebSlendernessLimit")); Close(eta, T(r, "ShearBuckling.Eta"));
        Assert.IsTrue(T(r, "ShearBuckling.ClearWebHeight") > r.Input.Beam.ClearWeb);
    }

    [TestMethod]
    public void AClassOneWebCanStillNeedShearBucklingAndNoPlasticCapacityIsExported()
    {
        var h = new SectionH(700, 10, 200, 20, 200, 20, "slender shear web", 20);
        var r = Evaluate(Input(shape: h, fy: 235, fu: 360, moment: 1e6, shear: 1000));
        Assert.AreEqual(1, r.Classification.SectionClass); Assert.IsFalse(r.ShearBucklingAssessment.IsBucklingExcluded);
        Unsupported(r, "SHEAR_BUCKLING_EXCLUSION_REQUIRED");
        Assert.IsTrue(r.SectionVerification.Checks.Any(c => c.Status == SteelExecutionStatus.NotSupported));
    }

    [DataTestMethod]
    [DataRow(1e-180, 0.0, 0.0, 0.0, 0.0)] [DataRow(0.0, -1e-180, 0.0, 0.0, 0.0)]
    [DataRow(0.0, 0.0, 1e-180, 0.0, 0.0)] [DataRow(0.0, 0.0, 0.0, -1e-180, 0.0)]
    [DataRow(0.0, 0.0, 0.0, 0.0, -1e-180)]
    public void SmallOutOfScopeComponentsAreRetainedAndNeverCancelled(double n, double v1, double t, double m2, double m1)
    {
        var actions = new SteelActions(n, v1, 1000, t, m1, m2);
        var r = Evaluate(Input(actions: actions)); Unsupported(r, "PURE_MAJOR_BENDING_AND_WEB_SHEAR");
        Assert.AreSame(actions, r.Input.Actions); Assert.AreEqual(n, T(r, "Actions.N"));
        Assert.AreEqual(v1, T(r, "Actions.V1")); Assert.AreEqual(t, T(r, "Actions.T"));
        Assert.AreEqual(m2, T(r, "Actions.M2")); Assert.AreEqual(m1, T(r, "Actions.M1"));
    }

    [DataTestMethod] [DataRow(0)] [DataRow(1)]
    public void UnknownOrDeclaredHolesCannotUseGrossBendingCapacity(int declaration)
        => Unsupported(Evaluate(Input(unperforated: declaration == 0 ? null : false)), "DECLARED_UNPERFORATED");
    [TestMethod]
    public void NetAreaAloneAndNotchesDoNotQualifyBendingGeometry()
    {
        Unsupported(Evaluate(Input(unperforated: false, netArea: 11000)), "DECLARED_UNPERFORATED");
        Unsupported(Evaluate(Input(notched: true)), "DECLARED_UNPERFORATED");
    }

    [DataTestMethod] [DataRow(265.0, 410.0)] [DataRow(275.0, 430.0)] [DataRow(355.0, 470.0)]
    public void DifferentFlangeStrengthIsNotReplacedWithTheWebValue(double flangeFy, double flangeFu)
    {
        var r = Evaluate(Input(flangeFy: flangeFy, flangeFu: flangeFu)); Unsupported(r, "HOMOGENEOUS_FLANGE_WEB_MODEL");
        Assert.AreEqual(flangeFy, T(r, "Material.FlangeFy")); Assert.AreEqual(flangeFu, T(r, "Material.FlangeFu"));
    }
    [TestMethod]
    public void ModelAndConnectionMaterialMustRepresentTheSameActualProduct()
    {
        Unsupported(Evaluate(Input(beam: Beam(SciShape(), 265, 410))), "HOMOGENEOUS_FLANGE_WEB_MODEL");
        Unsupported(Evaluate(Input(fy: 500, fu: 550)), "HOMOGENEOUS_FLANGE_WEB_MODEL");
    }

    [DataTestMethod] [DataRow("height")] [DataRow("web")] [DataRow("flange")] [DataRow("width")] [DataRow("radius")]
    public void SimilarNamedSectionsCannotBeSubstitutedForActualConnectionGeometry(string dimension)
    {
        var h = new SectionH(dimension == "height" ? 534 : 533.1, dimension == "web" ? 11 : 10.1,
            dimension == "width" ? 210 : 209.3, dimension == "flange" ? 16 : 15.6,
            dimension == "width" ? 210 : 209.3, dimension == "flange" ? 16 : 15.6, "different", dimension == "radius" ? 13 : 12.7);
        Unsupported(Evaluate(Input(shape: h, beam: Beam(SciShape()))), "MODEL_AND_CONNECTION_GEOMETRY");
    }
    [TestMethod]
    public void AsymmetryAndPrincipalAxisSwapAreNotAutomaticallyRemapped()
    {
        var asym = new SectionH(533.1, 10.1, 209.3, 15.6, 200, 15.6, "asymmetric", 12.7);
        Unsupported(Evaluate(Input(shape: asym)), "MODEL_AND_CONNECTION_GEOMETRY");
        var wide = new SectionH(100, 10, 400, 20, 400, 20, "horizontal maximum inertia", 0);
        Unsupported(Evaluate(Input(shape: wide)), "PRINCIPAL_AXIS_ONE_MUST_BE_HORIZONTAL");
    }
    [TestMethod]
    public void OtherRegisteredModelFamiliesAreNotSlicedIntoAnHSection()
        => Unsupported(Evaluate(Input(shape: new SectionRHS(300, 200, 12, 12, 12, 12, "RHS"))), "EXACT_ROLLED_HOT_FINISHED_H_REQUIRED");
    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void WeldedAndColdFormedOpenSectionsAreNotRolledAdapterInputs(bool welded)
        => Unsupported(Evaluate(Input(production: welded ? Section.SectionTypes.Welded : Section.SectionTypes.Rolled,
            formed: welded ? Section.FormedTypes.HotFinished : Section.FormedTypes.ColdFormed)), "EXACT_ROLLED_HOT_FINISHED_H");

    [DataTestMethod] [DataRow("geometry")] [DataRow("material")] [DataRow("actions")] [DataRow("eta")]
    public void MissingRequiredProvenancePreventsExport(string missing)
    {
        var r = Evaluate(Input(geometrySource: missing == "geometry" ? " " : "geometry",
            materialSource: missing == "material" ? " " : "material", caseSource: missing == "actions" ? " " : "actions",
            etaSource: missing == "eta" ? " " : "eta"));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.IsNull(r.DerivedResistance);
        Assert.IsTrue(r.Diagnostics.Any(d => d.EndsWith("SOURCE_REQUIRED")));
    }
    [TestMethod]
    public void EcEtaIsExplicitAndCannotBeTransferredIntoNtcPolicy()
    {
        Unsupported(Evaluate(Input(ecEta: null)), "EC3_SHEAR_ETA_REQUIRED");
        var p = Input(true);
        var mixed = new EndPlateBeamSectionInput(p.Code, p.Beam, p.Section, p.Actions, 275, 410, false,
            p.MaterialAtActualThicknessSource, p.GeometrySource, p.ActionCombinationSource, 1.2, "EC eta");
        Unsupported(Evaluate(mixed), "NTC_USES_OWN_SHEAR_ETA_ONE");
    }

    [DataTestMethod] [DataRow(0.0)] [DataRow(1000000.0)]
    public void NumericalFailureInActualCoreOrCapacityCannotPublishAFavourableToken(double moment)
    {
        var code = ConnectionDesignCode.Eurocode3(1e-308, 1.25, 1.25, "Numerical overflow test");
        var r = Evaluate(Input(moment: moment, shear: 0, code: code));
        Assert.AreEqual(ConnectionStatus.NotSupported, r.Status); Assert.IsNull(r.DerivedResistance);
        Assert.IsTrue(r.Diagnostics.Any(d => d.Contains("PIPELINE") || d.Contains("BASELINE_BENDING") || d.Contains("PLASTIC_SHEAR")));
        if (moment != 0) Assert.IsTrue(r.SectionVerification.Checks.Any(c => c.Status == SteelExecutionStatus.CalculationError));
    }

    [DataTestMethod] [DataRow(false, 200000.0)] [DataRow(false, -200000.0)] [DataRow(true, 200000.0)] [DataRow(true, -200000.0)]
    public void WrapperBindsTokenActualInputsAndActionsWithoutTreatingTextAsCertification(bool ntc, double v)
    {
        var r = Evaluate(Input(ntc, shear: v)); var p = Joint(r);
        var result = EndPlateMomentWithBeamSection.Verify(p, r);
        Assert.AreEqual(ConnectionStatus.Passed, result.Status, string.Join(";", result.Diagnostics));
        Assert.AreSame(r, result.BeamSection); Assert.AreSame(p, result.Input);
        Assert.AreSame(p, result.MomentComponents.ComponentAssessment.Input);
        Assert.IsTrue(result.IsComplete); Assert.IsFalse(result.CanQualifyWholeJoint); Assert.IsFalse(result.CanQualifyWholeMember);
        Assert.AreEqual(v, result.BeamSection.Input.Actions.V2); Assert.AreEqual(Math.Abs(v), result.Input.Shear);
        Assert.AreNotEqual(p.ActionCombinationSource, r.Input.ActionCombinationSource);
    }
    [TestMethod]
    public void IdenticalNumbersAndSourceCannotForgeTheDerivedResistanceToken()
    {
        var r = Evaluate(); var t = r.DerivedResistance;
        var manual = new EndPlateBeamMomentResistance(t.Code, t.Beam, t.Value, t.ConcomitantShear, t.Source);
        var result = EndPlateMomentWithBeamSection.Verify(Joint(r, token: manual), r);
        Assert.AreEqual(ConnectionStatus.NotSupported, result.Status); Assert.IsNull(result.MomentComponents);
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "DERIVED_RESISTANCE_TOKEN_MUST_BE_IDENTICAL");
    }
    [DataTestMethod] [DataRow("moment")] [DataRow("shear")] [DataRow("axial")]
    public void SameTokenCannotAuthorizeDifferentConcomitantActions(string changed)
    {
        var r = Evaluate(); var p = Joint(r, moment: changed == "moment" ? 300e6 + 1 : null,
            shear: changed == "shear" ? 200001 : null, axial: changed == "axial" ? 1e-180 : null);
        var result = EndPlateMomentWithBeamSection.Verify(p, r);
        Assert.AreEqual(ConnectionStatus.NotSupported, result.Status); Assert.IsNull(result.MomentComponents);
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "CONCOMITANT_ACTIONS_MUST_MATCH_EXACTLY");
    }
    [DataTestMethod] [DataRow(true)] [DataRow(false)]
    public void SameValuesDoNotReplaceCodeOrBeamIdentity(bool code)
    {
        var r = Evaluate(); var p = Joint(r,
            code: code ? ConnectionDesignCode.Eurocode3(1, 1.25, 1.25, "Explicit test factors") : null,
            beam: code ? null : Beam(SciShape()));
        var result = EndPlateMomentWithBeamSection.Verify(p, r);
        Assert.AreEqual(ConnectionStatus.NotSupported, result.Status);
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "CODE_AND_BEAM_INSTANCES_MUST_MATCH");
    }
    [TestMethod]
    public void FailedBeamWithoutExportCannotBeReplacedByAManualJointCapacity()
    {
        var r = Evaluate(Input(shear: 1e6)); var result = EndPlateMomentWithBeamSection.Verify(Joint(r), r);
        Assert.AreEqual(ConnectionStatus.Failed, result.Status); Assert.AreEqual(ConnectionStatus.Failed, result.BeamSection.Status);
        Assert.IsNull(result.MomentComponents); Assert.IsFalse(result.IsComplete);
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "BEAM_SECTION_HAS_NO_QUALIFIED_DERIVED_RESISTANCE");
    }
    [TestMethod]
    public void AJointComponentLimitationRemainsVisibleInTheWrapper()
    {
        var r = Evaluate(); var result = EndPlateMomentWithBeamSection.Verify(Joint(r, source: " "), r);
        Assert.AreEqual(ConnectionStatus.NotSupported, result.Status); Assert.IsNotNull(result.MomentComponents);
        Assert.AreEqual(ConnectionStatus.Passed, result.BeamSection.Status); Assert.IsFalse(result.IsComplete);
        Assert.IsTrue(result.Diagnostics.Any(d => d.StartsWith("Joint:")));
    }

    [TestMethod]
    public void SnapshotAndReadOnlyResultsCannotBeMutatedThroughModelCopies()
    {
        var shape = SciShape(); var p = Input(shape: shape); var first = Evaluate(p);
        shape.Height = 700; ((SectionH)p.Section.CreateSectionCopy().SectionShape).Height = 800;
        var second = Evaluate(p); Close(first.DerivedResistance.Value, second.DerivedResistance.Value);
        Assert.AreSame(p, second.Input); Assert.AreEqual(533.1, ((SectionH)p.Section.CreateSectionCopy().SectionShape).Height);
        Assert.ThrowsException<NotSupportedException>(() => ((IList<ConnectionCheck>)second.Checks).Clear());
        Assert.ThrowsException<NotSupportedException>(() => ((IList<string>)second.Diagnostics).Add("fake"));
    }
    [TestMethod]
    public void NullAndNonFiniteInputsAreRejectedAtTheContractBoundary()
    {
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateBeamSectionAdapter.Evaluate(null!));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(flangeFy: double.NaN));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Input(ecEta: double.PositiveInfinity));
        var p = Input();
        Assert.ThrowsException<ArgumentNullException>(() => new EndPlateBeamSectionInput(null!, p.Beam, p.Section, p.Actions,
            275, 410, false, "material", "geometry", "case"));
        Assert.ThrowsException<ArgumentNullException>(() => new EndPlateBeamSectionInput(p.Code, null!, p.Section, p.Actions,
            275, 410, false, "material", "geometry", "case"));
        var result = Evaluate();
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateMomentWithBeamSection.Verify(null!, result));
        Assert.ThrowsException<ArgumentNullException>(() => EndPlateMomentWithBeamSection.Verify(Joint(result), null!));
    }
}
