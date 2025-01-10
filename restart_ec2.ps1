# Define the path to the executable
$exePath = "D:\Games\POE Tools\poe2\ExileCore2_my_projects\Loader.exe"

# Get the process if it's running (using the full path to ensure we get the right one)
$process = Get-WmiObject Win32_Process | Where-Object { $_.ExecutablePath -eq $exePath }

# If the process exists, terminate it
if ($process) {
    Write-Host "Found existing process, terminating..."
    $process.Terminate()
    # Give it a moment to fully terminate
    Start-Sleep -Seconds 2
}

# Start the process with admin rights
#try {
#    Start-Process -FilePath $exePath -Verb RunAs
#    Write-Host "Process started successfully with admin rights"
#} catch {
#    Write-Host "Error starting process: $_"
#    exit 1
#}