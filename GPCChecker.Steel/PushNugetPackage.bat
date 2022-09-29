@echo off

REM delete existing nuget packages
del *.nupkg



echo PACKING
nuget pack GPCChecker.Steel.csproj -Version 1.0.3.0  -properties Configuration=Release


echo PUSHING
for /f %%l in ('dir /b /s *.nupkg') do (
	echo FILE %%l
	
	nuget add %%l -source \\studio\Software_Development\NuGetPackages\
		
	del %%l
)

cd ..