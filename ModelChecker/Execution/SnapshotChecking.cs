using System;
using System.Threading;
using GPC.Model.Checking;
using GPC.Model.Checking.Scenarios;

namespace GPC.Model.Checker
{
    public sealed partial class ModelChecker
    {
        public ModelCheckReport Verify(VerificationSnapshot snapshot, ModelCheckRequest request,
            CancellationToken token = default(CancellationToken), IProgress<VerificationProgress> progress = null)
            => Verify((snapshot ?? throw new ArgumentNullException(nameof(snapshot))).OpenModel(), request, token, progress);
        public ModelCheckReport Verify(VerificationSnapshot snapshot, MultiMaterialCheckRequest request,
            CancellationToken token = default(CancellationToken), IProgress<VerificationProgress> progress = null)
            => Verify((snapshot ?? throw new ArgumentNullException(nameof(snapshot))).OpenModel(), request, token, progress);
    }
}
