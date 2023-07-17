@echo off

REM delete existing nuget packages
del *.nupkg



echo PACKING
nuget pack GPCChecker.Concrete.csproj -Version 0.0.9.16  -properties Configuration=Release


echo PUSHING
for /f %%l in ('dir /b /s *.nupkg') do (
	echo FILE %%l
	
	nuget add %%l -source \\studio\Software_Development\NuGetPackages\
		
	del %%l
)

cd ..