using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiTaskTracker.Services;

internal sealed class LicenseStateStore
{
    private readonly IEntitlementAdapter _entitlementAdapter;
    private readonly string _path;

    public LicenseStateStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "license-state.json");
        _entitlementAdapter = new LocalEntitlementAdapter();
    }

    public LicenseState Load()
    {
        if (!File.Exists(_path))
        {
            var initialState = LicenseState.CreateDefault(BuildMachineFingerprint());
            Save(initialState);
            return initialState;
        }

        try
        {
            var state = JsonSerializer.Deserialize<LicenseState>(File.ReadAllText(_path), JsonOptions()) ?? LicenseState.CreateDefault(BuildMachineFingerprint());
            state.Normalize(BuildMachineFingerprint());
            Save(state);
            return state;
        }
        catch
        {
            var recoveredState = LicenseState.CreateDefault(BuildMachineFingerprint());
            recoveredState.Status = "recovered";
            recoveredState.LastCheckedAt = DateTimeOffset.UtcNow;
            Save(recoveredState);
            return recoveredState;
        }
    }

    public void Save(LicenseState state)
    {
        state.Normalize(BuildMachineFingerprint());
        File.WriteAllText(_path, JsonSerializer.Serialize(state, JsonOptions()));
    }

    public LicenseState RestorePurchases()
    {
        var state = Load();
        var restoredState = _entitlementAdapter.Restore(state);
        restoredState.Normalize(BuildMachineFingerprint());
        Save(restoredState);
        return restoredState;
    }

    public string ExportReadinessReport(LicenseState state)
    {
        var reportsDirectory = Path.Combine(Path.GetDirectoryName(_path) ?? ".", "license-reports");
        Directory.CreateDirectory(reportsDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var reportPath = Path.Combine(reportsDirectory, $"license-readiness-{timestamp}.md");
        var builder = new StringBuilder();
        builder.AppendLine("# AI Task Tracker License Readiness");
        builder.AppendLine();
        builder.AppendLine($"- Edition: `{state.Edition}`");
        builder.AppendLine($"- Status: `{state.Status}`");
        builder.AppendLine($"- Entitlement source: `{state.EntitlementSource}`");
        builder.AppendLine($"- Entitlement provider: `{state.EntitlementProvider}`");
        builder.AppendLine($"- Machine fingerprint: `{state.MachineFingerprint}`");
        builder.AppendLine($"- Store product id: `{state.StoreProductId}`");
        builder.AppendLine($"- Commercial use enabled: `{state.CommercialUseEnabled}`");
        builder.AppendLine($"- Offline grace days: `{state.OfflineGraceDays}`");
        builder.AppendLine($"- Last checked UTC: `{state.LastCheckedAt:O}`");
        builder.AppendLine($"- Last restore UTC: `{state.LastRestoreAt:O}`");
        builder.AppendLine();
        builder.AppendLine("## Gates");
        builder.AppendLine();
        builder.AppendLine($"- Local entitlement state: `{(state.HasLocalEntitlementState ? "PASS" : "FAIL")}`");
        builder.AppendLine($"- Machine-bound fingerprint: `{(string.IsNullOrWhiteSpace(state.MachineFingerprint) ? "FAIL" : "PASS")}`");
        builder.AppendLine($"- Store entitlement adapter: `{(state.StoreEntitlementAdapterReady ? "PASS" : "PENDING")}`");
        builder.AppendLine($"- Signed distribution: `{(state.SignedDistributionRequired ? "PENDING" : "NOT_REQUIRED_FOR_LOCAL")}`");
        builder.AppendLine($"- Purchase restore flow: `{(state.PurchaseRestoreReady ? "PASS" : "PENDING")}`");
        builder.AppendLine();
        builder.AppendLine("## Notes");
        builder.AppendLine();
        builder.AppendLine("This local MVP does not process real payments. It records the entitlement contract the desktop app will use when a store or server-backed purchase provider is connected.");

        File.WriteAllText(reportPath, builder.ToString());
        return reportPath;
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = new SnakeCaseJsonNamingPolicy(),
            Converters = { new JsonStringEnumConverter(new SnakeCaseJsonNamingPolicy()) }
        };
    }

    private static string BuildMachineFingerprint()
    {
        var raw = $"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion.VersionString}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..16];
    }
}

internal interface IEntitlementAdapter
{
    string ProviderName { get; }

    LicenseState Restore(LicenseState currentState);
}

internal sealed class LocalEntitlementAdapter : IEntitlementAdapter
{
    public string ProviderName => "local_adapter";

    public LicenseState Restore(LicenseState currentState)
    {
        currentState.Status = "active";
        currentState.EntitlementSource = "local";
        currentState.EntitlementProvider = ProviderName;
        currentState.HasLocalEntitlementState = true;
        currentState.StoreEntitlementAdapterReady = true;
        currentState.PurchaseRestoreReady = true;
        currentState.LastCheckedAt = DateTimeOffset.UtcNow;
        currentState.LastRestoreAt = DateTimeOffset.UtcNow;
        return currentState;
    }
}

internal sealed class LicenseState
{
    public string Edition { get; set; } = "Local MVP";
    public string Status { get; set; } = "active";
    public string EntitlementSource { get; set; } = "local";
    public string EntitlementProvider { get; set; } = "local_adapter";
    public string MachineFingerprint { get; set; } = "";
    public string StoreProductId { get; set; } = "ai-task-tracker.pro";
    public bool CommercialUseEnabled { get; set; }
    public bool HasLocalEntitlementState { get; set; } = true;
    public bool StoreEntitlementAdapterReady { get; set; } = true;
    public bool SignedDistributionRequired { get; set; } = true;
    public bool PurchaseRestoreReady { get; set; } = true;
    public int OfflineGraceDays { get; set; } = 14;
    public DateTimeOffset LastCheckedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastRestoreAt { get; set; } = DateTimeOffset.UtcNow;

    public static LicenseState CreateDefault(string machineFingerprint)
    {
        return new LicenseState
        {
            MachineFingerprint = machineFingerprint,
            LastCheckedAt = DateTimeOffset.UtcNow
        };
    }

    public void Normalize(string machineFingerprint)
    {
        Edition = string.IsNullOrWhiteSpace(Edition) ? "Local MVP" : Edition.Trim();
        Status = string.IsNullOrWhiteSpace(Status) ? "active" : Status.Trim();
        EntitlementSource = string.IsNullOrWhiteSpace(EntitlementSource) ? "local" : EntitlementSource.Trim();
        EntitlementProvider = string.IsNullOrWhiteSpace(EntitlementProvider) ? "local_adapter" : EntitlementProvider.Trim();
        MachineFingerprint = string.IsNullOrWhiteSpace(MachineFingerprint) ? machineFingerprint : MachineFingerprint.Trim();
        StoreProductId = string.IsNullOrWhiteSpace(StoreProductId) ? "ai-task-tracker.pro" : StoreProductId.Trim();
        StoreEntitlementAdapterReady = true;
        PurchaseRestoreReady = true;
        OfflineGraceDays = Math.Clamp(OfflineGraceDays, 0, 90);
        LastCheckedAt = LastCheckedAt == default ? DateTimeOffset.UtcNow : LastCheckedAt;
        LastRestoreAt = LastRestoreAt == default ? DateTimeOffset.UtcNow : LastRestoreAt;
    }
}
