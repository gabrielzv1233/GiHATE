namespace GiHATE;

internal sealed class DetectionSession
{
    private readonly List<KeyboardRawEvent> _keys = [];
    private readonly List<HidRawEvent> _hids = [];
    public void Add(KeyboardRawEvent e) { if (e.IsKeyDown) _keys.Add(e); }
    public void Add(HidRawEvent e) => _hids.Add(e);
    public void Clear() { _keys.Clear(); _hids.Clear(); }

    public DetectedProfile? FindButtonCandidate()
    {
        var keyboard = _keys.Where(x => x.ScanCode != 0).GroupBy(x => (x.DevicePath, x.InstanceId, x.ScanCode)).Select(g => new { g.Key, Count = g.Count() }).Where(x => x.Count >= 2).OrderByDescending(x => x.Count).FirstOrDefault();
        var hid = _hids.Where(x => x.UsagePage >= 0xFF00 && x.Bytes.Length > 0).GroupBy(x => (x.DevicePath, x.InstanceId, x.VendorId, x.ProductId, x.UsagePage, x.Usage, x.Hex)).Select(g => new { g.Key, Count = g.Count() }).Where(x => x.Count >= 2).OrderByDescending(x => x.Count).FirstOrDefault();
        if (keyboard is null || hid is null || string.Equals(keyboard.Key.InstanceId, hid.Key.InstanceId, StringComparison.OrdinalIgnoreCase)) return null;
        return new DetectedProfile { KeyboardDevicePath = keyboard.Key.DevicePath, KeyboardInstanceId = keyboard.Key.InstanceId, VendorDevicePath = hid.Key.DevicePath, VendorInstanceId = hid.Key.InstanceId, SourceScanCode = keyboard.Key.ScanCode, VendorId = hid.Key.VendorId, ProductId = hid.Key.ProductId, VendorUsagePage = hid.Key.UsagePage, VendorUsage = hid.Key.Usage, VendorReportHex = hid.Key.Hex };
    }

    public bool NormalKeySafetyPassed(DetectedProfile candidate)
    {
        var normal = _keys.Any(x => string.Equals(x.InstanceId, candidate.KeyboardInstanceId, StringComparison.OrdinalIgnoreCase) && x.ScanCode != candidate.SourceScanCode);
        var vendorTraffic = _hids.Any(x => string.Equals(x.InstanceId, candidate.VendorInstanceId, StringComparison.OrdinalIgnoreCase));
        return normal && !vendorTraffic;
    }
}
