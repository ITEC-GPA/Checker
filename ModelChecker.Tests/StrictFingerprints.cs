using GPC.Model.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ModelChecker.Tests;

/// <summary>Architecture review R8: in the tests a value without an explicit fingerprint contract fails instead of using the reflection.</summary>
[TestClass]
public class StrictFingerprints
{
    [AssemblyInitialize]
    public static void Strict(TestContext context) => AppContext.SetSwitch(FingerprintVocabulary.StrictSwitch, true);
}
