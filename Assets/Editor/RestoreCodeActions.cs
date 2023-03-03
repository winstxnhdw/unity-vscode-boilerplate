using System;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;

public class RestoreCodeActions : AssetPostprocessor {
    static string OnGeneratedCSProject(string _, string content) {
        XDocument document = XDocument.Parse(content);

        document.Root.Descendants()
                     .AsParallel()
                     .Where(x => x.Name.LocalName == "Analyzer")
                     .Where(x => x.Attribute("Include").Value.Contains("Unity.SourceGenerators"))
                     .Remove();

        return $"{document.Declaration}{Environment.NewLine}{document.Root}";
    }
}
