Imports System.Reflection
Imports System.Windows

'The ThemeInfo attribute describes where any theme specific and generic resource dictionaries can be found.
'1st parameter: where theme specific resource dictionaries are located
'(used if a resource is not found in the page,
' or application resource dictionaries)

'2nd parameter: where the generic resource dictionary is located
'(used if a resource is not found in the page,
'app, and any theme specific resource dictionaries)
<Assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)>

<Assembly: AssemblyTitle("SCOACH")>
<Assembly: AssemblyProduct("SCOACH Climate Simulator")>
<Assembly: AssemblyDescription("Ein vereinfachtes gekoppeltes Ozean-Atmosphären-Zirkulations-Klimamodell für Heim-Anwender")>
<Assembly: AssemblyCompany("Robin Harazim")>

<Assembly: AssemblyVersion("0.1.4.*")>
<Assembly: AssemblyFileVersion("0.1.4.0")>