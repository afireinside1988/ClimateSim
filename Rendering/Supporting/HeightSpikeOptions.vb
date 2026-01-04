Public Structure HeightSpikeOptions
    Public Property MinAbsDeviationM As Double      'Hard-Floor für die Abweichung zum Nachbar-Meridian
    Public Property MinNeighborDiffM As Double      'Isolations-Kriterium, alle Nachbarn müssen mindestens um diesen Wert zum Zentrum abweichen
    Public Property RobustFactor As Double          'Mulitpliziert die robuste Skala (MAD) und ergibt damit die adaptive Schwelle
    Public Property UseOceanLandSeperate As Boolean 'Derzeit nicht implementiert
End Structure
