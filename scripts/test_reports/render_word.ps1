param(
    [Parameter(Mandatory=$true)][string]$InputPath,
    [Parameter(Mandatory=$true)][string]$PdfPath
)
# Read-only rendering: Word refreshes fields in memory, leaving the DOCX package intact.
$ErrorActionPreference = 'Stop'
$reportWord = $null
$reportDocument = $null
try {
    $reportWord = New-Object -ComObject Word.Application
    $reportWord.Visible = $false
    $reportWord.DisplayAlerts = 0
    $reportWord.AutomationSecurity = 3
    $reportDocument = $reportWord.Documents.Open([System.IO.Path]::GetFullPath($InputPath), $false, $true, $false)
    $reportDocument.Fields.Update() | Out-Null
    foreach ($reportToc in $reportDocument.TablesOfContents) { $reportToc.Update() }
    $reportDocument.Repaginate()
    $reportDocument.ExportAsFixedFormat([System.IO.Path]::GetFullPath($PdfPath), 17)
    $reportTocRows=@()
    foreach ($reportToc in $reportDocument.TablesOfContents) {
        foreach ($reportTocParagraph in $reportToc.Range.Paragraphs) {
            $reportTocText=$reportTocParagraph.Range.Text.Trim()
            if ($reportTocText.Contains("`t")) { $reportTocRows += $reportTocText }
        }
    }
    ConvertTo-Json -InputObject @($reportTocRows) | Set-Content -LiteralPath ($PdfPath+'.toc.json') -Encoding utf8
    Write-Output ('Rendered pages: ' + $reportDocument.ComputeStatistics(2))
} finally {
    if ($null -ne $reportDocument) { $reportDocument.Close(0) }
    if ($null -ne $reportWord) { $reportWord.Quit() }
}
