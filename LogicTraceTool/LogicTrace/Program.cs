namespace LogicTrace;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
            return SmokeTest.Run(args.Skip(1).FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)));

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}

internal static class SmokeTest
{
    public static int Run(string? samplePath)
    {
        try
        {
            samplePath ??= Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Examples", "LegacySystem", "logictrace.sample.json"));
            var service = new LogicTraceDocumentService();
            var result = service.Load(samplePath);
            if (!result.Success || result.Document is null)
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Errors));

            var document = result.Document;
            if (document.LogicSections.Count != 5 || document.LogicSections.Sum(s => s.Rules.Count) < 5)
                throw new InvalidOperationException("範例 Section / Rule 數量不正確。");
            if (!document.Feature.Overview.References.Any(r => r.TargetType == ReferenceTargetType.Evidence))
                throw new InvalidOperationException("範例未涵蓋 Evidence Overview Link。");

            foreach (var evidence in document.LogicSections.SelectMany(s => s.Rules).SelectMany(r => r.Evidences))
            {
                var source = SourceFileReader.Read(Path.GetDirectoryName(samplePath)!, evidence);
                if (!source.Success)
                    throw new InvalidOperationException(source.Error);
            }

            using (var form = new MainForm())
            {
                form.LoadDocumentForTest(samplePath);
                form.RunUiSmokeTest();
            }

            var temp = Path.Combine(Path.GetTempPath(), $"logictrace-{Guid.NewGuid():N}.json");
            service.Save(temp, document);
            var roundTrip = service.Load(temp);
            File.Delete(temp);
            if (!roundTrip.Success)
                throw new InvalidOperationException("儲存後重新載入失敗。");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"LogicTrace Smoke Test 失敗：{ex.Message}");
            return 1;
        }
    }
}
