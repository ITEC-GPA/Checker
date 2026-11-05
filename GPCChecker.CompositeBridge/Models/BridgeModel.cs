using System.Runtime.Serialization;
using System.Text;
using GPC.Checkers.CompositeBridge.Layout;
using GPC.Model.Core;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Elements;
using GPC.Model.Persistence;

namespace GPC.Checkers.CompositeBridge;

/// <summary>
/// A bridge model by composition (lane L12, user decision of 5/11 «Toglilo prima», «composizione»): a generic GPC <see cref="Model"/>
/// (N-mm, the one the Straus7 writer writes and the analysis fingerprints) and the bridge data that GPCModel does not know
/// (<see cref="BridgeModelData"/>: per beam GUID the physical bridge member, the super element, the local section state and the composite
/// sections; the verification recipe D3). Not a class derived from the model: GPCModel stays a general structural model.
/// <para>The pair is not validated when it is built or read, as a model is not (an edit may leave a dangling key, and the package must
/// still open): <see cref="Validate"/> and <see cref="RequireValid"/> check the data against the model.</para>
/// </summary>
public sealed class BridgeModel
{
    /// <summary>The first bytes of a package: "GPCBRG" and the version of the package.</summary>
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("GPCBRG");
    private const byte PackageVersion = 1;

    /// <summary>A bridge model without bridge data yet.</summary>
    public BridgeModel(GPC.Model.Models.Model model) : this(model, new BridgeModelData()) { }

    public BridgeModel(GPC.Model.Models.Model model, BridgeModelData data)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>The generic GPC model.</summary>
    public GPC.Model.Models.Model Model { get; }
    /// <summary>The bridge data of the model.</summary>
    public BridgeModelData Data { get; }

    /// <summary>The diagnostics of the bridge data against the model (<see cref="BridgeModelData.Validate"/>).</summary>
    public IReadOnlyList<ModelDiagnostic> Validate() => Data.Validate(Model);

    /// <summary>Throws <see cref="InvalidOperationException"/> listing the errors of <see cref="Validate"/> (as ModelPreflight.RequireExportable).</summary>
    public void RequireValid()
    {
        var errors = Validate().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException("InvalidBridgeModel: " + string.Join("; ", errors.Select(d => d.Code + " (" + d.Message + ")")));
    }

    /// <summary>The beams of the model in their numbering.</summary>
    private IEnumerable<BeamElement> Beams() => Model.BeamElements.Values.OrderBy(b => b.Id);

    /// <summary>The names of the super elements, in the order of their first beam in the numbering of the model.</summary>
    public IReadOnlyList<string> SuperElementNames()
        => Beams().Select(b => Data.SuperElement(b.Guid)).Where(n => n != null).Select(n => n!).Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>The beams of a super element, in the numbering of the model (the order of the former elements).</summary>
    public IReadOnlyList<BeamElement> SuperElement(string name)
        => Beams().Where(b => string.Equals(Data.SuperElement(b.Guid), name, StringComparison.Ordinal)).ToArray();

    /// <summary>The ids of the physical bridge members, in the order of their first beam in the numbering of the model.</summary>
    public IReadOnlyList<string> PhysicalMemberIds()
        => Beams().Select(b => Data.Member(b.Guid)?.PhysicalId).Where(n => n != null).Select(n => n!).Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>The FE parts of a physical bridge member with their metadata, in the numbering of the model.</summary>
    public IReadOnlyList<(BeamElement Beam, BridgeMemberMetadata Metadata)> PhysicalMember(string physicalId)
        => Beams().Select(b => (Beam: b, Metadata: Data.Member(b.Guid)!)).Where(p => p.Metadata != null && string.Equals(p.Metadata.PhysicalId, physicalId, StringComparison.Ordinal)).ToArray();

    /// <summary>A copy: <see cref="ModelValues.Copy(GPC.Model.Models.Model, ModelCopyOptions)"/> of the model and
    /// <see cref="BridgeModelData.Copy(ModelCopyOptions)"/> of the data. The elements keep their GUIDs, so the keys stay valid.</summary>
    public BridgeModel Copy(ModelCopyOptions? options = null)
    {
        options ??= ModelCopyOptions.None;
        return new BridgeModel(ModelValues.Copy(Model, options), Data.Copy(options));
    }

    /// <summary>A copy under another model identity (<see cref="ModelValues.Copy(GPC.Model.Models.Model, ModelCopyOptions, Guid)"/>): the
    /// elements keep their GUIDs, so the bridge data are those of <see cref="Copy(ModelCopyOptions)"/>. The recipe is copied unchanged and
    /// still names the source model, as the analysis state of the copy: it is stale for the copy until it is built again.</summary>
    public BridgeModel Copy(ModelCopyOptions options, Guid modelIdentity)
        => new BridgeModel(ModelValues.Copy(Model, options ?? ModelCopyOptions.None, modelIdentity), Data.Copy(options ?? ModelCopyOptions.None));

    /// <summary>The package with the model in the compact form (<see cref="Save(Stream, ModelArchiveFormat)"/>).</summary>
    public void Save(Stream destination) => Save(destination, ModelArchiveFormat.Compact);

    /// <summary>
    /// One stream with the model and its bridge data: "GPCBRG", the version of the package (1), the length (Int32, little endian) and the
    /// bytes of <see cref="BridgeModelData.Save"/>, then the archive of the model exactly as <see cref="ModelArchive.Save(GPC.Model.Models.Model, Stream, ModelArchiveFormat)"/>
    /// writes it, to the end of the package (<see cref="ModelArchive.Load"/> recognizes its form). No other format: when ModelArchive writes
    /// one form only, so does the package; the data may change codec behind a new version of the package.
    /// The destination is written only when the whole package is ready.
    /// </summary>
    public void Save(Stream destination, ModelArchiveFormat format)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        var data = Data.Save();
        using var model = new MemoryStream();
        ModelArchive.Save(Model, model, format);
        using var package = new MemoryStream();
        package.Write(Magic, 0, Magic.Length); package.WriteByte(PackageVersion);
        package.Write(BitConverter.IsLittleEndian ? BitConverter.GetBytes(data.Length) : BitConverter.GetBytes(data.Length).Reverse().ToArray(), 0, 4);
        package.Write(data, 0, data.Length);
        model.Position = 0; model.CopyTo(package);
        package.Position = 0; package.CopyTo(destination);
    }

    /// <summary>Reads <see cref="Save(Stream, ModelArchiveFormat)"/>: the form of the package, of the data and of the model are checked,
    /// not the data against the model (<see cref="Validate"/>).</summary>
    public static BridgeModel Load(Stream source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var head = ReadExactly(source, Magic.Length + 1 + 4, "TruncatedBridgeModelPackage");
        if (!head.Take(Magic.Length).SequenceEqual(Magic)) throw new SerializationException("NotABridgeModelPackage");
        if (head[Magic.Length] != PackageVersion) throw new SerializationException("UnsupportedBridgeModelPackageVersion: " + head[Magic.Length]);
        var length = new byte[4]; Array.Copy(head, Magic.Length + 1, length, 0, 4);
        if (!BitConverter.IsLittleEndian) Array.Reverse(length);
        int count = BitConverter.ToInt32(length, 0);
        if (count <= 0 || count > LayoutContracts.MaximumBytes) throw new SerializationException("InvalidBridgeModelDataLength");
        var data = BridgeModelData.Read(ReadExactly(source, count, "TruncatedBridgeModelPackage"));
        var model = ModelArchive.Load(source);
        return new BridgeModel(model, data);
    }

    private static byte[] ReadExactly(Stream source, int count, string error)
    {
        var buffer = new byte[count]; int read = 0;
        while (read < count) { int n = source.Read(buffer, read, count - read); if (n == 0) throw new SerializationException(error); read += n; }
        return buffer;
    }
}
