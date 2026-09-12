$ErrorActionPreference = 'Stop'
$sourceFiles = @('DeviceProfile.cs', 'RasterValidator.cs', 'ManualMotionController.cs', 'LayerPrintWorkflow.cs', 'PowderFeedOutput.cs', 'MeteorControllerSession.cs', 'GeometryChecks.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$legacyDirectory = Get-ChildItem (Split-Path $PSScriptRoot) -Directory -Filter '1-3DP*' | Select-Object -First 1
$sourceFiles += Join-Path $legacyDirectory.FullName 'SmallMachineConfiguration.cs'
Add-Type -Path $sourceFiles -ReferencedAssemblies System.dll, System.Core.dll, System.Drawing.dll, System.Web.Extensions.dll
[LaserAdd.SmallPrinter.GeometryChecks]::Run((Join-Path $PSScriptRoot 'SmallPrinterProfile.json'))
