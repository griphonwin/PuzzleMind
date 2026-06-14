# List of folders to delete
$targetFolders = @("bin", "obj")

# Step 1: Collect all matching folders
Write-Host "Searching for folders..." -ForegroundColor Cyan
$foldersToDelete = Get-ChildItem -Recurse -Directory -Filter * | 
    Where-Object { $_.Name -in $targetFolders }

$totalCount = $foldersToDelete.Count

# Step 2: Delete folders and show progress
if ($totalCount -gt 0) {
    for ($i = 0; $i -lt $totalCount; $i++) {
        $folder = $foldersToDelete[$i]
        
        # Update progress bar
        $percent = [math]::Round(($i / $totalCount) * 100)
        Write-Progress -Activity "Deleting folders" `
                       -Status "Removing: $($folder.FullName)" `
                       -PercentComplete $percent

        # Delete the folder
        Remove-Item $folder.FullName -Recurse -Force
    }
    Write-Progress -Activity "Deleting folders" -Completed
    Write-Host "Successfully deleted folders count: $totalCount" -ForegroundColor Green
} else {
    Write-Host "No matching folders found." -ForegroundColor Yellow
}
