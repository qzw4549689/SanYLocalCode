$csproj = "C:\Projects\D365\D365\SanyD365.D365Extension.Sales\SanyD365.D365Extension.Sales.csproj"
$entry = "Plugins\ScoringCard\CreditScoringCardValidationPlugin.cs"
$xml = [xml](Get-Content $csproj -Encoding UTF8)
$ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
$ns.AddNamespace("ns", "http://schemas.microsoft.com/developer/msbuild/2003")
$found = $xml.Project.SelectSingleNode("//ns:Compile[@Include='$entry']", $ns)
if (-not $found) {
    $compile = $xml.CreateElement("Compile", "http://schemas.microsoft.com/developer/msbuild/2003")
    $compile.SetAttribute("Include", $entry)
    $itemGroup = $xml.Project.SelectSingleNode("//ns:ItemGroup[ns:Compile]", $ns)
    if (-not $itemGroup) {
        $itemGroup = $xml.CreateElement("ItemGroup", "http://schemas.microsoft.com/developer/msbuild/2003")
        $xml.Project.AppendChild($itemGroup)
    }
    $itemGroup.AppendChild($compile)
    Write-Host "Added: $entry"
} else {
    Write-Host "Exists: $entry"
}
$xml.Save($csproj)
Write-Host "csproj updated."
