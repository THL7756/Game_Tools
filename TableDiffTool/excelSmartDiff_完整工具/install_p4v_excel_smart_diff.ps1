[CmdletBinding()]
param(
    [string[]]$Extensions = @("xlsx", "xls", "xlsm", "csv", "tsv"),
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function Convert-ToP4VPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    return [System.IO.Path]::GetFullPath($Path).Replace("\", "/")
}

function Get-ChildElementByVarName {
    param(
        [Parameter(Mandatory = $true)][System.Xml.XmlNode]$Parent,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$VarName
    )

    foreach ($child in $Parent.ChildNodes) {
        if ($child.NodeType -ne [System.Xml.XmlNodeType]::Element) {
            continue
        }

        $element = [System.Xml.XmlElement]$child
        if ($element.LocalName -eq $Name -and $element.GetAttribute("varName") -eq $VarName) {
            return $element
        }
    }

    return $null
}

function Ensure-ChildElementByVarName {
    param(
        [Parameter(Mandatory = $true)][System.Xml.XmlDocument]$Document,
        [Parameter(Mandatory = $true)][System.Xml.XmlNode]$Parent,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$VarName
    )

    $element = Get-ChildElementByVarName -Parent $Parent -Name $Name -VarName $VarName
    if ($null -eq $element) {
        $element = $Document.CreateElement($Name)
        $element.SetAttribute("varName", $VarName)
        [void]$Parent.AppendChild($element)
    }

    return $element
}

function Set-TextElement {
    param(
        [Parameter(Mandatory = $true)][System.Xml.XmlDocument]$Document,
        [Parameter(Mandatory = $true)][System.Xml.XmlNode]$Parent,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $node = $null
    foreach ($child in $Parent.ChildNodes) {
        if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element -and $child.LocalName -eq $Name) {
            $node = [System.Xml.XmlElement]$child
            break
        }
    }

    if ($null -eq $node) {
        $node = $Document.CreateElement($Name)
        [void]$Parent.AppendChild($node)
    }

    $node.InnerText = $Value
}

function New-P4VSettingsDocument {
    $document = New-Object System.Xml.XmlDocument
    $declaration = $document.CreateXmlDeclaration("1.0", "utf-8", $null)
    [void]$document.AppendChild($declaration)
    [void]$document.AppendChild($document.CreateComment("perforce-xml-version=1"))

    $root = $document.CreateElement("PropertyList")
    $root.SetAttribute("IsManaged", "TRUE")
    $root.SetAttribute("varName", "ApplicationSettings")
    [void]$document.AppendChild($root)

    return $document
}

function Save-XmlDocument {
    param(
        [Parameter(Mandatory = $true)][System.Xml.XmlDocument]$Document,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $settings.Indent = $true

    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try {
        $Document.Save($writer)
    }
    finally {
        $writer.Close()
    }
}

$toolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$diffTool = Join-Path $toolRoot "p4v-diff\ExcelMergeP4VDiff.exe"
if (!(Test-Path -LiteralPath $diffTool)) {
    throw "Diff tool was not found: $diffTool"
}

$runningP4V = @(Get-Process p4v -ErrorAction SilentlyContinue)
if ($runningP4V.Count -gt 0 -and !$Force) {
    Write-Host "P4V is currently running."
    Write-Host "Close all P4V windows and run this script again, otherwise P4V may overwrite ApplicationSettings.xml on exit."
    Write-Host "Use -Force only for isolated testing."
    exit 20
}

$settingsDir = Join-Path $env:USERPROFILE ".p4qt"
$settingsPath = Join-Path $settingsDir "ApplicationSettings.xml"
New-Item -ItemType Directory -Force -Path $settingsDir | Out-Null

if (Test-Path -LiteralPath $settingsPath) {
    $backupPath = $settingsPath + ".bak_excelSmartDiff_" + (Get-Date -Format "yyyyMMdd_HHmmss")
    Copy-Item -LiteralPath $settingsPath -Destination $backupPath -Force
    Write-Host "Backup: $backupPath"

    $document = New-Object System.Xml.XmlDocument
    $document.PreserveWhitespace = $false
    $document.Load($settingsPath)
}
else {
    $document = New-P4VSettingsDocument
}

if ($null -eq $document.DocumentElement) {
    throw "Invalid P4V settings XML: missing document element."
}

$root = $document.DocumentElement
$diffAssociations = Ensure-ChildElementByVarName -Document $document -Parent $root -Name "Associations" -VarName "DiffAssociations"
[void](Set-TextElement -Document $document -Parent $diffAssociations -Name "RunExternal" -Value "true")

$associationList = Ensure-ChildElementByVarName -Document $document -Parent $diffAssociations -Name "PropertyList" -VarName "Associations"
$associationList.SetAttribute("IsManaged", "TRUE")

$application = Convert-ToP4VPath -Path $diffTool
$arguments = "%1 %2 --open"
$installedExtensions = New-Object System.Collections.Generic.List[string]

foreach ($extension in $Extensions) {
    $normalized = ($extension.Trim().TrimStart(".")).ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        continue
    }

    $association = Ensure-ChildElementByVarName -Document $document -Parent $associationList -Name "Association" -VarName $normalized
    [void](Set-TextElement -Document $document -Parent $association -Name "Application" -Value $application)
    [void](Set-TextElement -Document $document -Parent $association -Name "Arguments" -Value $arguments)
    $installedExtensions.Add($normalized)
}

Save-XmlDocument -Document $document -Path $settingsPath

Write-Host "P4V settings: $settingsPath"
Write-Host "Diff tool: $application"
Write-Host "Arguments: $arguments"
Write-Host "Extensions: $($installedExtensions -join ', ')"
Write-Host "Restart P4V before testing the new associations."
