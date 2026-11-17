namespace AlgoTrade.Core.Scripting
{
    /// <summary>
    /// "Read Data" asamasinin dosya-tabanli (scripts_bridge/readData.csx)
    /// script'ine globals saglar - kullanici bu script'i Notepad++ ile
    /// duzenleyip kendi okuma mantigini (FilterMode, kolon esleme, vb.) yazabilir.
    /// </summary>
    public class ReadDataScriptGlobals
    {
        public string FilePath { get; }
        public int LastN { get; }

        public ReadDataScriptGlobals(string filePath, int lastN)
        {
            FilePath = filePath;
            LastN = lastN;
        }
    }
}
