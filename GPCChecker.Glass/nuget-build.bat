@echo off

if "%1"=="create"  goto create
if "%1"=="publish" goto publish

echo Usage:
echo  - To create package     : nuget-build create   	   	 
echo  - To publish the package: nuget-build publish name.nupkg    

goto end

:create
nuget pack GPCChecker.Glass.csproj -properties Configuration=Release -IncludeReferencedProjects
goto end

:publish
if "%2"=="" (
	echo Missing the file name of the .nupkg file to publish
) else (
	nuget add %2 -source \\studio\Software_Development\NuGetPackages\
)

goto end

:end