param([string]$ReportDirectory,[string]$PreviewDirectory,[int]$JobIndex)
$ErrorActionPreference='Stop'
$jobs=@(
    @{File='Report5.1_Unit_Test.xlsx'; Areas=@(@('Cover','A1:F16','unit-cover'),@('Test Cases','B1:F20','unit-list'),@('Test Statistics','__unit_summary__','unit-statistics'),@('__unit_case__','A1:P30','unit-case'))},
    @{File='Report5.2_Integration_Test.xlsx'; Areas=@(@('Cover','A1:F16','integration-cover'),@('Test Statistics','B1:H22','integration-statistics'),@('__first_case__','A1:F14','integration-case'))},
    @{File='Report5.2.2_Integration_Test_API_Test.xlsx'; Areas=@(@('Cover','A1:F16','api-cover'),@('Test Statistics','B1:H22','api-statistics'),@('__first_case__','A1:F14','api-case'))},
    @{File='Report5.3_System_Test.xlsx'; Areas=@(@('Cover','A1:F16','system-cover'),@('Test Statistics','B1:H23','system-statistics'),@('WorkFlow1','A1:F13','system-case'))},
    @{File='Report5.4_Acceptance_Test.xlsx'; Areas=@(@('Cover','A1:F16','uat-cover'),@('Statistics','A1:E22','uat-statistics'),@('Statistics','A22:E22','uat-lastrow'),@('Customers Feedbacks','A1:F4','uat-feedback'))}
)
$reportExcel=$null;$reportBook=$null
try {
    $reportExcel=New-Object -ComObject Excel.Application
    $reportExcel.Visible=$false;$reportExcel.DisplayAlerts=$false;$reportExcel.AutomationSecurity=3
    $job=$jobs[$JobIndex]
    $reportBook=$reportExcel.Workbooks.Open((Join-Path $ReportDirectory $job.File),0,$true)
    foreach($area in $job.Areas) {
        if($area[0] -eq '__first_case__') { $sheet=$reportBook.Worksheets.Item(4) } elseif($area[0] -eq '__unit_case__') {$sheet=$reportBook.Worksheets.Item(4)} else { $sheet=$reportBook.Worksheets.Item($area[0]) }
        if($area[1] -eq '__unit_summary__') {
            # Module rows start at 12; Sub total sits two rows below them and the charts 22 rows lower.
            $sheet.PageSetup.PrintArea='A1:I'+(($reportBook.Worksheets.Count-3)+12+2+23)
        } elseif($area[1] -eq '__summary__') {
            $totalRow=$reportBook.Worksheets.Count-4+12
            $sheet.Range('12:'+($totalRow-1)).EntireRow.Hidden=$true
            $sheet.PageSetup.PrintArea='A1:L'+($totalRow+25)
        } elseif($sheet.Name -eq 'Test Statistics') {
            $summaryLastRow=$reportBook.Worksheets.Count+11
            $sheet.PageSetup.PrintArea='B1:H'+$summaryLastRow
        } else {$sheet.PageSetup.PrintArea=$area[1]}
        $sheet.PageSetup.Zoom=$false
        $sheet.PageSetup.FitToPagesWide=1
        $sheet.PageSetup.FitToPagesTall=1
        $sheet.PageSetup.Orientation=2
        $sheet.PageSetup.LeftMargin=0;$sheet.PageSetup.RightMargin=0
        $sheet.PageSetup.TopMargin=0;$sheet.PageSetup.BottomMargin=0
        $sheet.PageSetup.HeaderMargin=0;$sheet.PageSetup.FooterMargin=0
        $sheet.PageSetup.LeftHeader='';$sheet.PageSetup.CenterHeader='';$sheet.PageSetup.RightHeader=''
        $sheet.PageSetup.LeftFooter='';$sheet.PageSetup.CenterFooter='';$sheet.PageSetup.RightFooter=''
        $pdf=Join-Path $PreviewDirectory ($area[2]+'.pdf')
        $sheet.ExportAsFixedFormat(0,$pdf)
        $runtimeRoot=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies'
        $poppler=Join-Path $runtimeRoot 'native/poppler/Library/bin/pdftoppm.exe'
        $prefix=Join-Path $PreviewDirectory $area[2]
        & $poppler -png -singlefile -scale-to 1800 $pdf $prefix
        if($LASTEXITCODE -ne 0) {throw 'PDF preview conversion failed'}
    }
    Write-Output ('Native Excel verified: '+$job.File+'; sheets='+$reportBook.Worksheets.Count)
} finally {
    if($null -ne $reportBook){$reportBook.Close($false)}
    if($null -ne $reportExcel){$reportExcel.Quit()}
}
