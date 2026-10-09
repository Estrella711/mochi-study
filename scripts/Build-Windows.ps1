param(
    [Parameter(Mandatory=$true)][string]$EditorPath
)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectPath=Join-Path $repoRoot 'UnityProject'
$artifactsPath=Join-Path $repoRoot 'artifacts'
if(-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)){throw 'EditorPath must point to an installed Unity/Tuanjie executable.'}
New-Item -ItemType Directory -Path $artifactsPath -Force | Out-Null
function Run-Editor([string]$Method,[string]$LogName,[string[]]$Extra=@()){
    $arguments=@('-batchmode','-nographics','-quit','-buildTarget','StandaloneWindows64','-projectPath',('"'+$projectPath+'"'),'-executeMethod',$Method,'-logFile',('"'+(Join-Path $artifactsPath $LogName)+'"'))+$Extra
    $process=Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if($process.ExitCode -ne 0){throw ('Editor failed: '+$Method+'. See artifacts/'+$LogName)}
}
Run-Editor 'MochiDay.EditorTools.RunChecks.Run' 'engine-checks.log' @('--report',('"'+(Join-Path $artifactsPath 'engine-checks.txt')+'"'))
Run-Editor 'MochiDay.EditorTools.BuildProject.BuildWindows' 'windows-build.log'
Write-Output ('Build ready: '+(Join-Path $repoRoot 'Windows\MochiStudy.exe'))
