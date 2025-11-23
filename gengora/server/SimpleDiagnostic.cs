namespace BITS.Gengora.Server
{
    public class SimpleDiagnostic
    {
        public string FilePath { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int StartChar { get; set; }
        public int EndLine { get; set; }
        public int EndChar { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
