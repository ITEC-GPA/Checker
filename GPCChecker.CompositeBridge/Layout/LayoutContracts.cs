using System.Runtime.Serialization;
using System.Xml;
using GPC.Model.Core;

namespace GPC.Checkers.CompositeBridge.Layout;

/// <summary>
/// The XML of the data contracts of the bridge layout and of the bridge data (lane L12), as <see cref="BridgeWorkflowDefinition"/> and
/// <see cref="BridgeCompositeSectionDefinition"/> save: the closed vocabulary of GPCModel for its values (points, curves, frames,
/// properties, sections, materials) and the declared contracts of CompositeBridge for the rest, shared references preserved, DTDs
/// prohibited, a size limit on reading. GPCModel's own value archives (ModelValueArchive, ModelValues.CopyValue) do not know these
/// types: they left its vocabulary with the bridge vocabulary.
/// </summary>
internal static class LayoutContracts
{
    /// <summary>The largest XML read (bytes and characters): the size of a block of GPC.Archive.</summary>
    internal const int MaximumBytes = 256 * 1024 * 1024;

    internal static byte[] Save<T>(T value) where T : class
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        using var stream = new MemoryStream();
        Serializer(typeof(T)).WriteObject(stream, value);
        return stream.ToArray();
    }

    internal static T Read<T>(byte[] bytes, string missing) where T : class
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length > MaximumBytes) throw new SerializationException(typeof(T).Name + "TooLarge");
        using var stream = new MemoryStream(bytes, false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumBytes });
        try { return (T)(Serializer(typeof(T)).ReadObject(reader) ?? throw new SerializationException(missing)); }
        catch (InvalidCastException ex) { throw new SerializationException(missing, ex); }
        // A collection that refuses an entry (e.g. a key twice) is a corrupt document, not an argument of the caller.
        catch (ArgumentException ex) { throw new SerializationException(ex.Message, ex); }
    }

    /// <summary>A detached copy through the XML (no size limit: the copy reads what it has just written).</summary>
    internal static T Copy<T>(T value) where T : class
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        using var stream = new MemoryStream();
        var serializer = Serializer(typeof(T));
        serializer.WriteObject(stream, value); stream.Position = 0;
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return (T)serializer.ReadObject(reader)!;
    }

    private static DataContractSerializer Serializer(Type type) => new(type, new DataContractSerializerSettings
        { KnownTypes = ModelValues.DataContracts, PreserveObjectReferences = true, MaxItemsInObjectGraph = int.MaxValue });
}
