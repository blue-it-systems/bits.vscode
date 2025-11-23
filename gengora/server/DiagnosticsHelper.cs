namespace BITS.Gengora.Server;

internal static class DiagnosticsHelper
{
    public static ConcurrentDictionary<string, List<SimpleDiagnostic>> GetDiagnosticsMap(Microsoft.CodeAnalysis.Compilation compilation)
    {
        var diags = compilation.GetDiagnostics();
        var grouped = new ConcurrentDictionary<string, List<Microsoft.CodeAnalysis.Diagnostic>>();

        foreach (var d in diags)
        {
            var tree = d.Location.SourceTree;
            
            if (tree == null)
            {
                continue;
            }

            var path = tree.FilePath;
            grouped.GetOrAdd(path, _ => new List<Microsoft.CodeAnalysis.Diagnostic>()).Add(d);
        }

        var result = new ConcurrentDictionary<string, List<SimpleDiagnostic>>();
        
        foreach (var kv in grouped)
        {
            var file = kv.Key;
            var list = new List<SimpleDiagnostic>();

            foreach (var d in kv.Value)
            {
                var span = d.Location.GetLineSpan();

                var diagnostic = new SimpleDiagnostic
                {
                    FilePath = file,
                    StartLine = span.StartLinePosition.Line,
                    StartChar = span.StartLinePosition.Character,
                    EndLine = span.EndLinePosition.Line,
                    EndChar = span.EndLinePosition.Character,
                    Message = d.GetMessage(),
                    Severity = d.Severity.ToString(),
                    Code = d.Id
                };

                list.Add(diagnostic);
            }

            result.TryAdd(file, list);
        }

        return result;
    }
}

