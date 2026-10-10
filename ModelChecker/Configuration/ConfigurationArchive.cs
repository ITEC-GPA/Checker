using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using GPC.Model.Core;

namespace GPC.Model.Checker.Configuration
{
    public static class ConfigurationArchive
    {
        public const string Namespace = "urn:gpc:modelchecker:configuration:1";
        private static DataContractSerializer Serializer() => new DataContractSerializer(typeof(VerificationConfiguration),
            new DataContractSerializerSettings { KnownTypes = ModelValues.DataContracts, PreserveObjectReferences = true, MaxItemsInObjectGraph = 500000 });
        public static void Save(VerificationConfiguration configuration, Stream destination)
        {
            Validate(configuration);
            using (var buffer = new MemoryStream())
            {
                Serializer().WriteObject(buffer, configuration);
                buffer.Position = 0; buffer.CopyTo(destination ?? throw new ArgumentNullException(nameof(destination)));
            }
        }
        public static VerificationConfiguration Load(Stream source)
        {
            using (var reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null, MaxCharactersInDocument = 64 * 1024 * 1024, CloseInput = false }))
            {
                var result = (VerificationConfiguration)Serializer().ReadObject(reader);
                Validate(result); return result;
            }
        }
        public static VerificationConfiguration Copy(VerificationConfiguration configuration)
        {
            using (var stream = new MemoryStream()) { Save(configuration, stream); stream.Position = 0; return Load(stream); }
        }
        internal static T CopyData<T>(T value)
        {
            var serializer = new DataContractSerializer(typeof(T), new DataContractSerializerSettings {
                KnownTypes = ModelValues.DataContracts, PreserveObjectReferences = true, MaxItemsInObjectGraph = 500000 });
            using (var stream = new MemoryStream()) { serializer.WriteObject(stream, value); stream.Position = 0; return (T)serializer.ReadObject(stream); }
        }
        public static string Fingerprint(VerificationConfiguration configuration)
        { Validate(configuration); return ModelValues.Fingerprint(new object[] { "VerificationConfiguration-v1", configuration }); }
        internal static void Validate(VerificationConfiguration value)
        {
            if (value == null || value.Schema != 1) throw new SerializationException("UnsupportedVerificationConfigurationSchema");
            if (value.PlateJobs == null) value.PlateJobs = new ConfiguredPlateJob[0];
            if (value.Contexts == null || value.Engines == null || value.Jobs == null || value.Jobs.Length + value.PlateJobs.Length == 0
                || value.Contexts.Any(c => c == null || string.IsNullOrWhiteSpace(c.Id) || c.Revision < 1 || string.IsNullOrWhiteSpace(c.Edition)
                    || c.Units != CheckUnitConvention.N_Mm_Rad)
                || value.Engines.Any(e => e == null || string.IsNullOrWhiteSpace(e.Id) || string.IsNullOrWhiteSpace(e.Kind) || e.Schema < 1
                    || string.IsNullOrWhiteSpace(e.ContextId) || e.ContextRevision < 1 || e.Parameters == null)
                || value.Jobs.Any(j => j == null || string.IsNullOrWhiteSpace(j.Name) || j.Plan == null || j.Routes == null
                    || j.Routes.Any(r => r == null || string.IsNullOrWhiteSpace(r.EngineId)) || j.Cuts?.Any(c => c == null) == true)
                || value.PlateJobs.Any(j => j == null || string.IsNullOrWhiteSpace(j.Name) || j.Preparation == null || j.Routes == null
                    || j.Routes.Any(r => r == null || string.IsNullOrWhiteSpace(r.EngineId)) || j.Checks == null || j.Checks.Length == 0 || j.Checks.Any(c => c == null))
                || value.Contexts.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != value.Contexts.Length
                || value.Engines.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != value.Engines.Length
                || value.Jobs.Select(j => j.Name).Concat(value.PlateJobs.Select(j => j.Name)).Distinct(StringComparer.Ordinal).Count() != value.Jobs.Length + value.PlateJobs.Length)
                throw new SerializationException("InvalidVerificationConfiguration");
            foreach (var engine in value.Engines)
                if (!value.Contexts.Any(c => c.Id == engine.ContextId && c.Revision == engine.ContextRevision))
                    throw new SerializationException("MissingOrChangedDesignContext: " + engine.ContextId);
            foreach (var job in value.Jobs)
                if (job.Routes.Any(r => r.PlateCheckIds != null || !value.Engines.Any(e => e.Id == r.EngineId))) throw new SerializationException("InvalidConfiguredBeamRoute");
            foreach (var job in value.PlateJobs)
            {
                if (job.Routes.Any(r => r.Checks != null || !value.Engines.Any(e => e.Id == r.EngineId))) throw new SerializationException("InvalidConfiguredPlateRoute");
                foreach (var check in job.Checks) check.Validate();
            }
        }
    }
}
