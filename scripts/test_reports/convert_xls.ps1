param([string]$InputPath,[string]$OutputPath)
# The Report 5.1 template ships as legacy .xls; the OOXML adapter needs .xlsx.
# Excel opens the template read-only and saves an .xlsx copy; the original is never modified.
$ErrorActionPreference='Stop'
$excel=$null;$book=$null
try {
    $excel=New-Object -ComObject Excel.Application
    $excel.Visible=$false;$excel.DisplayAlerts=$false;$excel.AutomationSecurity=3
    $book=$excel.Workbooks.Open($InputPath,0,$true)
    $book.SaveAs($OutputPath,51)
    Write-Output ('Converted template: '+$OutputPath)
} finally {
    if($null -ne $book){$book.Close($false)}
    if($null -ne $excel){$excel.Quit()}
}
