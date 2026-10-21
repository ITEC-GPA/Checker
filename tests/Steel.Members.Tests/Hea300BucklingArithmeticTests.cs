using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Probe = Steel.Members.Tests.PublishedBucklingBenchmarks.BucklingProbe;

namespace Steel.Members.Tests;

[TestClass]
public class Hea300BucklingArithmeticTests
{
    // Independent rounded-section integration, Euler load and characteristic equation.
    // These are NOT Cordova benchmark values. SectionHBuckling1 now checks the S355 chain.
    // Reproducible Decimal reference: tools/Steel.LegacyDiagnostics/hea300_flexural_reference.py.
    [DataTestMethod]
    [DataRow(355d,3,1,1638514.4582632633)]
    [DataRow(355d,3,2,1859223.2199390416)]
    [DataRow(275d,2,1,1521420.8348446812)]
    [DataRow(275d,2,2,1671053.3584063118)]
    public void ReconstructedLegacyGeometryAndMaterialSensitivity(double fy,int cl,int axis,double nb)
    {
        var s=new SteelSection(new SectionH(290,8.5,300,14,300,14,"HEA300",27),
            new SteelMaterial("Diagnostic fy",210000,fy,550),
            Section.SectionTypes.Rolled,Section.FormedTypes.HotFinished);
        double h=290,b=300,tf=14,tw=8.5,r=27,hw=h-2*tf;
        double af=(1-Math.PI/4)*r*r,q=(5d/6-Math.PI/4)*Math.Pow(r,3),
            jf=(1-5*Math.PI/16)*Math.Pow(r,4);
        double area=2*b*tf+hw*tw+4*af;
        double iy=2*(b*Math.Pow(tf,3)/12+b*tf*Math.Pow((h-tf)/2,2))
            +tw*Math.Pow(hw,3)/12+4*(Math.Pow(h/2-tf,2)*af-2*(h/2-tf)*q+jf);
        double iz=2*tf*Math.Pow(b,3)/12+hw*Math.Pow(tw,3)/12
            +4*(Math.Pow(tw/2,2)*af+tw*q+jf);
        Assert.AreEqual(area,s.Area,area*1e-12);
        Assert.AreEqual(iy,s.J11,iy*1e-12);
        Assert.AreEqual(iz,s.J22,iz*1e-12);
        var actual=new Probe(s,13000,6500).Flexural(s,cl,axis);
        Assert.AreEqual(axis==1?"b":"c",actual.Curve);
        Assert.AreEqual(nb,actual.Nb,nb*1e-12);
    }
}
